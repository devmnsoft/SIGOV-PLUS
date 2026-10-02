$ErrorActionPreference='Continue'
$tokens=$null; $errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1',[ref]$tokens,[ref]$errors)
if($errors -and $errors.Count -gt 0){
  Write-Output ('SYNTAX_ERRORS=' + $errors.Count)
  foreach($e in $errors){ Write-Output ('  L' + $e.Extent.StartLineNumber + ': ' + $e.Message) }
} else { Write-Output 'SYNTAX=OK' }
try {
  $h=Invoke-WebRequest -Uri 'http://localhost:5001/api/health/live' -UseBasicParsing -TimeoutSec 15
  Write-Output ('HEALTH=' + [int]$h.StatusCode)
} catch { Write-Output ('HEALTH_ERR=' + $_.Exception.Message) }
Write-Output 'PREFLIGHT_DONE'
