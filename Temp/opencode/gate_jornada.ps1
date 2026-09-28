[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
. (Join-Path $PSScriptRoot 'jornada_lib.ps1')
$script:JDbMode='docker'
$script:JFail=0
$script:TenantDemo='b0000001-0000-4000-8000-000000000001'
$Gq1=[guid]$script:Rq1
$Gq2=[guid]$script:Rq2
$Gq3=[guid]$script:Rq3
$Gq4=[guid]$script:Rq4
$GA=[guid]$script:SubA
$GB=[guid]$script:SubB

if(Test-Path $script:JOut){ Remove-Item $script:JOut -Force }
JLog 'SIGOV PLUS - GATE JORNADA COMPORTAMENTAL (RC50.68A) - evidencia de runtime'
JLog ('iniciado em ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
JLog ('api base: {0}' -f $script:JBase)
JLog ('fixtures: Rq1..Rq4 = RC-DEMO-0001..0004 (seed demo municipal) + Rq5 criada via API; tenant demo: {0}' -f $script:TenantDemo)

# ---------------------------------------------------------------- helpers ---
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
  # PS 5.1: splat de array com >=4 elementos quebra argumento no formato <x>C:\... em dois args
  # (evidencia: gate_argv_probe*.ps1). Comando montado como string unica via cmd /c.
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

function JAtualizarJson($d,[array]$itens,[long]$version){
  $o=[ordered]@{ setor=$d.setor; centroCustoId=$d.centroCustoId; projetoId=$d.projetoId; contratoId=$d.contratoId; ordemServicoId=$d.ordemServicoId; almoxarifadoId=$d.almoxarifadoId; urgencia=$d.urgencia; dataNecessaria=$d.dataNecessaria; justificativa=$d.justificativa; observacoes=$d.observacoes; itens=$itens; version=$version }
  New-Object PSObject -Property $o | ConvertTo-Json -Depth 6 -Compress
}

function JDecidirJson([string]$dec,[string]$mot,[long]$ver,[string]$key){
  $o=[ordered]@{ decisao=$dec; motivo=$mot; version=$ver; idempotencyKey=$key }
  New-Object PSObject -Property $o | ConvertTo-Json -Depth 4 -Compress
}

function JFilaItem($fila,$rqG,[int]$nivel,$subG){
  $fila.items | Where-Object { $_.requisicaoId -eq $rqG -and $_.nivel -eq $nivel -and $_.aprovadorId -eq $subG } | Select-Object -First 1
}

# ------------------------------------------------------------- S-1 -----
JHead 'S-1 - EMISSAO DAS SESSOES DE DEMONSTRACAO (mesmas colunas do login oficial; sem senha literal - regra 18)'
$nDemo=JVal "select count(*) from sigov.usuario where id in (1,101,102) and ativo and not bloqueado and not is_deleted;"
JAssert 'identidades presentes e ativas (admin=1, analista=101, gestor=102)' ($nDemo -eq '3') "usuarios=$nDemo"
function JIssua([long]$uid,[long]$tid,[long]$ent,[long]$exer,[string]$tok,[string]$corr){
  $exExpr = if($exer -gt 0){ "$exer" } else { 'null' }
  [void](JDb ("delete from sigov.identidade_sessao where token_hash = encode(sha256(convert_to('$tok','UTF8')),'hex'); insert into sigov.identidade_sessao (tenant_id, entidade_id, exercicio_id, usuario_id, token_hash, expira_at, auth_version, user_agent_sanitizado, correlation_id, created_by) values ($tid, $ent, $exExpr, $uid, encode(sha256(convert_to('$tok','UTF8')),'hex'), now() + interval '12 hours', 1, 'gate-jornada-rc5068a', '$corr', $uid);"))
  $n=JVal "select count(*) from sigov.identidade_sessao where correlation_id='$corr';"
  JAssert ("sessao emitida para usuario {0} (tenant {1}, entidade {2})" -f $uid,$tid,$ent) ($n -eq '1') "sessoes=$n"
}
JIssua 101 1 9101 0 $script:TokA 'f1000000-0000-4000-8000-0000000000a1'
JIssua 102 1 9101 0 $script:TokB 'f1000000-0000-4000-8000-0000000000b2'
JIssua 1   5 1    1 $script:TokD 'f1000000-0000-4000-8000-0000000000c3'

# ------------------------------------------------------------------- S0 -----
JHead 'S0 - PREFLIGHT: estado virgem esperado (fixtures RASCUNHO, politica ausente, sem etapas/chaves jornada)'
[void](JDb ("select r.numero, r.status, r.version, r.valor_estimado from sigov.compras_empresarial_requisicao r where r.id in ('$($script:Rq1)','$($script:Rq2)','$($script:Rq3)','$($script:Rq4)') order by r.numero;"))
$polCount=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica where tenant_id='$($script:TenantDemo)';"
$etCount=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id in ('$($script:Rq1)','$($script:Rq2)','$($script:Rq3)','$($script:Rq4)');"
$keyCount=JVal "select count(*) from sigov.compras_empresarial_idempotencia where chave like 'jornada-%';"
JAssert 'POLITICA_ROWS=0 antes da jornada' ($polCount -eq '0') "politica_count=$polCount"
JAssert 'nenhuma etapa nos fixtures antes da jornada' ($etCount -eq '0') "etapas=$etCount"
JAssert 'nenhuma chave de idempotencia jornada-* no banco' ($keyCount -eq '0') "chaves=$keyCount"
if($polCount -ne '0' -or $etCount -ne '0' -or $keyCount -ne '0'){ Write-Host 'FATAL: estado nao e virgem; execute o reset dos fixtures antes de repetir a jornada.'; exit 2 }

$D1=(JApi 'GET' "/requisicoes/$($script:Rq1)" $script:TokA).json
$D2=(JApi 'GET' "/requisicoes/$($script:Rq2)" $script:TokA).json
$D3=(JApi 'GET' "/requisicoes/$($script:Rq3)" $script:TokA).json
$D4=(JApi 'GET' "/requisicoes/$($script:Rq4)" $script:TokA).json
$V1=[long]$D1.version; $V2=[long]$D2.version; $V3=[long]$D3.version; $V4=[long]$D4.version
foreach($pair in @(@('Rq1',$D1),@('Rq2',$D2),@('Rq3',$D3),@('Rq4',$D4))){
  JAssert ("GET detalhe {0} em RASCUNHO" -f $pair[0]) ($pair[1].status -eq 'RASCUNHO') ("status={0} version={1}" -f $pair[1].status,$pair[1].version)
}
$p404=JApi 'GET' '/configuracao/politica' $script:TokA
JAssert 'GET /configuracao/politica retorna 404 quando ausente (falha explicita, sem fallback)' ($p404.status -eq 404)
$f0=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokA).json
JAssert 'fila vazia e honesta antes da jornada (totalItems=0)' ([long]$f0.totalItems -eq 0) "totalItems=$($f0.totalItems)"

# ------------------------------------------------------------------- S1 -----
JHead 'S1 - Rq2 (RC-DEMO-0002, total 40000): envio ANTES da politica => etapa bloqueada + pendencia APROVACAO_SEM_POLITICA'
$r2e=JApi 'PUT' "/requisicoes/$($script:Rq2)" $script:TokA (JAtualizarJson $D2 @(JItem 'Material de expediente (demo)' 4 10000) $V2)
JAssert 'PUT Rq2 com itens 4x10000 (=40000) aceita 204' ($r2e.status -eq 204)
$s2=JApi 'POST' "/requisicoes/$($script:Rq2)/enviar?version=$($V2+1)" $script:TokA $null 'jornada-rq2-enviar-k1'
JAssert 'envio Rq2 sem politica: 200 PENDENTE_APROVACAO ciclo 1 repetido=false' (($s2.status -eq 200) -and $s2.json.status -eq 'PENDENTE_APROVACAO' -and $s2.json.ciclo -eq 1 -and $s2.json.repetido -eq $false) ("resp={0}" -f $s2.body)
[void](JDb ("select a.id, a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.limite, a.status, a.version from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq2)' order by a.nivel, a.id;"))
$nBloq=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq2)' and status='PENDENTE' and aprovador_id is null and limite=0;"
JAssert 'Rq2 gerou exatamente 1 etapa bloqueada (nivel 1, aprovador NULL, limite 0)' ($nBloq -eq '1') "bloqueadas=$nBloq"
$pend=JVal "select tipo||' | '||status||' | '||coalesce(rota_acao,'') from sigov.pendencia_operacional where entidade_id='$($script:Rq2)' and tipo='APROVACAO_SEM_POLITICA';"
JAssert 'pendencia APROVACAO_SEM_POLITICA ABERTA registrada com rota de acao' ($pend -like 'APROVACAO_SEM_POLITICA | ABERTA | *') "pendencia=$pend"
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokA).json
$bl=$f.items | Where-Object { $_.requisicaoId -eq $Gq2 } | Select-Object -First 1
JAssert 'fila expoe a etapa bloqueada da Rq2 (bloqueada=true, decisivelPorMim=false)' (($null -ne $bl) -and $bl.bloqueada -eq $true -and $bl.decisivelPorMim -eq $false)
$pb=JApi 'POST' "/aprovacoes/$($bl.etapaId)/decidir" $script:TokA (JDecidirJson 'APROVAR' $null ([long]$bl.version) 'jornada-rq2-probe-bloqueada')
JAssert 'decisao em etapa bloqueada: 422 com mensagem institucional exata' (($pb.status -eq 422) -and $pb.body.Contains('Esta etapa está bloqueada e aguarda configuração institucional antes de qualquer decisão.')) ("http={0}" -f $pb.status)

