param(
    [string]$OutputPath = "XianYuLauncher/Assets/Data/afdian-sponsors.json"
)

$ErrorActionPreference = "Stop"

function Write-Manifest {
    param(
        [string]$Path,
        [array]$Sponsors,
        [string]$Source
    )

    $manifest = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        source = $Source
        sponsors = $Sponsors
    }

    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $manifestJson = $manifest | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($Path, $manifestJson, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Afdian sponsors manifest generated: $Path (count=$($Sponsors.Count), source=$Source)"
}

function Get-Sign {
    param(
        [string]$Token,
        [string]$UserId,
        [string]$ParamsJson,
        [long]$Ts
    )

    $kvString = "params$ParamsJson" + "ts$Ts" + "user_id$UserId"
    $signString = $Token + $kvString

    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($signString)
        $hash = $md5.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash).Replace("-", "").ToLowerInvariant())
    }
    finally {
        $md5.Dispose()
    }
}

function Fetch-Sponsors {
    param(
        [string]$UserId,
        [string]$Token
    )

    $endpoint = "https://afdian.com/api/open/query-sponsor"
    $allSponsors = New-Object System.Collections.Generic.List[object]

    for ($page = 1; $page -le 10; $page++) {
        $paramsObj = @{ page = $page }
        $paramsJson = $paramsObj | ConvertTo-Json -Compress
        $ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        $sign = Get-Sign -Token $Token -UserId $UserId -ParamsJson $paramsJson -Ts $ts

        $request = @{
            user_id = $UserId
            params = $paramsJson
            ts = $ts
            sign = $sign
        }

        $requestJson = $request | ConvertTo-Json -Compress
        $resp = Invoke-RestMethod -Uri $endpoint -Method Post -ContentType "application/json" -Body $requestJson -TimeoutSec 20

        if ($null -eq $resp -or $resp.ec -ne 200) {
            $em = if ($null -ne $resp) { $resp.em } else { "null response" }
            throw "Afdian API error. ec=$($resp.ec), em=$em"
        }

        $list = @($resp.data.list)
        if ($null -eq $list -or $list.Count -eq 0) {
            break
        }

        foreach ($item in $list) {
            $user = $item.user
            if ($null -eq $user) { continue }

            $allSponsors.Add([ordered]@{
                name = [string]$user.name
                avatar = [string]$user.avatar
                allSumAmount = [string]$item.all_sum_amount
            })
        }

        if ($list.Count -lt 50) {
            break
        }
    }

    return $allSponsors
}

$userId = $env:AFDIAN_USER_ID
$token = $env:AFDIAN_TOKEN

if ([string]::IsNullOrWhiteSpace($userId) -or [string]::IsNullOrWhiteSpace($token)) {
    Write-Warning "AFDIAN_USER_ID or AFDIAN_TOKEN is empty. Generating empty sponsors manifest."
    Write-Manifest -Path $OutputPath -Sponsors @() -Source "empty-no-credentials"
    exit 0
}

try {
    $sponsors = Fetch-Sponsors -UserId $userId -Token $token
    Write-Manifest -Path $OutputPath -Sponsors $sponsors -Source "afdian-api"
}
catch {
    Write-Warning "Failed to fetch sponsors from Afdian: $($_.Exception.Message)"
    Write-Manifest -Path $OutputPath -Sponsors @() -Source "fallback-error"
}
