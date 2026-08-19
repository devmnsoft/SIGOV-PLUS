$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot; Set-Location $root
$out = if ($env:SIGOV_SMOKE_OUTPUT) { $env:SIGOV_SMOKE_OUTPUT } else { 'artifacts/smoke/rc50_53_prod_smoke_result.txt' }
New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null; Set-Content $out ''
function Write-Smoke([string]$Message) { Add-Content $out $Message; Write-Host $Message }
function Env-Or([string]$Value, [string]$Default) { if ($Value) { return $Value }; return $Default }
$blocked = $false
function Run-Step([string]$Name, [scriptblock]$Action) {
    $watch = [Diagnostics.Stopwatch]::StartNew(); Write-Smoke "RUN $Name"
    & $Action *>> $out; $code = $LASTEXITCODE; $watch.Stop()
    if ($code) { Write-Smoke "FAIL $Name exit_code=$code duration_seconds=$([math]::Round($watch.Elapsed.TotalSeconds, 3))"; throw "$Name falhou" }
    Write-Smoke "PASS $Name exit_code=0 duration_seconds=$([math]::Round($watch.Elapsed.TotalSeconds, 3))"
}
Run-Step 'manifest' { python -m json.tool database/postgres/migrations/manifest.json }
Run-Step 'partial-indexes' { bash scripts/check-migration-partial-index-columns.sh database/postgres/migrations }
Run-Step 'indexes' { bash scripts/check-migration-index-columns.sh database/postgres/migrations }
Run-Step 'immutable-indexes' { bash scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations }
Run-Step 'route-conflicts' { bash scripts/check-api-route-conflicts.sh }
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { Write-Smoke 'SKIP database reason=psql_not_found classification=P0_ENVIRONMENTAL'; $blocked = $true }
elseif ($env:SIGOV_SMOKE_APPLY_DATABASE -ne 'true') { Write-Smoke 'SKIP database reason=SIGOV_SMOKE_APPLY_DATABASE_not_true' }
else {
    if (-not $env:PGPASSWORD) { throw 'Defina PGPASSWORD somente no ambiente' }
    Run-Step 'database' { psql --host (Env-Or $env:SIGOV_DB_HOST 'localhost') --port (Env-Or $env:SIGOV_DB_PORT '5432') --username (Env-Or $env:SIGOV_DB_USER 'postgres') --dbname (Env-Or $env:SIGOV_DB_NAME 'postgres') --set ON_ERROR_STOP=1 --file database/postgres/script_completo_dev.sql }
}
if (Get-Command dotnet -ErrorAction SilentlyContinue) { Run-Step 'restore' { dotnet restore sigov.runtime.slnf --locked-mode }; Run-Step 'build' { dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror } }
else { Write-Smoke 'SKIP build reason=dotnet_not_found classification=P0_ENVIRONMENTAL'; $blocked = $true }
function Probe([string]$Name,[string]$Url,[int]$Expected=200) {
    $watch=[Diagnostics.Stopwatch]::StartNew(); $code=0
    try { $response=Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 15; $code=[int]$response.StatusCode } catch { if ($_.Exception.Response) { $code=[int]$_.Exception.Response.StatusCode } }
    $watch.Stop(); if($code -ne $Expected){ Write-Smoke "FAIL $Name endpoint=$Url http_status=$code expected=$Expected duration_seconds=$([math]::Round($watch.Elapsed.TotalSeconds, 3))"; throw "$Name falhou" }
    Write-Smoke "PASS $Name endpoint=$Url http_status=$code exit_code=0 duration_seconds=$([math]::Round($watch.Elapsed.TotalSeconds, 3))"
}
if($env:SIGOV_API_BASE_URL){ Probe 'api-health' "$($env:SIGOV_API_BASE_URL)/api/observabilidade/health"; if($env:SIGOV_SWAGGER_ENABLED -eq 'true'){ Probe 'swagger' "$($env:SIGOV_API_BASE_URL)/swagger/v1/swagger.json" } }
else { Write-Smoke 'SKIP API probes reason=SIGOV_API_BASE_URL_not_set' }
if($env:SIGOV_WEB_BASE_URL){ Probe 'login' "$($env:SIGOV_WEB_BASE_URL)/Auth/Login"; @('MinhaCentral','SystemHealth/ProjectStatus','Observabilidade/Dashboard','Seguranca/Dashboard','Seguranca/Permissoes','Auditoria/Dashboard','Lgpd/Dashboard') | ForEach-Object { Probe "web-$_" "$($env:SIGOV_WEB_BASE_URL)/$_" 302 } }
else { Write-Smoke 'SKIP Web probes reason=SIGOV_WEB_BASE_URL_not_set' }
Write-Smoke 'SMOKE COMPLETE output=sanitized secrets=not_logged'
if ($blocked) { Write-Smoke 'GATE BLOCKED reason=mandatory_tooling_unavailable exit_code=2'; exit 2 }
