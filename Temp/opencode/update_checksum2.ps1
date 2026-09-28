$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$repo = 'C:\MNSOFT\SIGOV-PLUS'
$mig  = Join-Path $repo 'database/postgres/migrations/20260927120000_compras_aprovacoes_fluxo_e_divergencia_codificada.sql'
$man  = Join-Path $repo 'database/postgres/migrations/manifest.json'

$text = [System.IO.File]::ReadAllText($mig)
$lf = $text -replace "`r`n", "`n"
$hasher = [System.Security.Cryptography.SHA256]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($lf)
$dig = $hasher.ComputeHash($bytes)
$hex = (($dig | ForEach-Object { $_.ToString('x2') }) -join '')
Write-Host "NEW_CHECKSUM=$hex"

$manifest = [System.IO.File]::ReadAllText($man)
$old = '513583d201d8e9298b25d04a5a398f1182016b39ff3a81adbcc78a73fade7497'
if ($manifest.Contains($old)) {
  if ([regex]::Matches($manifest, [regex]::Escape($old)).Count -ne 1) { throw "old checksum occurs more than once" }
  $manifest = $manifest.Replace($old, $hex)
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($man, $manifest, $enc)
  Write-Host 'MANIFEST_UPDATED'
} else {
  Write-Host 'OLD_CHECKSUM_NOT_FOUND (may already be updated)'
}
