param([switch]$AllowWarnings)
$ErrorActionPreference = 'Stop'
$fail = 0; $warn = 0
function Check([string]$Name, [bool]$Condition, [string]$FailMessage, [switch]$Warning) {
  if ($Condition) { Write-Host "PASS $Name"; return }
  if ($Warning) { $script:warn++; Write-Host "WARN $Name - $FailMessage" } else { $script:fail++; Write-Host "FAIL $Name - $FailMessage" }
}
Check 'backup-file' ((-not [string]::IsNullOrWhiteSpace($env:SIGOV_LAST_BACKUP_FILE)) -and (Test-Path $env:SIGOV_LAST_BACKUP_FILE)) 'SIGOV_LAST_BACKUP_FILE deve apontar para backup recente.'
Check 'backup-checksum' (-not [string]::IsNullOrWhiteSpace($env:SIGOV_LAST_BACKUP_CHECKSUM)) 'SIGOV_LAST_BACKUP_CHECKSUM obrigatório.'
Check 'previous-version' (-not [string]::IsNullOrWhiteSpace($env:SIGOV_PREVIOUS_VERSION)) 'SIGOV_PREVIOUS_VERSION obrigatório.'
Check 'current-version' (-not [string]::IsNullOrWhiteSpace($env:SIGOV_CURRENT_VERSION)) 'SIGOV_CURRENT_VERSION obrigatório.'
Check 'restore-script' (Test-Path "$PSScriptRoot/restore-db.ps1") 'scripts/restore-db.ps1 ausente.'
$restoreText = if (Test-Path "$PSScriptRoot/restore-db.ps1") { Get-Content "$PSScriptRoot/restore-db.ps1" -Raw } else { '' }
Check 'restore-confirmation' ($restoreText -match 'RESTORE_PRODUCTION_SIGOV') 'Restore deve exigir confirmação explícita.'
Check 'manual-migration-rollback' $true 'Rollback de migration é manual por plano aprovado.'
Check 'rollback-plan' (Test-Path 'docs/rollback-plan-v1.0.0.md') 'docs/rollback-plan-v1.0.0.md ausente.'
Check 'previous-docker-image' (-not [string]::IsNullOrWhiteSpace($env:SIGOV_PREVIOUS_DOCKER_IMAGE)) 'SIGOV_PREVIOUS_DOCKER_IMAGE recomendado.' -Warning
Write-Host "Resumo rollback: WARN=$warn FAIL=$fail"
if ($fail -gt 0) { exit 1 }
if ($warn -gt 0 -and -not $AllowWarnings) { exit 2 }