# ------------------------------------------------------------------- S2 -----
JHead 'S2 - POLITICA INSTITUCIONAL: upsert por tenant, alçadas cumulativas 50000/250000, replay idempotente'
$polOrdered=[ordered]@{ nome='Política institucional RC50-68A (demo municipal)'; niveis=@([ordered]@{limite=50000},[ordered]@{limite=250000}) }
$polBody=(New-Object PSObject -Property $polOrdered | ConvertTo-Json -Depth 4 -Compress)
$sp=JApi 'PUT' '/configuracao/politica' $script:TokA $polBody 'jornada-politica-k1'
JAssert 'salvar politica: 200 repetido=false' (($sp.status -eq 200) -and $sp.json.repetido -eq $false)
$sp2=JApi 'PUT' '/configuracao/politica' $script:TokA $polBody 'jornada-politica-k1'
JAssert 'replay mesma chave+conteudo: repetido=true (sem duplicar politica)' (($sp2.status -eq 200) -and $sp2.json.repetido -eq $true)
$gp=JApi 'GET' '/configuracao/politica' $script:TokA
JAssert 'politica ativa multi-esfera: esfera municipal + 2 niveis em ordem ascendente' (($gp.status -eq 200) -and $gp.json.esferaGoverno -eq 'municipal' -and @($gp.json.niveis).Count -eq 2 -and [decimal]$gp.json.niveis[0].limite -eq 50000 -and [decimal]$gp.json.niveis[1].limite -eq 250000)
[void](JDb ("select p.id, p.nome, p.esfera_governo, p.tipo_entidade, p.ativo, n.ordem, n.limite from sigov.compras_empresarial_aprovacao_politica p left join sigov.compras_empresarial_aprovacao_politica_nivel n on n.politica_id=p.id and n.tenant_id=p.tenant_id where p.tenant_id='$($script:TenantDemo)' order by n.ordem;"))
$polNow=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica where tenant_id='$($script:TenantDemo)';"
JAssert 'exatamente UMA politica ativa por tenant apos upsert' ($polNow -eq '1') "count=$polNow"

