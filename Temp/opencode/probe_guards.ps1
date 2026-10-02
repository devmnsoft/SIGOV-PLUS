$ErrorActionPreference = 'Stop'
function ShowAll([string]$t,[string]$n,[int]$b,[int]$a,[string]$tag){
  $j = 0; $k = -1; $c = 0
  while (($k = $t.IndexOf($n, $j)) -ge 0) {
    $c++
    Write-Output ('### ' + $tag + ' [' + $c + '] IDX=' + $k)
    $s = [Math]::Max(0, $k - $b)
    Write-Output $t.Substring($s, [Math]::Min($t.Length - $s, $b + $a))
    Write-Output '----'
    $j = $k + 1
  }
  if ($c -eq 0) { Write-Output ('### ' + $tag + ' : NONE') }
}
$t = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\efb_compras.cs', [System.Text.Encoding]::UTF8)
ShowAll $t 'Version!=version' 40 300 'EFB-verguard'
ShowAll $t "status in('RASCUNHO','DEVOLVIDA')" 160 120 'EFB-statusin'
ShowAll $t "case when status='DEVOLVIDA'" 120 200 'EFB-casewhen'
$w = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
ShowAll $w "status in('RASCUNHO','DEVOLVIDA')" 220 120 'WT-statusin'
ShowAll $w "case when status='DEVOLVIDA'" 120 220 'WT-casewhen'
