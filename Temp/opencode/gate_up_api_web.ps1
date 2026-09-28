[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
Set-Location C:\MNSOFT\SIGOV-PLUS
cmd /c "docker compose up -d --no-deps --force-recreate api web > C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_up_api_web.log 2>&1"
Write-Host ('UP_EXIT=' + $LASTEXITCODE)
Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_up_api_web.log'
$st = ''
for ($i = 1; $i -le 36; $i++) {
  Start-Sleep -Seconds 5
  $st = (docker inspect --format '{{.State.Health.Status}}|{{.State.Status}}' sigov-api sigov-web) -join ','
  Write-Host ('t=' + ($i * 5) + 's ' + $st)
  if ($st -eq 'healthy|running,healthy|running') { Write-Host 'BOTH_HEALTHY'; break }
}
Write-Host ('FINAL_STATUS=' + $st)
