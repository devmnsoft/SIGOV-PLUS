[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
. (Join-Path $PSScriptRoot 'jornada_lib.ps1')
$script:JDbMode='docker'
$script:JFail=0
$script:TenantDemo='b0000001-0000-4000-8000-000000000001'
$Gq2=[guid]$script:Rq2
$GB=[guid]$script:SubB

# ---------------------------------------------------------------- helpers ---
# Copiados verbatim de gate_jornada.ps1 (mesmas assinaturas; o JApi abaixo
# sobrescreve o Invoke-WebRequest da lib pelo curl/cmd exigido no PS 5.1).
function JApi($method,$path,$token,$json=$null,$key=$null){
  $uri = if($path -match '^http'){$path} else { "$($script:JBase)$path" }
  $hostFor = switch($token){
    $script:TokA { 'municipio-demo.sigov.local' }
    $script:TokB { 'municipio-demo.sigov.local' }
    $script:TokD { 'sigov-local.sigov.local' }
    default { throw 'token sem host mapeado' }
  }
  $stg=Join-Path $PSScriptRoot 'tmp'
  if(-not (Test-Path $stg)){ New-Item -ItemType Directory -Path $stg | Out-Null }
  $t=[guid]::NewGuid().ToString('N')
  $jtmp=Join-Path $stg "ja_$t.json"; $htmp=Join-Path $stg "ja_$t.hdr"; $btmp=Join-Path $stg "ja_$t.body"
  $cl='curl.exe -s -X ' + $method + ' --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: ' + $hostFor + '"'
  if($key){ $cl += ' -H "Idempotency-Key: ' + $key + '"' }
  if($null -ne $json){ [IO.File]::WriteAllText($jtmp,$json,(New-Object System.Text.UTF8Encoding($false))); $cl += ' --data-binary @"' + $jtmp + '" -H "Content-Type: application/json; charset=utf-8"' }
  $cl += ' -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
  $err=& cmd.exe /c $cl 2>&1 | Out-String
  $ce=$LASTEXITCODE
  if($ce -ne 0){ JLog ("!! curl exit={0} para {1} {2}: {3}" -f $ce,$method,$uri,($err.Trim())) }
  $statusLine=(Get-Content $htmp -ErrorAction SilentlyContinue | Where-Object { $_ -match '^HTTP' } | Select-Object -First 1)
  $code=0
  if($statusLine){ $m=[regex]::Match($statusLine,'(\d{3})'); if($m.Success){ $code=[int]$m.Groups[1].Value } }
  $bodytxt=''; if(Test-Path $btmp){ $bodytxt=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8) }
  foreach($f in @($jtmp,$htmp,$btmp)){ Remove-Item $f -Force -ErrorAction SilentlyContinue }
  JLog ("--- {0} {1} => HTTP {2}" -f $method,$path,$code)
  if($bodytxt.Length -gt 4000){ JLog ($bodytxt.Substring(0,4000) + ' ...[truncado]') } else { JLog $bodytxt }
  $parsed=$null
  if($bodytxt -and ($bodytxt.TrimStart().StartsWith('{') -or $bodytxt.TrimStart().StartsWith('['))){ try{ $parsed=$bodytxt | ConvertFrom-Json }catch{ JLog ('!! falha ao parsear JSON: ' + $_.Exception.Message) } }
  return @{ status=$code; body=$bodytxt; json=$parsed }
}

function JVal($sql){
  $out=JDb $sql
  $line=($out -split "`r?`n" | Where-Object { $_.Trim() -and $_ -notlike '### SQL*' -and $_ -notmatch '^\(\d+ rows?\)$' } | Select-Object -Last 1)
  return ([string]$line).Trim()
}

function JAssert([string]$label,[bool]$cond,[string]$extra=''){
  $tag=if($cond){'PASS'}else{'FAIL'}
  $msg="ASSERT [{0}] {1}" -f $tag,$label
  if(-not [string]::IsNullOrEmpty($extra)){ $msg += " :: " + $extra }
  JLog $msg
  Write-Host $msg
  if(-not $cond){ $script:JFail++ }
}

function JItem([string]$desc,[double]$quant,[double]$valor){
  [ordered]@{ tipo='MATERIAL'; descricao=$desc; especificacao=$null; unidade='UN'; quantidade=$quant; valorEstimado=$valor; permiteParcial=$false; exigeInspecao=$false }
}

function JDecidirJson([string]$dec,[string]$mot,[long]$ver,[string]$key){
  $o=[ordered]@{ decisao=$dec; motivo=$mot; version=$ver; idempotencyKey=$key }
  New-Object PSObject -Property $o | ConvertTo-Json -Depth 4 -Compress
}

