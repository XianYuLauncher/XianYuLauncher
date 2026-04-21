param(
    [Parameter(Mandatory = $true)]
    [string]$AppId,

    [Parameter(Mandatory = $true)]
    [string]$TenantId,

    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $true)]
    [string]$SubmissionPayloadPath,

    [Parameter(Mandatory = $true)]
    [string]$SubmissionPackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseNotesPath,

    [string]$TagName = '',

    [string]$OutputDirectory = "artifacts/store-submission-result",

    [ValidateSet('NoAction', 'Finalize', 'Halt')]
    [string]$ExistingPackageRolloutAction = 'NoAction',

    [ValidateSet('Default', 'Immediate', 'Manual', 'SpecificDate')]
    [string]$TargetPublishMode = 'Immediate',

    [ValidateRange(1, 240)]
    [int]$PollTimeoutMinutes = 20,

    [ValidateRange(5, 300)]
    [int]$PollIntervalSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$storeReleaseNotesMaxLength = 1500

function Assert-NotEmpty {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name cannot be empty."
    }
}

function Read-JsonFileUtf8 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $json = [System.IO.File]::ReadAllText($fullPath, [System.Text.Encoding]::UTF8)
    return $json | ConvertFrom-Json
}

function Connect-StoreBroker {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TenantId,

        [Parameter(Mandatory = $true)]
        [string]$ClientId,

        [Parameter(Mandatory = $true)]
        [string]$ClientSecret
    )

    $secureClientSecret = ConvertTo-SecureString -String $ClientSecret -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential ($ClientId, $secureClientSecret)
    Set-StoreBrokerAuthentication -TenantId $TenantId -Credential $credential | Out-Null
}

function ConvertTo-MinimalApplicationPackages {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Packages
    )

    $minimalPackages = @(
        foreach ($package in @($Packages)) {
            $fileName = [string]$package.fileName
            if ([string]::IsNullOrWhiteSpace($fileName)) {
                throw "A generated application package is missing fileName."
            }

            [pscustomobject]@{
                fileName = $fileName
                fileStatus = if ([string]::IsNullOrWhiteSpace([string]$package.fileStatus)) { 'PendingUpload' } else { [string]$package.fileStatus }
                minimumDirectXVersion = if ([string]::IsNullOrWhiteSpace([string]$package.minimumDirectXVersion)) { 'None' } else { [string]$package.minimumDirectXVersion }
                minimumSystemRam = if ([string]::IsNullOrWhiteSpace([string]$package.minimumSystemRam)) { 'None' } else { [string]$package.minimumSystemRam }
            }
        }
    )

    if ($minimalPackages.Count -eq 0) {
        throw "The submission payload does not contain any packages to upload."
    }

    return $minimalPackages
}

function Merge-ApplicationPackagesForReplacement {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$ExistingPackages,

        [Parameter(Mandatory = $true)]
        [object[]]$NewPackages
    )

    $mergedPackages = @()

    foreach ($package in @($ExistingPackages)) {
        if ($package.PSObject.Properties['fileStatus']) {
            $package.fileStatus = 'PendingDelete'
        }
        else {
            $package | Add-Member -MemberType NoteProperty -Name 'fileStatus' -Value 'PendingDelete'
        }

        $mergedPackages += $package
    }

    $mergedPackages += @($NewPackages)
    return $mergedPackages
}

function Set-ListingReleaseNotes {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Submission,

        [Parameter(Mandatory = $true)]
        [object[]]$ReleaseNotes
    )

    foreach ($note in @($ReleaseNotes)) {
        $language = [string]$note.language
        $content = [string]$note.content

        if ([string]::IsNullOrWhiteSpace($language)) {
            throw "A release note entry is missing language."
        }

        $listingProperty = $Submission.listings.PSObject.Properties[$language]
        if ($null -eq $listingProperty) {
            throw "The current Microsoft Store submission does not contain listing language '$language'."
        }

        $listing = $listingProperty.Value
        if ($null -eq $listing.baseListing) {
            throw "Listing '$language' does not contain baseListing."
        }

        if ($listing.baseListing.PSObject.Properties['releaseNotes']) {
            $listing.baseListing.releaseNotes = $content
        }
        else {
            $listing.baseListing | Add-Member -MemberType NoteProperty -Name 'releaseNotes' -Value $content
        }
    }
}

