[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$files = Get-ChildItem 'C:\MNSOFT\SIGOV-PLUS\src' -Recurse -Filter *.cs | Where-Object { $_.FullName -match 'ComprasEmpresariais|ComprasEmpresaria' }
foreach($f in $files){
  $t = Get-Content $f.FullName -Raw -Encoding UTF8
  $o = ($t.ToCharArray() | Where-Object {$_ -eq '('}).Count
  $c = ($t.ToCharArray() | Where-Object {$_ -eq ')'}).Count
  if($o -ne $c){ Write-Host ("DESBALANCEADO: {0} saldo={1}" -f $f.FullName.Substring(23),($o-$c)) }
}
Write-Host '=== verificação do insereEtapa corrigido ==='
$text = Get-Content 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs' -Raw -Encoding UTF8
$start = $text.IndexOf('const string insereEtapa="')
$end = $text.IndexOf('";', $start + 20)
$sql = $text.Substring($start + ('const string insereEtapa="'.Length), $end - $start - ('const string insereEtapa="'.Length))
$depth = 0
for($i=0; $i -lt $sql.Length; $i++){ if($sql[$i] -eq '('){$depth++} elseif($sql[$i] -eq ')'){$depth--} }
Write-Host ("saldo insereEtapa = " + $depth)
$vstart = $sql.IndexOf('values(') + 7
$d2 = 1; $commas = 0
for($i=$vstart; $i -lt $sql.Length; $i++){
  $ch = $sql[$i]
  if($ch -eq '('){ $d2++ } elseif($ch -eq ')'){ $d2--; if($d2 -eq 0){ break } } elseif($ch -eq ',' -and $d2 -eq 1){ $commas++ }
}
Write-Host ("expressoes values = " + ($commas+1) + " (colunas = 12)")
