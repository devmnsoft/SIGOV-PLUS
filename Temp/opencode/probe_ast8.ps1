$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe8.ps1',[ref]$tok,[ref]$err)
Write-Output ('parse errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR L'+$e.Extent.StartLineNumber+' ['+$e.Message+'] '+$e.Extent.Text) }
$st=@($ast.EndBlock.Statements)
Write-Output ('statements='+$st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  $txt=$s.Extent.Text -replace "`r",'<CR>' -replace "`n",'<LF>'
  Write-Output ('stmt '+$i+' L'+$s.Extent.StartLineNumber+' ['+$txt+']')
}
