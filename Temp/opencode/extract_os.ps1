$lines=[System.IO.File]::ReadAllLines('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\OrdemServico\OrdemServicoRepository.cs')
for($i=0;$i -lt $lines.Count;$i++){
  $l=$lines[$i]; $ix=$l.IndexOf('integracao_outbox');
  if($ix -ge 0){
    $start=[Math]::Max(0,$ix-120); $len=[Math]::Min(450,$l.Length-$start);
    Write-Output ('LINE=' + ($i+1));
    Write-Output $l.Substring($start,$len)
  }
}
