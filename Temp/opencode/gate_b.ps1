[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
. (Join-Path $PSScriptRoot 'jornada_lib.ps1')
$script:JDbMode='docker'
$script:JFail=0
$script:TenantDemo='b0000001-0000-4000-8000-000000000001'
$Gq1=[guid]$script:Rq1; $Gq2=[guid]$script:Rq2; $Gq4=[guid]$script:Rq4
$GA=[guid]$script:SubA; $GB=[guid]$script:SubB

JLog ''
JLog ''
JLog '################################################################################'
JLog '#####  BLOCO B - JORNADA DE RUNTIME: requisicao aprovada -> cotacao -> comparativo'
JLog '#####  -> selecao auditada -> pedido atomico -> reconhecimento pelo recebimento'
JLog '################################################################################'
JLog ('SIGOV PLUS - GATE BLOCO B (jornada de runtime) iniciado em ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
JLog ('api base: {0}' -f $script:JBase)
JLog ('tenant demo municipal: {0}; fixtures Rq1..Rq4 (seed institucional); fornecedores FORN-DEMO-2026-0001..0003 (seed de cotação)' -f $script:TenantDemo)

# --------------------------------------------------------------- helpers ---
# Espelho exato do gate do Bloco A (curl via cmd /c): splat de array com >=4
# elementos no PS 5.1 quebra argumentos no formato <x>C:\... em dois args.
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

function JIssua([long]$uid,[long]$tid,[long]$ent,[long]$exer,[string]$tok,[string]$corr){
  $exExpr = if($exer -gt 0){ "$exer" } else { 'null' }
  [void](JDb ("delete from sigov.identidade_sessao where token_hash = encode(sha256(convert_to('$tok','UTF8')),'hex'); insert into sigov.identidade_sessao (tenant_id, entidade_id, exercicio_id, usuario_id, token_hash, expira_at, auth_version, user_agent_sanitizado, correlation_id, created_by) values ($tid, $ent, $exExpr, $uid, encode(sha256(convert_to('$tok','UTF8')),'hex'), now() + interval '12 hours', 1, 'gate-bloco-b-jornada', '$corr', $uid);"))
  $n=JVal "select count(*) from sigov.identidade_sessao where correlation_id='$corr';"
  JAssert ("sessao emitida para usuario {0} (tenant {1}, entidade {2})" -f $uid,$tid,$ent) ($n -eq '1') "sessoes=$n"
}

# ---- builders de payload (JSON camelCase, conforme contratos da API) ----
function JObj($o){ New-Object PSObject -Property $o | ConvertTo-Json -Depth 8 -Compress }
function JPrazoIso([int]$dias){ (Get-Date).ToUniversalTime().AddDays($dias).ToString('yyyy-MM-ddTHH:mm:ss.fff\Z') }
function JCriarCot($rq,$forn,$prazo,$prod){ JObj ([ordered]@{ requisicaoId=$rq; fornecedorIds=$forn; prazo=$prazo; produtos=$prod }) }
function JProdLinha($ri,$prodId){ [ordered]@{ requisicaoItemId=$ri; produtoId=$prodId } }
function JRespItem($ri,[double]$pu,[double]$des,[double]$imp,[double]$frete,[int]$prazo,[bool]$rec){ [ordered]@{ requisicaoItemId=$ri; precoUnitario=$pu; desconto=$des; imposto=$imp; frete=$frete; prazoDias=$prazo; marca=$null; fabricante=$null; recusado=$rec } }
function JResp($cid,[long]$cv,$itens){ JObj ([ordered]@{ conviteId=$cid; conviteVersion=$cv; itens=$itens }) }
function JSelItem($ri,$cid,$just){ [ordered]@{ requisicaoItemId=$ri; conviteId=$cid; justificativa=$just } }
function JSel([long]$ver,$itens){ JObj ([ordered]@{ version=$ver; itens=$itens }) }
function JEnc($motivo,[long]$ver){ JObj ([ordered]@{ motivo=$motivo; version=$ver }) }
function JDecidir([string]$dec,[long]$ver,[string]$key){ JObj ([ordered]@{ decisao=$dec; motivo=$null; version=$ver; idempotencyKey=$key }) }
# ---- especho em PS da formula canonica (CUE arredondado 2 casas AwayFromZero) ----
function JCue([double]$pu,[double]$imp,[double]$des){ [math]::Round([decimal]$pu * ([decimal]1 + [decimal]$imp/[decimal]100 - [decimal]$des/[decimal]100), 2, [MidpointRounding]::AwayFromZero) }
function JCusto([double]$cue,[double]$q,[double]$frete){ [math]::Round([decimal]$cue * [decimal]$q + [decimal]$frete, 2, [MidpointRounding]::AwayFromZero) }
function JEqD($a,$b){ [math]::Abs([decimal][double]$a - [decimal]$b) -lt ([decimal]0.005) }
function JNearDate([string]$iso,[int]$dias){ $d=[datetime]$iso; $base=(Get-Date).Date.AddDays($dias); [math]::Abs(($d.Date - $base).Days) -le 1 }
function JGuidStr($r){ $s=([string]$r.body).Trim(); if($s.StartsWith('{')){ if(($null -ne $r.json) -and ($null -ne $r.json.id)){ return ([guid]$r.json.id).ToString('D') } } elseif($s.Length -ge 2 -and $s.StartsWith('"') -and $s.EndsWith('"')){ $s=$s.Substring(1,$s.Length-2) }; $g=[guid]::Empty; [void][guid]::TryParse($s,[ref]$g); $g.ToString('D') }

# ------------------------------------------------------------- S-1 -----
JHead 'S-1 - EMISSAO DAS SESSOES DE DEMONSTRACAO (mesmas colunas do login oficial; sem senha literal - regra 18)'
$nDemo=JVal "select count(*) from sigov.usuario where id in (1,101,102) and ativo and not bloqueado and not is_deleted;"
JAssert 'identidades presentes e ativas (admin=1, analista=101, gestor=102)' ($nDemo -eq '3') "usuarios=$nDemo"
JIssua 101 1 9101 0 $script:TokA 'b1000000-0000-4000-8000-0000000000a1'
JIssua 102 1 9101 0 $script:TokB 'b1000000-0000-4000-8000-0000000000b2'
JIssua 1   5 1    1 $script:TokD 'b1000000-0000-4000-8000-0000000000c3'

# ------------------------------------------------------------------- S0 -----
JHead 'S0 - PREFLIGHT: estado virgem pos-reset (fixtures RASCUNHO v1, sem politica/cotacao/pedido/recebimento, chaves jb-% zeradas)'
$fq=JVal ("select string_agg(r.status||':v'||r.version,',' order by r.numero) from sigov.compras_empresarial_requisicao r where r.id in ('$($script:Rq1)','$($script:Rq2)','$($script:Rq3)','$($script:Rq4)');")
JAssert 'fixtures Rq1..Rq4 em RASCUNHO v1 (estado virgem)' ($fq -eq 'RASCUNHO:v1,RASCUNHO:v1,RASCUNHO:v1,RASCUNHO:v1') "status=$fq"
$polCount=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica where tenant_id='$($script:TenantDemo)';"
$etCount=JVal ("select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id in ('$($script:Rq1)','$($script:Rq2)','$($script:Rq3)','$($script:Rq4)');")
$cotCount=JVal "select count(*) from sigov.compras_empresarial_cotacao where tenant_id='$($script:TenantDemo)';"
$pedCount=JVal "select count(*) from sigov.compras_empresarial_pedido where tenant_id='$($script:TenantDemo)';"
$recCount=JVal "select count(*) from sigov.compras_empresarial_recebimento where tenant_id='$($script:TenantDemo)';"
$keyCount=JVal "select count(*) from sigov.compras_empresarial_idempotencia where chave like 'jb-%';"
JAssert 'POLITICA=0 antes da jornada' ($polCount -eq '0') "politica=$polCount"
JAssert 'nenhuma etapa nos fixtures antes da jornada' ($etCount -eq '0') "etapas=$etCount"
JAssert 'COTACAO=0 antes da jornada' ($cotCount -eq '0') "cotacoes=$cotCount"
JAssert 'PEDIDO=0 antes da jornada' ($pedCount -eq '0') "pedidos=$pedCount"
JAssert 'RECEBIMENTO=0 antes da jornada' ($recCount -eq '0') "recebimentos=$recCount"
JAssert 'nenhuma chave de idempotencia jb-% no banco' ($keyCount -eq '0') "chaves=$keyCount"
$fRas=JVal "select count(*) from sigov.compras_empresarial_fornecedor where tenant_id='$($script:TenantDemo)' and status='RASCUNHO' and ativo and not is_deleted;"
$fBloq=JVal "select count(*) from sigov.compras_empresarial_fornecedor where tenant_id='$($script:TenantDemo)' and status='BLOQUEADO' and not is_deleted;"
JAssert 'fornecedores do seed: 2 RASCUNHO ativos + 1 BLOQUEADO' (($fRas -eq '2') -and ($fBloq -eq '1')) "ras=$fRas bloq=$fBloq"
$hFor=JVal "select count(*) from sigov.compras_empresarial_historico where aggregate_type='FORNECEDOR' and acao='CRIADO';"
JAssert 'historico FORNECEDOR CRIADO = 3 (seed idempotente)' ($hFor -eq '3') "linhas=$hFor"
$F1S=JVal "select id from sigov.compras_empresarial_fornecedor where tenant_id='$($script:TenantDemo)' and codigo='FORN-DEMO-2026-0001';"
$F2S=JVal "select id from sigov.compras_empresarial_fornecedor where tenant_id='$($script:TenantDemo)' and codigo='FORN-DEMO-2026-0002';"
$F3S=JVal "select id from sigov.compras_empresarial_fornecedor where tenant_id='$($script:TenantDemo)' and codigo='FORN-DEMO-2026-0003';"
JAssert 'fornecedores F1/F2/F3 resolvidos por codigo (sem fabricar ids)' (($F1S.Length -eq 36) -and ($F2S.Length -eq 36) -and ($F3S.Length -eq 36)) ("F1=$F1S F2=$F2S F3=$F3S")
$stF3=JVal "select status from sigov.compras_empresarial_fornecedor where id='$F3S';"
JAssert 'F3 (Gama) BLOQUEADO - exercita o filtro de elegibilidade' ($stF3 -eq 'BLOQUEADO') "status=$stF3"
$almok=JVal "select count(*) from sigov.estoque_almoxarifado where id='a0000001-0000-4000-8000-000000000201' and tenant_id='$($script:TenantDemo)' and ativo;"
JAssert 'almoxarifado municipal ficticio presente e ativo' ($almok -eq '1')
$prodN=JVal "select count(*) from sigov.estoque_produto where tenant_id='$($script:TenantDemo)' and ativo;"
JAssert 'catalogo ficticio municipal com 4 produtos ativos' ($prodN -eq '4') "produtos=$prodN"
$f0=(JApi 'GET' '/fornecedores' $script:TokA).json
JAssert 'GET /fornecedores lista exatamente os 3 do seed' ([long]$f0.totalItems -eq 3) "totalItems=$($f0.totalItems)"
$c0=(JApi 'GET' '/cotacoes' $script:TokA).json
JAssert 'GET /cotacoes vazio antes da jornada' ([long]$c0.totalItems -eq 0)
$p0=(JApi 'GET' '/pedidos' $script:TokA).json
JAssert 'GET /pedidos vazio antes da jornada' ([long]$p0.totalItems -eq 0)
$r0=(JApi 'GET' '/recebimentos' $script:TokA).json
JAssert 'central de recebimentos vazia antes da jornada' ([long]$r0.resultado.totalItems -eq 0)
$pl0=JApi 'GET' '/configuracao/politica' $script:TokA
JAssert 'politica ausente => 404 explícito, sem fallback inventado' ($pl0.status -eq 404)
if($script:JFail -gt 0){ Write-Host 'FATAL: estado nao e virgem; execute Temp/opencode/gate_reset.ps1 antes de repetir a jornada.'; exit 2 }

# ------------------------------------------------------------------- S1 -----
JHead 'S1 - POLITICA INSTITUCIONAL: upsert por tenant, alçadas cumulativas 50000/250000, replay idempotente'
$polOrdered=[ordered]@{ nome='Política institucional RC50-68A (demo municipal)'; niveis=@([ordered]@{limite=50000},[ordered]@{limite=250000}) }
$polBody=JObj $polOrdered
$sp=JApi 'PUT' '/configuracao/politica' $script:TokA $polBody 'jb-politica-k1'
JAssert 'salvar politica: 200 repetido=false' (($sp.status -eq 200) -and $sp.json.repetido -eq $false)
$sp2=JApi 'PUT' '/configuracao/politica' $script:TokA $polBody 'jb-politica-k1'
JAssert 'replay mesma chave+conteudo: repetido=true (sem duplicar politica)' (($sp2.status -eq 200) -and $sp2.json.repetido -eq $true)
$gp=JApi 'GET' '/configuracao/politica' $script:TokA
JAssert 'politica multi-esfera: esfera municipal + 2 niveis em ordem ascendente' (($gp.status -eq 200) -and $gp.json.esferaGoverno -eq 'municipal' -and @($gp.json.niveis).Count -eq 2 -and [decimal]$gp.json.niveis[0].limite -eq 50000 -and [decimal]$gp.json.niveis[1].limite -eq 250000)
$polNow=JVal "select count(*) from sigov.compras_empresarial_aprovacao_politica where tenant_id='$($script:TenantDemo)';"
JAssert 'exatamente UMA politica ativa por tenant apos upsert' ($polNow -eq '1') "count=$polNow"

# ------------------------------------------------------------------- S2 -----
JHead 'S2 - APROVACAO: Rq1 (80000 => 2 niveis) e Rq2 (40000 => 1 nivel); Rq3/Rq4 permanecem RASCUNHO'
$D1=(JApi 'GET' "/requisicoes/$($script:Rq1)" $script:TokA).json
JAssert 'Rq1 em RASCUNHO v1 com total 80000' (($D1.status -eq 'RASCUNHO') -and [long]$D1.version -eq 1 -and (JEqD $D1.valorEstimado 80000))
$s1=JApi 'POST' "/requisicoes/$($script:Rq1)/enviar?version=1" $script:TokA $null 'jb-rq1-enviar-k1'
JAssert 'envio Rq1: 200 PENDENTE_APROVACAO ciclo 1 repetido=false' (($s1.status -eq 200) -and $s1.json.status -eq 'PENDENTE_APROVACAO' -and $s1.json.ciclo -eq 1 -and $s1.json.repetido -eq $false)
[void](JDb ("select a.id, a.ciclo, a.nivel, coalesce(a.aprovador_id::text,'NULL'), a.limite, a.status, a.version from sigov.compras_empresarial_aprovacao a where a.requisicao_id='$($script:Rq1)' order by a.nivel, a.id;"))
$nR1=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)';"
$nR1b=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and aprovador_id='$GB' and status='PENDENTE';"
$nR1a=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and aprovador_id='$GA';"
JAssert 'Rq1: exatamente 2 etapas, ambas designadas ao gestor (SubB), nenhuma ao solicitante (regra X1)' (($nR1 -eq '2') -and ($nR1b -eq '2') -and ($nR1a -eq '0')) ("total=$nR1 subB=$nR1b subA=$nR1a")
$f=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$n1i=@($f.items) | Where-Object { $_.requisicaoId -eq $Gq1 -and $_.nivel -eq 1 } | Select-Object -First 1
$n2i=@($f.items) | Where-Object { $_.requisicaoId -eq $Gq1 -and $_.nivel -eq 2 } | Select-Object -First 1
JAssert 'fila do gestor expoe n1 decisivel e n2 aguardando quorum do nivel anterior (decisivelPorMim=false)' (($null -ne $n1i) -and ($null -ne $n2i) -and ($n1i.bloqueada -eq $false) -and ($n1i.decisivelPorMim -eq $true) -and ($n2i.bloqueada -eq $false) -and ($n2i.decisivelPorMim -eq $false))
$pp=JApi 'POST' "/aprovacoes/$($n2i.etapaId)/decidir" $script:TokB (JDecidir 'APROVAR' ([long]$n2i.version) 'jb-probe-rq1-d2-antes') 'jb-probe-rq1-d2-antes'
JAssert 'decidir nivel 2 ANTES do nivel 1: 422 com mensagem institucional exata' (($pp.status -eq 422) -and $pp.body.Contains('Não é possível decidir esta etapa antes da aprovação de todos os níveis anteriores.')) ("http={0}" -f $pp.status)
$d1=JApi 'POST' "/aprovacoes/$($n1i.etapaId)/decidir" $script:TokB (JDecidir 'APROVAR' ([long]$n1i.version) 'jb-rq1-d1-k1') 'jb-rq1-d1-k1'
JAssert 'APROVAR nivel 1 da Rq1: 200 etapa APROVADO, requisicao segue PENDENTE_APROVACAO' (($d1.status -eq 200) -and $d1.json.etapaStatus -eq 'APROVADO' -and $d1.json.requisicaoStatus -eq 'PENDENTE_APROVACAO' -and $d1.json.repetido -eq $false)
$d1r=JApi 'POST' "/aprovacoes/$($n1i.etapaId)/decidir" $script:TokB (JDecidir 'APROVAR' ([long]$n1i.version) 'jb-rq1-d1-k1') 'jb-rq1-d1-k1'
JAssert 'replay da decisao (mesma chave+conteudo): repetido=true sem nova mutacao' (($d1r.status -eq 200) -and $d1r.json.repetido -eq $true)
$f1b=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$n2i2=@($f1b.items) | Where-Object { $_.requisicaoId -eq $Gq1 -and $_.nivel -eq 2 } | Select-Object -First 1
$d2=JApi 'POST' "/aprovacoes/$($n2i2.etapaId)/decidir" $script:TokB (JDecidir 'APROVAR' ([long]$n2i2.version) 'jb-rq1-d2-k1') 'jb-rq1-d2-k1'
JAssert 'APROVAR nivel 2 da Rq1: 200 => requisicao APROVADA' (($d2.status -eq 200) -and $d2.json.etapaStatus -eq 'APROVADO' -and $d2.json.requisicaoStatus -eq 'APROVADA')
$st1=(JApi 'GET' "/requisicoes/$($script:Rq1)" $script:TokA).json
JAssert 'Rq1 APROVADA (API + sem duplicacao de etapas pelo replay)' (($st1.status -eq 'APROVADA') -and (JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq1)' and status='APROVADO';") -eq '2')
$D2=(JApi 'GET' "/requisicoes/$($script:Rq2)" $script:TokA).json
JAssert 'Rq2 em RASCUNHO v1 com total 40000' (($D2.status -eq 'RASCUNHO') -and [long]$D2.version -eq 1 -and (JEqD $D2.valorEstimado 40000))
$s2=JApi 'POST' "/requisicoes/$($script:Rq2)/enviar?version=1" $script:TokA $null 'jb-rq2-enviar-k1'
JAssert 'envio Rq2: 200 PENDENTE_APROVACAO ciclo 1 repetido=false' (($s2.status -eq 200) -and $s2.json.status -eq 'PENDENTE_APROVACAO' -and $s2.json.repetido -eq $false)
$nR2=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq2)';"
$nR2b=JVal "select count(*) from sigov.compras_empresarial_aprovacao where requisicao_id='$($script:Rq2)' and aprovador_id='$GB' and status='PENDENTE';"
JAssert 'Rq2 (<=50000): exatamente 1 etapa, designada ao gestor' (($nR2 -eq '1') -and ($nR2b -eq '1')) ("total=$nR2 subB=$nR2b")
$f2b=(JApi 'GET' '/aprovacoes?pagina=1&tamanho=50' $script:TokB).json
$n1i2=@($f2b.items) | Where-Object { $_.requisicaoId -eq $Gq2 -and $_.nivel -eq 1 } | Select-Object -First 1
$d1b=JApi 'POST' "/aprovacoes/$($n1i2.etapaId)/decidir" $script:TokB (JDecidir 'APROVAR' ([long]$n1i2.version) 'jb-rq2-d1-k1') 'jb-rq2-d1-k1'
JAssert 'APROVAR unico nivel da Rq2 => requisicao APROVADA' (($d1b.status -eq 200) -and $d1b.json.requisicaoStatus -eq 'APROVADA')
$rasc=JVal "select string_agg(status,',' order by numero) from sigov.compras_empresarial_requisicao where id in ('$($script:Rq3)','$($script:Rq4)');"
JAssert 'Rq3/Rq4 permanecem RASCUNHO (nao sao aprovadas nesta jornada)' ($rasc -eq 'RASCUNHO,RASCUNHO') "status=$rasc"

# ------------------------------------------------------------------- S3 -----
JHead 'S3 - ELEGIBILIDADE + COTACAO C1 sobre Rq1 (2 itens): negativas, criacao 201, replay, conflito de chave, duplicata ativa'
$e1=(JApi 'GET' "/cotacoes/elaboracao/$($script:Rq1)" $script:TokA).json
$obji1=@($e1.itens)[0]; $obji2=@($e1.itens)[1]
$IT1=$obji1.requisicaoItemId; $IT2=$obji2.requisicaoItemId
$Q1=[decimal]$obji1.quantidade; $Q2=[decimal]$obji2.quantidade
$EI1=$obji1.exigeInspecao; $EI2=$obji2.exigeInspecao
JAssert 'elaboracao Rq1: elegivel, proximaRodada=1, 2 itens com saldo integral e reserva zero' (($e1.elegivel -eq $true) -and $e1.proximaRodada -eq 1 -and @($e1.itens).Count -eq 2 -and (JEqD $obji1.saldoDisponivel $Q1) -and (JEqD $obji1.reservaAtiva 0) -and (JEqD $obji2.saldoDisponivel $Q2) -and (JEqD $obji2.reservaAtiva 0)) ("Q1={0} Q2={1}" -f $Q1,$Q2)
$e2=(JApi 'GET' "/cotacoes/elaboracao/$($script:Rq2)" $script:TokA).json
$obji3=@($e2.itens)[0]; $IT3=$obji3.requisicaoItemId
$Q3=[decimal]$obji3.quantidade; $EI3=$obji3.exigeInspecao
JAssert 'elaboracao Rq2: elegivel, 1 item com saldo integral' (($e2.elegivel -eq $true) -and @($e2.itens).Count -eq 1 -and (JEqD $obji3.saldoDisponivel $Q3)) ("Q3={0}" -f $Q3)
$e4=(JApi 'GET' "/cotacoes/elaboracao/$($script:Rq4)" $script:TokA).json
JAssert 'elaboracao Rq4 (RASCUNHO): inelegivel com motivo explicito' (($e4.elegivel -eq $false) -and (-not [string]::IsNullOrWhiteSpace([string]$e4.motivoInelegibilidade))) ("motivo={0}" -f $e4.motivoInelegibilidade)
$el=(JApi 'GET' '/cotacoes/elegiveis?pagina=1&tamanho=50' $script:TokA).json
$el1=@($el.items) | Where-Object { $_.requisicaoId -eq $Gq1 } | Select-Object -First 1
$el2=@($el.items) | Where-Object { $_.requisicaoId -eq $Gq2 } | Select-Object -First 1
$el4=@($el.items) | Where-Object { $_.requisicaoId -eq $Gq4 } | Select-Object -First 1
JAssert 'lista de elegiveis: Rq1 (2 itens) e Rq2 (1 item) com saldo por quantidade; Rq4 ausente' (($null -ne $el1) -and $el1.itensElegiveis -eq 2 -and (JEqD $el1.saldoDisponivelTotal ([decimal]($Q1+$Q2))) -and ($null -ne $el2) -and $el2.itensElegiveis -eq 1 -and (JEqD $el2.saldoDisponivelTotal $Q3) -and ($null -eq $el4))
$prods=JApi 'GET' '/cotacoes/produtos?limite=50' $script:TokA
$pArr=@($prods.json)
JAssert 'catalogo de produtos exposto no contexto (>=3 produtos distintos)' ($pArr.Count -ge 3 -and (@($pArr | Select-Object -ExpandProperty id | Sort-Object -Unique).Count -eq $pArr.Count)) ("n={0}" -f $pArr.Count)
$PPa=[string]$pArr[0].id; $PPb=[string]$pArr[1].id; $PPc=[string]$pArr[2].id
$prazo7=JPrazoIso 7; $prazo5=JPrazoIso 5; $prazo120=JPrazoIso 120
$nc1=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq4 @($F1S) $prazo7 @((JProdLinha $IT1 $PPa))) 'jb-probe-cot-naoaprovada'
JAssert 'negativa: requisicao nao aprovada => 422 com mensagem exata' (($nc1.status -eq 422) -and $nc1.body.Contains('Somente requisições aprovadas podem gerar cotação.')) ("http={0}" -f $nc1.status)
$nc2=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq1 @($F1S,$F3S) $prazo7 @((JProdLinha $IT1 $PPa),(JProdLinha $IT2 $PPb))) 'jb-probe-cot-bloqueado'
JAssert 'negativa: fornecedor BLOQUEADO => 400 habilitados neste contexto' (($nc2.status -eq 400) -and $nc2.body.Contains('Um ou mais fornecedores informados não existem ou não estão habilitados neste contexto.')) ("http={0}" -f $nc2.status)
$nc3=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq1 @($F1S,$F2S) $prazo120 @((JProdLinha $IT1 $PPa))) 'jb-probe-cot-prazo'
JAssert 'negativa: prazo acima de 90 dias => 400' (($nc3.status -eq 400) -and $nc3.body.Contains('O prazo da cotação não pode ultrapassar 90 dias.')) ("http={0}" -f $nc3.status)
$nc4=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq1 @($F1S,$F2S) $prazo7 @((JProdLinha $IT1 $PPa))) $null
JAssert 'negativa: sem header Idempotency-Key => 400 obrigatória' (($nc4.status -eq 400) -and $nc4.body.Contains('Idempotency-Key válida é obrigatória.')) ("http={0}" -f $nc4.status)
$bodyC1=JCriarCot $Gq1 @($F1S,$F2S) $prazo7 @((JProdLinha $IT1 $PPa),(JProdLinha $IT2 $PPb))
$c1=JApi 'POST' '/cotacoes' $script:TokA $bodyC1 'jb-c1-criar-k1'
JAssert 'CRIA C1: 201 rodada 1, 2 itens, 2 convites, repetido=false, numero gerado' (($c1.status -eq 201) -and $c1.json.rodada -eq 1 -and $c1.json.itens -eq 2 -and $c1.json.convites -eq 2 -and $c1.json.repetido -eq $false -and (-not [string]::IsNullOrWhiteSpace([string]$c1.json.numero))) ("numero={0}" -f $c1.json.numero)
# PS5.1: nomes de variavel sao case-insensitive; $C1 == $c1. Ler de $c1 apos
# escrever em $C1 na mesma declaracao sobrescreve a fonte => numero vazio.
$c1j=$c1.json
$C1=[string]$c1j.id; $NUMC1=[string]$c1j.numero
$c1r=JApi 'POST' '/cotacoes' $script:TokA $bodyC1 'jb-c1-criar-k1'
JAssert 'replay C1 (mesma chave+conteudo): repetido=true, mesmo id, sem duplicar' (($c1r.status -eq 201) -and $c1r.json.repetido -eq $true -and ([string]$c1r.json.id -eq $C1))
$c1m=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq1 @($F2S) $prazo7 @((JProdLinha $IT1 $PPa),(JProdLinha $IT2 $PPb))) 'jb-c1-criar-k1'
JAssert 'mesma chave + conteudo diferente => 409 conflito' (($c1m.status -eq 409) -and $c1m.body.Contains('A chave de idempotência já foi usada com conteúdo diferente.')) ("http={0}" -f $c1m.status)
$c1a=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq1 @($F1S,$F2S) $prazo7 @((JProdLinha $IT1 $PPa),(JProdLinha $IT2 $PPb))) 'jb-probe-cot-ativa'
JAssert 'segunda cotacao ATIVA na mesma requisicao => 422 com mensagem exata' (($c1a.status -eq 422) -and $c1a.body.Contains('Já existe cotação ativa para esta requisição; conclua a seleção ou encerre a rodada antes de criar outra.')) ("http={0}" -f $c1a.status)

