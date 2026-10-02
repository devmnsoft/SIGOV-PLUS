$ErrorActionPreference='Continue'
function Cnt($p){ ($p | Measure-Object).Count }
$syn = @(@{s='SELECIONADA';id='x'},@{s='ENCERRADA';id='a0000001-0000-4000-8000-000000000201'},@{s='SELECIONADA';id='y'})
Write-Output ('S1 syn where s eq ENC   : ' + (Cnt @($syn | Where-Object { $_.s -eq 'ENCERRADA' })))
Write-Output ('S2 syn where s eq SEL   : ' + (Cnt @($syn | Where-Object { $_.s -eq 'SELECIONADA' })))
Write-Output ('S3 syn where id eq guid : ' + (Cnt @($syn | Where-Object { $_.id -eq 'a0000001-0000-4000-8000-000000000201' })))
$ev=Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_evidence.txt' -Encoding UTF8
$cl=$ev[4236] | ConvertFrom-Json
Write-Output ('R1 cl where status ENC  : ' + (Cnt @($cl.items | Where-Object { $_.status -eq 'ENCERRADA' })))
Write-Output ('R2 cl where status SEL  : ' + (Cnt @($cl.items | Where-Object { $_.status -eq 'SELECIONADA' })))
Write-Output ('R3 cl foreach ENC       : ' + @($cl.items | ForEach-Object { if($_.status -eq 'ENCERRADA'){'X'} }).Count)
Write-Output ('R4 cl where match ENC   : ' + (Cnt @($cl.items | Where-Object { $_.status -match 'ENCERRADA' })))
$rpB=$ev[4157] | ConvertFrom-Json
Write-Output ('R5 rpB almox where      : ' + (Cnt @($rpB.almoxarifados | Where-Object { $_.id -eq 'a0000001-0000-4000-8000-000000000201' })))
Write-Output ('R6 rpB almox foreach    : ' + @($rpB.almoxarifados | ForEach-Object { if($_.id -eq 'a0000001-0000-4000-8000-000000000201'){'X'} }).Count)
Write-Output ('R7 PS version           : ' + $PSVersionTable.PSVersion.ToString())
Write-Output 'PROBE3_DONE'