function Normalize-ReleaseNotesForStore {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$ReleaseNotes,

        [Parameter(Mandatory = $true)]
        [int]$MaxLength
    )

    return @(
        foreach ($note in @($ReleaseNotes)) {
            $language = [string]$note.language
            $content = if ($null -eq $note.content) { '' } else { [string]$note.content }
            $originalLength = $content.Length

            if ($originalLength -gt $MaxLength) {
                $suffix = '...'
                $maxContentLength = [Math]::Max(0, $MaxLength - $suffix.Length)
                $candidate = $content.Substring(0, $maxContentLength)
                $lastLineBreak = $candidate.LastIndexOf("`n", [System.StringComparison]::Ordinal)
                if ($lastLineBreak -ge [Math]::Floor($maxContentLength * 0.6)) {
                    $candidate = $candidate.Substring(0, $lastLineBreak)
                }

                $candidate = $candidate.TrimEnd()
                if ([string]::IsNullOrWhiteSpace($candidate)) {
                    $candidate = $content.Substring(0, $maxContentLength).TrimEnd()
                }

                $content = $candidate + $suffix
                if ($content.Length -gt $MaxLength) {
                    $content = $content.Substring(0, $MaxLength)
                }

                Write-Warning "$language release notes exceeded $MaxLength characters and were truncated from $originalLength to $($content.Length)."
            }

            [pscustomobject]@{
                language = $language
                content = $content
                contentLength = $content.Length
                originalLength = $originalLength
                wasTruncated = ($originalLength -gt $content.Length)
            }
        }
    )
}

function Wait-ForSubmissionStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppId,

        [Parameter(Mandatory = $true)]
        [string]$SubmissionId,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMinutes,

        [Parameter(Mandatory = $true)]
        [int]$PollIntervalSeconds
    )

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $observedStatuses = New-Object System.Collections.Generic.List[string]
    $steadyStateStatuses = @('PreProcessing', 'Certification', 'Release', 'PendingPublication', 'Publishing', 'Published')

    while ($true) {
        $submission = $null
        try {
            $submission = Get-ApplicationSubmission -AppId $AppId -SubmissionId $SubmissionId -NoStatus
        }
        catch {
            if ((Get-Date) -ge $deadline) {
                throw
            }

            Write-Warning "Failed to query Microsoft Store submission status. Retrying. $($_.Exception.Message)"
            Start-Sleep -Seconds $PollIntervalSeconds
            continue
        }

        $status = [string]$submission.status
        if ($observedStatuses.Count -eq 0 -or $observedStatuses[$observedStatuses.Count - 1] -ne $status) {
            $observedStatuses.Add($status)
            Write-Host "Microsoft Store submission status: $status"
        }

        if ($status -match 'Failed$' -or $status -eq 'Canceled') {
            throw "Microsoft Store submission entered failure status '$status'."
        }

        if ($status -in $steadyStateStatuses) {
            return [pscustomobject]@{
                status = $status
                timedOut = $false
                observedStatuses = @($observedStatuses)
            }
        }

        if ((Get-Date) -ge $deadline) {
            return [pscustomobject]@{
                status = $status
                timedOut = $true
                observedStatuses = @($observedStatuses)
            }
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }
}

Assert-NotEmpty -Name 'AppId' -Value $AppId
Assert-NotEmpty -Name 'TenantId' -Value $TenantId
Assert-NotEmpty -Name 'ClientId' -Value $ClientId
Assert-NotEmpty -Name 'ClientSecret' -Value $ClientSecret

if (-not (Test-Path -Path $SubmissionPayloadPath -PathType Leaf)) {
    throw "SubmissionPayloadPath '$SubmissionPayloadPath' does not exist."
}

if (-not (Test-Path -Path $SubmissionPackagePath -PathType Leaf)) {
    throw "SubmissionPackagePath '$SubmissionPackagePath' does not exist."
}

if (-not (Test-Path -Path $ReleaseNotesPath -PathType Leaf)) {
    throw "ReleaseNotesPath '$ReleaseNotesPath' does not exist."
}