function JFilaItem($fila,$rqG,[int]$nivel,$subG){
  $fila.items | Where-Object { $_.requisicaoId -eq $rqG -and $_.nivel -eq $nivel -and $_.aprovadorId -eq $subG } | Select-Object -First 1
}

function JIssua([long]$uid,[long]$tid,[long]$ent,[long]$exer,[string]$tok,[string]$corr){
  $exExpr = if($exer -gt 0){ "$exer" } else { 'null' }
  [void](JDb ("delete from sigov.identidade_sessao where token_hash = encode(sha256(convert_to('$tok','UTF8')),'hex'); insert into sigov.identidade_sessao (tenant_id, entidade_id, exercicio_id, usuario_id, token_hash, expira_at, auth_version, user_agent_sanitizado, correlation_id, created_by) values ($tid, $ent, $exExpr, $uid, encode(sha256(convert_to('$tok','UTF8')),'hex'), now() + interval '12 hours', 1, 'gate-jornada-rc5068a', '$corr', $uid);"))
  $n=JVal "select count(*) from sigov.identidade_sessao where correlation_id='$corr';"
  JAssert ("sessao emitida para usuario {0} (tenant {1}, entidade {2})" -f $uid,$tid,$ent) ($n -eq '1') "sessoes=$n"
}

# -------------------------------------------------------------- cabecalho ---
JHead 'GATE A EXTRA - RC50.68A - reavaliar-encaminhamento (API), causas canonicas, quorum, corrida e replay'
JLog ('iniciado em ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
JLog 'prerequisito: gate_jornada.ps1 finalizado sem reset intermediario (sessoes persistem; JIssua reexecutado como medida defensiva)'

# ----------------------------------------------------- sessoes (defensivo) ---
JIssua 101 1 9101 0 $script:TokA 'f1000000-0000-4000-8000-0000000000a1'
JIssua 102 1 9101 0 $script:TokB 'f1000000-0000-4000-8000-0000000000b2'
JIssua 1   5 1    1 $script:TokD 'f1000000-0000-4000-8000-0000000000c3'

# ----------------------------------------------------------- SANITY estado ---
JHead 'SANITY - estado pos-jornada esperado'
$st1=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq1)';"
$st2=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq2)';"
$st3=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq3)';"
$st4=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq4)';"
JAssert 'sanity: Rq1 APROVADA, Rq2 PENDENTE_APROVACAO, Rq3 APROVADA, Rq4 REJEITADA' (($st1 -eq 'APROVADA') -and ($st2 -eq 'PENDENTE_APROVACAO') -and ($st3 -eq 'APROVADA') -and ($st4 -eq 'REJEITADA')) ("st1=$st1 st2=$st2 st3=$st3 st4=$st4")
$Rq5s=JVal "select r.id::text from sigov.compras_empresarial_requisicao r where r.tenant_id='$($script:TenantDemo)' and not r.is_deleted and r.status='PENDENTE_APROVACAO' and r.id::text not like 'd000%' limit 1;"
$Rq5g= if($Rq5s){ [guid]$Rq5s } else { [guid]::Empty }
$et5s=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq5s' and status='PENDENTE' and aprovador_id='$($script:SubB)';"
JAssert 'sanity: Rq5 (criada via API) pendente com 2 etapas do gestor' (($null -ne $Rq5s) -and ($et5s -eq '2')) ("rq5=$Rq5s etapas=$et5s")
$polAtiva=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica where tenant_id='$($script:TenantDemo)' and ativo and not is_deleted;"
$n1pol=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica_nivel n join sigov.compras_empresarial_aprovacao_politica p on p.id=n.politica_id and p.tenant_id=n.tenant_id where p.tenant_id='$($script:TenantDemo)' and p.ativo and not p.is_deleted and n.limite=50000;"
$n2pol=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica_nivel n join sigov.compras_empresarial_aprovacao_politica p on p.id=n.politica_id and p.tenant_id=n.tenant_id where p.tenant_id='$($script:TenantDemo)' and p.ativo and not p.is_deleted and n.limite=250000;"
JAssert 'sanity: 1 politica ativa com niveis 50000/250000' (($polAtiva -eq '1') -and ($n1pol -eq '1') -and ($n2pol -eq '1')) ("ativas=$polAtiva n1=$n1pol n2=$n2pol")
$flive=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=1' $script:TokB)
JAssert 'sanity: API responde para o gestor' ($flive.status -eq 200) ("http={0}" -f $flive.status)
if($script:JFail -gt 0){ JLog 'FATAL: estado pos-jornada diverge; execute reset + jornada novamente antes do extra.'; exit 2 }

