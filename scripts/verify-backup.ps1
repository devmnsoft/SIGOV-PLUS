param([Parameter(Mandatory=$true)][string]$BackupFile)
$ErrorActionPreference = "Stop"
if (!(Test-Path $BackupFile)) { throw "Arquivo de backup não encontrado." }
$hash = (Get-FileHash -Algorithm SHA256 -Path $BackupFile).Hash
$size = (Get-Item $BackupFile).Length
Write-Host "Arquivo: $BackupFile"
Write-Host "TamanhoBytes: $size"
Write-Host "SHA256: $hash"
