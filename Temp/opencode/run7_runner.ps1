$ErrorActionPreference='Continue'
Set-Location 'C:\MNSOFT\SIGOV-PLUS'
Write-Output '=== RESET ==='
& 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset.ps1' *>&1 | Tee-Object 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset_run7.log' | Out-Null
Write-Output ('RESET_EXIT=' + $LASTEXITCODE)
Select-String -Path 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset_run7.log' -SimpleMatch 'RESET_OK' | ForEach-Object { Write-Output $_.Line }
Write-Output '=== GATE RUN7 ==='
& 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1' *> 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b_run7.log'
Write-Output ('GATE_EXIT=' + $LASTEXITCODE)
Write-Output '=== RESUMO ==='
Select-String -Path 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b_run7.log' -SimpleMatch 'GATE BLOCO B RESUMO' | ForEach-Object { Write-Output $_.Line }
Write-Output 'RUNNER_DONE'
