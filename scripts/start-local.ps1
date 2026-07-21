$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$EnvFile = Join-Path $Root '.env.local'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK não encontrado.' }
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql não encontrado.' }
if (-not (Test-Path $EnvFile)) { throw 'Crie .env.local a partir de .env.local.example.' }
Get-Content $EnvFile | Where-Object { $_ -and -not $_.TrimStart().StartsWith('#') } | ForEach-Object { $p=$_.Split('=',2); if($p.Count -eq 2){ [Environment]::SetEnvironmentVariable($p[0],$p[1],'Process') } }
$cs = "Host=$env:SIGOV_DB_HOST;Port=$env:SIGOV_DB_PORT;Database=$env:SIGOV_DB_NAME;Username=$env:SIGOV_DB_USER;Password=$env:SIGOV_DB_PASSWORD"
$env:ConnectionStrings__DefaultConnection = $cs
$env:ASPNETCORE_ENVIRONMENT = 'Local'
$env:Sigov__Database__MigrationMode = 'ValidateOnly'
$runDir = Join-Path $Root '.local/run'; $logDir = Join-Path $Root '.local/logs'; $storage = Join-Path $Root ($env:SIGOV_STORAGE_PATH ?? '.local/storage')
New-Item -ItemType Directory -Force -Path $runDir,$logDir,$storage | Out-Null
$env:PGPASSWORD = $env:SIGOV_DB_PASSWORD
& psql -v ON_ERROR_STOP=1 -h $env:SIGOV_DB_HOST -p $env:SIGOV_DB_PORT -U $env:SIGOV_DB_USER -d $env:SIGOV_DB_NAME -c "select 1 from sigov.schema_migrations limit 1" | Out-Null
& dotnet restore (Join-Path $Root 'sigov.sln')
& dotnet build (Join-Path $Root 'sigov.sln') --configuration Release --no-restore
function Start-Sigov($name,$project,$url){ $env:ASPNETCORE_URLS=$url; $log=Join-Path $logDir "$name.log"; $p=Start-Process dotnet -ArgumentList @('run','--project',$project,'--no-launch-profile') -WorkingDirectory $Root -RedirectStandardOutput $log -RedirectStandardError $log -PassThru; Set-Content (Join-Path $runDir "$name.pid") $p.Id }
Start-Sigov 'api' 'src/Sigov.Api/Sigov.Api.csproj' $env:SIGOV_API_URL
Start-Sigov 'web' 'src/Sigov.Web/Sigov.Web.csproj' $env:SIGOV_WEB_URL
if (($env:SIGOV_RUN_WORKER ?? 'true').ToLowerInvariant() -eq 'true') { Start-Sigov 'worker' 'src/Sigov.Worker/Sigov.Worker.csproj' 'http://localhost:5002' }
Write-Host "Web: $env:SIGOV_WEB_URL"
Write-Host "API: $env:SIGOV_API_URL"
Write-Host "Swagger: $env:SIGOV_API_URL/swagger"
Write-Host "Health: $env:SIGOV_API_URL/api/health/ready"