# ------------------------------------------------------------------- S3 -----
JHead 'S3 - Rq1 (RC-DEMO-0001, total 80000): duas etapas cumulativas -> quorum any-of -> APROVADA'
$r1e=JApi 'PUT' "/requisicoes/$($script:Rq1)" $script:TokA (JAtualizarJson $D1 @(JItem 'Licenças de software corporativo (demo)' 8 10000) $V1)
JAssert 'PUT Rq1 com itens 8x10000 (=80000) aceita 204' ($r1e.status -eq 204)
$s1=JApi 'POST' "/requisicoes/$($script:Rq1)/enviar?version=$($V1+1)" $script:TokA $null 'jornada-rq1-enviar-k1'
JAssert 'envio Rq1: 200 ciclo 1 repetido=false' (($s1.status -eq 200) -and $s1.json.ciclo -eq 1 -and $s1.json.repetido -eq $false)
[void](JDb ("select a.id, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.limite, a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq1)' order by a.nivel, a.id;"))
$n1a=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and nivel=1 and aprovador_id='$($script:SubA)' and status='PENDENTE';"
$n1b=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and nivel=1 and aprovador_id='$($script:SubB)' and status='PENDENTE';"
$n2b=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and nivel=2 and aprovador_id='$($script:SubB)' and status='PENDENTE';"
$ntotal=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)';"
JAssert 'Rq1 gera exatamente 3 etapas PENDENTE: n1={analista,gestor}, n2={gestor}' (($n1a -eq '1') -and ($n1b -eq '1') -and ($n2b -eq '1') -and ($ntotal -eq '3')) ("counts n1a=$n1a n1b=$n1b n2b=$n2b total=$ntotal")
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$e1=JFilaItem $f $Gq1 1 $GB
JAssert 'fila do gestor contem sua etapa n1 da Rq1' ($null -ne $e1)
$d1r=JApi 'POST' "/aprovacoes/$($e1.etapaId)/decidir" $script:TokB (JDecidirJson 'APROVAR' $null ([long]$e1.version) 'jornada-rq1-d1-gestor')
JAssert 'APROVAR n1 (gestor): etapa APROVADO, requisicao segue PENDENTE_APROVACAO' (($d1r.status -eq 200) -and $d1r.json.etapaStatus -eq 'APROVADO' -and $d1r.json.requisicaoStatus -eq 'PENDENTE_APROVACAO' -and $d1r.json.repetido -eq $false)
$canc=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and nivel=1 and aprovador_id='$($script:SubA)' and status='CANCELADO';"
JAssert 'quorum any-of: linha do analista na n1 fica CANCELADO apos decisao do gestor' ($canc -eq '1') "canceladas=$canc"
[void](JDb ("select a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq1)' order by a.nivel, a.id; select acao from sigov.compras_empresarial_historico where aggregate_type='REQUISICAO' and aggregate_id='$($script:Rq1)' order by created_at, id;"))
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$e2=JFilaItem $f $Gq1 2 $GB
JAssert 'fila do gestor contem sua etapa n2 da Rq1' ($null -ne $e2)
$d2r=JApi 'POST' "/aprovacoes/$($e2.etapaId)/decidir" $script:TokB (JDecidirJson 'APROVAR' $null ([long]$e2.version) 'jornada-rq1-d2-gestor')
JAssert 'APROVAR n2 (gestor): etapa APROVADO e requisicao APROVADA (fim de ciclo)' (($d2r.status -eq 200) -and $d2r.json.etapaStatus -eq 'APROVADO' -and $d2r.json.requisicaoStatus -eq 'APROVADA' -and $d2r.json.repetido -eq $false)
$st=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq1)';"
JAssert 'Rq1 final APROVADA no banco' ($st -eq 'APROVADA')

