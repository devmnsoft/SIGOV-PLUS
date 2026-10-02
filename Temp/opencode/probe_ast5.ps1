$f = Join-Path $PSScriptRoot 'ast_case.txt'
$content = @'
$h=@{ json=@{ id='a'; numero='n' } }
Write-Output 'x'
$A=[string]$h.json.id; $B=[string]$h.json.numero
'@
[IO.File]::WriteAllText($f, $content, [System.Text.Encoding]::ASCII)
$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($f,[ref]$tok,[ref]$err)
Write-Output ('parse errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR [' + $e.Message + ']') }
$st=@($ast.EndBlock.Statements)
Write-Output ('statements=' + $st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  $ext=($s.Extent.Text -replace "`r","<CR>") -replace "`n","<LF>"
  Write-Output ('stmt ' + $i + ' type=' + $s.GetType().Name + ' extent=[' + $ext + ']')
  if($s -is [System.Management.Automation.Language.PipelineAst]){
    foreach($pe in $s.PipelineElements){
      Write-Output ('   elem type=' + $pe.GetType().Name + ' text=[' + $pe.Extent.Text + ']')
    }
  }
}
