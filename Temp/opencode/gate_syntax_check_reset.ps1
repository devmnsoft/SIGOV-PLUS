$t=$null; $e=$null
[void][System.Management.Automation.Language.Parser]::ParseFile('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset.ps1',[ref]$t,[ref]$e)
if($e.Count){ $e | ForEach-Object { '{0}:{1} {2}' -f $_.Extent.StartLineNumber, $_.Extent.StartColumnNumber, $_.Message }; exit 1 } else { Write-Host 'SYNTAX_OK' }
