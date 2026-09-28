[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$t=[IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Web\Views\ComprasEmpresariais\Aprovacoes\Index.cshtml',[Text.Encoding]::UTF8)
foreach($n in @('Aguarda configuração','Decisão sua','Outro aprovador designado','Idempotency-Key','Próxima ação')){
  $i=$t.IndexOf($n,[StringComparison]::Ordinal)
  Write-Host ('== ' + $n + ' idx=' + $i)
  if($i -ge 0){ Write-Host ($t.Substring([Math]::Max(0,$i-160), [Math]::Min(360, $t.Length-[Math]::Max(0,$i-160)))) }
}
