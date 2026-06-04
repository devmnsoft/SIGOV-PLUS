param(
  [Parameter(Mandatory=$true)][string]$BackupFile,
  [string]$Environment = $env:ASPNETCORE_ENVIRONMENT,
  [string]$ConnectionString = $env:ConnectionStrings__DefaultConnection,
  [string]$ConfirmRestore = ""
)
$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw "ConnectionStrings__DefaultConnection obrigatório." }
if ($Environment -eq "Production" -and $ConfirmRestore -ne "RESTORE_PRODUCTION_SIGOV") { throw "Restore em Production exige -ConfirmRestore RESTORE_PRODUCTION_SIGOV." }
if (!(Test-Path $BackupFile)) { throw "Arquivo de backup não encontrado." }
pg_restore --clean --if-exists --no-owner --no-privileges --dbname=$ConnectionString $BackupFile
Write-Host "Restore sigov concluído para ambiente $Environment."