# ------------------------------------------------------------------- S4 -----
JHead 'S4 - RESPOSTAS da C1: stale version, proposta interna Alfa/Beta, replay, conflito, revisao antes do julgamento'
$dd1=(JApi 'GET' "/cotacoes/$C1" $script:TokA).json
JAssert 'detalhe C1: ABERTA, 2 itens com produto mapeado, 2 convites PENDENTE' (($dd1.status -eq 'ABERTA') -and @($dd1.itens).Count -eq 2 -and @($dd1.convites).Count -eq 2 -and @($dd1.convites | Where-Object { $_.status -eq 'PENDENTE' }).Count -eq 2)
$cv1=@($dd1.convites) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1   # Alfa
$cv2=@($dd1.convites) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1   # Beta
JAssert 'convites mapeados por fornecedor (Alfa + Beta, documento mascarado exposto)' (($null -ne $cv1) -and ($null -ne $cv2) -and (-not [string]::IsNullOrWhiteSpace([string]$cv1.fornecedorDocumento)))
$vCv1O=[long]$cv1.version; $vCv2O=[long]$cv2.version
# Alfa (F1): IT1 PU=100 frete=25 (maior); IT2 PU=100 desc=50% (CUE 50.00) => empate no item 2
# Beta (F2): IT1 PU=90 (menor); IT2 PU=50 (CUE 50.00)
$respA1=JResp $cv1.id $vCv1O @((JRespItem $IT1 100 0 0 25 10 $false),(JRespItem $IT2 100 50 0 0 5 $false))
$respB1=JResp $cv2.id $vCv2O @((JRespItem $IT1 90 0 0 0 7 $false),(JRespItem $IT2 50 0 0 0 15 $false))
$rs0=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA (JResp $cv1.id 999 @((JRespItem $IT1 100 0 0 25 10 $false),(JRespItem $IT2 100 50 0 0 5 $false))) 'jb-probe-resp-stale'
JAssert 'stale version do convite => 409 com mensagem exata' (($rs0.status -eq 409) -and $rs0.body.Contains('Versão desatualizada; recarregue o detalhe da cotação e tente novamente.')) ("http={0}" -f $rs0.status)
$rs1=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA $respA1 'jb-c1-resp-f1-k1'
JAssert 'resposta Alfa registrada por operador autorizado: convite RESPONDIDO, cotacao EM_RESPOSTA' (($rs1.status -eq 200) -and $rs1.json.conviteStatus -eq 'RESPONDIDO' -and $rs1.json.cotacaoStatus -eq 'EM_RESPOSTA' -and $rs1.json.repetido -eq $false)
$rs1r=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA $respA1 'jb-c1-resp-f1-k1'
JAssert 'replay resposta Alfa (mesma chave+conteudo): repetido=true sem mutacao' (($rs1r.status -eq 200) -and $rs1r.json.repetido -eq $true)
$rs1m=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA (JResp $cv1.id $vCv1O @((JRespItem $IT1 101 0 0 25 10 $false),(JRespItem $IT2 100 50 0 0 5 $false))) 'jb-c1-resp-f1-k1'
JAssert 'mesma chave + preco diferente na resposta => 409' (($rs1m.status -eq 409) -and $rs1m.body.Contains('A chave de idempotência já foi usada com conteúdo diferente.')) ("http={0}" -f $rs1m.status)
$rs2=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA $respB1 'jb-c1-resp-f2-k1'
JAssert 'resposta Beta registrada: cotacao segue EM_RESPOSTA' (($rs2.status -eq 200) -and $rs2.json.cotacaoStatus -eq 'EM_RESPOSTA')
$dd2=(JApi 'GET' "/cotacoes/$C1" $script:TokA).json
$cv1n=@($dd2.convites) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$cv2n=@($dd2.convites) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
JAssert 'apos as respostas: cotacao EM_RESPOSTA, convites RESPONDIDO com versao avancada' (($dd2.status -eq 'EM_RESPOSTA') -and ($cv1n.status -eq 'RESPONDIDO') -and ($cv2n.status -eq 'RESPONDIDO') -and ([long]$cv1n.version -gt $vCv1O) -and ([long]$cv2n.version -gt $vCv2O))
$rs3=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA $respA1 'jb-probe-resp-stale2'
JAssert 're-responder com versao antiga do convite => 409 (otimistica por convite)' (($rs3.status -eq 409) -and $rs3.body.Contains('Versão desatualizada; recarregue o detalhe da cotação e tente novamente.')) ("http={0}" -f $rs3.status)
$rs4=JApi 'POST' "/cotacoes/$C1/respostas" $script:TokA (JResp $cv1n.id ([long]$cv1n.version) @((JRespItem $IT1 100 0 0 25 10 $false),(JRespItem $IT2 100 50 0 0 5 $false))) 'jb-c1-resp-f1-revisao-k1'
JAssert 'revisao da resposta ja registrada (nova versao): aceita sem quebrar o comparativo' (($rs4.status -eq 200) -and $rs4.json.cotacaoStatus -eq 'EM_RESPOSTA')

