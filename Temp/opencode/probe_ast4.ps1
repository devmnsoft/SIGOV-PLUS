$f = Join-Path $PSScriptRoot 'ast_case.txt'
@'
$h=@{ json=@{ id='a'; numero='n' } }
Write-Output 'x'
$A=[string]$h.json.id; $B=[string]$h.json.numero
'@ | Set-Content -Path $f -Encoding ASCII
$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($f,[ref]$tok,[ref]$err)
Write-Output ('parse errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR [' + $e.Message + ']') }
$st=@($ast.EndBlock.Statements)
Write-Output ('statements=' + $st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  Write-Output ('stmt ' + $i + ' type=' + $s.GetType().Name + ' extent=[' + ($s.Extent.Text -replace "`r","<CR>") -replace "`n","<LF>") + ']')
  if($s -is [System.Management.Automation.Language.PipelineAst]){
    foreach($pe in $s.PipelineElements){
      Write-Output ('   elem type=' + $pe.GetType().Name + ' text=[' + $pe.Extent.Text + ']')
    }
  }
}
