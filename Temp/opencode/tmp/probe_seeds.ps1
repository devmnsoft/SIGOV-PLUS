$ErrorActionPreference = 'Continue'
Get-ChildItem "C:\MNSOFT\SIGOV-PLUS\database\postgres\seeds" -Filter "compras_*" | ForEach-Object { Write-Host ("{0}  {1} bytes" -f $_.Name, $_.Length) }
Write-Host "--- container /tmp:"
& docker exec sigov-postgres ls -la /tmp/ | Select-String seed
Write-Host "--- hash container gate_seed.sql:"
& docker exec sigov-postgres sha256sum /tmp/gate_seed.sql
Write-Host "--- hash local institucional:"
(Get-FileHash "C:\MNSOFT\SIGOV-PLUS\database\postgres\seeds\compras_aprovacao_institucional_seed.sql").Hash.ToLowerInvariant()
Write-Host "--- ids de requisicao no seed institucional:"
Select-String -Path "C:\MNSOFT\SIGOV-PLUS\database\postgres\seeds\compras_aprovacao_institucional_seed.sql" -Pattern "d0000001-[0-9a-f-]{32}" -AllMatches | ForEach-Object { $_.Matches.Value } | Sort-Object -Unique