# ------------------------------------------------------------------- S5 -----
JHead 'S5 - COMPARATIVO da C1: formula impressa, menor custo, EMPATE sinalizado, valores conferidos em PS'
$cmp=(JApi 'GET' "/cotacoes/$C1/comparativo" $script:TokA).json
$Vc1=[long]$cmp.version
$lin1=@($cmp.linhas) | Where-Object { $_.requisicaoItemId -eq $IT1 } | Select-Object -First 1
$lin2=@($cmp.linhas) | Where-Object { $_.requisicaoItemId -eq $IT2 } | Select-Object -First 1
$ofA1=@($lin1.ofertas) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$ofB1=@($lin1.ofertas) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
$ofA2=@($lin2.ofertas) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$ofB2=@($lin2.ofertas) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
$expCueB1=JCue 90 0 0      # 90.00
$expCustoB1=JCusto $expCueB1 $Q1 0
$expCueA1=JCue 100 0 0     # 100.00
$expCustoA1=JCusto $expCueA1 $Q1 25
$expCueA2=JCue 100 0 50    # 50.00 (via desconto de 50%)
$expCueB2=JCue 50 0 0      # 50.00 (preco direto)
$expCustoA2=JCusto $expCueA2 $Q2 0
$expCustoB2=JCusto $expCueB2 $Q2 0
JAssert 'comparativo: EM_RESPOSTA, elegivelParaSelecao=true, formula explicita impressa' (($cmp.status -eq 'EM_RESPOSTA') -and ($cmp.elegivelParaSelecao -eq $true) -and $cmp.formula.Contains('Custo Unitário Efetivo (CUE)') -and $cmp.formula.Contains('menor Custo Total do Item') -and $cmp.formula.Contains('justificativa registrada em audito'))
JAssert ("item 1: CUE/custo conferem com a formula (Alfa {0} vs Beta {1}); Beta = menor, sem empate" -f $expCustoA1,$expCustoB1) ((JEqD $ofA1.custoUnitarioEfetivo $expCueA1) -and (JEqD $ofB1.custoUnitarioEfetivo $expCueB1) -and (JEqD $ofA1.custoTotalItem $expCustoA1) -and (JEqD $ofB1.custoTotalItem $expCustoB1) -and ($ofB1.menorCusto -eq $true) -and ($ofB1.empateMenor -eq $false) -and ($ofA1.menorCusto -eq $false) -and ($ofA1.recusado -eq $false)) ("apiA={0} apiB={1}" -f $ofA1.custoTotalItem,$ofB1.custoTotalItem)
JAssert ("item 2: EMPATE no menor custo sinalizado nas duas ofertas (CUE {0} por caminhos distintos)" -f $expCustoA2) ((JEqD $ofA2.custoUnitarioEfetivo $expCueA2) -and (JEqD $ofB2.custoUnitarioEfetivo $expCueB2) -and (JEqD $ofA2.custoTotalItem $expCustoA2) -and (JEqD $ofB2.custoTotalItem $expCustoB2) -and ($ofA2.empateMenor -eq $true) -and ($ofB2.empateMenor -eq $true) -and ($ofA2.menorCusto -eq $true) -and ($ofB2.menorCusto -eq $true))
JAssert 'todos os itens com ofertas (semOfertas=false); nenhuma oferta marcada como recusada nesta rodada' (($lin1.semOfertas -eq $false) -and ($lin2.semOfertas -eq $false) -and (@($lin1.ofertas + $lin2.ofertas) | Where-Object { $_.recusado }).Count -eq 0)

