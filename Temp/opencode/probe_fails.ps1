$ErrorActionPreference='Continue'
# Replica as 3 assercoes falhas do gate com os bodies EXATOS da evidencia (run6)
$ev = Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_evidence.txt' -Encoding UTF8

function JEqD($a,$b){ try{ return [math]::Abs([decimal][double]$a - [decimal]$b) -lt ([decimal]0.005) }catch{ return ('ERR:' + $_.Exception.Message) } }
function JNearDate([string]$iso,[int]$dias){ try{ $d=[datetime]$iso; $base=(Get-Date).Date.AddDays($dias); return [math]::Abs(($d.Date - $base).Days) -le 1 }catch{ return ('ERR:' + $_.Exception.Message) } }

# linhas da evidencia (1-based): 4110 = dpB; 4158 = rpB; 4237 = cl
$dpB = $ev[4109] | ConvertFrom-Json
$rpB = $ev[4157] | ConvertFrom-Json
$cl  = $ev[4236] | ConvertFrom-Json

Write-Output ('hoje_local=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))

# constantes reconstruidas a partir da mesma evidencia (S3)
$PB='548e1758-bf7a-43ea-bc6b-3b1334ccaa51'
$C1='10a1de96-c460-4cfe-b35c-a78785e6d2e9'
$NUMC1='CT-2026-000007'
$Rq1='d0000001-0000-4000-8000-000000000001'
$Q1=[decimal]10
$EI1=$false
$PPa='e0000001-0000-4000-8000-000000000101'
$expCueB1 =[math]::Round([decimal]90 * ([decimal]1 + [decimal]0/[decimal]100 - [decimal]0/[decimal]100), 2, [MidpointRounding]::AwayFromZero)
$expCustoB1=[math]::Round([decimal]$expCueB1 * [decimal]$Q1 + [decimal]0, 2, [MidpointRounding]::AwayFromZero)

Write-Output '--- FAIL1 pedido Beta ---'
$ipB=@($dpB.itens)[0]
Write-Output ('c1 status CONFIRMADO : ' + ($dpB.status -eq 'CONFIRMADO'))
Write-Output ('c2 cotacaoId == C1   : ' + ([string]$dpB.cotacaoId -eq $C1))
Write-Output ('c3 requisicaoId == R1: ' + ([string]$dpB.requisicaoId -eq $Rq1))
Write-Output ('c4 numeroCotacao     : ' + ([string]$dpB.numeroCotacao -eq $NUMC1))
Write-Output ('c5 fornecedorNome    : ' + ([string]$dpB.fornecedorNome -like '*Beta*'))
Write-Output ('c6 valorTotal 900    : ' + (JEqD $dpB.valorTotal $expCustoB1))
Write-Output ('c7 previsao +7       : ' + (JNearDate $dpB.previsao 7))
Write-Output ('c8 itens count==1    : ' + (@($dpB.itens).Count -eq 1))
Write-Output ('c9 quantidade Q1     : ' + (JEqD $ipB.quantidade $Q1))
Write-Output ('c10 qtdCancelada 0   : ' + (JEqD $ipB.quantidadeCancelada 0))
Write-Output ('c11 valorUnitario CUE: ' + (JEqD $ipB.valorUnitario $expCueB1))
Write-Output ('c12 produtoId PPa    : ' + ([string]$ipB.produtoId -eq $PPa))
Write-Output ('c13 exigeInspecao EI1: ' + ($ipB.exigeInspecao -eq $EI1))
Write-Output ('  tipo exigeInspecao : ' + $null.GetType().FullName)

Write-Output '--- FAIL2 recebimento ---'
$rpi=@($rpB.itens)[0]
Write-Output ('d2 id == PB          : ' + ([string]$rpB.id -eq $PB))
Write-Output ('d3 status CONFIRMADO : ' + ($rpB.status -eq 'CONFIRMADO'))
Write-Output ('d4 version>=1        : ' + ([long]$rpB.version -ge 1))
Write-Output ('d5 fornecedor Beta   : ' + ([string]$rpB.fornecedor -like '*Beta*'))
Write-Output ('d6 qtdPedida Q1      : ' + (JEqD $rpi.quantidadePedida $Q1))
Write-Output ('d7 qtdPendente Q1    : ' + (JEqD $rpi.quantidadePendente $Q1))
Write-Output ('d8 qtdCancelada 0    : ' + (JEqD $rpi.quantidadeCancelada 0))
$d9=(@($rpB.almoxarifados) | Where-Object { ([string]$_.id) -eq 'a0000001-0000-4000-8000-000000000201' }).Count
Write-Output ('d9 almox count==1    : ' + $d9)

Write-Output '--- FAIL3 lista cotacoes ---'
Write-Output ('e1 totalItems==3     : ' + ([long]$cl.totalItems -eq 3))
$e2=(@($cl.items) | Where-Object { $_.status -eq 'SELECIONADA' }).Count
$e3=(@($cl.items) | Where-Object { $_.status -eq 'ENCERRADA' }).Count
Write-Output ('e2 SELECIONADA==2    : ' + $e2)
Write-Output ('e3 ENCERRADA==1      : ' + $e3)
Write-Output 'PROBE_DONE'
