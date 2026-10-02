[CmdletBinding()] param(
    [string]$HostName = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'sigov',
    [string]$MaintenanceDatabase = 'postgres',
    [string]$AdminUser = 'postgres',
    [string]$AdminPassword = $env:PGPASSWORD,
    [string]$AppDbUser = 'sigov',
    [string]$AppDbPassword = $env:SIGOV_DB_PASSWORD,
    [string]$EnvFile = (Join-Path (Split-Path -Parent $PSScriptRoot) '.env.local')
)

$ErrorActionPreference = 'Stop'
if (!(Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql não encontrado no PATH.' }
if ([string]::IsNullOrWhiteSpace($AppDbPassword)) { throw 'Defina AppDbPassword ou SIGOV_DB_PASSWORD.' }

# Validação estrita de identificadores para impedir injeção SQL estrutural
$identRegex = '^[a-zA-Z_][a-zA-Z0-9_]*$'
if ($AppDbUser -notmatch $identRegex) { throw "Nome de usuário de banco inválido: '$AppDbUser'." }
if ($Database -notmatch $identRegex) { throw "Nome de banco de dados inválido: '$Database'." }
if ($MaintenanceDatabase -notmatch $identRegex) { throw "Nome de banco de manutenção inválido: '$MaintenanceDatabase'." }
if ($AdminUser -notmatch $identRegex) { throw "Nome de usuário admin inválido: '$AdminUser'." }

$env:PGPASSWORD = $AdminPassword
$escapedPassword = $AppDbPassword.Replace("'", "''")
$quotedUser = "`"$AppDbUser`""
$quotedDb = "`"$Database`""

# Criação ou atualização do usuário de runtime com senha escapada e identificadores delimitados
$sql = @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$AppDbUser') THEN
        CREATE ROLE $quotedUser LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$escapedPassword';
    ELSE
        ALTER ROLE $quotedUser LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$escapedPassword';
    END IF;
END `$`$;
GRANT CONNECT ON DATABASE $quotedDb TO $quotedUser;
"@

& psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AdminUser -d $MaintenanceDatabase -c $sql | Out-Null
if ($LASTEXITCODE) { throw 'Falha ao criar/atualizar usuário runtime.' }

# Concessões mínimas necessárias ao runtime da aplicação (PostgreSQL + Dapper)
# - Conexão e uso do schema sigov
# - CRUD de tabelas de negócio
# - Sequências
# - Default privileges para tabelas futuras
# - Proteção de tabelas de auditoria contra truncamento
$grantsSql = @"
GRANT USAGE ON SCHEMA sigov TO $quotedUser;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sigov TO $quotedUser;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA sigov TO $quotedUser;
ALTER DEFAULT PRIVILEGES IN SCHEMA sigov GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO $quotedUser;
ALTER DEFAULT PRIVILEGES IN SCHEMA sigov GRANT USAGE, SELECT ON SEQUENCES TO $quotedUser;
DO `$`$
BEGIN
    IF to_regclass('sigov.auditoria_evento') IS NOT NULL THEN
        REVOKE TRUNCATE, DELETE ON TABLE sigov.auditoria_evento FROM $quotedUser;
    END IF;
    IF to_regclass('sigov.saas_evento_comercial') IS NOT NULL THEN
        REVOKE TRUNCATE, DELETE ON TABLE sigov.saas_evento_comercial FROM $quotedUser;
    END IF;
END `$`$;
"@

& psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AdminUser -d $Database -c $grantsSql | Out-Null
if ($LASTEXITCODE) { throw 'Falha ao conceder permissões runtime.' }

# Validação funcional da conexão autenticada do usuário de runtime
$env:PGPASSWORD = $AppDbPassword
& psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AppDbUser -d $Database -Atqc 'select 1' | Out-Null
if ($LASTEXITCODE) { throw 'Usuário provisionado, mas o teste runtime falhou.' }
Write-Host "Usuário runtime '$AppDbUser' sincronizado e validado com identificadores verificados (senha omitida)."