# ------------------------------------------------------------------- S6 -----
JHead 'S6 - SELECAO AUDITADA da C1: negativas (duplicado/justificativa/stale/sem permissao), selecao real, 2 pedidos, trancamento'
$sp0=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB (JSel $Vc1 @((JSelItem $IT1 $cv2.id $null),(JSelItem $IT1 $cv1.id $null))) 'jb-probe-sel-dup'
JAssert 'negativa: mesmo item para 2 fornecedores => 400 com mensagem exata' (($sp0.status -eq 400) -and $sp0.body.Contains('Um mesmo item não pode ser atribuído a mais de um fornecedor nesta seleção.')) ("http={0}" -f $sp0.status)
$sp1=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB (JSel $Vc1 @((JSelItem $IT1 $cv1.id $null))) 'jb-probe-sel-just'
JAssert 'negativa: acima do menor custo SEM justificativa => 400 com mensagem exata' (($sp1.status -eq 400) -and $sp1.body.Contains('A seleção acima do menor custo exige justificativa com ao menos 10 caracteres.')) ("http={0}" -f $sp1.status)
$sp1b=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB (JSel $Vc1 @((JSelItem $IT1 $cv1.id 'abc'))) 'jb-probe-sel-just2'
JAssert 'negativa: justificativa curta (<10 caracteres apos trim) => 400' (($sp1b.status -eq 400) -and $sp1b.body.Contains('A seleção acima do menor custo exige justificativa com ao menos 10 caracteres.')) ("http={0}" -f $sp1b.status)
$sp2=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB (JSel 999 @((JSelItem $IT2 $cv1.id $null))) 'jb-probe-sel-stale'
JAssert 'negativa: versao desatualizada do comparativo => 409 com mensagem exata' (($sp2.status -eq 409) -and $sp2.body.Contains('Versão desatualizada; recarregue o comparativo e tente novamente.')) ("http={0}" -f $sp2.status)
$sp3=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokA (JSel $Vc1 @((JSelItem $IT2 $cv1.id $null))) 'jb-probe-sel-toka'
JAssert 'negativa: perfil sem cotacoes.julgar (analista) => 403' ($sp3.status -eq 403) ("http={0}" -f $sp3.status)
# Selecao real: IT1 -> Beta (menor custo, sem justificativa) | IT2 -> Alfa (empate, custo = menor)
$bodySel=JSel $Vc1 @((JSelItem $IT1 $cv2.id $null),(JSelItem $IT2 $cv1.id $null))
$sel=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB $bodySel 'jb-c1-sel-k1'
JAssert 'SELECAO C1: 200 SELECIONADA, exatamente 2 pedidos (1 por fornecedor), repetido=false' (($sel.status -eq 200) -and $sel.json.status -eq 'SELECIONADA' -and @($sel.json.pedidos).Count -eq 2 -and $sel.json.repetido -eq $false)
$pedB=@($sel.json.pedidos) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
$pedA=@($sel.json.pedidos) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$PB=[string]$pedB.pedidoId; $PA=[string]$pedA.pedidoId
JAssert ("pedidos gerados atomicamente com valor_total conforme formula (Beta {0} | Alfa {1})" -f $expCustoB1,$expCustoA2) ((JEqD $pedB.valorTotal $expCustoB1) -and (JEqD $pedA.valorTotal $expCustoA2) -and $pedB.itens -eq 1 -and $pedA.itens -eq 1) ("PB={0} PA={1}" -f $pedB.valorTotal,$pedA.valorTotal)
$selr=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB $bodySel 'jb-c1-sel-k1'
JAssert 'replay da selecao (mesma chave+conteudo): repetido=true, mesmo pedido do Beta' (($selr.status -eq 200) -and $selr.json.repetido -eq $true -and ([string](@($selr.json.pedidos) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1).pedidoId -eq $PB))
$selm=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB (JSel $Vc1 @((JSelItem $IT1 $cv2.id $null),(JSelItem $IT2 $cv2.id $null))) 'jb-c1-sel-k1'
JAssert 'mesma chave + conteudo diferente na selecao => 409' (($selm.status -eq 409) -and $selm.body.Contains('A chave de idempotência já foi usada com conteúdo diferente.')) ("http={0}" -f $selm.status)
$sp4=JApi 'POST' "/cotacoes/$C1/selecionar" $script:TokB (JSel $Vc1 @((JSelItem $IT2 $cv2.id $null))) 'jb-probe-sel-pos'
JAssert 'selecao nova (chave nova) em cotacao SELECIONADA => 422: a selecao parcial e unica chance, sem duplicidade posterior' (($sp4.status -eq 422) -and $sp4.body.Contains('A seleção é permitida apenas em cotação com respostas registradas (situação EM_RESPOSTA).')) ("http={0}" -f $sp4.status)
$dd3=(JApi 'GET' "/cotacoes/$C1" $script:TokA).json
JAssert 'detalhe C1: SELECIONADA com selecionadoEm, 2 seletions auditadas e 2 pedidos vinculados' (($dd3.status -eq 'SELECIONADA') -and ($null -ne $dd3.selecionadoEm) -and @($dd3.selecoes).Count -eq 2 -and @($dd3.pedidos).Count -eq 2)
$c1b=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq1 @($F1S,$F2S) $prazo5 @((JProdLinha $IT1 $PPa),(JProdLinha $IT2 $PPb))) 'jb-probe-cot-reservado'
JAssert 'nova cotacao sobre Rq1 integralmente reservada (selecionada) => 422 com mensagem exata' (($c1b.status -eq 422) -and $c1b.body.Contains('Todos os itens da requisição já estão integralmente reservados; não há saldo para nova cotação.')) ("http={0}" -f $c1b.status)
$elb=(JApi 'GET' '/cotacoes/elegiveis?pagina=1&tamanho=50' $script:TokA).json
$el1b=@($elb.items) | Where-Object { $_.requisicaoId -eq $Gq1 } | Select-Object -First 1
JAssert 'apos a selecao, Rq1 sai da lista de elegiveis (saldo integralmente reservado)' ($null -eq $el1b)

