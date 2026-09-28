[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$lines = Get-Content 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs' -Encoding UTF8
$text = ($lines -join "`n")
$start = $text.IndexOf('const string insereEtapa="')
if($start -lt 0){ Write-Host 'insereEtapa nao encontrado'; exit 1 }
$end = $text.IndexOf('";', $start + 20)
$sql = $text.Substring($start + ('const string insereEtapa="'.Length), $end - $start - ('const string insereEtapa="'.Length))
Write-Host ("SQL LEN=" + $sql.Length)
# mapa de parenteses
$vpos = $sql.IndexOf('values(')
$depth = 0
for($i=0; $i -lt $sql.Length; $i++){
  if($sql[$i] -eq '('){ $depth++ } elseif($sql[$i] -eq ')'){ $depth-- }
}
Write-Host ("saldo total de parenteses na SQL completa = " + $depth)
# profundidade dentro de values(
$vstart = $vpos + 7
$depth = 1
$commas = 0
$events = @()
for($i=$vstart; $i -lt $sql.Length; $i++){
  $ch = $sql[$i]
  if($ch -eq '('){ $depth++; $events += ("{0}:OPEN d={1}" -f $i,$depth) }
  elseif($ch -eq ')'){ $depth--; $events += ("{0}:CLOSE d={1}" -f $i,$depth); if($depth -eq 0){ break } }
  elseif($ch -eq ',' -and $depth -eq 1){ $commas++ }
}
Write-Host ("commas em nivel values = " + $commas + " => expressoes = " + ($commas+1))
foreach($e in $events){ Write-Host $e }
