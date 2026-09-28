[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$lines = Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe_enviar_r2.sql' -Encoding UTF8
$l = $lines[22]  # linha 23 (0-based 22) = values(...)
Write-Host ("LINE LEN=" + $l.Length)
$idx = $l.IndexOf('values(') + 7  # apos o '(' de values
$depth = 1
$commas = 0
for($i=$idx; $i -lt $l.Length; $i++){
  $ch = $l[$i]
  if($ch -eq '('){ $depth++ }
  elseif($ch -eq ')'){ $depth--; if($depth -eq 0){ Write-Host ("values() CLOSED at pos " + $i + " of " + $l.Length + " | depois: '" + $l.Substring([Math]::Min($i+1,$l.Length-8)) + "'"); break } }
  elseif($ch -eq ',' -and $depth -eq 1){ $commas++ }
}
Write-Host ("commas em nivel values = " + $commas + " => expressoes = " + ($commas+1) + " (colunas = 12)")
