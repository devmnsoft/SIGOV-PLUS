$ErrorActionPreference = 'Stop'
$hostName = if ($env:SIGOV_DB_HOST) { $env:SIGOV_DB_HOST } else { 'localhost' }; $port = if ($env:SIGOV_DB_PORT) { $env:SIGOV_DB_PORT } else { '5432' }; $user = if ($env:SIGOV_DB_USER) { $env:SIGOV_DB_USER } else { 'postgres' }; $schema = if ($env:SIGOV_DB_SCHEMA) { $env:SIGOV_DB_SCHEMA } else { 'sigov' }
$db = $env:SIGOV_DB_NAME
if ($schema -notmatch '^[a-z_][a-z0-9_]*$') { throw 'SIGOV_DB_SCHEMA inválido.' }
if (-not $db -or $db -eq 'postgres') { throw 'SIGOV_DB_NAME deve indicar banco separado restaurado.' }
$sql = "SELECT to_regnamespace('$schema'); SELECT count(*) FROM $schema.schema_migrations;"
& psql --host $hostName --port $port --username $user --dbname $db --set ON_ERROR_STOP=1 --no-psqlrc --command $sql
if ($LASTEXITCODE) { throw "Validação falhou: $LASTEXITCODE" }