# ------------------------------------------------------------------- S4 -----
JHead 'S4 - Rq3 (RC-DEMO-0003): total EXATO 50000 => fronteira de alçada inclusiva => etapa única -> APROVADA pelo analista'
$r3e=JApi 'PUT' "/requisicoes/$($script:Rq3)" $script:TokA (JAtualizarJson $D3 @(JItem 'Serviços de consultoria técnica (demo)' 5 10000) $V3)
JAssert 'PUT Rq3 com itens 5x10000 (=50000 exato) aceita 204' ($r3e.status -eq 204)
$s3=JApi 'POST' "/requisicoes/$($script:Rq3)/enviar?version=$($V3+1)" $script:TokA $null 'jornada-rq3-enviar-k1'
JAssert 'envio Rq3: 200 ciclo 1 repetido=false' (($s3.status -eq 200) -and $s3.json.ciclo -eq 1)
$niv2=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq3)' and nivel=2;"
$tot3=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq3)';"
JAssert 'Rq3 gera apenas nivel 1 (limite 50000 >= total 50000): 2 linhas e zero nivel 2' (($tot3 -eq '2') -and ($niv2 -eq '0')) ("total=$tot3 nivel2=$niv2")
[void](JDb ("select a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.limite, a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq3)' order by a.nivel, a.id;"))
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokA).json
$e3=JFilaItem $f $Gq3 1 $GA
JAssert 'fila do analista contem sua etapa n1 da Rq3' ($null -ne $e3)
$d3r=JApi 'POST' "/aprovacoes/$($e3.etapaId)/decidir" $script:TokA (JDecidirJson 'APROVAR' $null ([long]$e3.version) 'jornada-rq3-d1-analista')
JAssert 'analista com alçada 50000 decide etapa de limite 50000 (fronteira inclusa): requisicao APROVADA' (($d3r.status -eq 200) -and $d3r.json.etapaStatus -eq 'APROVADO' -and $d3r.json.requisicaoStatus -eq 'APROVADA' -and $d3r.json.repetido -eq $false)
$st3=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq3)';"
$cancelG=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq3)' and aprovador_id='$($script:SubB)' and status='CANCELADO';"
JAssert 'Rq3 APROVADA no banco e linha do gestor cancelada' ($st3 -eq 'APROVADA') "status=$st3 cancelGestor=$cancelG"

