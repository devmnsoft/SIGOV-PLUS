$ErrorActionPreference='Continue'
$ev = Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_evidence.txt' -Encoding UTF8
$cl  = $ev[4236] | ConvertFrom-Json
$rpB = $ev[4157] | ConvertFrom-Json

function DumpStr([string]$s){
  if($null -eq $s){ return '<NULL>' }
  $codes = ($s.ToCharArray() | ForEach-Object { [int][char]$_ }) -join ','
  "$s | len=$($s.Length) | codes=$codes"
}

Write-Output '--- cl.items ---'
$i=0
foreach($it in @($cl.items)){ Write-Output ("item[$i]: " -f $i); Write-Output ('  status => ' + (DumpStr ([string]$it.status))); $i++ }
Write-Output '--- rpB.almoxarifados ---'
$j=0
foreach($am in @($rpB.almoxarifados)){ Write-Output ("almox[$j]: " -f $j); Write-Output ('  id     => ' + (DumpStr ([string]$am.id))); $j++ }
$lit='a0000001-0000-4000-8000-000000000201'
Write-Output ('literal  => ' + (DumpStr $lit))
$enc='ENCERRADA'
Write-Output ('enc-lit  => ' + (DumpStr $enc))
Write-Output ('eq-test status[1] vs ENCERRADA: ' + (([string]$cl.items[1].status) -eq 'ENCERRADA'))
Write-Output ('eq-test almox id vs literal   : ' + (([string]$rpB.almoxarifados[0].id) -eq $lit))
Write-Output 'PROBE2_DONE'