# ------------------------------------------------------------------- S7 -----
JHead 'S7 - C2 sobre Rq2: oferta RECUSADA, comparativo exclui recusada, encerramento liberando saldo, C3 (rodada=2) com novo pedido'
$bodyC2=JCriarCot $Gq2 @($F1S,$F2S) $prazo5 @((JProdLinha $IT3 $PPc))
$c2=JApi 'POST' '/cotacoes' $script:TokA $bodyC2 'jb-c2-criar-k1'
JAssert 'CRIA C2 (rodada 1 da Rq2): 201, 1 item, 2 convites' (($c2.status -eq 201) -and $c2.json.rodada -eq 1 -and $c2.json.itens -eq 1 -and $c2.json.convites -eq 2)
$C2=[string]$c2.json.id
$dc2=(JApi 'GET' "/cotacoes/$C2" $script:TokA).json
$cv2a=@($dc2.convites) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$cv2b=@($dc2.convites) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
$expCueA3=JCue 120 12 0    # 134.40
$expCustoA3=JCusto $expCueA3 $Q3 0
$ra2=JApi 'POST' "/cotacoes/$C2/respostas" $script:TokA (JResp $cv2a.id ([long]$cv2a.version) @((JRespItem $IT3 120 0 12 0 12 $false))) 'jb-c2-resp-f1-k1'
JAssert ("C2 resposta Alfa normal: PU 120 + imposto 12% => CUE {0}" -f $expCueA3) (($ra2.status -eq 200) -and $ra2.json.cotacaoStatus -eq 'EM_RESPOSTA')
$rb2=JApi 'POST' "/cotacoes/$C2/respostas" $script:TokA (JResp $cv2b.id ([long]$cv2b.version) @((JRespItem $IT3 10 0 0 0 5 $true))) 'jb-c2-resp-f2-k1'
JAssert 'C2 resposta Beta RECUSADA registrada (recusa explicita)' (($rb2.status -eq 200) -and $rb2.json.cotacaoStatus -eq 'EM_RESPOSTA')
$cm2=(JApi 'GET' "/cotacoes/$C2/comparativo" $script:TokA).json
$Vc2=[long]$cm2.version
$l2=@($cm2.linhas) | Where-Object { $_.requisicaoItemId -eq $IT3 } | Select-Object -First 1
$oA2=@($l2.ofertas) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$oB2=@($l2.ofertas) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
JAssert ("comparativo C2: recusa sinalizada e EXCLUIDA do menor; Alfa = unico menor valido (custo {0})" -f $expCustoA3) ((JEqD $oA2.custoUnitarioEfetivo $expCueA3) -and (JEqD $oA2.custoTotalItem $expCustoA3) -and ($oA2.menorCusto -eq $true) -and ($oA2.recusado -eq $false) -and ($oB2.recusado -eq $true) -and ($oB2.menorCusto -eq $false) -and ($oB2.empateMenor -eq $false))
$sr0=JApi 'POST' "/cotacoes/$C2/selecionar" $script:TokB (JSel $Vc2 @((JSelItem $IT3 $cv2b.id $null))) 'jb-probe-sel-recusado'
JAssert 'negativa: selecionar a oferta recusada => 400 com mensagem exata' (($sr0.status -eq 400) -and $sr0.body.Contains('Este item foi recusado pelo fornecedor selecionado; escolha outro.')) ("http={0}" -f $sr0.status)
$er0=JApi 'POST' "/cotacoes/$C2/encerrar" $script:TokB (JEnc 'abc' $Vc2) 'jb-probe-enc-motivo'
JAssert 'negativa: encerramento com motivo <5 caracteres => 400 com mensagem exata' (($er0.status -eq 400) -and $er0.body.Contains('O motivo do encerramento deve ter entre 5 e 500 caracteres.')) ("http={0}" -f $er0.status)
$er1=JApi 'POST' "/cotacoes/$C2/encerrar" $script:TokB (JEnc 'Rodada encerrada apos recusa do segundo fornecedor; abre-se nova rodada para o mesmo item.' 999) 'jb-probe-enc-stale'
JAssert 'negativa: encerramento com versao desatualizada => 409' (($er1.status -eq 409) -and $er1.body.Contains('Versão desatualizada; recarregue o detalhe da cotação e tente novamente.')) ("http={0}" -f $er1.status)
$motivoC2='Rodada encerrada apos recusa do segundo fornecedor; abre-se nova rodada para o mesmo item.'
$enc=JApi 'POST' "/cotacoes/$C2/encerrar" $script:TokB (JEnc $motivoC2 $Vc2) 'jb-c2-encerrar-k1'
JAssert 'ENCERRAR C2: 200 ENCERRADA, repetido=false, motivo auditado' (($enc.status -eq 200) -and $enc.json.status -eq 'ENCERRADA' -and $enc.json.repetido -eq $false)
$encre=JApi 'POST' "/cotacoes/$C2/encerrar" $script:TokB (JEnc $motivoC2 $Vc2) 'jb-c2-encerrar-k1'
JAssert 'replay do encerramento (mesma chave+conteudo): repetido=true' (($encre.status -eq 200) -and $encre.json.repetido -eq $true)
$enc2=JApi 'POST' "/cotacoes/$C2/encerrar" $script:TokB (JEnc $motivoC2 $Vc2) 'jb-probe-enc-dobro'
JAssert 'encerrar de novo (chave nova) em cotacao ENCERRADA => 422' (($enc2.status -eq 422) -and $enc2.body.Contains('Apenas cotações abertas ou em resposta podem ser encerradas.')) ("http={0}" -f $enc2.status)
$dc2b=(JApi 'GET' "/cotacoes/$C2" $script:TokA).json
$cv2bn=@($dc2b.convites) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
$rp2=JApi 'POST' "/cotacoes/$C2/respostas" $script:TokA (JResp $cv2bn.id ([long]$cv2bn.version) @((JRespItem $IT3 10 0 0 0 5 $true))) 'jb-probe-resp-posencerr'
JAssert 'responder apos encerramento => 422 com mensagem exata' (($rp2.status -eq 422) -and $rp2.body.Contains('A cotação não está mais aberta para respostas.')) ("http={0}" -f $rp2.status)
$c3=JApi 'POST' '/cotacoes' $script:TokA (JCriarCot $Gq2 @($F1S,$F2S) $prazo5 @((JProdLinha $IT3 $PPc))) 'jb-c3-criar-k1'
JAssert 'CRIA C3 sobre a MESMA requisicao apos encerramento: 201 com RODADA=2 (saldo liberado pelo encerramento)' (($c3.status -eq 201) -and $c3.json.rodada -eq 2 -and $c3.json.itens -eq 1) ("numero={0}" -f $c3.json.numero)
$C3=[string]$c3.json.id
$dc3=(JApi 'GET' "/cotacoes/$C3" $script:TokA).json
$cv3a=@($dc3.convites) | Where-Object { $_.fornecedorId -eq $F1S } | Select-Object -First 1
$cv3b=@($dc3.convites) | Where-Object { $_.fornecedorId -eq $F2S } | Select-Object -First 1
$ra3=JApi 'POST' "/cotacoes/$C3/respostas" $script:TokA (JResp $cv3a.id ([long]$cv3a.version) @((JRespItem $IT3 120 0 12 0 12 $false))) 'jb-c3-resp-f1-k1'
JAssert 'C3 resposta Alfa registrada (PU 120 + 12% de imposto)' (($ra3.status -eq 200) -and $ra3.json.cotacaoStatus -eq 'EM_RESPOSTA')
$rb3=JApi 'POST' "/cotacoes/$C3/respostas" $script:TokA (JResp $cv3b.id ([long]$cv3b.version) @((JRespItem $IT3 10 0 0 0 5 $true))) 'jb-c3-resp-f2-k1'
JAssert 'C3 resposta Beta recusada novamente (segunda rodada da mesma requisicao)' (($rb3.status -eq 200) -and $rb3.json.cotacaoStatus -eq 'EM_RESPOSTA')
$cm3=(JApi 'GET' "/cotacoes/$C3/comparativo" $script:TokA).json
$Vc3=[long]$cm3.version
$sel3=JApi 'POST' "/cotacoes/$C3/selecionar" $script:TokB (JSel $Vc3 @((JSelItem $IT3 $cv3a.id $null))) 'jb-c3-sel-k1'
$ped3it=@($sel3.json.pedidos)[0]
JAssert ("SELECAO C3: unico fornecedor valido (recusada excluida) => pedido Alfa total {0}" -f $expCustoA3) (($sel3.status -eq 200) -and $sel3.json.status -eq 'SELECIONADA' -and @($sel3.json.pedidos).Count -eq 1 -and ([string]$ped3it.fornecedorId -eq $F1S) -and (JEqD $ped3it.valorTotal $expCustoA3))
$PC3=[string]$ped3it.pedidoId

