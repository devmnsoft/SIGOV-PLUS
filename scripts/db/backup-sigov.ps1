$ErrorActionPreference = 'Stop'
$hostName = if ($env:SIGOV_DB_HOST) { $env:SIGOV_DB_HOST } else { 'localhost' }
$port = if ($env:SIGOV_DB_PORT) { $env:SIGOV_DB_PORT } else { '5432' }
$db = if ($env:SIGOV_DB_NAME) { $env:SIGOV_DB_NAME } else { 'postgres' }
$user = if ($env:SIGOV_DB_USER) { $env:SIGOV_DB_USER } else { 'postgres' }
$schema = if ($env:SIGOV_DB_SCHEMA) { $env:SIGOV_DB_SCHEMA } else { 'sigov' }
$outDir = if ($args.Count) { $args[0] } else { 'artifacts/backups' }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir ("sigov_{0}_{1}.dump" -f $db, (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
& pg_dump --host $hostName --port $port --username $user --dbname $db --schema $schema --format custom --no-owner --no-privileges --file $out
if ($LASTEXITCODE) { throw "pg_dump falhou: $LASTEXITCODE" }; Write-Output $out
