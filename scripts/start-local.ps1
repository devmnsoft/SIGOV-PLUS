$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $PSScriptRoot
$EnvFile = Join-Path $Root '.env.local'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK não encontrado.' }
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql não encontrado.' }
if (-not (Test-Path $EnvFile)) { throw 'Crie .env.local a partir de .env.local.example.' }

Get-Content $EnvFile |
    Where-Object { $_ -and -not $_.TrimStart().StartsWith('#') } |
    ForEach-Object {
        $pair = $_.Split('=', 2)
        if ($pair.Count -eq 2) {
            [Environment]::SetEnvironmentVariable($pair[0].Trim(), $pair[1].Trim(), 'Process')
        }
    }

foreach ($required in @('SIGOV_DB_HOST','SIGOV_DB_PORT','SIGOV_DB_NAME','SIGOV_DB_USER','SIGOV_DB_PASSWORD','SIGOV_WEB_URL','SIGOV_API_URL')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($required))) {
        throw "Variável obrigatória ausente no .env.local: $required"
    }
}

$connectionString = "Host=$env:SIGOV_DB_HOST;Port=$env:SIGOV_DB_PORT;Database=$env:SIGOV_DB_NAME;Username=$env:SIGOV_DB_USER;Password=$env:SIGOV_DB_PASSWORD"
$env:ConnectionStrings__DefaultConnection = $connectionString
$env:ASPNETCORE_ENVIRONMENT = 'Local'

$migrationMode = $env:SIGOV_MIGRATION_MODE
if ([string]::IsNullOrWhiteSpace($migrationMode)) {
    $runMigrations = ($env:SIGOV_RUN_MIGRATIONS ?? 'false').Trim().ToLowerInvariant()
    $migrationMode = switch ($runMigrations) {
        'true' { 'ApplyPending' }
        '1' { 'ApplyPending' }
        'yes' { 'ApplyPending' }
        'sim' { 'ApplyPending' }
        'validate' { 'ValidateOnly' }
        'validar' { 'ValidateOnly' }
        default { 'Disabled' }
    }
}
$env:Sigov__Database__MigrationMode = $migrationMode
$env:Sigov__Database__RunMigrationsOnStartup = if ($migrationMode -eq 'ApplyPending') { 'true' } else { 'false' }

$runDir = Join-Path $Root '.local/run'
$logDir = Join-Path $Root '.local/logs'
$storage = Join-Path $Root ($env:SIGOV_STORAGE_PATH ?? '.local/storage')
New-Item -ItemType Directory -Force -Path $runDir, $logDir, $storage | Out-Null

function Invoke-LocalPsql {
    param([Parameter(Mandatory)][string]$Sql)
    $env:PGPASSWORD = $env:SIGOV_DB_PASSWORD
    $output = & psql -X -v ON_ERROR_STOP=1 -h $env:SIGOV_DB_HOST -p $env:SIGOV_DB_PORT -U $env:SIGOV_DB_USER -d $env:SIGOV_DB_NAME -t -A -c $Sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao conectar no PostgreSQL com SIGOV_DB_USER=$env:SIGOV_DB_USER. Verifique SIGOV_DB_PASSWORD ou rode scripts/provision-sigov-db-user.ps1.`n$($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine).Trim()
}

Invoke-LocalPsql "select current_user || '@' || current_database();" | Out-Null
$schemaState = Invoke-LocalPsql "select case when to_regclass('sigov.schema_migrations') is null then 'MISSING' else 'OK' end;"
if ($schemaState -ne 'OK') {
    throw "Schema SIGOV ainda não está instalado no banco '$env:SIGOV_DB_NAME'. Execute script_completop.sql ou scripts/install-sigov-database.ps1 antes de iniciar o runtime."
}

Write-Host "MigrationMode local: $migrationMode"

& dotnet restore (Join-Path $Root 'sigov.sln')
& dotnet build (Join-Path $Root 'sigov.sln') --configuration Release --no-restore

function Start-Sigov {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Url
    )
    $env:ASPNETCORE_URLS = $Url
    $log = Join-Path $logDir "$Name.log"
    $process = Start-Process dotnet -ArgumentList @('run', '--project', $Project, '--no-launch-profile') -WorkingDirectory $Root -RedirectStandardOutput $log -RedirectStandardError $log -PassThru
    Set-Content (Join-Path $runDir "$Name.pid") $process.Id
}

Start-Sigov 'api' 'src/Sigov.Api/Sigov.Api.csproj' $env:SIGOV_API_URL
Start-Sigov 'web' 'src/Sigov.Web/Sigov.Web.csproj' $env:SIGOV_WEB_URL
if (($env:SIGOV_RUN_WORKER ?? 'true').ToLowerInvariant() -eq 'true') {
    Start-Sigov 'worker' 'src/Sigov.Worker/Sigov.Worker.csproj' 'http://localhost:5002'
}

Write-Host "Web: $env:SIGOV_WEB_URL"
Write-Host "API: $env:SIGOV_API_URL"
Write-Host "Swagger: $env:SIGOV_API_URL/swagger"
Write-Host "Health: $env:SIGOV_API_URL/api/health/ready"
