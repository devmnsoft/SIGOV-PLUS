$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
Set-Location C:\MNSOFT\SIGOV-PLUS
$envFile = Join-Path (Get-Location) '.env'
if (Test-Path $envFile) {
  Write-Host 'ENV_FILE_EXISTS'
  (Get-Content $envFile | ForEach-Object { if ($_ -match '^(\w+)=') { $Matches[1] } }) | ForEach-Object { Write-Host ("KEY=" + $_) }
} else {
  Write-Host 'ENV_FILE_MISSING'
}