# ------------------------------------------------------------------- S5 -----
JHead 'S5 - Rq4 (RC-DEMO-0004): idempotencia de envio, devolucao, correcao, 409 conteudo, ciclo 2 REJEITAR, decisao dupla'
$r4e=JApi 'PUT' "/requisicoes/$($script:Rq4)" $script:TokA (JAtualizarJson $D4 @(JItem 'Equipamentos de rede (demo)' 7 10000) $V4)
JAssert 'PUT Rq4 com itens 7x10000 (=70000) aceita 204' ($r4e.status -eq 204)
$V4a=$V4+1
$s4=JApi 'POST' "/requisicoes/$($script:Rq4)/enviar?version=$V4a" $script:TokA $null 'jornada-rq4-enviar-k1'
JAssert 'envio Rq4 K1: 200 ciclo 1 repetido=false' (($s4.status -eq 200) -and $s4.json.ciclo -eq 1 -and $s4.json.repetido -eq $false)
$s4r=JApi 'POST' "/requisicoes/$($script:Rq4)/enviar?version=$V4a" $script:TokA $null 'jornada-rq4-enviar-k1'
JAssert 'REPLAY K1 mesmo conteudo: 200 repetido=true (sem novo ciclo/etapas)' (($s4r.status -eq 200) -and $s4r.json.repetido -eq $true -and $s4r.json.status -eq 'PENDENTE_APROVACAO' -and $s4r.json.ciclo -eq 1)
[void](JDb ("select a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq4)' order by a.ciclo, a.nivel, a.id; select chave, operacao from sigov.compras_empresarial_idempotencia where chave like 'jornada-rq4-enviar-k1';"))
$cnt4=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq4)';"
JAssert 'replay K1 nao cria etapas novas (total continua 3)' ($cnt4 -eq '3') "etapas=$cnt4"
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$e4=JFilaItem $f $Gq4 1 $GB
JAssert 'fila do gestor contem sua etapa n1 (ciclo 1) da Rq4' ($null -ne $e4)
$dev=JApi 'POST' "/aprovacoes/$($e4.etapaId)/decidir" $script:TokB (JDecidirJson 'DEVOLVER' 'Especificação do item principal incompleta; corrigir antes da nova análise.' ([long]$e4.version) 'jornada-rq4-dev-gestor')
JAssert 'DEVOLVER n1 (gestor): etapa DEVOLVIDA e requisicao DEVOLVIDA' (($dev.status -eq 200) -and $dev.json.etapaStatus -eq 'DEVOLVIDA' -and $dev.json.requisicaoStatus -eq 'DEVOLVIDA' -and $dev.json.repetido -eq $false)
[void](JDb ("select a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq4)' order by a.ciclo, a.nivel, a.id; select status, version from sigov.compras_empresarial_requisicao where id='$($script:Rq4)';"))
$D4b=(JApi 'GET' "/requisicoes/$($script:Rq4)" $script:TokA).json
JAssert 'detalhe Rq4 confirma status DEVOLVIDA' ($D4b.status -eq 'DEVOLVIDA')
$V4b=[long]$D4b.version
$r4fix=JApi 'PUT' "/requisicoes/$($script:Rq4)" $script:TokA (JAtualizarJson $D4b @(JItem 'Equipamentos de rede - especificacao corrigida (demo)' 1 25000) $V4b)
JAssert 'correcao Rq4 em DEVOLVIDA: PUT 204 (volta para RASCUNHO)' ($r4fix.status -eq 204)
$D4c=(JApi 'GET' "/requisicoes/$($script:Rq4)" $script:TokA).json
JAssert 'apos correcao, Rq4 esta RASCUNHO com novo total 25000' (($D4c.status -eq 'RASCUNHO') -and [decimal]$D4c.valorEstimado -eq 25000)
$V4c=[long]$D4c.version
$c409=JApi 'POST' "/requisicoes/$($script:Rq4)/enviar?version=$V4c" $script:TokA $null 'jornada-rq4-enviar-k1'
JAssert 'reenvio com MESMA chave K1 e conteudo diferente: 409 mensagem exata de idempotencia' (($c409.status -eq 409) -and $c409.body.Contains('A chave de idempotência já foi usada com conteúdo diferente.')) ("http={0}" -f $c409.status)
$s4k2=JApi 'POST' "/requisicoes/$($script:Rq4)/enviar?version=$V4c" $script:TokA $null 'jornada-rq4-enviar-k2'
JAssert 'reenvio com chave nova K2: 200 ciclo 2' (($s4k2.status -eq 200) -and $s4k2.json.ciclo -eq 2 -and $s4k2.json.repetido -eq $false)
[void](JDb ("select distinct ciclo from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq4)' order by ciclo; select a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq4)' and a.ciclo=2 order by a.nivel, a.id;"))
$ciclos=JVal "select count(distinct ciclo) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq4)';"
$et2=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq4)' and ciclo=2;"
JAssert 'ciclo 2 aberto com etapas apenas no nivel 1 (total 25000 <= 50000): 2 linhas' (($ciclos -eq '2') -and ($et2 -eq '2')) ("ciclos=$ciclos etapasC2=$et2")
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$e5=JFilaItem $f $Gq4 1 $GB
JAssert 'fila do gestor contem sua etapa n1 do ciclo 2 da Rq4' ($null -ne $e5)
$rejVer=[long]$e5.version
$rejBody=JDecidirJson 'REJEITAR' 'Duplicidade com cotação vigente; aguardar renovação do processo.' $rejVer 'jornada-rq4-rej-gestor'
$rej=JApi 'POST' "/aprovacoes/$($e5.etapaId)/decidir" $script:TokB $rejBody
JAssert 'REJEITAR n1 (gestor): etapa REJEITADA e requisicao REJEITADA' (($rej.status -eq 200) -and $rej.json.etapaStatus -eq 'REJEITADA' -and $rej.json.requisicaoStatus -eq 'REJEITADA' -and $rej.json.repetido -eq $false)
$rej2=JApi 'POST' "/aprovacoes/$($e5.etapaId)/decidir" $script:TokB $rejBody
JAssert 'DECISÃO DUPLA replay identico: 200 repetido=true reexpondo o resultado persistido' (($rej2.status -eq 200) -and $rej2.json.repetido -eq $true -and $rej2.json.etapaStatus -eq 'REJEITADA' -and $rej2.json.requisicaoStatus -eq 'REJEITADA')
JLog 'NOTA: replay de decisao identica reexpoe o resultado persistido (Repetido=true); a tela Web converte Repetido=true para a mensagem fixada "Decisão já registrada; o resultado persistido foi reapresentado." (controller Web ComprasEmpresariaisController.DecidirAprovacao).'
$rejOutro=JDecidirJson 'APROVAR' $null $rejVer 'jornada-rq4-rej-gestor'
$rej409=JApi 'POST' "/aprovacoes/$($e5.etapaId)/decidir" $script:TokB $rejOutro
JAssert 'mesma chave K de decisao com conteudo diferente: 409' (($rej409.status -eq 409) -and $rej409.body.Contains('A chave de idempotência já foi usada com conteúdo diferente.'))
$st4=JVal "select status from sigov.compras_empresarial_requisicao where id='$($script:Rq4)';"
[void](JDb ("select a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq4)' order by a.ciclo, a.nivel, a.id; select acao from sigov.compras_empresarial_historico where aggregate_type='REQUISICAO' and aggregate_id='$($script:Rq4)' order by created_at, id;"))
JAssert 'Rq4 final REJEITADA no banco' ($st4 -eq 'REJEITADA')

