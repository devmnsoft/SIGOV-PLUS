# =============================================================================
# SIGOV PLUS · Ciclo 1 (Educacao Avancada) · TIER2 — E2E HTTP real sobre p1_dev
# =============================================================================
# Prova por HTTP REAL (Web :5099 -> ApiToken -> bearer na Api :5100 -> Dapper ->
# PG16) as correcoes comprovadas do Ciclo 1 em educacao_biblioteca_acervo
# (fixture de 120 linhas no tenant 910001, marcador 'c1e26-%') e do ciclo
# escolar (Sprint 3, fixture 'c1e26-s3-%'/'C1S3-%'):
#   E1  Dashboard: totais sobre o conjunto COMPLETO (Dapper ReadFirstAsync<IDict>)
#   E2  Exportar CSV: todas as linhas (sem teto 100), BOM, escape e protecao formula
#   E3  Schema ausente: PagedResult.Diagnostic explicito em vez de lista vazia
#   E4  AlterarStatus: auditoria append-only com de/para/motivo
#   E5  Maquina de estados por recurso + recusa nomeada + guarda de versao (S2)
#   E6  Escopo institucional antes de qualquer vinculo (S2)
#   E7  Ciclo escolar: race da ultima vaga (enturmação), indice de unicidade por
#       oferta, transferencia com historico/linhagem, correcao auditada de
#       frequencia e bloqueio apos fechamento/reabertura do diario de classe (S3)
# O script LIMPA o fixture ao final (regra 9: dados ficticios, idempotente).
# Uso: powershell -File scripts\c1_tier2_e2e.ps1
# Exit: 0 = tudo verde; 1 = ha FAIL.
# =============================================================================
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $root 'artifacts/c1_tier2_e2e'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$WebBase = 'http://localhost:5099'
$ApiBase = 'http://localhost:5100'
$Password = 'Homologacao!2026'

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Phase,[string]$Name,[bool]$Passed,[string]$Evidence){
  $checks.Add([pscustomobject]@{ phase=$Phase; name=$Name; passed=$Passed; evidence=$Evidence; checkedAt=(Get-Date).ToUniversalTime().ToString('o') })
  Write-Host ("[" + $(if($Passed){'PASS'}else{'FAIL'}) + "] $Name :: $Evidence")
}
function New-SigovLogin {
  param([string]$User,[string]$Pass,[string]$Base)
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    $jar = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_" + [guid]::NewGuid().ToString('N') + ".jar")
    $html = (& curl.exe -s -c $jar "$Base/Auth/Login") -join "`n"
    $tok = ''
    $i = $html.IndexOf('__RequestVerificationToken')
    if ($i -ge 0) { $tok = ([regex]::Match($html.Substring($i), 'value="([^"]+)"')).Groups[1].Value }
    $args = @('-s','-b',$jar,'-c',$jar,'-o','NUL','-w','%{http_code}|%{redirect_url}','-X','POST',"$Base/Auth/Login","--data-urlencode","Login=$User","--data-urlencode","Senha=$Pass","--data-urlencode","LembrarLogin=false","--data-urlencode","__RequestVerificationToken=$tok")
    $post = (& curl.exe @args).Trim()
    if ($post -match '^302\|.*MinhaCentral') { return [pscustomobject]@{ jar="$jar"; post="$post"; ok=$true } }
    Write-Host ("login tentativa $attempt -> $post (backoff 8s p/ rate-limit)")
    Start-Sleep -Seconds 8
  }
  return [pscustomobject]@{ jar="$jar"; post="$post"; ok=$false }
}
function Get-ApiToken {
  param([string]$Jar,[string]$Base)
  $of = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_" + [guid]::NewGuid().ToString('N') + ".out")
  & curl.exe -s -b $Jar -o $of -w "%{http_code}" "$Base/Auth/ApiToken" | Out-Null
  $j = (Get-Content $of -Raw) | ConvertFrom-Json
  return [pscustomobject]@{ token=[string]$j.token }
}
function Invoke-Api {
  param([string]$Bearer,[string]$Method='GET',[string]$Path='',[string]$Base,[string]$Body='')
  $u = if ($Path -match '^http') { $Path } else { "$Base$Path" }
  $of = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_" + [guid]::NewGuid().ToString('N') + ".out")
  $w = @('-s','-X',$Method,'-H','Content-Type: application/json','-o',$of,'-w','%{http_code}')
  if ($Bearer) { $w += @("-H","Authorization: Bearer $Bearer") }
  if ($Body) { $bf = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_body_" + [guid]::NewGuid().ToString('N') + ".json"); [IO.File]::WriteAllText($bf, $Body, (New-Object Text.UTF8Encoding($false))); $w += @('--data',"@$bf") }
  $w += $u
  $code = (& curl.exe @w).Trim()
  # ReadAllText (UTF-8): Get-Content sem encoding leria ANSI e quebraria acesos nas falhas nomeadas
  $body = if (Test-Path $of) { [IO.File]::ReadAllText($of) } else { '' }
  return [pscustomobject]@{ code=[int]$code; body="$body"; outFile="$of" }
}
function Get-Psql {
  param([string]$Sql)
  $hf = Join-Path ([IO.Path]::GetTempPath()) 'c1t2_psql.sql'
  Set-Content -LiteralPath $hf -Value $Sql -Encoding utf8
  docker cp "$hf" "sigov-postgres:/tmp/c1t2_psql.sql" | Out-Null
  # EAP=Stop + 2>&1 transformaria o stderr do psql em excecao; o texto de erro
  # e DADO do resultado (checks esperam falha nomeada), entao captura em Continue.
  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $r = docker exec sigov-postgres psql -U postgres -d p1_dev -Atq -v ON_ERROR_STOP=1 -f /tmp/c1t2_psql.sql 2>&1 | Out-String
    return @{ code = $LASTEXITCODE; text = $r.Trim() }
  } finally { $ErrorActionPreference = $prevEap }
}

Write-Host "==============================================================="
Write-Host "Ciclo 1 Educao Avancada · TIER2 E2E HTTP (p1_dev)"
Write-Host ("ts=" + (Get-Date).ToUniversalTime().ToString('o'))
Write-Host "==============================================================="

# --- espera de readiness dos apps -------------------------------------------
$webUp = $false
for ($t = 0; $t -lt 30; $t++) {
  $c = (& curl.exe -s -o NUL -w "%{http_code}" "$WebBase/Auth/Login")
  if ($c -eq '200') { $webUp = $true; break }
  Start-Sleep -Seconds 3
}
Add-Check 'E0' 'env.web-up' $webUp ("GET $WebBase/Auth/Login -> " + $c)
$apiUp = '000'
for ($t = 0; $t -lt 30; $t++) {
  $apiUp = (& curl.exe -s -o NUL -w "%{http_code}" "$ApiBase/api/saas/tenant/modulos")
  if ($apiUp -in '401','200') { break }
  Start-Sleep -Seconds 3
}
Add-Check 'E0' 'env.api-up' ($apiUp -in '401','200') ("GET $ApiBase/api/saas/tenant/modulos sem bearer -> $apiUp (401 esperado)")

