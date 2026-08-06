[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$HostName = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'sigov',
    [string]$MaintenanceDatabase = 'postgres',
    [string]$AdminUser = 'postgres',
    [string]$AdminPassword = $env:PGPASSWORD,
    [string]$AppDbUser = $(if ($env:SIGOV_DB_USER) { $env:SIGOV_DB_USER } else { 'sigov' }),
    [string]$AppDbPassword = $env:SIGOV_DB_PASSWORD,
    [string]$PsqlPath = 'psql'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeIdentifier {
    param([Parameter(Mandatory)][string]$Value, [Parameter(Mandatory)][string]$Name)
    if ($Value -notmatch '^[A-Za-z_][A-Za-z0-9_\-]{0,62}$') {
        throw "$Name inválido. Use somente letras, números, sublinhado e hífen, iniciando por letra ou sublinhado."
    }
}

function ConvertTo-SqlLiteral {
    param([AllowEmptyString()][string]$Value)
    if ($null -eq $Value) { return '' }
    return $Value.Replace("'", "''")
}

function Quote-PgIdentifier {
    param([Parameter(Mandatory)][string]$Value)
    return '"' + $Value.Replace('"', '""') + '"'
}

function Invoke-Psql {
    param(
        [Parameter(Mandatory)][string]$TargetDatabase,
        [Parameter(Mandatory)][string]$Username,
        [string]$Command,
        [switch]$Capture
    )

    $arguments = @(
        '-X',
        '--set', 'ON_ERROR_STOP=1',
        '--host', $HostName,
        '--port', $Port.ToString(),
        '--username', $Username,
        '--dbname', $TargetDatabase
    )
    if ($Capture) { $arguments += @('--tuples-only', '--no-align') }
    if ($Command) { $arguments += @('--command', $Command) }

    $output = & $PsqlPath @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "psql falhou no banco '$TargetDatabase' como usuário '$Username' com código $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }

    if ($Capture) { return ($output -join [Environment]::NewLine).Trim() }
    $output | ForEach-Object { Write-Host $_ }
}

Assert-SafeIdentifier -Value $Database -Name 'Database'
Assert-SafeIdentifier -Value $MaintenanceDatabase -Name 'MaintenanceDatabase'
Assert-SafeIdentifier -Value $AdminUser -Name 'AdminUser'
Assert-SafeIdentifier -Value $AppDbUser -Name 'AppDbUser'
if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
    throw 'Informe a senha administrativa do PostgreSQL em -AdminPassword ou na variável PGPASSWORD.'
}
if ([string]::IsNullOrWhiteSpace($AppDbPassword)) {
    throw 'Informe a senha do usuário runtime em -AppDbPassword ou na variável SIGOV_DB_PASSWORD. Use a mesma senha no .env.local/ConnectionStrings__DefaultConnection.'
}
if (-not (Get-Command $PsqlPath -ErrorAction SilentlyContinue)) {
    throw "psql não foi encontrado em '$PsqlPath'. Instale o cliente PostgreSQL 16+ ou informe -PsqlPath."
}

$previousPgPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $AdminPassword

    $databaseLiteral = ConvertTo-SqlLiteral $Database
    $databaseExists = Invoke-Psql -TargetDatabase $MaintenanceDatabase -Username $AdminUser -Capture -Command "select case when exists(select 1 from pg_database where datname = '$databaseLiteral') then '1' else '0' end;"
    if ($databaseExists -ne '1') {
        throw "Banco '$Database' não encontrado. Execute primeiro o script_completop.sql ou scripts/install-sigov-database.ps1."
    }

    $appUserLiteral = ConvertTo-SqlLiteral $AppDbUser
    $appPasswordLiteral = ConvertTo-SqlLiteral $AppDbPassword
    $quotedDatabase = Quote-PgIdentifier $Database
    $quotedAppUser = Quote-PgIdentifier $AppDbUser

    $roleSql = @"
do `$`$
begin
    if not exists (select 1 from pg_roles where rolname = '$appUserLiteral') then
        execute format('create role %I login password %L', '$appUserLiteral', '$appPasswordLiteral');
    else
        execute format('alter role %I with login password %L', '$appUserLiteral', '$appPasswordLiteral');
    end if;
end `$`$;
grant connect, create on database $quotedDatabase to $quotedAppUser;
"@

    if ($PSCmdlet.ShouldProcess("role $AppDbUser", 'criar/atualizar senha e permissões de conexão')) {
        Invoke-Psql -TargetDatabase $MaintenanceDatabase -Username $AdminUser -Command $roleSql
    }

    $grantSql = @"
create schema if not exists sigov;
grant usage, create on schema sigov to $quotedAppUser;
grant select, insert, update, delete on all tables in schema sigov to $quotedAppUser;
grant usage, select, update on all sequences in schema sigov to $quotedAppUser;
grant execute on all functions in schema sigov to $quotedAppUser;
alter default privileges in schema sigov grant select, insert, update, delete on tables to $quotedAppUser;
alter default privileges in schema sigov grant usage, select, update on sequences to $quotedAppUser;
alter default privileges in schema sigov grant execute on functions to $quotedAppUser;
do `$`$
begin
    if to_regclass('sigov.schema_migrations') is not null then
        execute format('grant select on table %I.%I to %I', 'sigov', 'schema_migrations', '$appUserLiteral');
    end if;
end `$`$;
"@

    if ($PSCmdlet.ShouldProcess("schema sigov", 'conceder permissões runtime')) {
        Invoke-Psql -TargetDatabase $Database -Username $AdminUser -Command $grantSql
    }

    $env:PGPASSWORD = $AppDbPassword
    $test = Invoke-Psql -TargetDatabase $Database -Username $AppDbUser -Capture -Command "select current_user || '@' || current_database();"

    Write-Host ''
    Write-Host 'Usuário runtime do SIGOV provisionado com sucesso.' -ForegroundColor Green
    Write-Host "Conexão testada: $test"
    Write-Host "Use no .env.local: SIGOV_DB_USER=$AppDbUser"
    Write-Host 'Use no .env.local a mesma senha informada em SIGOV_DB_PASSWORD/AppDbPassword.'
    Write-Host 'Depois de executar script_completop.sql, mantenha Sigov__Database__MigrationMode=Disabled ou SIGOV_RUN_MIGRATIONS=false no runtime local.'
}
finally {
    $env:PGPASSWORD = $previousPgPassword
}