# ---------------------------------------------------------------------- G1 ---
JHead 'G1 - Rq6 (total 500000 sob 50000/250000): ALCADA_INSUFICIENTE -> ampliacao -> SEM_APROVADOR + replay idempotente'
$cria6=[ordered]@{ setor='TI'; urgencia='NORMAL'; dataNecessaria='2026-10-31'; justificativa='Requisicao GATE-A G1 (demo): alçada insuficiente e reavaliacao do encaminhamento.'; observacoes='RC50-68A gate A extra G1'; itens=@(JItem 'Licencas de infraestrutura cloud (demo)' 5 100000) }
$b6=(New-Object PSObject -Property $cria6 | ConvertTo-Json -Depth 6 -Compress)
$c6=JApi 'POST' '/requisicoes' $script:TokA $b6 'g-a-rq6-criar-k1'
JAssert 'criar Rq6: 201' ($c6.status -eq 201) ("http={0}" -f $c6.status)
$Rq6=$c6.json.id
$D6=(JApi 'GET' "/requisicoes/$Rq6" $script:TokA).json
$V6=[long]$D6.version
$s6=JApi 'POST' "/requisicoes/$Rq6/enviar?version=$V6" $script:TokA $null 'g-a-rq6-enviar-k1'
JAssert 'enviar Rq6 (500000): 200 ciclo 1 repetido=false' (($s6.status -eq 200) -and $s6.json.ciclo -eq 1 -and $s6.json.repetido -eq $false) ("http={0}" -f $s6.status)
$n6a=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq6' and nivel=1 and aprovador_id='$($script:SubB)' and status='PENDENTE';"
$n6lim1=JVal "select case when count(*)=1 and bool_and(limite=50000) then 'SIM' else 'NAO' end from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq6' and nivel=1;"
$n6b=JVal "select coalesce(causa_bloqueio,'NULL') from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq6' and nivel=2 and aprovador_id is null and status='PENDENTE';"
$n6tot=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq6';"
JAssert 'Rq6: n1 designada ao gestor (limite 50000) + n2 bloqueada ALCADA_INSUFICIENTE (topo) + 2 etapas no total' (($n6a -eq '1') -and ($n6lim1 -eq 'SIM') -and ($n6b -eq 'ALCADA_INSUFICIENTE') -and ($n6tot -eq '2')) ("n1a=$n6a lim1=$n6lim1 n2causa=$n6b tot=$n6tot")
$p6alc1=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_ALCADA_INSUFICIENTE' and entidade_id='$Rq6' and status='ABERTA';"
JAssert 'pendencia ALCADA_INSUFICIENTE ABERTA para Rq6' ($p6alc1 -eq '1') "abertas=$p6alc1"

$r1=JApi 'POST' "/requisicoes/$Rq6/reavaliar-encaminhamento" $script:TokB '{"motivo":"GATE-A G1: politica nao cobre o total; reavaliacao sem mudanca esperada","idempotencyKey":"g-a-rq6-rev-k1"}'
JAssert 'reavaliar #1: 200 desbloqueado=false repetido=false' (($r1.status -eq 200) -and $r1.json.desbloqueado -eq $false -and $r1.json.repetido -eq $false) ("http={0}" -f $r1.status)
JAssert 'reavaliar #1: mensagem de ausencia de aprovador com alçada' ($r1.body.Contains('Nenhum novo aprovador com alçada foi identificado'))
$n6b2=JVal "select coalesce(causa_bloqueio,'NULL') from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq6' and nivel=2;"
$p6alc2=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_ALCADA_INSUFICIENTE' and entidade_id='$Rq6' and status='ABERTA';"
$p6sem1=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_APROVADOR' and entidade_id='$Rq6';"
JAssert 'reavaliar #1: sem mutacao (n2 segue ALCADA_INSUFICIENTE, pendencia ABERTA, sem pendencia SEM_APROVADOR criada)' (($n6b2 -eq 'ALCADA_INSUFICIENTE') -and ($p6alc2 -eq '1') -and ($p6sem1 -eq '0')) ("causa=$n6b2 alcAbertas=$p6alc2 semRows=$p6sem1")

$pol6=[ordered]@{ nome='Política institucional RC50-68A (demo municipal)'; niveis=@([ordered]@{limite=50000},[ordered]@{limite=600000}) }
$bp6=(New-Object PSObject -Property $pol6 | ConvertTo-Json -Depth 4 -Compress)
$sp6=JApi 'PUT' '/configuracao/politica' $script:TokA $bp6 'g-a-pol-k2'
JAssert 'ampliar politica para 50000/600000: 200 repetido=false' (($sp6.status -eq 200) -and $sp6.json.repetido -eq $false) ("http={0}" -f $sp6.status)
$gp6=(JApi 'GET' '/configuracao/politica' $script:TokA).json
JAssert 'politica ativa confirmada 50000/600000' ((@($gp6.niveis).Count -eq 2) -and [double](@($gp6.niveis))[0].limite -eq 50000 -and [double](@($gp6.niveis))[1].limite -eq 600000)