# --- login + bearer ----------------------------------------------------------
$lg = New-SigovLogin -User 'hom.a.admin' -Pass $Password -Base $WebBase
Add-Check 'E0' 'auth.login-web' $lg.ok ("hom.a.admin POST /Auth/Login -> $($lg.post)")
$tk = Get-ApiToken -Jar $lg.jar -Base $WebBase
Add-Check 'E0' 'auth.api-token-mint' ($tk.token.Length -eq 64) ("GET /Auth/ApiToken len=" + $tk.token.Length)

# --- fixture idempotente (dados ficticios; removido em CX) ---------------------
$fixSrc = Join-Path $root 'database/postgres/seeds/homologacao/20261010_c1_educacao_avancada_tier2_fixture.sql'
$hf = Join-Path ([IO.Path]::GetTempPath()) 'c1t2_fixture.sql'
Copy-Item -LiteralPath $fixSrc -Destination $hf -Force
docker cp "$hf" "sigov-postgres:/tmp/c1t2_fixture.sql" | Out-Null
$null = docker exec sigov-postgres psql -U postgres -d p1_dev -v ON_ERROR_STOP=1 -q -f /tmp/c1t2_fixture.sql 2>&1
Add-Check 'E0' 'fixture.aplicado' ($LASTEXITCODE -eq 0) ("fixture c1 tier2 (120 linhas ficticias, marcador c1e26-%, tenant 910001) exit=$LASTEXITCODE")

# --- E1: dashboard sobre o conjunto completo ---------------------------------
$d = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/biblioteca/dashboard' -Base $ApiBase
$j = $null
try { $j = $d.body | ConvertFrom-Json } catch {}
$okCounts = ($d.code -eq 200 -and $j.success -eq $true -and
            $j.data.total -eq 120 -and $j.data.ativos -eq 60 -and
            $j.data.pendentes -eq 20 -and $j.data.alertas -eq 20)
$recentes = 0
try { $recentes = @($j.data.recentes).Count } catch {}
Add-Check 'E1' 'dashboard.conjunto-completo' ($okCounts -and $recentes -eq 6) ("GET /api/educacao/biblioteca/dashboard -> $($d.code) total=$($j.data.total) ativos=$($j.data.ativos) pendentes=$($j.data.pendentes) alertas=$($j.data.alertas) recentes=$recentes (fixo antigo veria 0/0/0 sobre os 6 mais recentes)")

# --- E2: export CSV integral + BOM + escape + protecao de formula -------------
$csvFile = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_export_" + [guid]::NewGuid().ToString('N') + ".csv")
& curl.exe -s -X GET -H "Authorization: Bearer $($tk.token)" -o $csvFile -w '%{http_code}' "$ApiBase/api/educacao/biblioteca/relatorios/exportar-csv" | Out-Null
$bom = (Get-Content $csvFile -Encoding Byte -TotalCount 3) -join ','
# Leitura em bytes/texto bruto: preserva o BOM detectado e quebras de linha DENTRO de campos aspas
$text = [IO.File]::ReadAllText($csvFile)
$linhas = @(($text.TrimEnd("`r`n") -split "`n"))
# A linha esperada no CSV (EducacaoCsv.Campo): "'=CMD(); ""a""; b"  e  "linha1<LF>linha2"  (celula aspas, aspas internas dobradas)
$csvQ = [char]34; $csvApost = [char]39
$esperada1 = $csvQ + $csvApost + '=CMD(); ' + $csvQ + $csvQ + 'a' + $csvQ + $csvQ + '; b' + $csvQ
$esperada2 = "linha1`nlinha2"
$hasFormula = $text.Contains($esperada1)
$hasNewline = $text.Contains($esperada2)
Add-Check 'E2' 'csv.export-integral-bom-escape' (($bom -eq '239,187,191') -and ($linhas.Count -eq 122) -and $hasFormula -and $hasNewline) ("GET /api/educacao/biblioteca/relatorios/exportar-csv -> linhas_csv=$($linhas.Count) (1 cabecalho + 120 dados + 1 linha extra por quebra de linha embutida em campo aspas; fixo antigo truncava em 101) bom_utf8=$($bom -eq '239,187,191') formula_escapada=$hasFormula newline_preservado=$hasNewline")

# --- E3: schema ausente => diagnostic explicito (probe por rename transitório) --
# A tabela educacao_merenda_requisicao existe com 0 linhas e sem FKs: renomeamos
# para simular schema ausente, chamamos a rota e restauramos (janela de ms em p1_dev).
$tbl = 'educacao_merenda_requisicao'
$r0 = Get-Psql "select count(*) from sigov.$tbl;"
Add-Check 'E3' 'probe.tabela-vazia-prerename' ($r0.code -eq 0 -and $r0.text -eq '0') ("sigov.$tbl rows antes do rename = $($r0.text) (0 => rename sem perda de dados)")
(Get-Psql "alter table sigov.$tbl rename to c1t2_probe_omitida;") | Out-Null
$r1 = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/merenda/requisicoes?pagina=1&tamanho=20" -Base $ApiBase
$j1 = $null; try { $j1 = $r1.body | ConvertFrom-Json } catch {}
$diag = [string]$j1.data.diagnostic
$ok3 = ($r1.code -eq 200 -and $j1.success -eq $true -and
       (@($j1.data.items).Count -eq 0) -and $diag.Length -gt 10 -and $diag -like '*schema ausente*')
Add-Check 'E3' 'listar.diagnostic-schema-ausente' $ok3 ("GET /api/educacao/merenda/requisicoes com tabela renomeada -> $($r1.code) items=" + @($j1.data.items).Count + " diagnostic=" + $diag.Substring(0,[Math]::Min(90,$diag.Length)))
Get-Psql "alter table sigov.c1t2_probe_omitida rename to $tbl;" | Out-Null
$r3 = Get-Psql "select count(*) from sigov.$tbl;"
Add-Check 'E3' 'probe.tabela-restaurada' ($r3.code -eq 0 -and $r3.text -eq '0') ("rename de volta ok; sigov.$tbl rows depois = $($r3.text)")