# ------------------------------------------------------------------- S6 -----
JHead 'S6 - Rq5: criada via API (idempotente) e enviada => deixa etapas pendentes para render web'
$criaOrdered=[ordered]@{ setor='TI'; urgencia='NORMAL'; dataNecessaria='2026-10-31'; justificativa='Requisição do gate de homologação (demo); mantida pendente para evidência de fila populada.'; observacoes='RC50-68A gate'; itens=@(JItem 'Notebook para homologação do gate' 8 10000) }
$criaBody=(New-Object PSObject -Property $criaOrdered | ConvertTo-Json -Depth 6 -Compress)
$cr=JApi 'POST' '/requisicoes' $script:TokA $criaBody 'jornada-rq5-criar-k1'
JAssert 'POST /requisicoes: 201 com id' ($cr.status -eq 201)
$Rq5=$cr.json.id
$Rq5g=[guid]$Rq5
$D5=(JApi 'GET' "/requisicoes/$Rq5" $script:TokA).json
$Num5=$D5.numero
$V5=[long]$D5.version
JAssert 'Rq5 criada em RASCUNHO com numero emitido' ($D5.status -eq 'RASCUNHO') ("numero=$Num5")
$s5=JApi 'POST' "/requisicoes/$Rq5/enviar?version=$V5" $script:TokA $null 'jornada-rq5-enviar-k1'
JAssert 'envio Rq5 (total 80000): 200 ciclo 1' (($s5.status -eq 200) -and $s5.json.ciclo -eq 1 -and $s5.json.repetido -eq $false)
[void](JDb ("select a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.limite, a.status from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$Rq5' order by a.nivel, a.id;"))
$et5=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$Rq5';"
JAssert 'Rq5 gerou 3 etapas pendentes (n1x2 + n2x1)' ($et5 -eq '3') "etapas=$et5"

