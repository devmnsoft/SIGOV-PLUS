$f = Join-Path $PSScriptRoot 'ast_case.txt'
$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($f,[ref]$tok,[ref]$err)
Write-Output ('parse errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR L' + $e.StartLineNumber + ':' + $e.StartColumnNumber + ' [' + $e.Message + ']') }
$raw = [IO.File]::ReadAllText($f)
Write-Output ('raw file repr: ' + ($raw -replace "`r","<CR>") -replace "`n","<LF>"))
$src = $raw
$tok2=$null;$err2=$null
$ast2=[System.Management.Automation.Language.Parser]::ParseInput($src,[ref]$tok2,[ref]$err2)
Write-Output ('parse2 errors: ' + @($err2).Count)
Write-Output ('parse2 statements: ' + @($ast2.EndBlock.Statements).Count)
for($i=0; $i -lt @($ast2.EndBlock.Statements).Count; $i++){
  $s = @($ast2.EndBlock.Statements)[$i]
  Write-Output ('stmt ' + $i + ' type=' + $s.GetType().FullName + ' extent=[' + ($s.Extent.Text -replace "`r","<CR>") -replace "`n","<LF>") + ']')
}