# --- E4: alterar status => auditoria de/para/motivo append-only ----------------
$seedId = Get-Psql "select id from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%' order by id limit 1;"
$s1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/operacoes/educacao_biblioteca_acervo/$($seedId.text)/status?status=ATIVA&justificativa=c1-t2-ativacao" -Base $ApiBase
$s2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/operacoes/educacao_biblioteca_acervo/$($seedId.text)/status?status=CANCELADA&justificativa=c1-t2-cancelamento-e2e" -Base $ApiBase
$row = Get-Psql @"
select status, justificativa, updated_by, auditoria::text from sigov.educacao_biblioteca_acervo
where tenant_id=910001 and id=$($seedId.text);
"@
# a linha 1 do fixture nasce ATIVA: cadeia esperada ATIVA->ATIVA -> ATIVA->CANCELADA (ambas visiveis, sem sobrescrita)
$rowOk = ($row.text -like 'CANCELADA*' -and $row.text -like '*c1-t2-cancelamento-e2e*')
$aud = "$($row.text)"
$statusEvents = [regex]::Matches($aud, '"acao"\s*:\s*"STATUS"').Count
$hasDeAtiva = $aud -match '"de"\s*:\s*"ATIVA"'
$hasParaAtiva = $aud -match '"para"\s*:\s*"ATIVA"'
$hasParaCancelada = $aud -match '"para"\s*:\s*"CANCELADA"'
$hasMotivo1 = $aud -match '"motivo"\s*:\s*"c1-t2-ativacao"'
$hasMotivo2 = $aud -match '"motivo"\s*:\s*"c1-t2-cancelamento-e2e"'
$ok4 = ($s1.code -eq 200) -and ($s2.code -eq 200) -and $rowOk -and ($statusEvents -eq 2) -and $hasDeAtiva -and $hasParaAtiva -and $hasParaCancelada -and $hasMotivo1 -and $hasMotivo2
Add-Check 'E4' 'status.auditoria-de-para-motivo-append' $ok4 ("POST status ATIVA -> $($s1.code); POST status CANCELADA -> $($s2.code); auditoria com 2 entradas acao=STATUS contendo de/para/motivo e preservando a anterior (append-only); amostra=" + $aud.Substring(0,[Math]::Min(220,$aud.Length)))

