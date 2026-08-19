[CmdletBinding()]
param(
    [string]$Repository = 'C:\MNSOFT\SIGOV-PLUS',
    [string]$ApiUrl = 'http://localhost:5001',
    [string]$WebUrl = 'http://localhost:5002'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Repository)) { $Repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
Set-Location $Repository
$out = 'artifacts/smoke/rc50_54_prod_gate_local_windows_result.txt'
New-Item (Split-Path $out) -ItemType Directory -Force | Out-Null
Set-Content $out "SIGOV+ RC50.54 local production gate`nStartedUtc=$([DateTime]::UtcNow.ToString('o'))"
function Write-Evidence([string]$Message) { $Message | Tee-Object -FilePath $out -Append }
function Invoke-Gate([string]$Name, [scriptblock]$Action) {
    Write-Evidence "RUN $Name"
    & $Action
    if ($LASTEXITCODE) { throw "$Name falhou (exit code $LASTEXITCODE)." }
    Write-Evidence "PASS $Name"
}
foreach ($tool in 'dotnet','psql','pg_dump','pg_restore','python','bash') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { Write-Evidence "FAIL tooling tool=$tool classification=P0_ENVIRONMENTAL"; throw "$tool não encontrado no PATH." }
    Write-Evidence "PASS tooling tool=$tool"
}
$env:SIGOV_API_BASE_URL=$ApiUrl; $env:SIGOV_WEB_BASE_URL=$WebUrl
$env:SIGOV_DB_HOST = if ($env:SIGOV_DB_HOST) {$env:SIGOV_DB_HOST} else {'localhost'}
$env:SIGOV_DB_PORT = if ($env:SIGOV_DB_PORT) {$env:SIGOV_DB_PORT} else {'5432'}
$env:SIGOV_DB_NAME = if ($env:SIGOV_DB_NAME) {$env:SIGOV_DB_NAME} else {'postgres'}
$env:SIGOV_DB_USER = if ($env:SIGOV_DB_USER) {$env:SIGOV_DB_USER} else {'postgres'}
$env:SIGOV_DB_SCHEMA='sigov'; $env:SIGOV_SMOKE_APPLY_DATABASE='true'; $env:SIGOV_SWAGGER_ENABLED='true'
if (-not $env:PGPASSWORD) { throw 'Defina PGPASSWORD somente na sessão antes de executar o gate.' }
$env:ConnectionStrings__DefaultConnection="Host=$($env:SIGOV_DB_HOST);Port=$($env:SIGOV_DB_PORT);Database=$($env:SIGOV_DB_NAME);Username=$($env:SIGOV_DB_USER);Password=$($env:PGPASSWORD);Search Path=sigov;Application Name=sigov.local.gate"
$api=$null; $web=$null
try {
    Invoke-Gate manifest { python -m json.tool database/postgres/migrations/manifest.json | Out-Null }
    Invoke-Gate partial-indexes { bash scripts/check-migration-partial-index-columns.sh database/postgres/migrations }
    Invoke-Gate indexes { bash scripts/check-migration-index-columns.sh database/postgres/migrations }
    Invoke-Gate immutable-indexes { bash scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations }
    Invoke-Gate route-conflicts { bash scripts/check-api-route-conflicts.sh }
    Invoke-Gate database { psql -X -h $env:SIGOV_DB_HOST -p $env:SIGOV_DB_PORT -U $env:SIGOV_DB_USER -d $env:SIGOV_DB_NAME -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql }
    Invoke-Gate clean { dotnet clean sigov.runtime.slnf }
    Invoke-Gate restore { dotnet restore sigov.runtime.slnf --locked-mode }
    Invoke-Gate build { dotnet build sigov.runtime.slnf -c Release --no-restore --nologo -warnaserror }
    $api=Start-Process dotnet -ArgumentList 'run --project src/Sigov.Api/Sigov.Api.csproj -c Release --no-build --urls http://localhost:5001' -PassThru -RedirectStandardOutput artifacts/smoke/api-local.log -RedirectStandardError artifacts/smoke/api-local-error.log
    $web=Start-Process dotnet -ArgumentList 'run --project src/Sigov.Web/Sigov.Web.csproj -c Release --no-build --urls http://localhost:5002' -PassThru -RedirectStandardOutput artifacts/smoke/web-local.log -RedirectStandardError artifacts/smoke/web-local-error.log
    for($i=0;$i -lt 60;$i++){ try { Invoke-WebRequest "$ApiUrl/health" -UseBasicParsing | Out-Null; Invoke-WebRequest "$WebUrl/Auth/Login" -UseBasicParsing | Out-Null; break } catch { Start-Sleep 2 } }
    Invoke-Gate critical-pages { & scripts/check-critical-pages.ps1 -ApiUrl $ApiUrl -BaseUrl $WebUrl }
    Invoke-Gate backup { $script:backup = (& scripts/db/backup-sigov.ps1 | Select-Object -Last 1) }
    $restoreDb='sigov_restore_local'; & createdb -h $env:SIGOV_DB_HOST -p $env:SIGOV_DB_PORT -U $env:SIGOV_DB_USER $restoreDb 2>$null
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 1) { throw 'Não foi possível preparar banco isolado de restore.' }
    $originalDb=$env:SIGOV_DB_NAME; $env:SIGOV_DB_NAME=$restoreDb
    Invoke-Gate restore-backup { & scripts/db/restore-sigov.ps1 -Backup $script:backup }
    Invoke-Gate verify-restore { & scripts/db/verify-restore-sigov.ps1 }
    $env:SIGOV_DB_NAME=$originalDb
    $env:SIGOV_SMOKE_OUTPUT='artifacts/smoke/rc50_54_prod_gate_result.txt'
    Invoke-Gate smoke { bash scripts/smoke-production-like.sh }
    Write-Evidence 'LOGIN NOTE admin/superadmin exige execução de scripts/check-local-login.ps1 com credenciais fornecidas somente na sessão; antiforgery não é contornado.'
    Write-Evidence 'GATE PASS secrets=not_logged connection_string=not_logged'
} finally {
    foreach($process in @($api,$web)) { if ($process -and -not $process.HasExited) { Stop-Process $process.Id -Force } }
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
}
