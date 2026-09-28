[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$lines = Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe_enviar_r2.sql' -Encoding UTF8
$l = $lines[22]
$idx = $l.IndexOf('values(') + 7
$depth = 1
for($i=$idx; $i -lt $l.Length; $i++){
  $ch = $l[$i]
  if($ch -eq '('){ $depth++; $ctx = $l.Substring([Math]::Max(0,$i-30), [Math]::Min(30, $i)) ; Write-Host ("POS {0} OPEN  depth={1}  antes: ...{2}" -f $i,$depth,$ctx.TrimEnd()) }
  elseif($ch -eq ')'){ $pre = $l.Substring([Math]::Max(0,$i-30), [Math]::Min(30,$i)); $depth--; Write-Host ("POS {0} CLOSE depth->{1}  antes: ...{2}" -f $i,$depth,$pre.TrimEnd()) }
}