# ------------------------------------------------------------------- S8 -----
JHead 'S8 - PEDIDOS: lista, detalhes (valor/previsao/item atomicos), 404, isolacao por contexto, auditoria'
$pl=(JApi 'GET' '/pedidos' $script:TokA).json
JAssert 'lista de pedidos: exatamente 3, todos CONFIRMADO, todos vinculados a cotação+requisição' (([long]$pl.totalItems -eq 3) -and (@($pl.items) | Where-Object { $_.status -ne 'CONFIRMADO' }).Count -eq 0 -and (@($pl.items) | Where-Object { [string]::IsNullOrEmpty([string]$_.cotacaoId) -or [string]::IsNullOrEmpty([string]$_.requisicaoId) }).Count -eq 0)
$dpB=(JApi 'GET' "/pedidos/$PB" $script:TokA).json
$ipB=@($dpB.itens)[0]
$bb1=$dpB.status -eq 'CONFIRMADO'
$bb2=[string]$dpB.cotacaoId -eq $C1
$bb3=[string]$dpB.requisicaoId -eq $script:Rq1
$bb4=[string]$dpB.numeroCotacao -eq $NUMC1
$bb5=[string]$dpB.fornecedorNome -like '*Beta*'
$bb6=(JEqD $dpB.valorTotal $expCustoB1)
$bb7=(JNearDate $dpB.previsao 7)
$bb8=(@($dpB.itens).Count -eq 1)
$bb9=(JEqD $ipB.quantidade $Q1)
$bb10=(JEqD $ipB.quantidadeCancelada 0)
$bb11=(JEqD $ipB.valorUnitario $expCueB1)
$bb12=[string]$ipB.produtoId -eq $PPa
$bb13=$ipB.exigeInspecao -eq $EI1
JAssert ("pedido Beta: CONFIRMADO, liga C1/Rq1 (numeroCotacao confere), total {0}, previsao hoje+7, item atomico (produto/qtd/CUE/exigeInspecao)" -f $expCustoB1) (($bb1) -and ($bb2) -and ($bb3) -and ($bb4) -and ($bb5) -and ($bb6) -and ($bb7) -and ($bb8) -and ($bb9) -and ($bb10) -and ($bb11) -and ($bb12) -and ($bb13))
$dpA=(JApi 'GET' "/pedidos/$PA" $script:TokA).json
$ipA=@($dpA.itens)[0]
JAssert ("pedido Alfa (C1): total {0}, previsao hoje+5, item do item 2 com CUE 50.00" -f $expCustoA2) (($dpA.status -eq 'CONFIRMADO') -and ([string]$dpA.cotacaoId -eq $C1) -and (JEqD $dpA.valorTotal $expCustoA2) -and (JNearDate $dpA.previsao 5) -and (JEqD $ipA.quantidade $Q2) -and (JEqD $ipA.valorUnitario $expCueA2) -and ([string]$ipA.produtoId -eq $PPb) -and ($ipA.exigeInspecao -eq $EI2) -and ([string]$dpA.fornecedorNome -like '*Alfa*'))
$dp3=(JApi 'GET' "/pedidos/$PC3" $script:TokA).json
$ip3=@($dp3.itens)[0]
JAssert ("pedido Alfa (C3/rodada 2): total {0}, previsao hoje+12, liga C3/Rq2" -f $expCustoA3) (($dp3.status -eq 'CONFIRMADO') -and ([string]$dp3.cotacaoId -eq $C3) -and ([string]$dp3.requisicaoId -eq $script:Rq2) -and (JEqD $dp3.valorTotal $expCustoA3) -and (JNearDate $dp3.previsao 12) -and (JEqD $ip3.quantidade $Q3) -and ([string]$ip3.produtoId -eq $PPc) -and ($ip3.exigeInspecao -eq $EI3))
$pd404=JApi 'GET' '/pedidos/11111111-1111-1111-1111-111111111111' $script:TokA
JAssert 'pedido inexistente => 404' ($pd404.status -eq 404)
[void](JDb ("select p.numero, p.status, coalesce(f.razao_social,'-'), p.valor_total, p.total, p.previsao::date, p.cotacao_id, p.requisicao_id from sigov.compras_empresarial_pedido p left join sigov.compras_empresarial_fornecedor f on f.id=p.fornecedor_id and f.tenant_id=p.tenant_id where p.tenant_id='$($script:TenantDemo)' order by p.id;"))
$peq=JVal "select count(*) from sigov.compras_empresarial_pedido where tenant_id='$($script:TenantDemo)' and status='CONFIRMADO' and valor_total=total and valor_total>0;"
JAssert 'banco: 3 pedidos CONFIRMADO com valor_total=total>0' ($peq -eq '3') "count=$peq"
$hped=JVal "select count(*) from sigov.compras_empresarial_historico where aggregate_type='PEDIDO' and aggregate_id in ('$PB','$PA','$PC3');"
JAssert 'historico registra o agregado PEDIDO dos 3 pedidos (rastreabilidade completa)' ([int]$hped -ge 3) "linhas=$hped"
$hc1=JVal "select count(*) from sigov.compras_empresarial_historico where aggregate_type='COTACAO' and aggregate_id='$C1' and acao='SELECAO_REGISTRADA';"
JAssert 'historico COTACAO: SELECAO_REGISTRADA presente na C1' ([int]$hc1 -ge 1) "linhas=$hc1"
$henc=JVal "select string_agg(distinct acao,',' order by acao) from sigov.compras_empresarial_historico where aggregate_type='COTACAO' and aggregate_id in ('$C1','$C2','$C3');"
JLog ("acoes historicas observadas nos agregados de cotação: {0}" -f $henc)
$tokd=JApi 'GET' '/pedidos' $script:TokD
JAssert 'isolacao por contexto: admin de OUTRO contexto nao vaza pedidos do tenant demo' (((($tokd.status -eq 200) -and [long]$tokd.json.totalItems -eq 0)) -or ($tokd.status -in @(400,401,403,404))) ("http={0}" -f $tokd.status)