$r2=JApi 'POST' "/requisicoes/$Rq6/reavaliar-encaminhamento" $script:TokB '{"motivo":"GATE-A G1: politica ampliada; reclassificacao ALCADA->SEM_APROVADOR esperada","idempotencyKey":"g-a-rq6-rev-k2"}'
JAssert 'reavaliar #2: 200 desbloqueado=false repetido=false' (($r2.status -eq 200) -and $r2.json.desbloqueado -eq $false -and $r2.json.repetido -eq $false) ("http={0}" -f $r2.status)
JAssert 'reavaliar #2: mensagem de ausencia de aprovador com alçada' ($r2.body.Contains('Nenhum novo aprovador com alçada foi identificado'))
$n6b3=JVal "select coalesce(causa_bloqueio,'NULL') from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq6' and nivel=2 and aprovador_id is null;"
$p6alc3=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_ALCADA_INSUFICIENTE' and entidade_id='$Rq6' and status='RESOLVIDA';"
$p6sem2=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_APROVADOR' and entidade_id='$Rq6' and status='ABERTA';"
JAssert 'reavaliar #2: n2 reclassificada SEM_APROVADOR; pendencia ALCADA RESOLVIDA + SEM_APROVADOR ABERTA' (($n6b3 -eq 'SEM_APROVADOR') -and ($p6alc3 -eq '1') -and ($p6sem2 -eq '1')) ("causa=$n6b3 alcResolvidas=$p6alc3 semAbertas=$p6sem2")

$r2r=JApi 'POST' "/requisicoes/$Rq6/reavaliar-encaminhamento" $script:TokB '{"motivo":"GATE-A G1: politica ampliada; reclassificacao ALCADA->SEM_APROVADOR esperada","idempotencyKey":"g-a-rq6-rev-k2"}'
JAssert 'replay reavaliar #2 (mesma chave+conteudo): 200 repetido=true com mensagem fixada' (($r2r.status -eq 200) -and $r2r.json.repetido -eq $true -and $r2r.body.Contains('já reavaliado para esta chave idempotente')) ("http={0}" -f $r2r.status)
JLog 'LIMITACAO DE FIXTURE (registrada): sem usuario com alçada >= 600000 no tenant demo; Rq6 encerra propositalmente em PENDENTE_APROVACAO/SEM_APROVADOR - demonstracao de bloqueio explicito, nao falha de fluxo.'

