$f = Join-Path $PSScriptRoot 'ast_case.txt'
$bytes = [IO.File]::ReadAllBytes($f)
Write-Output ('file bytes: ' + ($bytes -join ','))
Write-Output ('errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR L' + $e.Extent.StartLineNumber + ':' + $e.Extent.StartColumnNumber + ' ' + $e.Message + ' :: ' + $e.Extent.Text) }
$st=$ast.Body.Statements
Write-Output ('statements=' + $st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  Write-Output ('--- stmt ' + $i + ' type=' + $s.GetType().Name + ' extent=[' + $s.Extent.Text + ']')
}
