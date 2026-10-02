$ErrorActionPreference = 'Stop'
$head = [System.IO.File]::ReadAllText('C:\Users\NCELL-DEV-020\AppData\Local\Temp\opencode\sigov-head\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
function Show([string]$t,[string]$n,[int]$r){
  $i = $t.IndexOf($n, [System.StringComparison]::Ordinal)
  if ($i -lt 0) { Write-Output ('NOT FOUND: ' + $n.Substring(0,[Math]::Min(50,$n.Length))); return }
  $start = [Math]::Max(0, $i - $r)
  $len = [Math]::Min($t.Length - $start, ($i - $start) + [Math]::Min($n.Length,200) + $r)
  Write-Output ('IDX=' + $i)
  Write-Output $t.Substring($start, $len)
  Write-Output '----'
}
Show $head '"APROVACAO_SEM_POLITICA":"APROVACAO_SEM_APROVADOR"' 600
Show $head 'throw new ComprasConcurrencyException("A chave de idempot' 1200