# ---------------------------------------------------------------------- G2 ---
JHead 'G2 - Rq2: politica INATIVA nao libera; reativacao + reavaliar libera; aprovacao conclui'
[void](JDb ("update sigov.compras_empresarial_aprovacao_politica set ativo=false where tenant_id='$($script:TenantDemo)' and ativo and not is_deleted;"))
$g1=JApi 'POST' "/requisicoes/$($script:Rq2)/reavaliar-encaminhamento" $script:TokB '{"motivo":"GATE-A G2: politica inativa; atribuicao sozinha nao deve liberar","idempotencyKey":"g-a-rq2-rev-k1"}'
JAssert 'reavaliar Rq2 com politica inativa: 200 desbloqueado=false' (($g1.status -eq 200) -and $g1.json.desbloqueado -eq $false) ("http={0}" -f $g1.status)
$g2a=JVal "select coalesce(causa_bloqueio,'NULL') from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq2)' and aprovador_id is null;"
$g2p1=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_POLITICA' and entidade_id='$($script:Rq2)' and status='ABERTA';"
JAssert 'politica inativa: n1 continua SEM_POLITICA e pendencia ABERTA (prova contra fail-open)' (($g2a -eq 'SEM_POLITICA') -and ($g2p1 -eq '1')) ("causa=$g2a pendAbertas=$g2p1")
[void](JDb ("update sigov.compras_empresarial_aprovacao_politica set ativo=true where tenant_id='$($script:TenantDemo)' and not is_deleted;"))
$g2=JApi 'POST' "/requisicoes/$($script:Rq2)/reavaliar-encaminhamento" $script:TokB '{"motivo":"GATE-A G2: politica reativada; desencontro esperado","idempotencyKey":"g-a-rq2-rev-k2"}'
JAssert 'reavaliar Rq2 com politica reativada: 200 desbloqueado=true' (($g2.status -eq 200) -and $g2.json.desbloqueado -eq $true) ("http={0}" -f $g2.status)
JAssert 'mensagem de sucesso com etapa atribuída' ($g2.body.Contains('Reavaliação concluída: 1 etapa(s) foram atribuídas a aprovadores elegíveis.'))
$g2b=JVal "select case when count(*)=1 and bool_and(aprovador_id='$($script:SubB)' and causa_bloqueio is null and status='PENDENTE') then 'SIM' else 'NAO' end from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq2)';"
$g2p2=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_POLITICA' and entidade_id='$($script:Rq2)' and status='RESOLVIDA';"
$g2h=JVal "select count(*) from sigov.compras_empresarial_historico where aggregate_type='REQUISICAO' and aggregate_id='$($script:Rq2)' and acao='ENCAMINHAMENTO_REAVALIADO';"
JAssert 'Rq2: etapa unica designada ao gestor (causa null), pendencia RESOLVIDA e historico ENCAMINHAMENTO_REAVALIADO' (($g2b -eq 'SIM') -and ($g2p2 -eq '1') -and ([long]$g2h -ge 1)) ("etapa=$g2b pendResolvidas=$g2p2 hist=$g2h")
$fg=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$eg=JFilaItem $fg $Gq2 1 $GB
JAssert 'fila do gestor contem a etapa reaberta da Rq2' ($null -ne $eg)
$dg=JApi 'POST' "/aprovacoes/$($eg.etapaId)/decidir" $script:TokB (JDecidirJson 'APROVAR' $null ([long]$eg.version) 'g-a-rq2-d1-gestor')
JAssert 'APROVAR Rq2 n1 (gestor): etapa APROVADO e requisicao APROVADA' (($dg.status -eq 200) -and $dg.json.etapaStatus -eq 'APROVADO' -and $dg.json.requisicaoStatus -eq 'APROVADA' -and $dg.json.repetido -eq $false) ("http={0}" -f $dg.status)
$st2f=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq2)';"
JAssert 'Rq2 final APROVADA no banco' ($st2f -eq 'APROVADA')

