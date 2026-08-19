param([Parameter(Mandatory=$true)][string]$Backup)
$ErrorActionPreference = 'Stop'
$hostName = if ($env:SIGOV_DB_HOST) { $env:SIGOV_DB_HOST } else { 'localhost' }; $port = if ($env:SIGOV_DB_PORT) { $env:SIGOV_DB_PORT } else { '5432' }; $user = if ($env:SIGOV_DB_USER) { $env:SIGOV_DB_USER } else { 'postgres' }
$db = $env:SIGOV_DB_NAME
if (-not $db -or $db -eq 'postgres') { throw 'SIGOV_DB_NAME deve indicar banco separado (nunca postgres).' }
if (-not (Test-Path $Backup)) { throw "Backup inexistente: $Backup" }
& pg_restore --host $hostName --port $port --username $user --dbname $db --no-owner --no-privileges --exit-on-error $Backup
if ($LASTEXITCODE) { throw "pg_restore falhou: $LASTEXITCODE" }
