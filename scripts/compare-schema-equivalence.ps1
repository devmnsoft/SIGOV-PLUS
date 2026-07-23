[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$HostName,
  [Parameter(Mandatory=$true)][int]$Port,
  [Parameter(Mandatory=$true)][string]$User,
  [Parameter(Mandatory=$true)][string]$MigrationDatabase,
  [Parameter(Mandatory=$true)][string]$BaselineDatabase
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path artifacts/schema-equivalence | Out-Null
function Invoke-Psql([string[]]$Arguments) {
  & psql @Arguments
  if ($LASTEXITCODE -ne 0) { throw "psql failed: $($Arguments -join ' ')" }
}
Invoke-Psql @('-h',$HostName,'-p',[string]$Port,'-U',$User,'-d','postgres','-v','ON_ERROR_STOP=1','-c',"drop database if exists $MigrationDatabase")
Invoke-Psql @('-h',$HostName,'-p',[string]$Port,'-U',$User,'-d','postgres','-v','ON_ERROR_STOP=1','-c',"drop database if exists $BaselineDatabase")
Invoke-Psql @('-h',$HostName,'-p',[string]$Port,'-U',$User,'-d','postgres','-v','ON_ERROR_STOP=1','-c',"create database $MigrationDatabase")
Invoke-Psql @('-h',$HostName,'-p',[string]$Port,'-U',$User,'-d','postgres','-v','ON_ERROR_STOP=1','-c',"create database $BaselineDatabase")
$env:PGDATABASE = $MigrationDatabase
./scripts/apply-migrations-manifest.ps1 -HostName $HostName -Port $Port -Database $MigrationDatabase -User $User
Invoke-Psql @('-h',$HostName,'-p',[string]$Port,'-U',$User,'-d',$BaselineDatabase,'-v','ON_ERROR_STOP=1','-f','script_completop.sql')
$schemaSql = @'
select table_schema, table_name, column_name, data_type, is_nullable, column_default
from information_schema.columns
where table_schema not in ('pg_catalog','information_schema')
order by 1,2,3;
'@
$migration = & psql -h $HostName -p $Port -U $User -d $MigrationDatabase -At -c $schemaSql
$baseline = & psql -h $HostName -p $Port -U $User -d $BaselineDatabase -At -c $schemaSql
$status = if (($migration -join "`n") -eq ($baseline -join "`n")) { 'equivalent' } else { 'different' }
@{ status = $status; comparedAt = (Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json | Set-Content artifacts/schema-equivalence/schema-diff.json
"# Schema equivalence`n`nStatus: $status`n" | Set-Content artifacts/schema-equivalence/schema-diff.md
if ($status -ne 'equivalent') { throw 'Schemas are different' }
