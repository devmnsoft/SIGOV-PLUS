$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\ast_case.txt',[ref]$tok,[ref]$err)
Write-Output ('parse errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR [' + $e.Message + ']') }
$st=@($ast.EndBlock.Statements)
Write-Output ('statements=' + $st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  $txt=$s.Extent.Text -replace "`r",'<CR>' -replace "`n",'<LF>'
  Write-Output ('stmt ' + $i + ' type=' + $s.GetType().Name + ' extent=[' + $txt + ']')
}
