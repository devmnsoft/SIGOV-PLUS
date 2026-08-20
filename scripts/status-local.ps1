$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/local-runtime-common.ps1"
Import-SigovEnv "$Root/.env.local"
Assert-SigovVariables

function Get-SigovProcessStatus([string]$Name) {
    $pidFile = "$Root/.local/run/$Name.pid"
    if (-not (Test-Path $pidFile)) { return 'Stopped' }
    $process = Get-Process -Id (Get-Content $pidFile) -ErrorAction SilentlyContinue
    if ($process) { return 'Running' }
    Remove-Item $pidFile -Force
    return 'Stopped'
}

function Get-SigovHttpStatus([string]$Url) {
    try {
        $response = Invoke-WebRequest $Url -TimeoutSec 5 -UseBasicParsing
        if ($response.StatusCode -lt 400) { return 'OK' }
        return 'Erro'
    }
    catch { return 'Erro' }
}

$database = 'OK'
$schema = 'OK'
try {
    $schemaResult = "$(Invoke-RuntimePsql "select exists(select 1 from information_schema.schemata where schema_name='sigov')")"
    if ($schemaResult -notmatch 't|true|1') { $schema = 'Erro' }
}
catch {
    $database = 'Erro'
    $schema = 'Erro'
}

$workerSetting = if ([string]::IsNullOrWhiteSpace($env:SIGOV_RUN_WORKER)) { 'false' } else { $env:SIGOV_RUN_WORKER.Trim() }
$runWorker = $workerSetting.Equals('true', [StringComparison]::OrdinalIgnoreCase)
$workerStatus = if ($runWorker) { Get-SigovProcessStatus worker } else { 'Desabilitado' }
$result = [ordered]@{
    timestamp = (Get-Date).ToUniversalTime().ToString('o')
    api = Get-SigovProcessStatus api
    web = Get-SigovProcessStatus web
    worker = $workerStatus
    database = $database
    runtimeUser = $database
    schema = $schema
    healthLive = Get-SigovHttpStatus "$env:SIGOV_API_URL/api/health/live"
    healthReady = Get-SigovHttpStatus "$env:SIGOV_API_URL/api/health/ready"
    webHome = Get-SigovHttpStatus $env:SIGOV_WEB_URL
    migrationMode = $env:SIGOV_MIGRATION_MODE
}

[pscustomobject]$result | Format-List
$directory = "$Root/artifacts/local-setup"
Write-SafeJson $result "$directory/status-result.json"
$lines = @('# Status local SIGOV+', '', "Gerado: $($result.timestamp)", '', '| Item | Status |', '|---|---|') + @($result.Keys | ForEach-Object { "| $_ | $($result[$_]) |" })
$lines | Set-Content "$directory/status-report.md"
if ($database -eq 'Erro' -or $result.healthReady -eq 'Erro') { exit 2 }
