[CmdletBinding()]
param(
    [string]$HostName = $(if ($env:SIGOV_DB_HOST) { $env:SIGOV_DB_HOST } else { 'localhost' }),
    [int]$Port = $(if ($env:SIGOV_DB_PORT) { [int]$env:SIGOV_DB_PORT } else { 5432 }),
    [string]$Database = $(if ($env:SIGOV_DB_NAME) { $env:SIGOV_DB_NAME } else { 'postgres' }),
    [string]$Schema = 'sigov',
    [string]$User = $(if ($env:SIGOV_DB_USER) { $env:SIGOV_DB_USER } else { 'postgres' }),
    [string]$Password = $(if ($env:SIGOV_DB_PASSWORD) { $env:SIGOV_DB_PASSWORD } elseif ($env:PGPASSWORD) { $env:PGPASSWORD } else { '123456' }),
    [string]$MaintenanceUser = $(if ($env:SIGOV_DB_ADMIN_USER) { $env:SIGOV_DB_ADMIN_USER } else { 'postgres' }),
    [string]$MaintenancePassword = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { '123456' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7+ obrigatório.' }
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql não encontrado. Instale o cliente PostgreSQL 16+.' }

function Invoke-CheckPsql([string]$TargetDatabase, [string]$TargetUser, [string]$TargetPassword, [string]$Sql) {
    $previous = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $TargetPassword
        $output = & psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $TargetUser -d $TargetDatabase -Atqc $Sql 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($output -join [Environment]::NewLine) }
        return ($output -join [Environment]::NewLine).Trim()
    }
    finally { $env:PGPASSWORD = $previous }
}

Write-Host 'Diagnóstico seguro do PostgreSQL local SIGOV+' -ForegroundColor Cyan
Write-Host "Destino: Host=$HostName; Port=$Port; Database=$Database; Username=$User; Password=<redacted>"
Write-Host "PostgreSQL encontrado: $((Get-Command psql).Source)"
Write-Host "Versão do cliente: $(& psql --version)"

$maintenancePassword = if ([string]::IsNullOrWhiteSpace($MaintenancePassword)) { $Password } else { $MaintenancePassword }
$serverVersion = Invoke-CheckPsql 'postgres' $MaintenanceUser $maintenancePassword "select current_setting('server_version');"
Write-Host "Versão do servidor: $serverVersion"
$databases = Invoke-CheckPsql 'postgres' $MaintenanceUser $maintenancePassword 'select datname from pg_database where datallowconn order by datname;'
Write-Host "Bancos existentes: $($databases -replace "`n", ', ')"
$databaseLiteral = $Database.Replace("'", "''")
$userLiteral = $User.Replace("'", "''")
$databaseExists = (Invoke-CheckPsql 'postgres' $MaintenanceUser $maintenancePassword "select exists(select 1 from pg_database where datname='$databaseLiteral');") -eq 't'
$userExists = (Invoke-CheckPsql 'postgres' $MaintenanceUser $maintenancePassword "select exists(select 1 from pg_roles where rolname='$userLiteral');") -eq 't'
Write-Host "Banco $Database existe? $databaseExists"
Write-Host "Usuário $User existe? $userExists"
if (-not $databaseExists -or -not $userExists) { throw 'Banco ou usuário runtime ausente. Execute: pwsh ./scripts/setup-dev.ps1' }

$runtime = Invoke-CheckPsql $Database $User $Password @"
select json_build_object(
  'database', current_database(),
  'connected_user', current_user,
  'search_path', current_setting('search_path'),
  'schema', to_regnamespace('sigov') is not null,
  'usuario_table', to_regclass('sigov.usuario') is not null,
  'admin_exists', exists(select 1 from sigov.usuario where lower(login)='admin' and not is_deleted),
  'superadmin_exists', exists(select 1 from sigov.usuario where lower(login)='superadmin' and not is_deleted),
  'admin_active', exists(select 1 from sigov.usuario where lower(login)='admin' and ativo and not is_deleted and not bloqueado),
  'hash_accepted', exists(select 1 from sigov.usuario where lower(login)='admin' and senha_hash ~ '^SIGOV_PBKDF2_V1[$][0-9]+[$][A-Za-z0-9+/]+={0,2}[$][A-Za-z0-9+/]+={0,2}$')
)::text;
"@
$state = $runtime | ConvertFrom-Json
Write-Host 'Usuário runtime conecta no banco? True'
Write-Host "Database atual: $($state.database)"
Write-Host "Usuário conectado: $($state.connected_user)"
Write-Host "search_path efetivo: $($state.search_path)"
Write-Host "Schema sigov existe? $($state.schema)"
Write-Host "Tabela sigov.usuario existe? $($state.usuario_table)"
Write-Host "Admin existe? $($state.admin_exists)"
Write-Host "Superadmin existe? $($state.superadmin_exists)"
Write-Host "Admin ativo e desbloqueado? $($state.admin_active)"
Write-Host "Hash admin aceito? $($state.hash_accepted)"
if (-not $state.schema -or -not $state.usuario_table -or -not $state.admin_exists -or -not $state.superadmin_exists -or -not $state.admin_active -or -not $state.hash_accepted) {
    throw 'Diagnóstico inválido. Execute novamente: pwsh ./scripts/setup-dev.ps1'
}
Write-Host 'Diagnóstico local concluído com sucesso.' -ForegroundColor Green
