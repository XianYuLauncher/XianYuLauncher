param(
    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [string]$OutputDirectory = "artifacts/store-release-notes"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseErrorMessage {
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    if ($null -eq $ErrorRecord.Exception.Response) {
        return $ErrorRecord.Exception.Message
    }

    $response = $ErrorRecord.Exception.Response
    $statusCode = [int]$response.StatusCode
    $statusText = $response.StatusCode.ToString()

    try {
        $reader = [System.IO.StreamReader]::new($response.GetResponseStream())
        try {
            $body = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        if (-not [string]::IsNullOrWhiteSpace($body)) {
            return "HTTP $statusCode ($statusText): $body"
        }
    }
    catch {
    }

    return "HTTP $statusCode ($statusText)"
}

function Remove-GitHubTrailingSections {
    param(
        [AllowNull()]
        [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ''
    }

    $normalized = $Text -replace "`r`n?", "`n"
    $normalized = [System.Text.RegularExpressions.Regex]::Replace(
        $normalized,
        '(?ms)^##\s+New Contributors\s*$.*?(?=^##\s+|\Z)',
        '')
    $normalized = [System.Text.RegularExpressions.Regex]::Replace(
        $normalized,
        '(?m)^\*\*Full Changelog\*\*:\s+.+$',
        '')

    return $normalized
}

function Convert-MarkdownToPlainText {
    param(
        [AllowNull()]
        [string]$Text
    )

    $normalized = Remove-GitHubTrailingSections -Text $Text
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return ''
    }

    $cleanedLines = foreach ($line in ($normalized -split "`n")) {
        $current = $line.TrimEnd()

        if ($current -match '^\s*!\[[^\]]*\]\([^)]+\)\s*$') {
            continue
        }

        if ($current -match '^\s*---+\s*$') {
            continue
        }

        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '\[([^\]]+)\]\([^)]+\)', '$1')
        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '`([^`]*)`', '$1')
        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '\*\*([^*]+)\*\*', '$1')
        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '__([^_]+)__', '$1')
        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '^\s*#{1,6}\s+', '')
        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '^\s*\*\s+', '- ')
        $current = [System.Text.RegularExpressions.Regex]::Replace($current, '^\s*>\s?', '')

        $current
    }

    $cleaned = [string]::Join("`n", $cleanedLines)
    $cleaned = [System.Text.RegularExpressions.Regex]::Replace($cleaned, '(\n\s*){3,}', "`n`n")

    return $cleaned.Trim()
}

if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository must be in owner/name format."
}

if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    throw "GitHubToken cannot be empty."
}

$escapedTagName = [System.Uri]::EscapeDataString($TagName)
$releaseApiUrl = "https://api.github.com/repos/$Repository/releases/tags/$escapedTagName"
$headers = @{
    Authorization = "Bearer $GitHubToken"
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'github-actions'
    'X-GitHub-Api-Version' = '2022-11-28'
}

try {
    $release = Invoke-RestMethod -Headers $headers -Uri $releaseApiUrl -Method Get
}
catch {
    $message = Get-ReleaseErrorMessage -ErrorRecord $_
    throw "Failed to fetch GitHub release '$TagName'. $message"
}

$zhNotes = Convert-MarkdownToPlainText -Text ([string]$release.body)
if ([string]::IsNullOrWhiteSpace($zhNotes)) {
    throw "GitHub release '$TagName' does not contain a usable release body."
}

$enNotes = 'Fixed known bugs and improved user experience.'

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$result = [ordered]@{
    tagName = $TagName
    releaseId = [string]$release.id
    releaseName = [string]$release.name
    notes = @(
        [ordered]@{
            language = 'zh-CN'
            content = $zhNotes
        },
        [ordered]@{
            language = 'en-US'
            content = $enNotes
        }
    )
}

$jsonPath = Join-Path $OutputDirectory 'store-release-notes.json'
$zhPath = Join-Path $OutputDirectory 'store-release-notes.zh-CN.txt'
$enPath = Join-Path $OutputDirectory 'store-release-notes.en-US.txt'

[System.IO.File]::WriteAllText($jsonPath, ($result | ConvertTo-Json -Depth 5), [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($zhPath, $zhNotes, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($enPath, $enNotes, [System.Text.UTF8Encoding]::new($false))

Write-Host "Store release notes generated for $TagName"