# ------------------------------------------------------------------- S9 -----
JHead 'S9 - RECEBIMENTO EXISTENTE reconhece o pedido (sem estoque e sem obrigacao financeira automaticas)'
$rpRaw=JApi 'GET' "/recebimentos/pedidos/$PB" $script:TokA
$rpB=$rpRaw.json
$rpi=@($rpB.itens)[0]
JAssert 'pedido reconhecido pelo recebimento: 200 com itens (pedida/pendente) e almoxarifado do contexto municipal' (($rpRaw.status -eq 200) -and ([string]$rpB.id -eq $PB) -and ($rpB.status -eq 'CONFIRMADO') -and ([long]$rpB.version -ge 1) -and ([string]$rpB.fornecedor -like '*Beta*') -and (JEqD $rpi.quantidadePedida $Q1) -and (JEqD $rpi.quantidadePendente $Q1) -and (JEqD $rpi.quantidadeCancelada 0) -and (@($rpB.almoxarifados) | Where-Object { ([string]$_.id) -eq 'a0000001-0000-4000-8000-000000000201' } | Measure-Object).Count -eq 1)
$rp4=JApi 'GET' '/recebimentos/pedidos/11111111-1111-1111-1111-111111111111' $script:TokA
JAssert 'recebimento de pedido inexistente => 404' ($rp4.status -eq 404)
$rl=(JApi 'GET' '/recebimentos' $script:TokA).json
JAssert 'central de recebimentos continua VAZIA (pedido != recebimento; nada criado automaticamente)' ([long]$rl.resultado.totalItems -eq 0)
$rlb=JApi 'GET' '/recebimentos' $script:TokB
JAssert 'perfil gestor sem recebimentos.* => 403 na central (autorizacao do banco vale)' ($rlb.status -eq 403) ("http={0}" -f $rlb.status)

