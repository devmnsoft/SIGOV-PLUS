$ErrorActionPreference='Continue'
$tokens=$null; $errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset.ps1',[ref]$tokens,[ref]$errors)
if($errors -and $errors.Count -gt 0){ Write-Output ('SYNTAX_ERRORS=' + $errors.Count); foreach($e in $errors){ Write-Output ('  L' + $e.Extent.StartLineNumber + ': ' + $e.Message) } } else { Write-Output 'SYNTAX_RESET=OK' }
Set-Location 'C:\MNSOFT\SIGOV-PLUS'
Write-Output '=== RESET ==='
& 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset.ps1' *>&1 | Tee-Object 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset_run8.log' | Out-Null
Write-Output ('RESET_EXIT=' + $LASTEXITCODE)
Select-String -Path 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset_run8.log' -SimpleMatch 'RESET_OK' | ForEach-Object { Write-Output $_.Line }
Write-Output '=== GATE RUN8 ==='
& 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1' *> 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b_run8.log'
Write-Output ('GATE_EXIT=' + $LASTEXITCODE)
Select-String -Path 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b_run8.log' -SimpleMatch 'RESUMO' | ForEach-Object { Write-Output $_.Line }
Write-Output 'RUNNER_DONE'
