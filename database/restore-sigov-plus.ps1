param(
    [string]$HostName = $(if ($env:PGHOST) { $env:PGHOST } else { 'localhost' }),
    [int]$Port = $(if ($env:PGPORT) { [int]$env:PGPORT } else { 5432 }),
    [string]$User = $(if ($env:PGUSER) { $env:PGUSER } else { 'postgres' }),
    [string]$Database = $(if ($env:PGDATABASE) { $env:PGDATABASE } else { 'sigov_plus' })
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    Write-Error 'BLOCKED: comando psql não executado porque psql não está instalado ou não está no PATH.'
    exit 127
}
$base = Join-Path $PSScriptRoot 'SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql'
& psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -f $base
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$checks = "select count(*) as tabelas from information_schema.tables where table_schema='sigov'; select count(*) as modulos from sigov.modulo_saas where ativo and not is_deleted; select count(*) as permissoes from sigov.permissao where ativo and not is_deleted; select count(*) as super_admins from sigov.usuario where email='superadmin@mnsoft.local' and ativo and not is_deleted;"
& psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -c $checks
exit $LASTEXITCODE