Import-Module StoreBroker -ErrorAction Stop
Connect-StoreBroker -TenantId $TenantId -ClientId $ClientId -ClientSecret $ClientSecret

$payload = Read-JsonFileUtf8 -Path $SubmissionPayloadPath
$releaseNotesDocument = Read-JsonFileUtf8 -Path $ReleaseNotesPath
$releaseNotes = @(Normalize-ReleaseNotesForStore -ReleaseNotes @($releaseNotesDocument.notes) -MaxLength $storeReleaseNotesMaxLength)

if ($releaseNotes.Count -eq 0) {
    throw "The release notes file does not contain any notes entries."
}

$clonedSubmission = New-ApplicationSubmission `
    -AppId $AppId `
    -Force `
    -ExistingPackageRolloutAction $ExistingPackageRolloutAction `
    -NoStatus

$newApplicationPackages = @(ConvertTo-MinimalApplicationPackages -Packages @($payload.applicationPackages))
$existingPackageIdsToDelete = @(
    foreach ($package in @($clonedSubmission.applicationPackages)) {
        $packageId = [string]$package.id
        if (-not [string]::IsNullOrWhiteSpace($packageId)) {
            $packageId
        }
    }
)
$clonedSubmission.applicationPackages = @(Merge-ApplicationPackagesForReplacement -ExistingPackages @($clonedSubmission.applicationPackages) -NewPackages $newApplicationPackages)

if ($TargetPublishMode -ne 'Default') {
    $clonedSubmission.targetPublishMode = $TargetPublishMode
    if ($TargetPublishMode -ne 'SpecificDate') {
        $clonedSubmission.targetPublishDate = $null
    }
}

Set-ListingReleaseNotes -Submission $clonedSubmission -ReleaseNotes $releaseNotes

$updatedSubmission = Set-ApplicationSubmission -AppId $AppId -UpdatedSubmission $clonedSubmission -NoStatus
$submissionId = [string]$updatedSubmission.id
if ([string]::IsNullOrWhiteSpace($submissionId)) {
    throw "Set-ApplicationSubmission did not return a submission id."
}

$uploadUrl = [string]$updatedSubmission.fileUploadUrl
if ([string]::IsNullOrWhiteSpace($uploadUrl)) {
    throw "Set-ApplicationSubmission did not return a fileUploadUrl."
}

Set-SubmissionPackage -PackagePath $SubmissionPackagePath -UploadUrl $uploadUrl -NoStatus
Complete-ApplicationSubmission -AppId $AppId -SubmissionId $submissionId -NoStatus

$statusResult = Wait-ForSubmissionStatus `
    -AppId $AppId `
    -SubmissionId $submissionId `
    -TimeoutMinutes $PollTimeoutMinutes `
    -PollIntervalSeconds $PollIntervalSeconds

$outputFullPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputFullPath | Out-Null

$portalUrl = "https://partner.microsoft.com/en-us/dashboard/products/$AppId/submissions/$submissionId/"
$resultPath = Join-Path $outputFullPath 'store-submission-result.json'
$result = [ordered]@{
    tagName = $TagName
    appId = $AppId
    submissionId = $submissionId
    targetPublishMode = if ($TargetPublishMode -eq 'Default') { [string]$clonedSubmission.targetPublishMode } else { $TargetPublishMode }
    status = [string]$statusResult.status
    timedOut = [bool]$statusResult.timedOut
    observedStatuses = @($statusResult.observedStatuses)
    packageFiles = @($newApplicationPackages | ForEach-Object { [string]$_.fileName })
    pendingDeletePackageIds = @($existingPackageIdsToDelete)
    releaseNoteLanguages = @($releaseNotes | ForEach-Object { [string]$_.language })
    portalUrl = $portalUrl
    submittedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}

$encoding = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 5), $encoding)

Write-Host "Store submission committed. Submission Id: $submissionId"
Write-Host "Microsoft Partner Center URL: $portalUrl"
Write-Host "Current submission status: $($result.status)"

if ($result.timedOut) {
    Write-Warning "Status polling timed out before the submission moved beyond the initial commit stage. Continue monitoring in Partner Center."
}