$ErrorActionPreference = 'Stop'
$f1 = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
function Show([string]$t,[string]$n,[int]$b,[int]$a,[string]$tag){
  $i = $t.IndexOf($n, [System.StringComparison]::Ordinal)
  Write-Output ('### ' + $tag + ' IDX=' + $i)
  if ($i -lt 0) { return }
  $start = [Math]::Max(0, $i - $b)
  $len = [Math]::Min($t.Length - $start, $b + $a)
  Write-Output $t.Substring($start, $len)
  Write-Output '----'
}
Show $f1 'id and not is_deleted for update' 0 1900 'A4-guard'
$i = $f1.LastIndexOf('RegistrarPendenciaAsync')
Write-Output ('### A8-pendencia IDX=' + $i)
if ($i -ge 0) { $s=[Math]::Max(0,$i-1600); Write-Output $f1.Substring($s, [Math]::Min($f1.Length-$s, 2400)) }
Write-Output '----'
Show $f1 "'total_requisicao'" 120 950 'B3-snapshot'