# ------------------------------------------------------------------ S10 -----
JHead 'S10 - FORNECEDOR via API (operacao canonica FORNECEDOR_CRIAR): nasce RASCUNHO, replay, conflito, obrigatorios'
$bF=JObj ([ordered]@{ tipoPessoa='J'; documento='12345678000190'; razaoSocial='Delta Suprimentos Fictícios LTDA'; nomeFantasia=$null; categoria='Suprimentos e serviços'; porte='EMPRESA_DE_PEQUENO_PORTE'; condicaoPagamento='30 dias'; prazoMedio=8; observacoes='Fornecedor fictício criado pela jornada do Bloco B.' })
$fn=JApi 'POST' '/fornecedores' $script:TokA $bF 'jb-forn-k1'
$F4S=JGuidStr $fn
JAssert 'criar fornecedor: 2xx devolvendo id novo' ((($fn.status -eq 200) -or ($fn.status -eq 201)) -and ($F4S -ne '00000000-0000-0000-0000-000000000000')) ("id=$F4S")
$fnr=JApi 'POST' '/fornecedores' $script:TokA $bF 'jb-forn-k1'
JAssert 'replay do fornecedor (mesma chave+conteudo): mesmo id, sem duplicar' ((($fnr.status -eq 200) -or ($fnr.status -eq 201)) -and (JGuidStr $fnr -eq $F4S))
$bFm=JObj ([ordered]@{ tipoPessoa='J'; documento='12345678000190'; razaoSocial='Delta Suprimentos Alterada LTDA'; nomeFantasia=$null; categoria=$null; porte=$null; condicaoPagamento=$null; prazoMedio=8; observacoes=$null })
$fnm=JApi 'POST' '/fornecedores' $script:TokA $bFm 'jb-forn-k1'
JAssert 'mesma chave + conteudo diferente no fornecedor => 409' (($fnm.status -eq 409) -and $fnm.body.Contains('A chave de idempotência já foi usada com conteúdo diferente.')) ("http={0}" -f $fnm.status)
$bFn=JObj ([ordered]@{ tipoPessoa='J'; documento=''; razaoSocial='Sem Documento Fictício ME'; nomeFantasia=$null; categoria=$null; porte=$null; condicaoPagamento=$null; prazoMedio=5; observacoes=$null })
$fnb=JApi 'POST' '/fornecedores' $script:TokA $bFn 'jb-probe-forn-doc'
JAssert 'fornecedor sem documento => 400 obrigatorios' (($fnb.status -eq 400) -and $fnb.body.Contains('Documento e razão social são obrigatórios.')) ("http={0}" -f $fnb.status)
$fd=JApi 'GET' "/fornecedores/$F4S" $script:TokA
JAssert 'fornecedor criado via API nasce RASCUNHO com codigo gerado (estado canonico)' (($fd.status -eq 200) -and ($fd.json.status -eq 'RASCUNHO') -and (-not [string]::IsNullOrWhiteSpace([string]$fd.json.codigo))) ("codigo={0}" -f $fd.json.codigo)

# ------------------------------------------------------------------ S11 -----
JHead 'S11 - SNAPSHOT FINAL (banco) + RESUMO DO GATE'
[void](JDb ("select c.numero, c.rodada, c.status, coalesce(c.selecionado_em::text,'-') from sigov.compras_empresarial_cotacao c where c.tenant_id='$($script:TenantDemo)' order by c.id;"))
[void](JDb ("select cs.cotacao_id, cs.requisicao_item_id, cs.fornecedor_id, cs.quantidade, cs.custo_total_item, coalesce(cs.justificativa,'-') from sigov.compras_empresarial_cotacao_selecao cs where cs.tenant_id='$($script:TenantDemo)' order by cs.id;"))
[void](JDb ("select i.operacao, i.chave, (i.request_hash is not null) from sigov.compras_empresarial_idempotencia i where i.chave like 'jb-%' order by i.operacao, i.chave;"))
[void](JDb ("select h.aggregate_type, h.acao, count(*) from sigov.compras_empresarial_historico h where h.tenant_id='$($script:TenantDemo)' and h.aggregate_type in ('COTACAO','PEDIDO','FORNECEDOR') group by 1,2 order by 1,2;"))
$cl=(JApi 'GET' '/cotacoes' $script:TokA).json
JAssert 'lista final de cotacoes: 3 rodadas (SELECIONADA x2 + ENCERRADA x1)' (([long]$cl.totalItems -eq 3) -and (@($cl.items) | Where-Object { $_.status -eq 'SELECIONADA' }).Count -eq 2 -and (@($cl.items) | Where-Object { $_.status -eq 'ENCERRADA' } | Measure-Object).Count -eq 1)
$finalFail=$script:JFail
JLog ''
if($finalFail -gt 0){ JLog ('GATE BLOCO B RESUMO: FAIL - {0} assercao(oes) em falha' -f $finalFail) } else { JLog 'GATE BLOCO B RESUMO: PASS - todas as assercoes verdes (jornada requisicao -> cotacao -> comparativo -> selecao -> pedido -> recebimento)' }
Write-Host ('GATE B: FAILS=' + $finalFail)
exit $(if($finalFail -gt 0){1}else{0})