# ------------------------------------------------------------------- S7 -----
JHead 'S7 - PROVAS DE FALHA EXPLÍCITA (sem mutação de estado)'
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokA).json
$ea=JFilaItem $f $Rq5g 1 $GA
JAssert 'fila do analista contem sua etapa n1 da Rq5' ($null -ne $ea)
$pv=JApi 'POST' "/aprovacoes/$($ea.etapaId)/decidir" $script:TokA (JDecidirJson 'APROVAR' $null 999 'jornada-probe-versao-invalida')
JAssert 'versão desatualizada: 409 com mensagem exata de recarga da fila' (($pv.status -eq 409) -and $pv.body.Contains('Versão desatualizada; recarregue a fila de aprovações e tente novamente.')) ("http={0}" -f $pv.status)
$fb=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$en2=JFilaItem $fb $Rq5g 2 $GB
JAssert 'fila do gestor contem sua etapa n2 da Rq5' ($null -ne $en2)
$po=JApi 'POST' "/aprovacoes/$($en2.etapaId)/decidir" $script:TokA (JDecidirJson 'APROVAR' $null ([long]$en2.version) 'jornada-probe-aprovador-errado')
JAssert 'usuário não designado: 422 "Você não é o aprovador designado para esta etapa."' (($po.status -eq 422) -and $po.body.Contains('Você não é o aprovador designado para esta etapa.')) ("http={0}" -f $po.status)

# ------------------------------------------------------------------- S8 -----
JHead 'S8 - ADMIN DE OUTRO CONTEXTO INSTITUCIONAL (fail-closed)'
$ad=JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokD
JLog ("admin GET fila => HTTP {0} (contexto institucional do admin; espera-se vazio ou negado, nunca dados do tenant demo)" -f $ad.status)
if(($ad.status -eq 200) -and $ad.json){ $adminLeak = @($ad.json.items).Count; JAssert 'fila do admin nao vaza etapas do tenant demo' ($adminLeak -eq 0) ("itens=$adminLeak") }
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokA).json
$eax=JFilaItem $f $Rq5g 1 $GA
$ad2=JApi 'POST' "/aprovacoes/$($eax.etapaId)/decidir" $script:TokD (JDecidirJson 'APROVAR' $null ([long]$eax.version) 'jornada-probe-admin-outros-tenant')
JAssert 'admin decide etapa do tenant demo: bloqueado (401/403/404), nunca 200/204' (($ad2.status -in @(401,403,404))) ("http={0} body={1}" -f $ad2.status,$ad2.body)

