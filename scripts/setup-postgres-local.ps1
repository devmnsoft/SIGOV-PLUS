param([string]$HostName='localhost',[int]$Port=5432,[string]$AdminUser='postgres',[string]$Database='sigov',[string]$AppUser='sigov',[switch]$OnlyValidate,[switch]$CreateRole,[switch]$CreateDatabase)
$ErrorActionPreference='Stop'; $root=Split-Path -Parent $PSScriptRoot; if(-not(Get-Command psql -ErrorAction SilentlyContinue)){throw 'psql não encontrado.'}
$log=Join-Path $root '.local/logs/setup-postgres-local.log'; New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
if(-not $OnlyValidate){ & psql -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AdminUser -d $Database -f (Join-Path $root 'script_completop.sql') *>&1 | Tee-Object -FilePath $log }
& psql -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $AppUser -d $Database -c "select count(*) from sigov.schema_migrations" | Out-Null
Write-Host 'PostgreSQL local validado.'
