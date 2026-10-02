$f = Join-Path $PSScriptRoot 'ast_case.txt'
@"
$h=@{ json=@{ id='a'; numero='n' } }
Write-Output 'x'
$A=[string]$h.json.id; $B=[string]$h.json.numero
"@ | Set-Content -Path $f -Encoding ASCII
$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($f,[ref]$tok,[ref]$err)
$st=$ast.Body.Statements
Write-Output ('statements=' + $st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  Write-Output ('--- stmt ' + $i + ' type=' + $s.GetType().Name)
  Write-Output ('    extent=[' + $s.Extent.Text + ']')
  if($s -is [System.Management.Automation.Language.PipelineAst]){
    foreach($ps in $s.PipelineElements){
      Write-Output ('    pipelineElem type=' + $ps.GetType().Name + ' text=[' + $ps.Extent.Text + ']')
      if($ps -is [System.Management.Automation.Language.CommandAst]){
        Write-Output ('      command=' + $ps.GetCommandName().Extent.Text)
        foreach($ea in $ps.CommandElements){ Write-Output ('      elem=[' + $ea.Extent.Text + ']') }
      }
    }
  }
}