# ---------------------------------------------------------------------- G3 ---
JHead 'G3 - Rq7 (total 600000 sob 50000/600000): SEM_APROVADOR -> politica reduzida a 1 nivel -> n2 CANCELADA (prova do fix L282)'
$cria7=[ordered]@{ setor='TI'; urgencia='NORMAL'; dataNecessaria='2026-10-31'; justificativa='Requisicao GATE-A G3 (demo): cobertura reduzida apos reavaliacao.'; observacoes='RC50-68A gate A extra G3'; itens=@(JItem 'Datacenter dedicado (demo)' 6 100000) }
$b7=(New-Object PSObject -Property $cria7 | ConvertTo-Json -Depth 6 -Compress)
$c7=JApi 'POST' '/requisicoes' $script:TokA $b7 'g-a-rq7-criar-k1'
JAssert 'criar Rq7: 201' ($c7.status -eq 201) ("http={0}" -f $c7.status)
$Rq7=$c7.json.id
$D7=(JApi 'GET' "/requisicoes/$Rq7" $script:TokA).json
$V7=[long]$D7.version
$s7=JApi 'POST' "/requisicoes/$Rq7/enviar?version=$V7" $script:TokA $null 'g-a-rq7-enviar-k1'
JAssert 'enviar Rq7 (600000): 200 ciclo 1 repetido=false' (($s7.status -eq 200) -and $s7.json.ciclo -eq 1 -and $s7.json.repetido -eq $false) ("http={0}" -f $s7.status)
$n7a=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq7' and nivel=1 and aprovador_id='$($script:SubB)' and status='PENDENTE';"
$n7b=JVal "select coalesce(causa_bloqueio,'NULL') from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq7' and nivel=2 and aprovador_id is null and status='PENDENTE';"
$p7a=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_APROVADOR' and entidade_id='$Rq7' and status='ABERTA';"
JAssert 'Rq7 enviado: n1 do gestor + n2 SEM_APROVADOR + pendencia ABERTA' (($n7a -eq '1') -and ($n7b -eq 'SEM_APROVADOR') -and ($p7a -eq '1')) ("n1a=$n7a n2causa=$n7b pendAbertas=$p7a")
$pol7=[ordered]@{ nome='Política institucional RC50-68A (demo municipal)'; niveis=@([ordered]@{limite=700000}) }
$bp7=(New-Object PSObject -Property $pol7 | ConvertTo-Json -Depth 4 -Compress)
$sp7=JApi 'PUT' '/configuracao/politica' $script:TokA $bp7 'g-a-pol-k3'
JAssert 'reduzir politica a nivel unico (700000): 200 repetido=false' (($sp7.status -eq 200) -and $sp7.json.repetido -eq $false) ("http={0}" -f $sp7.status)
$gp7=(JApi 'GET' '/configuracao/politica' $script:TokA).json
JAssert 'politica ativa confirmada com 1 nivel 700000' ((@($gp7.niveis).Count -eq 1) -and [double](@($gp7.niveis))[0].limite -eq 700000)
$r7=JApi 'POST' "/requisicoes/$Rq7/reavaliar-encaminhamento" $script:TokB '{"motivo":"GATE-A G3: politica reduzida a nivel unico; n2 fica fora da cobertura","idempotencyKey":"g-a-rq7-rev-k1"}'
JAssert 'reavaliar Rq7: 200 desbloqueado=false repetido=false' (($r7.status -eq 200) -and $r7.json.desbloqueado -eq $false -and $r7.json.repetido -eq $false) ("http={0}" -f $r7.status)
$g3c=JVal "select case when count(*)=1 then 'SIM' else 'NAO' end from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq7' and nivel=2 and status='CANCELADO' and motivo like '%fora da cobertura%';"
$g3n1=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq7' and nivel=1 and aprovador_id='$($script:SubB)' and status='PENDENTE';"
$p7b=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_APROVADOR' and entidade_id='$Rq7' and status='RESOLVIDA';"
JAssert 'Rq7: n2 CANCELADA fora da cobertura, n1 intacta e pendencia RESOLVIDA' (($g3c -eq 'SIM') -and ($g3n1 -eq '1') -and ($p7b -eq '1')) ("canc=$g3c n1=$g3n1 pendResolvidas=$p7b")
$fr7=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$e7=JFilaItem $fr7 ([guid]$Rq7) 1 $GB
JAssert 'fila do gestor contem n1 da Rq7' ($null -ne $e7)
$d7=JApi 'POST' "/aprovacoes/$($e7.etapaId)/decidir" $script:TokB (JDecidirJson 'APROVAR' $null ([long]$e7.version) 'g-a-rq7-d1-gestor')
JAssert 'APROVAR n1 da Rq7: APROVADO + requisicao APROVADA (prova do fix L282: maximoNivel ignora etapas CANCELADAS)' (($d7.status -eq 200) -and $d7.json.etapaStatus -eq 'APROVADO' -and $d7.json.requisicaoStatus -eq 'APROVADA') ("http={0}" -f $d7.status)
$st7=JVal "select status from sigov.compras_empresarial_requisicao where id='$Rq7';"
$tot7=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq7';"
JAssert 'Rq7 final APROVADA com 2 etapas (1 APROVADA + 1 CANCELADA, sem duplicacao)' (($st7 -eq 'APROVADA') -and ($tot7 -eq '2')) ("st=$st7 tot=$tot7")

