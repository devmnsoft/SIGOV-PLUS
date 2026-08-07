[CmdletBinding()] param([switch]$SkipBuild)
$ErrorActionPreference='Stop'; $Root=Split-Path -Parent $PSScriptRoot; . "$PSScriptRoot/local-runtime-common.ps1"; Import-SigovEnv "$Root/.env.local"; Assert-SigovVariables
foreach($tool in 'dotnet','psql'){if(!(Get-Command $tool -ErrorAction SilentlyContinue)){throw "$tool não encontrado."}}
Invoke-RuntimePsql "select case when exists(select 1 from information_schema.schemata where schema_name='sigov') then 1 else 0 end" | Out-Null
$schema=Invoke-RuntimePsql "select exists(select 1 from information_schema.schemata where schema_name='sigov')"; if("$schema" -notmatch 't|true|1'){throw 'Schema sigov ausente. Execute ./scripts/diagnose-sigov-database.ps1 e ./scripts/install-sigov-database.ps1.'}
$env:ConnectionStrings__DefaultConnection="Host=$env:SIGOV_DB_HOST;Port=$env:SIGOV_DB_PORT;Database=$env:SIGOV_DB_NAME;Username=$env:SIGOV_DB_USER;Password=$env:SIGOV_DB_PASSWORD"; $env:ASPNETCORE_ENVIRONMENT='Local'; $env:Sigov__Database__MigrationMode=$env:SIGOV_MIGRATION_MODE; $env:Sigov__Database__RunMigrationsOnStartup=$env:SIGOV_RUN_MIGRATIONS
$run="$Root/.local/run";$logs="$Root/.local/logs";New-Item -ItemType Directory -Force $run,$logs | Out-Null
if(!$SkipBuild){dotnet build "$Root/sigov.sln" --configuration Release; if($LASTEXITCODE){throw 'Build falhou.'}}
function Start-Service($name,$project,$url){$env:ASPNETCORE_URLS=$url;$log="$logs/$name.log";$err="$logs/$name.error.log";$p=Start-Process dotnet -ArgumentList @('run','--project',$project,'--no-launch-profile','--no-build','--configuration','Release') -WorkingDirectory $Root -RedirectStandardOutput $log -RedirectStandardError $err -PassThru;Set-Content "$run/$name.pid" $p.Id}
Start-Service api src/Sigov.Api/Sigov.Api.csproj $env:SIGOV_API_URL; Start-Service web src/Sigov.Web/Sigov.Web.csproj $env:SIGOV_WEB_URL
if(($env:SIGOV_RUN_WORKER??'false') -eq 'true'){Start-Service worker src/Sigov.Worker/Sigov.Worker.csproj 'http://localhost:5002'}
Write-Host "API: $env:SIGOV_API_URL`nWeb: $env:SIGOV_WEB_URL`nLogs: $logs`nMigrationMode: $env:SIGOV_MIGRATION_MODE (SIGOV_RUN_MIGRATIONS=$env:SIGOV_RUN_MIGRATIONS)"
