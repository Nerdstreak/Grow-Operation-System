param(
    [int]$Port = 5076,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"

function Test-TcpPortInUse {
    param([int]$CheckPort)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect("127.0.0.1", $CheckPort, $null, $null)
        $connected = $async.AsyncWaitHandle.WaitOne(300)
        if ($connected -and $client.Connected) {
            $client.EndConnect($async) | Out-Null
            return $true
        }
        return $false
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

if (Test-TcpPortInUse -CheckPort $Port) {
    Write-Host "Port $Port is already in use." -ForegroundColor Yellow
    Write-Host "Stop that process first or run: .\\scripts\\start-dev.ps1 -Port <otherPort>" -ForegroundColor Yellow
    exit 1
}

if (-not $NoBuild) {
    dotnet build GrowDiary.slnx -m:1 -v:minimal
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host "Starting GrowDiary on http://localhost:$Port"
$runArgs = @("run", "--project", "GrowDiary.Web\\GrowDiary.Web.csproj", "--no-launch-profile")
if ($NoBuild) {
    $runArgs += "--no-build"
}
dotnet @runArgs
exit $LASTEXITCODE
