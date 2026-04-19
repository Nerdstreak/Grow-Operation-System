param(
    [int]$Port = 5076,
    [switch]$SkipBuild,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"

if (-not $SkipBuild) {
    dotnet build GrowDiary.slnx -m:1 -v:minimal
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not $SkipTests) {
    dotnet test GrowDiary.Web.Tests\GrowDiary.Web.Tests.csproj --no-restore -v:minimal
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$job = Start-Job -ScriptBlock {
    param($root, $portValue)
    Set-Location $root
    $env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"
    $env:ASPNETCORE_URLS = "http://0.0.0.0:$portValue"
    dotnet run --project GrowDiary.Web\GrowDiary.Web.csproj --no-launch-profile --no-build
} -ArgumentList $repoRoot, $Port

try {
    Start-Sleep -Seconds 5

    $baseUrl = "http://localhost:$Port"
    $baseRoutes = @(
        "/",
        "/grows",
        "/grows/new",
        "/zelte",
        "/einstellungen",
        "/wissen",
        "/analyse",
        "/archiv"
    )

    $growId = 1
    try {
        $growsPage = Invoke-WebRequest -Uri ($baseUrl + "/grows") -UseBasicParsing -TimeoutSec 10
        $match = [regex]::Match($growsPage.Content, "/grows/([0-9]+)")
        if ($match.Success) {
            $growId = [int]$match.Groups[1].Value
        }
    }
    catch {
    }

    $detailRoutes = @(
        "/grows/$growId",
        "/grows/$growId/setup",
        "/grows/$growId/messung",
        "/grows/$growId/journal/create",
        "/zelte/1"
    )

    $routes = $baseRoutes + $detailRoutes

    $results = foreach ($route in $routes) {
        try {
            $resp = Invoke-WebRequest -Uri ($baseUrl + $route) -UseBasicParsing -TimeoutSec 10
            [pscustomobject]@{
                Route = $route
                Status = [int]$resp.StatusCode
                Length = $resp.Content.Length
                Error = ""
            }
        }
        catch {
            [pscustomobject]@{
                Route = $route
                Status = 0
                Length = 0
                Error = $_.Exception.Message
            }
        }
    }

    $results | Format-Table -AutoSize
    $failures = $results | Where-Object { $_.Status -ne 200 }

    if ($failures.Count -gt 0) {
        Write-Host ""
        Write-Host "Smoke check failed." -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Smoke check passed." -ForegroundColor Green
    exit 0
}
finally {
    Stop-Job $job -ErrorAction SilentlyContinue | Out-Null
    Receive-Job $job -ErrorAction SilentlyContinue | Out-Null
    Remove-Job $job -Force -ErrorAction SilentlyContinue | Out-Null
}
