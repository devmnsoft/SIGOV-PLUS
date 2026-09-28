[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
foreach($f in @(
  'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs',
  'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\AprovacaoRequisicaoRepository.cs'
)){
  $t = Get-Content $f -Raw -Encoding UTF8
  $o = ($t.ToCharArray() | Where-Object {$_ -eq '('}).Count
  $c = ($t.ToCharArray() | Where-Object {$_ -eq ')'}).Count
  Write-Host ("{0}: abertos={1} fechados={2} saldo={3}" -f (Split-Path $f -Leaf),$o,$c,($o-$c))
}