# ---------------------------------------------------------------------- G4 ---
JHead 'G4 - Rq5: quorum por nivel anterior + corrida sobre a mesma etapa + replay do vencedor'
$fb4=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$ea4=JFilaItem $fb4 $Rq5g 1 $GB
$en4=JFilaItem $fb4 $Rq5g 2 $GB
JAssert 'fila do gestor contem n1+n2 da Rq5' (($null -ne $ea4) -and ($null -ne $en4))
$qu=JApi 'POST' "/aprovacoes/$($en4.etapaId)/decidir" $script:TokB (JDecidirJson 'APROVAR' $null ([long]$en4.version) 'g-a-rq5-probe-quorum')
JAssert 'quorum: decidir n2 antes de n1 => 422 com mensagem institucional exata' (($qu.status -eq 422) -and $qu.body.Contains('Não é possível decidir esta etapa antes da aprovação de todos os níveis anteriores.')) ("http={0}" -f $qu.status)
# Corrida paralela sobre n1: duas chaves distintas, mesma versao, dois curls simultaneos.
$dirR=Join-Path $PSScriptRoot 'tmp'
if(-not (Test-Path $dirR)){ New-Item -ItemType Directory -Path $dirR | Out-Null }
foreach($rf in @('race_k1.json','race_k2.json','race_b1.json','race_b2.json','race_c1.txt','race_c2.txt')){ Remove-Item (Join-Path $dirR $rf) -Force -ErrorAction SilentlyContinue }
$uR="$($script:JBase)/aprovacoes/$($ea4.etapaId)/decidir"
$bR1=JDecidirJson 'APROVAR' $null ([long]$ea4.version) 'g-a-rq5-raca-k1'
$bR2=JDecidirJson 'APROVAR' $null ([long]$ea4.version) 'g-a-rq5-raca-k2'
# O cmd interpreta aspas do JSON embutido e corrompe o corpo; passa o body via arquivo (UTF-8 sem BOM).
[IO.File]::WriteAllText((Join-Path $dirR 'race_k1.json'),$bR1,(New-Object System.Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $dirR 'race_k2.json'),$bR2,(New-Object System.Text.UTF8Encoding($false)))
$bk1=(Join-Path $dirR 'race_k1.json')
$bk2=(Join-Path $dirR 'race_k2.json')
$batPath=Join-Path $dirR 'race_g4.bat'
$bat=@"
@echo off
curl.exe -s -X POST `"$uR`" -H `"Authorization: Bearer $($script:TokB)`" -H `"Host: municipio-demo.sigov.local`" -H `"Idempotency-Key: g-a-rq5-raca-k1`" -H `"Content-Type: application/json; charset=utf-8`" --data-binary `"@$bk1`" -o `"$dirR\race_b1.json`" -w `"%%{http_code}`" > `"$dirR\race_c1.txt`" 2>&1 &
curl.exe -s -X POST `"$uR`" -H `"Authorization: Bearer $($script:TokB)`" -H `"Host: municipio-demo.sigov.local`" -H `"Idempotency-Key: g-a-rq5-raca-k2`" -H `"Content-Type: application/json; charset=utf-8`" --data-binary `"@$bk2`" -o `"$dirR\race_b2.json`" -w `"%%{http_code}`" > `"$dirR\race_c2.txt`" 2>&1 &
wait
"@
[IO.File]::WriteAllText($batPath,$bat,(New-Object System.Text.UTF8Encoding($false)))
Start-Process -FilePath $batPath -Wait -NoNewWindow
$c1=(Get-Content (Join-Path $dirR 'race_c1.txt') -Raw).Trim()
$c2=(Get-Content (Join-Path $dirR 'race_c2.txt') -Raw).Trim()
$j1raw=(Get-Content (Join-Path $dirR 'race_b1.json') -Raw -Encoding UTF8)
$j2raw=(Get-Content (Join-Path $dirR 'race_b2.json') -Raw -Encoding UTF8)
JLog ("corrida: code1=$c1 code2=$c2")
JLog ("corrida body1=$j1raw")
JLog ("corrida body2=$j2raw")
$codes=@($c1,$c2)|Sort-Object
JAssert 'corrida: exatamente um 200 (vencedor) e um 422 "ja foi decidida" (perdedor)' ((($codes -join ',')) -eq '200,422') ("codes=$c1,$c2")
if($c1 -eq '200'){ $winRaw=$j1raw; $loseRaw=$j2raw; $wKey='g-a-rq5-raca-k1'; $wBody=$bR1 } else { $winRaw=$j2raw; $loseRaw=$j1raw; $wKey='g-a-rq5-raca-k2'; $wBody=$bR2 }
$w=$winRaw|ConvertFrom-Json
JAssert 'corrida: vencedor APROVADO + requisicao PENDENTE_APROVACAO + repetido=false' (($w.etapaStatus -eq 'APROVADO') -and ($w.requisicaoStatus -eq 'PENDENTE_APROVACAO') -and ($w.repetido -eq $false))
JAssert 'corrida: perdedor recebeu a mensagem exata de etapa ja decidida' ($loseRaw.Contains('já foi decidida'))
$rp=JApi 'POST' "/aprovacoes/$($ea4.etapaId)/decidir" $script:TokB $wBody $wKey
JAssert 'replay do vencedor: 200 repetido=true reexpondo o original (requisicao segue PENDENTE_APROVACAO)' (($rp.status -eq 200) -and $rp.json.repetido -eq $true -and $rp.json.etapaStatus -eq 'APROVADO' -and $rp.json.requisicaoStatus -eq 'PENDENTE_APROVACAO') ("http={0}" -f $rp.status)
$fb5=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$e2f=JFilaItem $fb5 $Rq5g 2 $GB
JAssert 'fila do gestor ainda contem n2 da Rq5 apos a corrida' ($null -ne $e2f)
$d2f=JApi 'POST' "/aprovacoes/$($e2f.etapaId)/decidir" $script:TokB (JDecidirJson 'APROVAR' $null ([long]$e2f.version) 'g-a-rq5-d2-gestor')
JAssert 'APROVAR n2 (gestor): etapa APROVADO e requisicao APROVADA' (($d2f.status -eq 200) -and $d2f.json.etapaStatus -eq 'APROVADO' -and $d2f.json.requisicaoStatus -eq 'APROVADA') ("http={0}" -f $d2f.status)
$hEt=JVal "select count(*) from sigov.compras_empresarial_historico where aggregate_type='REQUISICAO' and aggregate_id='$Rq5s' and acao='ETAPA_APROVADA';"
$hA=JVal "select count(*) from sigov.compras_empresarial_historico where aggregate_type='REQUISICAO' and aggregate_id='$Rq5s' and acao='APROVADA';"
$iR=JVal "select count(*) from sigov.compras_empresarial_idempotencia where chave like 'g-a-rq5-raca-%' and resultado is not null;"
JAssert 'Rq5: n1 -> ETAPA_APROVADA + n2 (nivel final) -> APROVADA; replay e perdedor nao duplicam eventos' (($hEt -eq '1') -and ($hA -eq '1')) ("etapaAprovadas=$hEt aprovadas=$hA")
JAssert 'idempotencia: chave do vencedor persistiu resultado jsonb (perdedor 422 nao persiste, sem mutacao)' ($iR -eq '1') "chavesComResultado=$iR"

