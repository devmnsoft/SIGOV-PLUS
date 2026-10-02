$code = $null
$lines = @(
  `$h=@{ json=@{ id='a'; numero='n' } }`,
  `Write-Output 'x'`,
  `$A=[string]$h.json.id; $B=[string]$h.json.numero`
)
# sem backtick: construir sem interpolar
$code = "`$h=@{ json=@{ id='a'; numero='n' } }`r`nWrite-Output 'x'`r`n`$A=[string]`$h.json.id; `$B=[string]`$h.json.numero"
$f = Join-Path $PSScriptRoot 'ast_case.txt'
[IO.File]::WriteAllText($f, $code, [System.Text.Encoding]::ASCII)
Write-Output '--- file content:'
[IO.File]::ReadAllLines($f) | ForEach-Object { Write-Output ('|' + $_ + '|') }
$tok=$null;$err=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($f,[ref]$tok,[ref]$err)
Write-Output ('parse errors: ' + @($err).Count)
foreach($e in $err){ Write-Output ('ERR [' + $e.Message + ']') }
$st=@($ast.EndBlock.Statements)
Write-Output ('statements=' + $st.Count)
for($i=0;$i -lt $st.Count;$i++){
  $s=$st[$i]
  $txt=$s.Extent.Text -replace [char]10,'<LF>' -replace [char]13,'<CR>'
  Write-Output ('stmt ' + $i + ' type=' + $s.GetType().Name + ' extent=[' + $txt + ']')
}
