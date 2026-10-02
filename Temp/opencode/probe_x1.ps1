$ErrorActionPreference = 'Stop'
function ShowAll([string]$t,[string]$n,[string]$tag){
  $j = 0; $k = -1; $c = 0
  while (($k = $t.IndexOf($n, $j)) -ge 0) {
    $c++
    Write-Output ('### ' + $tag + ' [' + $c + '] IDX=' + $k)
    Write-Output $t.Substring([Math]::Max(0,$k-240), [Math]::Min($t.Length-[Math]::Max(0,$k-240), 460))
    Write-Output '----'
    $j = $k + 1
  }
  if ($c -eq 0) { Write-Output ('### ' + $tag + ' : NONE') }
}
$efb = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\efb_compras.cs', [System.Text.Encoding]::UTF8)
$head = [System.IO.File]::ReadAllText('C:\Users\NCELL-DEV-020\AppData\Local\Temp\opencode\sigov-head\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
$wt = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
ShowAll $efb '.Where(ap=>' 'EFB-filters'
ShowAll $head '.Where(ap=>' 'HEAD-filters'
ShowAll $wt '.Where(ap=>' 'WT-filters'