# ------------------------------------------------------------ SNAPSHOT -----
JHead 'SNAPSHOT FINAL DO GATE A'
[void](JDb ("select r.numero, r.status, r.version, r.valor_estimado from sigov.compras_empresarial_requisicao r where r.tenant_id='$($script:TenantDemo)' and not r.is_deleted order by r.created_at;"))
[void](JDb ("select r.numero, a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL') as aprovador, a.limite, a.status, coalesce(a.causa_bloqueio,'-') as causa, left(coalesce(a.motivo,''),60) as motivo from sigov.compras_empresarial_aprovacao a join sigov.compras_empresarial_requisicao r on r.id=a.requisicao_id where a.tenant_id='$($script:TenantDemo)' order by r.created_at, a.ciclo, a.nivel, a.id;"))
[void](JDb ("select chave, operacao, left(coalesce(resultado::text,'NULL'),120) from sigov.compras_empresarial_idempotencia where chave like 'g-a-%' order by chave;"))
[void](JDb ("select tipo, status, entidade_id, titulo from sigov.pendencia_operacional where tipo like 'APROVACAO_%' order by id;"))
[void](JDb ("select h.acao, count(*) as n from sigov.compras_empresarial_historico h join sigov.compras_empresarial_requisicao r on r.id=h.aggregate_id where r.tenant_id='$($script:TenantDemo)' and h.aggregate_type='REQUISICAO' group by h.acao order by h.acao;"))
$inIds="'$($script:Rq1)','$($script:Rq2)','$($script:Rq3)','$($script:Rq4)','$Rq5s','$Rq6','$Rq7'"
$cntAp=JVal "select count(*) from sigov.compras_empresarial_requisicao where tenant_id='$($script:TenantDemo)' and not is_deleted and status='APROVADA';"
$cntRej=JVal "select count(*) from sigov.compras_empresarial_requisicao where tenant_id='$($script:TenantDemo)' and not is_deleted and status='REJEITADA';"
$cntPen=JVal "select count(*) from sigov.compras_empresarial_requisicao where tenant_id='$($script:TenantDemo)' and not is_deleted and status='PENDENTE_APROVACAO';"
JAssert 'estado final: 5 APROVADA (Rq1,Rq2,Rq3,Rq5,Rq7) + 1 REJEITADA (Rq4) + 1 PENDENTE (Rq6)' (($cntAp -eq '5') -and ($cntRej -eq '1') -and ($cntPen -eq '1')) ("ap=$cntAp rej=$cntRej pen=$cntPen")
$penAb=JVal "select count(*) from sigov.pendencia_operacional where tipo like 'APROVACAO_%' and status='ABERTA' and entidade_id in ($inIds);"
$penAb6=JVal "select count(*) from sigov.pendencia_operacional where tipo='APROVACAO_SEM_APROVADOR' and entidade_id='$Rq6' and status='ABERTA';"
JAssert 'pendencias abertas no fim: exatamente 1 (Rq6 SEM_APROVADOR)' (($penAb -eq '1') -and ($penAb6 -eq '1')) ("abertas=$penAb rq6=$penAb6")
$totEt=JVal "select count(*) from sigov.compras_empresarial_aprovacao where tenant_id='$($script:TenantDemo)';"
JAssert 'total de etapas no tenant demo: 13 (Rq1=2, Rq2=1, Rq3=1, Rq4=3, Rq5=2, Rq6=2, Rq7=2)' ($totEt -eq '13') "etapas=$totEt"
JLog ('finalizado em ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
if($script:JFail -gt 0){ JLog ("GATE-A EXTRA RESULTADO: FAIL ($script:JFail falha(s))"); exit 1 }
JLog 'GATE-A EXTRA RESULTADO: PASS (todas as asserções verdes)'
exit 0