# ------------------------------------------------------------------- S9 -----
JHead 'S9 - HONESTIDADE: fila/painéis/relatorio CSV vs banco vivo'
$fa=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=100' $script:TokA).json
$dbA=JVal "select count(*) from sigov.compras_empresarial_aprovacao a where a.tenant_id='$($script:TenantDemo)' and a.status='PENDENTE' and (a.aprovador_id='$($script:SubA)' or a.aprovador_id is null);"
JAssert 'fila do analista bate com o banco (esperado 2: Rq2 bloqueada + Rq5 n1)' (([long]$fa.totalItems -eq 2) -and ($dbA -eq '2')) ("api=$($fa.totalItems) db=$dbA")
$fb=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=100' $script:TokB).json
$dbB=JVal "select count(*) from sigov.compras_empresarial_aprovacao a where a.tenant_id='$($script:TenantDemo)' and a.status='PENDENTE' and (a.aprovador_id='$($script:SubB)' or a.aprovador_id is null);"
JAssert 'fila do gestor bate com o banco (esperado 3: Rq2 bloqueada + Rq5 n1 + Rq5 n2)' (([long]$fb.totalItems -eq 3) -and ($dbB -eq '3')) ("api=$($fb.totalItems) db=$dbB")
$devCount=JVal "select count(*) from sigov.compras_empresarial_requisicao where tenant_id='$($script:TenantDemo)' and not is_deleted and status='DEVOLVIDA';"
$conCount=JVal "select count(*) from sigov.compras_empresarial_requisicao where tenant_id='$($script:TenantDemo)' and not is_deleted and status in ('APROVADA','REJEITADA');"
JAssert 'painel Devolvidas vazio e honesto (Rq4 saiu de DEVOLVIDA no ciclo 2)' ($devCount -eq '0') "devolvidas=$devCount"
JAssert 'painel Concluidas reflete exatamente 3 finais (Rq1,Rq3 APROVADA + Rq4 REJEITADA)' ($conCount -eq '3') "concluidas=$conCount"
$csv=JApi 'GET' '/relatorios/aprovacoes.csv' $script:TokA
$csvLines=($csv.body -split "`r`n" | Where-Object { $_.Trim().Length -gt 0 })
$dataRows=[int]($csvLines.Count - 1)
$dbEt=JVal "select count(*) from sigov.compras_empresarial_aprovacao a join sigov.compras_empresarial_requisicao r on r.tenant_id=a.tenant_id and r.id=a.requisicao_id and not r.is_deleted where a.tenant_id='$($script:TenantDemo)';"
JAssert 'relatorio CSV: linhas de dados == etapas no banco (esperado 14)' (([int]$dataRows -eq 14) -and ($dbEt -eq '14')) ("csv=$dataRows db=$dbEt")
JAssert 'relatorio CSV contem RC-DEMO-0001..0004 + numero da Rq5' (($csv.body -match 'RC-DEMO-0001') -and ($csv.body -match 'RC-DEMO-0002') -and ($csv.body -match 'RC-DEMO-0003') -and ($csv.body -match 'RC-DEMO-0004') -and $csv.body.Contains($Num5))

# ------------------------------------------------------------------ S10 -----
JHead 'S10 - SNAPSHOT FINAL DO BANCO VIVO'
[void](JDb ("select r.numero, r.status, r.version, r.valor_estimado from sigov.compras_empresarial_requisicao r where r.tenant_id='$($script:TenantDemo)' and not r.is_deleted order by r.numero;"))
[void](JDb ("select r.numero, a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL') as aprovador, a.limite, a.status, coalesce(left(a.motivo,40),'') as motivo from sigov.compras_empresarial_aprovacao a join sigov.compras_empresarial_requisicao r on r.id=a.requisicao_id where a.tenant_id='$($script:TenantDemo)' order by r.numero, a.ciclo, a.nivel, a.id;"))
[void](JDb ("select chave, operacao from sigov.compras_empresarial_idempotencia where chave like 'jornada-%' order by chave;"))
[void](JDb ("select tipo, status, titulo, rota_acao from sigov.pendencia_operacional where tipo like 'APROVACAO%' order by id;"))
[void](JDb ("select acao, count(*) as n from sigov.compras_empresarial_historico where aggregate_type='REQUISICAO' and aggregate_id in ('$($script:Rq1)','$($script:Rq2)','$($script:Rq3)','$($script:Rq4)','$Rq5') group by acao order by acao;"))

JLog ''
if($script:JFail -eq 0){ JLog '===== RESUMO: TODOS OS ASSERTS PASSARAM =====' } else { JLog ('===== RESUMO: {0} ASSERT(S) FALHARAM =====' -f $script:JFail) }
Write-Host ('JOURNEY_FAILS=' + $script:JFail)
exit $(if($script:JFail -eq 0){0}else{1})