# --- E5 (Sprint 2): maquina de estados por recurso + recusa nomeada + guarda de versao --
# Cria uma requisicao ficticia (marcador c1e26-s2-%) e prova ao vivo a matriz de estados:
#   5.1 RASCUNHO->ENCERRADA            = 409 TRANSICAO_INVALIDA (pendente nao encerra direto)
#   5.2 RASCUNHO->ATIVA                = 200
#   5.3 ATIVA->ENCERRADA @versaoAtual  = 200 (guarda de versao otima via updated_at)
#   5.4 ENCERRADA->ENCERRADA @vAntiga  = 409 CONFLITO_VERSAO (outra gravacao venceu)
#   5.5 ENCERRADA->ATIVA               = 409 TRANSICAO_INVALIDA (terminal e final)
#   5.6 id inexistente                 = 404 REGISTRO_NAO_ENCONTRADO
$bodyCriar = '{"escolaId":null,"alunoId":null,"referenciaId":null,"codigo":"c1e26-s2-req","nome":"Requisicao Sprint2 E2E","tipo":"REQUISICAO","status":"RASCUNHO","descricao":"Prova maquina de estados + guarda de versao","justificativa":null,"turno":null,"quantidade":null,"valor":null,"anoLetivo":null,"dados":{}}'
$cr = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body $bodyCriar
$jcr = $null; try { $jcr = $cr.body | ConvertFrom-Json } catch {}
$rid = [long]$jcr.data
$ok50 = ($cr.code -eq 200 -and $jcr.success -eq $true -and $rid -gt 0)
Add-Check 'E5' 'maquina.registro-criado-rascunho' $ok50 ("POST /api/educacao/merenda/requisicoes (codigo c1e26-s2-req, status RASCUNHO) -> $($cr.code) id=$rid")
function Post-StatusS2([long]$Id,[string]$Query){
  return (Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$Id/status?$Query" -Base $ApiBase)
}
$t51 = Post-StatusS2 $rid 'status=ENCERRADA&justificativa=c1-s2-prova-invalida'
Add-Check 'E5' 'maquina.pendente-nao-encerra-direto' (($t51.code -eq 409) -and ($t51.body -like '*TRANSICAO_INVALIDA*')) ("POST status=ENCERRADA sobre RASCUNHO -> $($t51.code) (409 TRANSICAO_INVALIDA; pendente so ativa, permanece ou cancela)")
$t52 = Post-StatusS2 $rid 'status=ATIVA&justificativa=c1-s2-ativar'
Add-Check 'E5' 'maquina.rascunho-ativa-valida' (($t52.code -eq 200) -and $t52.body -like '*"success":true*') ("POST status=ATIVA sobre RASCUNHO -> $($t52.code) (transicao canonica permitida)")
$lst = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/merenda/requisicoes?pagina=1&tamanho=100' -Base $ApiBase
$jl = $null; try { $jl = $lst.body | ConvertFrom-Json } catch {}
$it = @($jl.data.items) | Where-Object { [long]$_.id -eq $rid } | Select-Object -First 1
$v1 = [string]$it.updatedAt
Add-Check 'E5' 'maquina.updatedat-exposto-no-contrato' (([string]$it.id).Length -gt 0 -and ($v1.Length -ge 19)) ("GET requisicoes -> item id=$rid expoe updatedAt=$v1 (entrada da guarda de versao no cliente)")
$qEncerrar = 'status=ENCERRADA&justificativa=c1-s2-encerrar&updatedAt=' + [uri]::EscapeDataString($v1)
$t53 = Post-StatusS2 $rid $qEncerrar
$row5 = Get-Psql "select status||'|'||coalesce(justificativa,'') from sigov.educacao_merenda_requisicao where tenant_id=910001 and id=$rid;"
Add-Check 'E5' 'maquina.guarda-versao-sucesso' (($t53.code -eq 200) -and ($row5.text -eq 'ENCERRADA|c1-s2-encerrar')) ("POST status=ENCERRADA com updatedAt atual -> $($t53.code); banco: $($row5.text)")
$t54 = Post-StatusS2 $rid $qEncerrar
Add-Check 'E5' 'maquina.guarda-versao-conflito' (($t54.code -eq 409) -and ($t54.body -like '*CONFLITO_VERSAO*')) ("repeticao do POST com updatedAt OBSELECIDA -> $($t54.code) (409 CONFLITO_VERSAO: a propria gravacao 5.3 atualizou updated_at)")
$t55 = Post-StatusS2 $rid 'status=ATIVA&justificativa=c1-s2-reabrir'
Add-Check 'E5' 'maquina.terminal-e-final' (($t55.code -eq 409) -and ($t55.body -like '*TRANSICAO_INVALIDA*')) ("POST status=ATIVA sobre ENCERRADA -> $($t55.code) (terminal nao reabre)")
$t56 = Post-StatusS2 9999999999 'status=ATIVA&justificativa=c1-s2-probe'
Add-Check 'E5' 'maquina.registro-inexistente-404' (($t56.code -eq 404) -and ($t56.body -like '*REGISTRO_NAO_ENCONTRADO*')) ("POST status sobre id 9999999999 -> $($t56.code) (404 nomeado, nao 500)")

# --- E6 (Sprint 2 #6): escopo institucional antes de qualquer vinculo -----------------------
# Escola/ano letivo do comando devem pertencer ao mesmo tenant/entidade do contexto autenticado.
# (a) escola inexistente -> 404 ESCOLA_INEXISTENTE; (b) ano letivo ausente -> 422 ANO_LETIVO_INEXISTENTE;
# (c) escola de OUTRA entidade -> 409 ESCOLA_FORA_DO_ESCOPO; (d) escola da MESMA entidade -> 200.
# Fixture dinamico: 2 entidades ficticias (CNPJ 999.999.999/0001-91 e /0002-09) + 1 escola em cada.
$entHasTenant = Get-Psql "select case when exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='entidade' and column_name='tenant_id') then 't' else 'n' end;"
if ($entHasTenant.text -eq 't') { $colTid = 'tenant_id,'; $valTid = '910001,' } else { $colTid = ''; $valTid = '' }
$fixEnt = Get-Psql @"
insert into sigov.entidade($colTid nome,cnpj) select $valTid 'Fixtures C1 Entidade A','99999999000191' where not exists(select 1 from sigov.entidade where cnpj='99999999000191');
insert into sigov.entidade($colTid nome,cnpj) select $valTid 'Fixtures C1 Entidade B','99999999000209' where not exists(select 1 from sigov.entidade where cnpj='99999999000209');
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,e.id,'c1e26-esc-a','Fixtures C1 Escola A',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
from sigov.entidade e where e.cnpj='99999999000191' and not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-esc-a');
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,e.id,'c1e26-esc-b','Fixtures C1 Escola B',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
from sigov.entidade e where e.cnpj='99999999000209' and not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-esc-b');
select 'ok';
"@
Add-Check 'E6' 'fixture.escopo-aplicado' ($fixEnt.code -eq 0) ("fixture escopo (2 entidades ficticias + escolas c1e26-esc-a/b, tenant 910001) exit=$($fixEnt.code): $($fixEnt.text)")
$e61 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body '{"escolaId":9999999999,"codigo":"c1e26-s6x1","nome":"Requisicao Escola Inexistente","tipo":"REQUISICAO","status":"RASCUNHO"}'
Add-Check 'E6' 'escopo.escola-inexistente-404' (($e61.code -eq 404) -and ($e61.body -like '*ESCOLA_INEXISTENTE*')) ("POST requisicoes com escolaId=9999999999 -> $($e61.code) (404 ESCOLA_INEXISTENTE nomeado, nao 500)")
$anoAusente = Get-Psql "select coalesce(min(ano)-1, 1900) from sigov.ano_letivo where tenant_id=910001;"
$e62 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body ('{"codigo":"c1e26-s6x2","nome":"Requisicao Ano Ausente","tipo":"REQUISICAO","status":"RASCUNHO","anoLetivo":' + $anoAusente.text + '}')
Add-Check 'E6' 'escopo.ano-letivo-ausente-422' (($e62.code -eq 422) -and ($e62.body -like '*ANO_LETIVO_INEXISTENTE*')) ("POST requisicoes com anoLetivo=$($anoAusente.text) (garantido ausente p/ tenant 910001) -> $($e62.code) (422 ANO_LETIVO_INEXISTENTE)")
# Entidade efetiva do contexto autenticado: lida da linha gravada pelo proprio comando (entidade_id do insert)
$cr6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body '{"codigo":"c1e26-s6-ctx","nome":"Requisicao Probe Contexto","tipo":"REQUISICAO","status":"RASCUNHO"}'
$ctxEnt = Get-Psql "select coalesce(entidade_id,-1) from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo='c1e26-s6-ctx' order by id desc limit 1;"
if ($ctxEnt.code -eq 0 -and [long]$ctxEnt.text -gt 0) {
  # Escola da MESMA entidade (criada sob a entidade efetiva do contexto) -> aceita
  $fixC = Get-Psql @"
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,$($ctxEnt.text),'c1e26-esc-c','Fixtures C1 Escola C (mesma entidade)',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-esc-c');
select 'ok';
"@
  $escMesma = Get-Psql "select id from sigov.escola where tenant_id=910001 and codigo='c1e26-esc-c';"
  $escOutra = Get-Psql "select coalesce(id,-1) from (select id,entidade_id from sigov.escola where tenant_id=910001 and codigo in ('c1e26-esc-a','c1e26-esc-b') order by id) x where x.entidade_id <> $($ctxEnt.text) limit 1;"
  $e64 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body ('{"escolaId":' + $escMesma.text + ',"codigo":"c1e26-s6-mesma","nome":"Requisicao Mesma Entidade","tipo":"REQUISICAO","status":"RASCUNHO"}')
  Add-Check 'E6' 'escopo.mesma-entidade-aceita' (($e64.code -eq 200) -and ($e64.body -like '*"success":true*')) ("POST requisicoes com escola $($escMesma.text) da mesma entidade ($($ctxEnt.text)) -> $($e64.code) (aceita; sem falso positivo)")
  $e63 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body ('{"escolaId":' + $escOutra.text + ',"codigo":"c1e26-s6-outra","nome":"Requisicao Outra Entidade","tipo":"REQUISICAO","status":"RASCUNHO"}')
  Add-Check 'E6' 'escopo.outra-entidade-409' (($e63.code -eq 409) -and ($e63.body -like '*ESCOLA_FORA_DO_ESCOPO*')) ("POST requisicoes com escola $($escOutra.text) de outra entidade -> $($e63.code) (409 ESCOLA_FORA_DO_ESCOPO; vinculo nao cruza entidades)")
} else {
  Add-Check 'E6' 'escopo.outra-entidade-409' $false ("contexto do usuario nao tem entidade resolvida (entidade_id=$($ctxEnt.text)); prova de cruzamento de entidade NAO EXECUTADA (regra 13: sem sucesso simulado)")
}

# --- E7 (Sprint 3): ciclo escolar — unicidade por oferta, transferencia com historia,
# correcao auditada de frequencia e bloqueio apos fechamento do diario de classe ----
# Fixture real na entidade efetiva do contexto (probe resolvida pelo proprio comando; nada
# hardcoded, regra 24): escola c1e26-s3-esc + ano letivo 2026 + Turma A (capacidade 2, 1
# vaga ocupada) + Turma B (capacidade 1) + 3 alunos + 1 professor (MATEMATICA nas duas
# turmas) + matricula X enturmada em A + matriculas A e B ainda SEM turma (corrida p/ vaga).
#   7.1 enturmar concorrente (2 HTTP paralelos pela ultima vaga) -> exatamente 1 sucesso
#   7.2 prova direta do indice ux_matricula_tenant_aluno_turma_vigencia (duplicada ATIVA
#       na mesma turma com ativo=false para o indice por ano nao dispare — isola o novo indice)
#   7.3 transferencia -> origem TRANSFERIDA + nova ATIVA com linhagem TRANSFERENCIA:<origem>
#       + saldo de vagas + 2 eventos de auditoria
#   7.4 transferencia para a MESMA turma -> 400 nomeada
#   7.5 criacao de frequencia (permissao canonica frequencia/criar)
#   7.6 correcao auditada (JUSTIFICADA; auditoria FrequenciaCorrigida com de/para)
#   7.7 fechar o diário de classe da turma B -> correção bloqueada FECHADO -> reabrir com
#       justificativa -> correção volta a operar (regra 13: falha explícita, sem sucesso simulado)
$hoje = (Get-Date).ToString('yyyy-MM-dd')
$cr7 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body '{"codigo":"c1e26-s3-ctx","nome":"Requisicao Probe S3 Contexto","tipo":"REQUISICAO","status":"RASCUNHO"}'
$e7ent = Get-Psql "select coalesce(entidade_id,-1) from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo='c1e26-s3-ctx' order by id desc limit 1;"
Add-Check 'E7' 'fixture.contexto-resolvido' ([long]$e7ent.text -gt 0) ("probe c1e26-s3-ctx -> entidade efetiva=$($e7ent.text) (contexto vem do comando autenticado; multi-esfera, sem municipio hardcoded)")

$fix7 = Get-Psql @"
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao,created_by)
select 910001,$($e7ent.text),'c1e26-s3-esc','Fixtures C1 S3 Escola','OURO','ATIVA',910301
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s3-esc');
insert into sigov.ano_letivo(tenant_id,entidade_id,escola_id,ano,data_inicio,data_fim,status,created_by)
select 910001,$($e7ent.text),e.id,2026,'2026-02-02','2027-01-10','ABERTO',910301
from sigov.escola e where e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
and not exists(select 1 from sigov.ano_letivo a where a.tenant_id=910001 and a.escola_id=e.id and a.ano=2026);
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixtures C1 S3 Aluno X','999999000101','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000101');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixtures C1 S3 Aluno A','999999000102','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000102');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixtures C1 S3 Aluno B','999999000103','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000103');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixtures C1 S3 Professor','999999000201','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000201');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C1S3-ALX','ATIVO',910301 from sigov.pessoa p where p.documento='999999000101'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S3-ALX');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C1S3-ALA','ATIVO',910301 from sigov.pessoa p where p.documento='999999000102'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S3-ALA');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C1S3-ALB','ATIVO',910301 from sigov.pessoa p where p.documento='999999000103'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S3-ALB');
insert into sigov.professor(tenant_id,entidade_id,pessoa_id,codigo_professor,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C1S3-PROF','ATIVO',910301 from sigov.pessoa p where p.documento='999999000201'
and not exists(select 1 from sigov.professor where tenant_id=910001 and codigo_professor='C1S3-PROF');
insert into sigov.curso(tenant_id,entidade_id,codigo,nome,etapa_ensino,modalidade,created_by)
select 910001,$($e7ent.text),'C1S3-CURSO','Fixture C1 S3 Curso','ENSINO_FUNDAMENTAL','REGULAR',910301
where not exists(select 1 from sigov.curso where tenant_id=910001 and codigo='C1S3-CURSO');
insert into sigov.serie_ano(tenant_id,entidade_id,curso_id,codigo,nome,ordem,created_by)
select 910001,$($e7ent.text),c.id,'C1S3-SERIE','Fixture C1 S3 Serie',1,910301
from sigov.curso c where c.tenant_id=910001 and c.codigo='C1S3-CURSO'
and not exists(select 1 from sigov.serie_ano where tenant_id=910001 and codigo='C1S3-SERIE');
insert into sigov.turma(tenant_id,entidade_id,escola_id,ano_letivo_id,curso_id,serie_ano_id,codigo,nome,turno,capacidade,vagas_ocupadas,status,created_by)
select 910001,$($e7ent.text),e.id,l.id,c.id,s.id,'c1e26-s3-turma-a','Fixture C1 S3 Turma A','MATUTINO',2,0,'ABERTA',910301
from sigov.escola e join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
join sigov.curso c on c.tenant_id=910001 and c.codigo='C1S3-CURSO'
join sigov.serie_ano s on s.tenant_id=910001 and s.codigo='C1S3-SERIE'
where e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
and not exists(select 1 from sigov.turma where tenant_id=910001 and codigo='c1e26-s3-turma-a');
insert into sigov.turma(tenant_id,entidade_id,escola_id,ano_letivo_id,curso_id,serie_ano_id,codigo,nome,turno,capacidade,vagas_ocupadas,status,created_by)
select 910001,$($e7ent.text),e.id,l.id,c.id,s.id,'c1e26-s3-turma-b','Fixture C1 S3 Turma B','MATUTINO',1,0,'ABERTA',910301
from sigov.escola e join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
join sigov.curso c on c.tenant_id=910001 and c.codigo='C1S3-CURSO'
join sigov.serie_ano s on s.tenant_id=910001 and s.codigo='C1S3-SERIE'
where e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
and not exists(select 1 from sigov.turma where tenant_id=910001 and codigo='c1e26-s3-turma-b');
insert into sigov.professor_turma(tenant_id,entidade_id,professor_id,turma_id,componente_curricular,carga_horaria_semanal,created_by)
select 910001,$($e7ent.text),pr.id,t.id,'MATEMATICA',2,910301
from sigov.professor pr join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-s3-turma-a'
where pr.tenant_id=910001 and pr.codigo_professor='C1S3-PROF'
and not exists(select 1 from sigov.professor_turma x where x.professor_id=pr.id and x.turma_id=t.id and x.componente_curricular='MATEMATICA');
insert into sigov.professor_turma(tenant_id,entidade_id,professor_id,turma_id,componente_curricular,carga_horaria_semanal,created_by)
select 910001,$($e7ent.text),pr.id,t.id,'MATEMATICA',2,910301
from sigov.professor pr join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-s3-turma-b'
where pr.tenant_id=910001 and pr.codigo_professor='C1S3-PROF'
and not exists(select 1 from sigov.professor_turma x where x.professor_id=pr.id and x.turma_id=t.id and x.componente_curricular='MATEMATICA');
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,t.id,'C1S3-MAT-X',current_date-1,'ATIVA',true,910301
from sigov.aluno a
join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-s3-turma-a'
where a.tenant_id=910001 and a.codigo_aluno='C1S3-ALX'
and not exists(select 1 from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-X');
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,null,'C1S3-MAT-A',current_date-1,'ATIVA',true,910301
from sigov.aluno a
join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
where a.tenant_id=910001 and a.codigo_aluno='C1S3-ALA'
and not exists(select 1 from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-A');
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,null,'C1S3-MAT-B',current_date-1,'ATIVA',true,910301
from sigov.aluno a
join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
where a.tenant_id=910001 and a.codigo_aluno='C1S3-ALB'
and not exists(select 1 from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-B');
update sigov.turma set vagas_ocupadas=1 where tenant_id=910001 and codigo='c1e26-s3-turma-a';
select 'ok';
"@
Add-Check 'E7' 'fixture.aplicado' ($fix7.code -eq 0) ("fixture S3 (escola+ano+2 turmas+3 alunos+professor MATEMATICA+matricula X enturmada+2 matriculas sem turma; marcadores c1e26-s3-%/C1S3-%, entidade $($e7ent.text)) exit=$($fix7.code)")

$tA = Get-Psql "select id from sigov.turma where tenant_id=910001 and codigo='c1e26-s3-turma-a';"
$tB = Get-Psql "select id from sigov.turma where tenant_id=910001 and codigo='c1e26-s3-turma-b';"
$mX = Get-Psql "select id from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-X';"
$mA = Get-Psql "select id from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-A';"
$mB = Get-Psql "select id from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-B';"
$prof = Get-Psql "select id from sigov.professor where tenant_id=910001 and codigo_professor='C1S3-PROF';"
$esc = Get-Psql "select id from sigov.escola where tenant_id=910001 and codigo='c1e26-s3-esc';"
$ano = Get-Psql "select id from sigov.ano_letivo where tenant_id=910001 and escola_id=$($esc.text) and ano=2026;"

# 7.1 corrida pela ultima vaga (capacidade A = 2, ocupada = 1 => exatamente 1 pode entrar)
# HttpClient.SendAsync em vez de curl via Start-Process: PS 5.1 nao faz quoting de argumentos
# com espaco no ArgumentList (o bearer 'Authorization: Bearer xxx' se partia e o -o nao era criado).
function New-Race([string]$MatriculaId,[string]$TurmaId,[string]$DataEntrada){
  Add-Type -AssemblyName System.Net.Http
  $bf = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_raceb_" + [guid]::NewGuid().ToString('N') + ".json")
  [IO.File]::WriteAllText($bf,'{"turmaId":' + $TurmaId + ',"dataEntrada":"' + $DataEntrada + '"}',(New-Object Text.UTF8Encoding($false)))
  $client = New-Object System.Net.Http.HttpClient
  $req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, ($ApiBase + '/api/educacao/matriculas/' + $MatriculaId + '/enturmar'))
  $req.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$tk.token)
  $req.Content = New-Object System.Net.Http.StringContent((Get-Content -LiteralPath $bf -Raw),[Text.Encoding]::UTF8,'application/json')
  return @{ client=$client; task=$client.SendAsync($req) }
}
$ra = New-Race $mA.text $tA.text $hoje
$rb = New-Race $mB.text $tA.text $hoje
$respA = $ra.task.GetAwaiter().GetResult(); $respB = $rb.task.GetAwaiter().GetResult()
$codeA = [string][int]$respA.StatusCode; $codeB = [string][int]$respB.StatusCode
Add-Check 'E7' 'enturmamento.race-ultima-vaga-exatamente-uma' (([bool]($codeA -eq '200') -xor [bool]($codeB -eq '200'))) ("POST enturmar mA($($mA.text))/mB($($mB.text)) -> turma $($tA.text) em paralelo (capacidade 2, 1 vaga livre) -> codes=$codeA/$codeB (exatamente 1 sucesso; update-first com for update serializa, sem oversell)")
$win = Get-Psql "select id from sigov.matricula where tenant_id=910001 and numero_matricula in ('C1S3-MAT-A','C1S3-MAT-B') and status='ATIVA' and turma_id=$($tA.text);"
$lose = Get-Psql "select count(*) from sigov.matricula where tenant_id=910001 and numero_matricula in ('C1S3-MAT-A','C1S3-MAT-B') and status='ATIVA' and turma_id is null;"
Add-Check 'E7' 'enturmamento.loser-permanece-sem-turma' (($win.code -eq 0) -and ($lose.text -eq '1')) ("vencedor id=$($win.text) enturmado; perdedor permanece sem turma (contagem turma_id IS NULL = $($lose.text))")
$alaW = Get-Psql "select aluno_id from sigov.matricula where id=$($win.text);"

# 7.2 prova direta do novo indice parcial (registro duplicado ATIVA na mesma oferta)
$dup = Get-Psql "insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by) select 910001,$($e7ent.text),s.aluno_id,s.escola_id,s.ano_letivo_id,$($tA.text),'C1S3-DUP-PROBE',current_date,'ATIVA',false,910301 from sigov.matricula s where s.id=$($win.text);"
Add-Check 'E7' 'unicidade.indice-oferta-rejeita-duplicada' (($dup.code -ne 0) -and ($dup.text -like '*ux_matricula_tenant_aluno_turma_vigencia*')) ("insert SQL de 2ª matricula ATIVA do mesmo aluno na mesma turma (ativo=false isola o novo indice do indice por ano) -> exit=$($dup.code): $($dup.text.Substring(0,[Math]::Min(130,$dup.text.Length)))")

# 7.3 transferencia com historico/linhagem
$tr = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/matriculas/$($win.text)/transferir" -Base $ApiBase -Body ('{"novaTurmaId":' + $tB.text + ',"motivo":"c1-s3-transferencia-e2e"}')
$jtr = $null; try { $jtr = $tr.body | ConvertFrom-Json } catch {}
$novaId = [string]$jtr.data
$state = Get-Psql @"
select (select coalesce(status,'?')||'|'||coalesce(observacao,'') from sigov.matricula where id=$($win.text))
   || '@@@' || (select coalesce(status,'?')||'|'||coalesce(origem,'')||'|'||coalesce(turma_id::text,'?') from sigov.matricula where id=$novaId)
   || '@@@' || (select coalesce(vagas_ocupadas::text,'?') from sigov.turma where tenant_id=910001 and codigo='c1e26-s3-turma-a')
   || '-' || (select coalesce(vagas_ocupadas::text,'?') from sigov.turma where tenant_id=910001 and codigo='c1e26-s3-turma-b')
   || '@@@' || (select count(*) from sigov.educacao_evento where tenant_id=910001 and tipo_evento in ('MatriculaTransferida','MatriculaCriadaPorTransferencia') and agregado_id in ($($win.text),$novaId));
"@
$ok73 = ($tr.code -eq 200) -and ($state.text -eq "TRANSFERIDA|c1-s3-transferencia-e2e@@@ATIVA|TRANSFERENCIA:$($win.text)|$($tB.text)@@@1-1@@@2")
Add-Check 'E7' 'transferencia.historico-linhagem-vagas-auditoria' $ok73 ("POST transferir $($win.text)->turma $($tB.text) -> $($tr.code) novaId=$novaId; banco: $($state.text) (origem TRANSFERIDA com motivo; nova ATIVA com origem TRANSFERENCIA:<origem>; vagas A 2->1, B 0->1; 2 eventos de auditoria)")

# 7.4 transferencia para a mesma turma rejeitada (nomeada)
$trX = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/matriculas/$($mX.text)/transferir" -Base $ApiBase -Body ('{"novaTurmaId":' + $tA.text + ',"motivo":"c1-s3-mesma-turma-e2e"}')
Add-Check 'E7' 'transferencia.mesma-turma-rejeitada' (($trX.code -eq 400) -and ($trX.body -like '*Transferência rejeitada*')) ("POST transferir mX já na turma $($tA.text) para a mesma turma -> $($trX.code) (rejeição nomeada, nao 500)")

# 7.5 criacao de frequencia (aluno vencedor, agora na turma B)
$frq = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/frequencias' -Base $ApiBase -Body ('{"turmaId":' + $tB.text + ',"alunoId":' + $alaW.text + ',"professorId":' + $prof.text + ',"dataAula":"' + $hoje + '","componenteCurricular":"MATEMATICA","presente":true,"status":"PRESENTE"}')
$jf = $null; try { $jf = $frq.body | ConvertFrom-Json } catch {}
$fid = 0; try { $fid = [long]$jf.data } catch {}
Add-Check 'E7' 'frequencia.criacao-permissao-canonica' (($frq.code -eq 200) -and ($fid -gt 0)) ("POST /api/educacao/frequencias (turma B, aluno vencedor, MATEMATICA) -> $($frq.code) id=$fid (guard corrigido para a permissao existente frequencia/criar; antes o recurso 'diario_frequencia/criar' inexistente no catalogo negava sempre)")

# 7.6 correcao auditada (antes do fechamento)
$cr1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/frequencias/$fid/correcao" -Base $ApiBase -Body '{"status":"JUSTIFICADA","justificativa":"c1-s3-correcao-e2e"}'
$rowf = Get-Psql "select coalesce(situacao,'?')||'|'||presente||'|'||coalesce(justificativa,'')||'|'||coalesce(updated_by::text,'') from sigov.diario_frequencia where id=$fid;"
$ev = Get-Psql "select payload::text from sigov.educacao_evento where tenant_id=910001 and tipo_evento='FrequenciaCorrigida' and agregado_id=$fid order by id desc limit 1;"
$ok76 = ($cr1.code -eq 200) -and ($rowf.text -eq "JUSTIFICADA|false|c1-s3-correcao-e2e|910301") -and ($ev.text -match '"de"\s*:\s*"PRESENTE"') -and ($ev.text -match '"para"\s*:\s*"JUSTIFICADA"')
Add-Check 'E7' 'correcao.auditada-de-para' $ok76 ("POST correcao PRESENTE->JUSTIFICADA -> $($cr1.code); banco: $($rowf.text); auditoria FrequenciaCorrigida: $($ev.text.Substring(0,[Math]::Min(120,$ev.text.Length)))")

# 7.7 fechar diário da turma B -> correção bloqueada -> reabrir -> correção ok
$dio = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/diario-classe' -Base $ApiBase -Body ('{"escolaId":' + $esc.text + ',"turmaId":' + $tB.text + ',"disciplinaId":9999999101,"professorId":' + $prof.text + ',"anoLetivoId":' + $ano.text + ',"periodo":"1"}')
$jd = $null; try { $jd = $dio.body | ConvertFrom-Json } catch {}
$did = 0; try { $did = [long]$jd.data } catch {}
$aul = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did/aulas" -Base $ApiBase -Body ('{"dataAula":"' + $hoje + '","cargaHoraria":2}')
$ja = $null; try { $ja = $aul.body | ConvertFrom-Json } catch {}
$auid = 0; try { $auid = [long]$ja.data } catch {}
$ctd = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did/conteudo" -Base $ApiBase -Body ('{"aulaId":' + $auid + ',"conteudo":"Conteudo E2E Sprint 3"}')
$fdi = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did/frequencia" -Base $ApiBase -Body ('{"aulaId":' + $auid + ',"alunos":[{"alunoId":' + $alaW.text + ',"status":"PRESENTE"}]}')
Add-Check 'E7' 'diario.lancamento-completo' (($did -gt 0) -and ($aul.code -eq 200) -and ($ctd.code -eq 200) -and ($fdi.code -eq 200)) ("POST diario-classe id=$did + aula id=$auid + conteudo -> $($ctd.code) + frequencia do dia -> $($fdi.code) (periodo completo e fechável)")
$con = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/diario-classe/$did/conferencia" -Base $ApiBase
$jcon = $null; try { $jcon = $con.body | ConvertFrom-Json } catch {}
$token = [string]$jcon.data.tokenConferencia
$okcon = ($con.code -eq 200) -and ($jcon.data.podeFechar -eq $true) -and ($jcon.data.pendencias -eq 0)
Add-Check 'E7' 'diario.conferencia-pode-fechar' $okcon ("GET conferencia -> podeFechar=$($jcon.data.podeFechar) pendencias=$($jcon.data.pendencias) token_len=$($token.Length)")
$fec = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did/fechar-periodo" -Base $ApiBase -Body ('{"observacao":"c1-s3-fechamento-e2e","tokenConferencia":"' + $token + '"}')
$stat = Get-Psql "select status from sigov.educacao_diario_classe where id=$did;"
Add-Check 'E7' 'diario.fechamento-com-token' (($fec.code -eq 200) -and ($stat.text -eq 'FECHADO')) ("POST fechar-periodo com token de conferencia -> $($fec.code); status banco=$($stat.text)")
$blq = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/frequencias/$fid/correcao" -Base $ApiBase -Body '{"status":"PRESENTE"}'
Add-Check 'E7' 'correcao.bloqueada-diario-fechado' (($blq.code -eq 400) -and ($blq.body -like '*Correção bloqueada*FECHADO*')) ("correcao com diário FECHADO -> $($blq.code) (falha nomeada: reabra o diário antes de corrigir; regra 13)")
$reb = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did/reabrir" -Base $ApiBase -Body '{"justificativa":"c1-s3-reabertura-e2e"}'
$stat2 = Get-Psql "select d.status||'|'||(select count(*) from sigov.educacao_diario_historico h where h.diario_id=d.id and h.justificativa='c1-s3-reabertura-e2e') from sigov.educacao_diario_classe d where d.id=$did;"
Add-Check 'E7' 'diario.reabertura-com-justificativa' (($reb.code -eq 200) -and ($stat2.text -eq 'REABERTO|1')) ("POST reabrir -> $($reb.code); status/historico: $($stat2.text)")
$cr2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/frequencias/$fid/correcao" -Base $ApiBase -Body '{"status":"PRESENTE"}'
$rowf2 = Get-Psql "select coalesce(situacao,'?')||'|'||presente from sigov.diario_frequencia where id=$fid;"
Add-Check 'E7' 'correcao.pos-reabertura-ok' (($cr2.code -eq 200) -and ($rowf2.text -eq 'PRESENTE|true')) ("correcao apos reabertura -> $($cr2.code); banco: $($rowf2.text)")
$his = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/diario-classe/$did/historico-fechamentos" -Base $ApiBase
$jhis = $null; try { $jhis = $his.body | ConvertFrom-Json } catch {}
$nhis = 0; try { $nhis = @($jhis.data).Count } catch {}
Add-Check 'E7' 'diario.historico-conservado' (($his.code -eq 200) -and ($nhis -ge 1)) ("GET historico-fechamentos -> $($his.code) entradas=$nhis (fechamento registrado com justificativa e versao)")

# --- limpeza do fixture --------------------------------------------------------
$cl = Get-Psql @"
create temp table if not exists c1t2_alvo (diario_id bigint, aula_id bigint);
truncate table c1t2_alvo;
insert into c1t2_alvo
  select d.id as diario_id, a.id as aula_id
  from sigov.educacao_diario_classe d
  left join sigov.turma t on t.id=d.turma_id and t.is_deleted=false
  left join sigov.educacao_diario_aula a on a.diario_id=d.id
  where t.tenant_id=910001 and t.codigo like 'c1e26-s3-%';
delete from sigov.educacao_diario_frequencia where aula_id in (select aula_id from c1t2_alvo where aula_id is not null);
delete from sigov.educacao_diario_conteudo where aula_id in (select aula_id from c1t2_alvo where aula_id is not null);
delete from sigov.educacao_diario_avaliacao where aula_id in (select aula_id from c1t2_alvo where aula_id is not null);
delete from sigov.educacao_diario_reposicao where aula_id in (select aula_id from c1t2_alvo where aula_id is not null);
delete from sigov.educacao_diario_pendencia where diario_id in (select diario_id from c1t2_alvo);
delete from sigov.educacao_diario_historico where diario_id in (select diario_id from c1t2_alvo);
delete from sigov.educacao_diario_fechamento where diario_id in (select diario_id from c1t2_alvo);
delete from sigov.educacao_diario_aula where id in (select aula_id from c1t2_alvo where aula_id is not null);
delete from sigov.educacao_diario_classe where id in (select diario_id from c1t2_alvo);
drop table if exists c1t2_alvo;
delete from sigov.diario_frequencia where turma_id in (select id from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%');
delete from sigov.professor_turma where turma_id in (select id from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%');
delete from sigov.matricula where tenant_id=910001 and numero_matricula like 'C1S3-%';
delete from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%';
delete from sigov.serie_ano where tenant_id=910001 and codigo='C1S3-SERIE';
delete from sigov.curso where tenant_id=910001 and codigo='C1S3-CURSO';
delete from sigov.ano_letivo where tenant_id=910001 and escola_id=(select id from sigov.escola where tenant_id=910001 and codigo='c1e26-s3-esc');
delete from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%';
delete from sigov.professor where tenant_id=910001 and codigo_professor='C1S3-PROF';
delete from sigov.pessoa where documento in ('999999000101','999999000102','999999000103','999999000201');
delete from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c1e26-%';
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%';
delete from sigov.escola where tenant_id=910001 and codigo like 'c1e26-esc-%';
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s3-esc';
delete from sigov.entidade where cnpj in ('99999999000191','99999999000209') and not exists(select 1 from sigov.escola where entidade_id=sigov.entidade.id);
select (select count(*) from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c1e26-%')
     + (select count(*) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%')
     + (select count(*) from sigov.escola where tenant_id=910001 and (codigo like 'c1e26-esc-%' or codigo='c1e26-s3-esc'))
     + (select count(*) from sigov.entidade where cnpj in ('99999999000191','99999999000209'))
     + (select count(*) from sigov.matricula where tenant_id=910001 and numero_matricula like 'C1S3-%')
     + (select count(*) from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%')
     + (select count(*) from sigov.curso where tenant_id=910001 and codigo='C1S3-CURSO')
     + (select count(*) from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%')
     + (select count(*) from sigov.pessoa where documento in ('999999000101','999999000102','999999000103','999999000201')) as restantes;
"@
Add-Check 'CX' 'fixture.removido' ($cl.code -eq 0 -and $cl.text -eq '0') ("delete c1e26-%/C1S3-% (diario classe+aulas+conteudo+frequencia+fechamento+historico, frequencias, professor_turma, matriculas, turmas, ano, alunos, professor, pessoas, escola + merenda/biblioteca/entidade) tenant 910001 -> restantes=" + $cl.text)

# --- artefatos -----------------------------------------------------------------
$passed = @($checks | Where-Object passed).Count
$failed = @($checks | Where-Object { -not $_.passed }).Count
$result = [pscustomobject]@{ titulo='Ciclo 1 Educao Avancada · TIER2 E2E HTTP (p1_dev)'; generatedAt=(Get-Date).ToUniversalTime().ToString('o'); passed=$passed; failed=$failed; checks=$checks }
$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'c1-tier2-e2e-result.json') -Encoding utf8
$lines = @('# Ciclo 1 (Educação Avançada) — TIER2 E2E HTTP real sobre p1_dev','','Gerado em: ' + $result.generatedAt,'',"Aprovados: **$passed** · Pendentes: **$failed**",'')
foreach ($c in $checks) { $tag = if ($c.passed) {'PASS'}else{'FAIL'}; $lines += ("- **$tag** [$($c.phase)/$($c.name)] $($c.evidence)") }
$lines | Set-Content (Join-Path $out 'c1-tier2-e2e-report.md') -Encoding utf8
Write-Host "==============================================================="
Write-Host ("RESULTADO TIER2: PASS=$passed FAIL=$failed")
Write-Host ("Artefatos: $out")
Write-Host "==============================================================="
if ($failed -gt 0) { exit 1 }
exit 0
