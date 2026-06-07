$ErrorActionPreference = 'Stop'
Write-Host '== sigov go-live check =='
& "$PSScriptRoot/security-check.ps1"
& "$PSScriptRoot/check-residues.ps1"
& "$PSScriptRoot/check-module-map.ps1"
& "$PSScriptRoot/check-web-assets.ps1"
Write-Host '[OK] Segurança e resíduos verificados.'
Write-Host '[INFO] Execute scripts/validate.ps1 em ambiente com .NET 6 e Docker antes do release.'
Write-Host '[INFO] Valide /api/health, /api/health/live, /api/health/ready, /api/health/db, /api/health/outbox e /api/health/version.'
