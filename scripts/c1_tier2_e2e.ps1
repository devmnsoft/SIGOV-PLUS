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
  $w = @('-s','--max-time','45','-X',$Method,'-H','Content-Type: application/json','-o',$of,'-w','%{http_code}')
  if ($Bearer) { $w += @("-H","Authorization: Bearer $Bearer") }
  if ($Body) { $bf = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_body_" + [guid]::NewGuid().ToString('N') + ".json"); [IO.File]::WriteAllText($bf, $Body, (New-Object Text.UTF8Encoding($false))); $w += @('--data',"@$bf") }
  $w += $u
  $ef = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_err_" + [guid]::NewGuid().ToString('N') + ".txt")
  $raw = & curl.exe @w 2> $ef
  $cexit = $LASTEXITCODE
  $code = ("$raw").Trim()
  # ReadAllText (UTF-8): Get-Content sem encoding leria ANSI e quebraria acesos nas falhas nomeadas
  $body = if (Test-Path $of) { [IO.File]::ReadAllText($of) } else { '' }
  $errTxt = ''
  try { if (Test-Path $ef) { $errTxt = ([IO.File]::ReadAllText($ef)).Trim() } } catch {}
  $codeInt = 0; try { $codeInt = [int]$code } catch {}
  # DIAG temporario (S6): log curl exit + raw p/ investigar code=0 em /produtos
  try { Add-Content -Path 'C:\MNSOFT\SIGOV-PLUS\artifacts\c1_tier2_e2e\curl_diag.log' -Value ((Get-Date -Format 'HH:mm:ss.fff') + " exit=" + $cexit + " raw=[" + $raw + "] bytes=" + $body.Length + " err=[" + $errTxt + "] " + $Method + " " + $Path) } catch {}
  return [pscustomobject]@{ code=$codeInt; body="$body"; outFile="$of"; err="$errTxt" }
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
# S7: endpoint migrado do dashboard genérico para o DTO dedicado EducacaoBibliotecaDashboardDto.
# Sobre o fixture de 120 acervos ativos (c1e26-%) e ainda sem exemplares/emprestimos:
# titulos=120, disponiveis/emprestimosAtivos/atrasos=0; campos legados (total/ativos/pendentes/alertas/recentes) ausentes.
$okDto = ($d.code -eq 200 -and $j.success -eq $true -and
          $null -ne $j.data.titulos -and $null -ne $j.data.disponiveis -and $null -ne $j.data.emprestimosAtivos -and $null -ne $j.data.atrasos -and
          [long]$j.data.titulos -eq 120 -and [long]$j.data.disponiveis -eq 0 -and
          [long]$j.data.emprestimosAtivos -eq 0 -and [long]$j.data.atrasos -eq 0 -and
          $null -eq $j.data.total -and $null -eq $j.data.recentes)
Add-Check 'E1' 'dashboard.conjunto-completo' $okDto ("GET /api/educacao/biblioteca/dashboard -> $($d.code) titulos=$($j.data.titulos) disponiveis=$($j.data.disponiveis) emprestimosAtivos=$($j.data.emprestimosAtivos) atrasos=$($j.data.atrasos) (S7: DTO dedicado sobre os 120 acervos do fixture; contrato legado total/ativos/pendentes/alertas/recentes ausente)")

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
$ok3 = ($r1.code -eq 404) -and ($r1.body -like '*RECURSO_INDISPONIVEL*') -and ($r1.body -like '*schema ausente*')
Add-Check 'E3' 'listar.diagnostic-schema-ausente-404-uniforme' $ok3 ("GET /api/educacao/merenda/requisicoes com tabela renomeada -> $($r1.code) (C2S2 BR-2.6: era 200+diagnostico, agora 404 RECURSO_INDISPONIVEL nomeada); corpo=" + $r1.body.Substring(0,[Math]::Min(120,$r1.body.Length)))
Get-Psql "alter table sigov.c1t2_probe_omitida rename to $tbl;" | Out-Null
$r3 = Get-Psql "select count(*) from sigov.$tbl;"
Add-Check 'E3' 'probe.tabela-restaurada' ($r3.code -eq 0 -and $r3.text -eq '0') ("rename de volta ok; sigov.$tbl rows depois = $($r3.text)")

# --- E4: alterar status => auditoria de/para/motivo append-only ----------------
$seedId = Get-Psql "select id from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%' order by id limit 1;"
$up1 = Get-Psql "select coalesce(updated_at::text,'null') from sigov.educacao_biblioteca_acervo where tenant_id=910001 and id=$($seedId.text);"
$uq1 = if ($up1.text -ne 'null') {'&updatedAt=' + [Uri]::EscapeDataString($up1.text)} else {''}
$s1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/operacoes/educacao_biblioteca_acervo/$($seedId.text)/status?status=ATIVA&justificativa=c1-t2-ativacao$uq1" -Base $ApiBase
$up2 = Get-Psql "select coalesce(updated_at::text,'null') from sigov.educacao_biblioteca_acervo where tenant_id=910001 and id=$($seedId.text);"
$uq2 = if ($up2.text -ne 'null') {'&updatedAt=' + [Uri]::EscapeDataString($up2.text)} else {''}
$s2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/operacoes/educacao_biblioteca_acervo/$($seedId.text)/status?status=CANCELADA&justificativa=c1-t2-cancelamento-e2e$uq2" -Base $ApiBase
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
# data de referencia vem do current_date do BANCO (fonte autoritativa das janelas de elegibilidade);
# o relogio local do host (UTC-3) diverge da data UTC do servidor na janela 21h-24h BRT
# (fix S10: corrida oficial cruzou essa fronteira entre as corridas 1 e 2)
$hoje = (Get-Psql 'select (current_date)::text;').text
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
$hoje = (Get-Psql 'select (current_date)::text;').text  # refresh: garante DataEntrada >= data_matricula do fixture mesmo se a fronteira UTC cruzar entre fixture e race
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
# refresh pos-transferencia: a matricula transferida recebe data_matricula=current_date do SERVIDOR;
# a data da aula precisa ser >= ela -> mesmo relogio (banco) elimina a divergencia de fuso
$hoje = (Get-Psql 'select (current_date)::text;').text
$frq = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/frequencias' -Base $ApiBase -Body ('{"turmaId":' + $tB.text + ',"alunoId":' + $alaW.text + ',"professorId":' + $prof.text + ',"dataAula":"' + $hoje + '","componenteCurricular":"MATEMATICA","presente":true,"status":"PRESENTE"}')
$jf = $null; try { $jf = $frq.body | ConvertFrom-Json } catch {}
$fid = 0; try { $fid = [long]$jf.data } catch {}
Add-Check 'E7' 'frequencia.criacao-permissao-canonica' (($frq.code -eq 200) -and ($fid -gt 0)) ("POST /api/educacao/frequencias (turma B, aluno vencedor, MATEMATICA, dataAula do banco) -> $($frq.code) id=$fid (guard corrigido para a permissao existente frequencia/criar; antes o recurso 'diario_frequencia/criar' inexistente no catalogo negava sempre; corpo: $($frq.body.Substring(0,[Math]::Min(160,$frq.body.Length)))")

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

# --- E8 (Sprint 4): notas e boletim — publicação de histórico escolar + portais ----
# Reaproveita o fixture S3 (escola/ano 2026/turma A/aluno X/C1S3-PROF): cria vínculo de portal
# (hom.a.admin <-> ALX via pessoa fictícia + responsável cadastral), avaliação com nota por
# aluno e componente curricular (dados->Alunos jsonb) e três chamadas (2 PRESENTE + 1 FALTA).
# Publica o histórico escolar da matrícula-âncora (mX/turma A) e prova ao vivo: versão 1 ->
# re-publicação versao 2; boletim e vida-escolar do portal reagregados (MATEMATICA 9.00/66.67%);
# falhas explícitas (404 matrícula alheia/sem turma; 400 sem lançamentos); permissão no banco.
$alX = Get-Psql "select id from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S3-ALX';"
$fix8 = Get-Psql @"
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixtures C1 S4 Responsavel','999999000401','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000401');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixtures C1 S4 Aluno D','999999000104','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000104');
update sigov.usuario set pessoa_id=(select id from sigov.pessoa where documento='999999000401')
where id=910301 and pessoa_id is null;
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C1S3-ALD','ATIVO',910301 from sigov.pessoa p where p.documento='999999000104'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S3-ALD');
insert into sigov.responsavel_aluno(tenant_id,entidade_id,aluno_id,pessoa_id,parentesco,responsavel_legal,financeiro,autorizado_buscar,contato_emergencia,ativo,created_by)
select 910001,$($e7ent.text),a.id,p.id,'RESPONSAVEL',true,false,false,false,true,910301
from sigov.aluno a join sigov.pessoa p on p.tenant_id=a.tenant_id and p.documento='999999000401'
where a.tenant_id=910001 and a.codigo_aluno='C1S3-ALX'
and not exists(select 1 from sigov.responsavel_aluno r where r.aluno_id=a.id and r.pessoa_id=p.id and not r.is_deleted);
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,t.id,'C1S3-MAT-D',current_date-1,'ATIVA',true,910301
from sigov.aluno a
join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-s3-esc'
join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-s3-turma-a'
where a.tenant_id=910001 and a.codigo_aluno='C1S3-ALD'
and not exists(select 1 from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-D');
update sigov.turma set vagas_ocupadas=2 where tenant_id=910001 and codigo='c1e26-s3-turma-a' and vagas_ocupadas=1;
select 'ok';
"@
Add-Check 'E8' 'fixture.s4-aplicado' ($fix8.code -eq 0) ("fixture S4 (pessoa responsável fictícia + vínculo pessoa->hom.a.admin + responsável cadastral ALX + aluno D com matricula C1S3-MAT-D em A, sem lancamentos; marcadores 999999000401/999999000104/C1S3-MAT-D) exit=$($fix8.code)")

$respId = Get-Psql "select id from sigov.responsavel_aluno where tenant_id=910001 and pessoa_id=(select id from sigov.pessoa where documento='999999000401') and aluno_id=$($alX.text);"
$vinc = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/portal/admin/vinculos' -Base $ApiBase -Body ('{"usuarioVinculadoId":910301,"responsavelId":' + $respId.text + ',"alunoId":' + $alX.text + '}')
$vrow = Get-Psql "select coalesce(status,'?')||'|'||coalesce(usuario_id::text,'?')||'|'||coalesce(aluno_id::text,'?') from sigov.educacao_portal_vinculo where tenant_id=910001 and usuario_id=910301 and aluno_id=$($alX.text) order by id desc limit 1;"
Add-Check 'E8' 'vinculo.portal-criado-pela-secretaria' (($vinc.code -eq 200) -and ($vrow.text -eq "ATIVO|910301|$($alX.text)")) ("POST /api/educacao/portal/admin/vinculos (secretaria libera o hom.a.admin para o aluno X via responsável cadastral) -> $($vinc.code); banco: $($vrow.text)")

$dio8 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/diario-classe' -Base $ApiBase -Body ('{"escolaId":' + $esc.text + ',"turmaId":' + $tA.text + ',"disciplinaId":9999999101,"professorId":' + $prof.text + ',"anoLetivoId":' + $ano.text + ',"periodo":"2"}')
$jdid8 = $null; try { $jdid8 = $dio8.body | ConvertFrom-Json } catch {}
$did8 = 0; try { $did8 = [long]$jdid8.data } catch {}
$aul8 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did8/aulas" -Base $ApiBase -Body ('{"dataAula":"' + $hoje + '","cargaHoraria":2}')
$ja8 = $null; try { $ja8 = $aul8.body | ConvertFrom-Json } catch {}
$auid8 = 0; try { $auid8 = [long]$ja8.data } catch {}
$aval = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/diario-classe/$did8/avaliacoes" -Base $ApiBase -Body ('{"aulaId":' + $auid8 + ',"titulo":"Prova 1 E2E S4","valorMaximo":10,"peso":1,"componenteCurricular":"MATEMATICA","alunos":[{"alunoId":' + $alX.text + ',"nota":9}]}')
$arow = Get-Psql "select coalesce((dados->>'ComponenteCurricular'),'?')||'|'||coalesce(jsonb_array_length(coalesce(dados->'Alunos','[]'::jsonb))::text,'?')||'|'||coalesce(((dados->'Alunos'->0)->>'Nota')::text,'?')||'|'||coalesce(((dados->'Alunos'->0)->>'AlunoId')::text,'?') from sigov.educacao_diario_avaliacao where aula_id=$auid8 order by id desc limit 1;"
Add-Check 'E8' 'avaliacao.notas-por-aluno-no-jsonb' (($dio8.code -eq 200) -and ($aul8.code -eq 200) -and ($aval.code -eq 200) -and ($arow.text -eq "MATEMATICA|1|9|$($alX.text)")) ("diário periodo 2 na turma A (id=$did8) + aula id=$auid8 + POST avaliacoes c/ componenteCurricular MATEMATICA e nota 9 para o aluno X -> $($aval.code); dados jsonb: $($arow.text)")

Get-Psql "delete from sigov.diario_frequencia where tenant_id=910001 and turma_id=$($tA.text) and aluno_id=$($alX.text) and professor_id=$($prof.text);" | Out-Null
$dF2 = (Get-Psql 'select (current_date + 1)::text;').text  # datas relativas ao relogio do BANCO (mesmo padrao de $hoje): garante datas distintas na janela de fuso
$dF3 = (Get-Psql 'select (current_date + 2)::text;').text
$frq1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/frequencias' -Base $ApiBase -Body ('{"turmaId":' + $tA.text + ',"alunoId":' + $alX.text + ',"professorId":' + $prof.text + ',"dataAula":"' + $hoje + '","componenteCurricular":"MATEMATICA","presente":true,"status":"PRESENTE"}')
$frq2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/frequencias' -Base $ApiBase -Body ('{"turmaId":' + $tA.text + ',"alunoId":' + $alX.text + ',"professorId":' + $prof.text + ',"dataAula":"' + $dF2 + '","componenteCurricular":"MATEMATICA","presente":true,"status":"PRESENTE"}')
$frq3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/frequencias' -Base $ApiBase -Body ('{"turmaId":' + $tA.text + ',"alunoId":' + $alX.text + ',"professorId":' + $prof.text + ',"dataAula":"' + $dF3 + '","componenteCurricular":"MATEMATICA","presente":false,"status":"FALTA","justificativa":"c1-s4-falta-e2e"}')
$frow = Get-Psql "select count(*)||'|'||count(*) filter (where presente) from sigov.diario_frequencia where tenant_id=910001 and turma_id=$($tA.text) and aluno_id=$($alX.text) and professor_id=$($prof.text);"
Add-Check 'E8' 'frequencia.tres-chamadas-para-agregacao' (($frq1.code -eq 200) -and ($frq2.code -eq 200) -and ($frq3.code -eq 200) -and ($frow.text -eq '3|2')) ("3x POST /api/educacao/frequencias no aluno X (turma A, MATEMATICA): PRESENTE+PRESENTE+FALTA -> codes $($frq1.code)/$($frq2.code)/$($frq3.code); banco total|presentes: $($frow.text)")

$pub1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/portal/admin/historico-escolar/$($alX.text)/publicar" -Base $ApiBase -Body ('{"matriculaId":' + $mX.text + ',"justificativa":"c1-s4-publicacao-e2e"}')
$jpub1 = $null; try { $jpub1 = $pub1.body | ConvertFrom-Json } catch {}
$hrow = Get-Psql "select coalesce(h.status,'?')||'|'||coalesce((h.dados->>'versao')::text,'?')||'|'||(select count(*) from sigov.educacao_historico_escolar_item i where i.historico_id=h.id) from sigov.educacao_historico_escolar h where h.tenant_id=910001 and h.matricula_id=$($mX.text) order by h.id desc limit 1;"
$irow = Get-Psql "select coalesce(i.componente_curricular,'?')||'|'||coalesce(i.nota::text,'?')||'|'||coalesce(i.frequencia::text,'?') from sigov.educacao_historico_escolar_item i join sigov.educacao_historico_escolar h on h.id=i.historico_id where h.tenant_id=910001 and h.matricula_id=$($mX.text) order by i.componente_curricular limit 1;"
$okpub1 = ($pub1.code -eq 200) -and ($jpub1.data.versao -eq 1) -and ($jpub1.data.itens -ge 1) -and ($hrow.text -eq 'PUBLICADO|1|1') -and ($irow.text -eq 'MATEMATICA|9.00|66.67')
Add-Check 'E8' 'publicar.primeira-versao-agregada' $okpub1 ("POST /api/educacao/portal/admin/historico-escolar/{X}/publicar (matricula mX) -> $($pub1.code) versao=$($jpub1.data.versao) itens=$($jpub1.data.itens); historico banco: $($hrow.text); item: $($irow.text) (nota media 9.00 e frequencia 2/3=66.67%)")

$bol = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/portal/alunos/$($alX.text)/boletim" -Base $ApiBase
$jbol = $null; try { $jbol = $bol.body | ConvertFrom-Json } catch {}
$itB = @($jbol.data.itens) | Where-Object { $_.componenteCurricular -eq 'MATEMATICA' } | Select-Object -First 1
$okbol = ($bol.code -eq 200) -and ($jbol.data.situacao -eq 'PUBLICADO') -and ([int]$jbol.data.versao -eq 1) -and ($null -ne $itB) -and ([double]$itB.nota -eq 9.0) -and ([double]$itB.frequencia -eq 66.67)
Add-Check 'E8' 'boletim.portal-publicado-notas-frequencia' $okbol ("GET /api/educacao/portal/alunos/{X}/boletim -> $($bol.code) situacao=$($jbol.data.situacao) versao=$($jbol.data.versao) item MATEMATICA nota=$($itB.nota) frequencia=$($itB.frequencia) (portal consome o histórico publicado, sem regra inventada)")

$vid = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/portal/alunos/$($alX.text)/vida-escolar" -Base $ApiBase
$jvid = $null; try { $jvid = $vid.body | ConvertFrom-Json } catch {}
$okvid = ($vid.code -eq 200) -and ([int]$jvid.data.aulasLancadas -eq 3) -and ([int]$jvid.data.presencas -eq 2) -and ([int]$jvid.data.faltas -eq 1) -and ([double]$jvid.data.percentualFrequencia -eq 66.67)
Add-Check 'E8' 'vida-escolar.reagregada-com-chamadas' $okvid ("GET vida-escolar -> $($vid.code) aulasLancadas=$($jvid.data.aulasLancadas) presencas=$($jvid.data.presencas) faltas=$($jvid.data.faltas) percentual=$($jvid.data.percentualFrequencia)% (agregação real sobre as chamadas lançadas)")

$pub2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/portal/admin/historico-escolar/$($alX.text)/publicar" -Base $ApiBase -Body ('{"matriculaId":' + $mX.text + ',"justificativa":"c1-s4-republicacao-e2e"}')
$jpub2 = $null; try { $jpub2 = $pub2.body | ConvertFrom-Json } catch {}
$bol2 = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/portal/alunos/$($alX.text)/boletim" -Base $ApiBase
$jbol2 = $null; try { $jbol2 = $bol2.body | ConvertFrom-Json } catch {}
$okpub2 = ($pub2.code -eq 200) -and ([int]$jpub2.data.versao -eq 2) -and ([int]$jbol2.data.versao -eq 2)
Add-Check 'E8' 'publicar.republicacao-incrementa-versao' $okpub2 ("re-publicação -> $($pub2.code) versao=$($jpub2.data.versao); boletim reflete versao=$($jbol2.data.versao) (mesmo histórico versionado, itens substituídos)")

$pub404 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/portal/admin/historico-escolar/$($alX.text)/publicar" -Base $ApiBase -Body '{"matriculaId":9999999999}'
Add-Check 'E8' 'publicar.matricula-alheia-404-nomeada' (($pub404.code -eq 404) -and ($pub404.body -like '*não encontrad*')) ("publicar com matriculaId=9999999999 -> $($pub404.code) (falha nomeada não encontrada, nao 500; regra 13)")

$alD = Get-Psql "select id from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S3-ALD';"
$mD = Get-Psql "select id from sigov.matricula where tenant_id=910001 and numero_matricula='C1S3-MAT-D';"
$pub400 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/portal/admin/historico-escolar/$($alD.text)/publicar" -Base $ApiBase -Body ('{"matriculaId":' + $mD.text + '}')
Add-Check 'E8' 'publicar.sem-lancamentos-400-explicita' (($pub400.code -eq 400) -and ($pub400.body -like '*Sem avaliações ou frequência*')) ("publicar p/ aluno D (enturmado em A, zero lancamentos) -> $($pub400.code) (falha explicita nomeada: sem sucesso simulado vazio; regra 13)")

$prow = Get-Psql "select coalesce((select id::text from sigov.permissao where chave='educacao.historico_escolar.publicar' and ativo and not is_deleted),'ausente')||'|'||count(*) filter (where pp.tenant_id=910001)||'|'||count(*) from sigov.perfil_permissao pp join sigov.permissao p on p.id=pp.permissao_id where p.chave='educacao.historico_escolar.publicar' and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR';"
Add-Check 'E8' 'permissao.banco-autoridade-concessao' ($prow.text -match '^\d+\|1\|\d+$') ("permissão educacao.historico_escolar.publicar: id no catálogo | concessão PERMITIR tenant 910001 | total concessões: $($prow.text) (banco como fonte de autoridade da autorização; regra 11)")

# --- E9 (Sprint 5): merenda base — cardápio tipado com produtos + unicidade de período ----
# POST /api/educacao/merenda/cardapios/completo {escolaId,inicio,fim,produtos}: escopo
# institucional da escola (mesma entidade do contexto autenticado), produtos válidos do
# tenant, unicidade de período vigente (escola x data de início) com backstop no índice
# ux_merenda_cardapio_tenant_escola_inicio e itens transacionais referenciando o cardápio.
# Falhas nomeadas (regra 13); multi-esfera, nada municipal hardcoded (regra 24).
$fix9 = Get-Psql @"
insert into sigov.entidade($colTid nome,cnpj) select $valTid 'Fixtures C1 Entidade S5','99999999000301' where not exists(select 1 from sigov.entidade where cnpj='99999999000301');
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,1,'c1e26-s5-esc','Fixtures C1 Escola S5',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s5-esc');
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,e.id,'c1e26-s5-esc-out','Fixtures C1 Escola S5 Out',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
from sigov.entidade e where e.cnpj='99999999000301' and not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s5-esc-out');
select 'ok';
"@
Add-Check 'E9' 'fixture.s5-aplicado' ($fix9.code -eq 0) ("fixture S5 (entidade fictícia 99999999000301 + escola c1e26-s5-esc na entidade do contexto + escola c1e26-s5-esc-out fora do escopo, tenant 910001) exit=$($fix9.code)")
$s5esc = Get-Psql "select coalesce(id,-1) from sigov.escola where tenant_id=910001 and codigo='c1e26-s5-esc';"
$s5out = Get-Psql "select coalesce(id,-1) from sigov.escola where tenant_id=910001 and codigo='c1e26-s5-esc-out';"
$prA = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C1S5-PROD-A","nome":"Arroz Branco C1S5","tipo":"ALIMENTO","status":"ATIVA"}'
$prB = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C1S5-PROD-B","nome":"Feijao C1S5","tipo":"ALIMENTO","status":"ATIVA"}'
$prowA = Get-Psql "select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S5-PROD-A' order by id desc limit 1;"
$prowB = Get-Psql "select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S5-PROD-B' order by id desc limit 1;"
Add-Check 'E9' 'produto.criacao-base-via-api' (($prA.code -eq 200) -and ($prB.code -eq 200) -and ($prowA.text -ne '') -and ($prowB.text -ne '')) ("POST /api/educacao/merenda/produtos x2 -> codes $($prA.code)/$($prB.code); ids no banco: $($prowA.text)/$($prowB.text)")
$card1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s5esc.text + ',"inicio":"2026-11-02","fim":"2026-11-30","produtos":[' + $prowA.text + ',' + $prowB.text + ']}')
$card9j = $null; try { $card9j = $card1.body | ConvertFrom-Json } catch {}
$crow9 = Get-Psql "select status||'|'||coalesce(data_inicio::text,'null')||'|'||coalesce(data_fim::text,'null')||'|'||(select count(*) from sigov.educacao_merenda_cardapio_item i where i.referencia_id=c.id and i.is_deleted=false)||'|'||(select count(distinct i.produto_id) from sigov.educacao_merenda_cardapio_item i where i.referencia_id=c.id and i.produto_id in (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo in ('C1S5-PROD-A','C1S5-PROD-B'))) from sigov.educacao_merenda_cardapio c where c.tenant_id=910001 and c.escola_id=$($s5esc.text) and c.data_inicio=date '2026-11-02' order by c.id desc limit 1;"
Add-Check 'E9' 'cardapio.criado-com-itens-transacionais' (($card1.code -eq 200) -and ($crow9.text -eq 'RASCUNHO|2026-11-02|2026-11-30|2|2')) ("POST /api/educacao/merenda/cardapios/completo -> $($card1.code) data=$($card9j.data); banco status|inicio|fim|itens|produtos-distintos: $($crow9.text)")
$idx9 = Get-Psql "insert into sigov.educacao_merenda_cardapio(tenant_id,entidade_id,escola_id,data_inicio,data_fim,nome,tipo,status,dados) values(910001,1,$($s5esc.text),date '2026-11-02',date '2026-11-10','Backstop S5','CARDAPIO_MERENDA','RASCUNHO','{}'::jsonb);"
Add-Check 'E9' 'cardapio.indice-backstop-unicidade' (($idx9.code -eq 3) -and ($idx9.text -like '*ux_merenda_cardapio_tenant_escola_inicio*')) ("insert SQL de 2o cardapio RASCUNHO mesmo escola+inicio -> exit=$($idx9.code) (indice unico parcial como backstop transacional; o comando tipado falharia antes com 409 nomeada)")
$dupP = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s5esc.text + ',"inicio":"2026-11-02","fim":"2026-12-01","produtos":[' + $prowA.text + ']}')
Add-Check 'E9' 'cardapio.periodo-duplicado-409-nomeada' (($dupP.code -eq 409) -and ($dupP.body -like '*CARDAPIO_PERIODO_EXISTENTE*')) ("POST cardapios/completo com mesmo inicio -> $($dupP.code) (409 CARDAPIO_PERIODO_EXISTENTE nomeada, nao 500)")
$dupPr = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s5esc.text + ',"inicio":"2026-11-16","fim":"2026-11-30","produtos":[' + $prowA.text + ',' + $prowA.text + ']}')
Add-Check 'E9' 'cardapio.produto-duplicado-409-nomeada' (($dupPr.code -eq 409) -and ($dupPr.body -like '*CARDAPIO_PRODUTO_DUPLICADO*')) ("POST cardapios/completo com produto repetido -> $($dupPr.code) (409 CARDAPIO_PRODUTO_DUPLICADO)")
$noPr = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s5esc.text + ',"inicio":"2026-11-23","fim":"2026-11-30","produtos":[' + $prowA.text + ',9999999999]}')
Add-Check 'E9' 'cardapio.produto-inexistente-404-nomeada' (($noPr.code -eq 404) -and ($noPr.body -like '*CARDAPIO_PRODUTO_INEXISTENTE*')) ("POST cardapios/completo com produto 9999999999 -> $($noPr.code) (404 CARDAPIO_PRODUTO_INEXISTENTE)")
$badFim = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s5esc.text + ',"inicio":"2026-11-30","fim":"2026-11-02","produtos":[' + $prowA.text + ']}')
Add-Check 'E9' 'cardapio.fim-antes-inicio-400' ($badFim.code -eq 400) ("POST cardapios/completo com fim < inicio -> $($badFim.code) (400 validacao nomeada do periodo)")
$outS = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s5out.text + ',"inicio":"2026-11-15","fim":"2026-11-30","produtos":[' + $prowA.text + ']}')
Add-Check 'E9' 'cardapio.escola-fora-do-escopo-409' (($outS.code -eq 409) -and ($outS.body -like '*ESCOLA_FORA_DO_ESCOPO*')) ("POST cardapios/completo com escola de outra entidade -> $($outS.code) (409 ESCOLA_FORA_DO_ESCOPO; escopo institucional antes do cadastro; regras 11/24)")
$mact = 0; if ($card9j -and $null -ne $card9j.data) { $mact = [long]$card9j.data }
$t91 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_cardapio/$mact/status?status=ATIVA&justificativa=c1-s5-ativa" -Base $ApiBase
$t92u = Get-Psql "select coalesce(updated_at::text,'null') from sigov.educacao_merenda_cardapio where tenant_id=910001 and id=$mact;"
$t92q = if ($t92u.text -ne 'null') {'&updatedAt=' + [Uri]::EscapeDataString($t92u.text)} else {''}
$t92 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_cardapio/$mact/status?status=ENCERRADA&justificativa=c1-s5-encerra$t92q" -Base $ApiBase
$t93 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_cardapio/$mact/status?status=ATIVA&justificativa=c1-s5-reabrir" -Base $ApiBase
Add-Check 'E9' 'cardapio.maquina-ativa-terminal-e-final' (($t91.code -eq 200) -and ($t92.code -eq 200) -and ($t93.code -eq 409) -and ($t93.body -like '*TRANSICAO_INVALIDA*')) ("maquina S2 aplicada ao cardapio: RASCUNHO->ATIVA $($t91.code) ->ENCERRADA $($t92.code) ->ATIVA $($t93.code) (terminal nao reabre; TRANSICAO_INVALIDA nomeada)")

# --- E10 (Sprint 6): merenda operação — registro de estoque + distribuição com consumo real ---
# POST /api/educacao/merenda/estoques/lancar {produtoId,operacao,quantidade,motivo?,chaveIdempotencia?}: ABERTURA cria a
# unica linha de estoque; AJUSTE/ENTRADA somam ao saldo (falha nomeada se o produto
# não existir/estiver inativo). POST /api/educacao/merenda/distribuicoes/confirmar
# {cardapioId,data,itens}: cardápio ATIVO dentro do período, itens restritos ao menu,
# uma distribuição vigente por cardápio × data (índice ux_merenda_distribuicao_tenant_
# cardapio_data como backstop), lock do estoque em ordem determinística e movimentos
# SAIDA + baixa de saldo na mesma transação. Falhas nomeadas (regra 13); regra 24.
$fix10 = Get-Psql @"
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,1,'c1e26-s6-esc','Fixtures C1 Escola S6',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s6-esc');
select 'ok';
"@
Add-Check 'E10' 'fixture.s6-aplicado' ($fix10.code -eq 0) ("fixture S6 (escola c1e26-s6-esc na entidade do contexto, tenant 910001) exit=$($fix10.code)")
$s6esc = Get-Psql "select coalesce(id,-1) from sigov.escola where tenant_id=910001 and codigo='c1e26-s6-esc';"
# Nomes pApi* (nao $pA): variaveis PS sao case-insensitive e $pa (psql) abaixo apagaria a resposta HTTP
$pApiA = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C1S6-PROD-A","nome":"Arroz C1S6","tipo":"ALIMENTO","status":"ATIVA"}'
$pApiB = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C1S6-PROD-B","nome":"Feijao C1S6","tipo":"ALIMENTO","status":"ATIVA"}'
$pApiC = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C1S6-PROD-C","nome":"Leite C1S6","tipo":"ALIMENTO","status":"ATIVA"}'
$pa = Get-Psql "select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-A' order by id desc limit 1;"
$pb = Get-Psql "select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-B' order by id desc limit 1;"
$pc = Get-Psql "select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-C' order by id desc limit 1;"
Add-Check 'E10' 'produto.criacao-base-s6' (($pApiA.code -eq 200) -and ($pApiB.code -eq 200) -and ($pApiC.code -eq 200) -and ($pa.text -ne '') -and ($pb.text -ne '') -and ($pc.text -ne '')) ("POST /api/educacao/merenda/produtos x3 -> codes $($pApiA.code)/$($pApiB.code)/$($pApiC.code); ids: $($pa.text)/$($pb.text)/$($pc.text); curl-err: $(if($pApiA.err){$pApiA.err}else{'-'})/$(if($pApiB.err){$pApiB.err}else{'-'})/$(if($pApiC.err){$pApiC.err}else{'-'})")
$estA = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pa.text + ',"operacao":"ABERTURA","quantidade":100}')
$erowA = Get-Psql "select round(coalesce(s.saldo,-1))::text||'|'||coalesce(s.nome,'null') from sigov.educacao_merenda_estoque s where s.tenant_id=910001 and s.produto_id=(select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-A') order by s.id desc limit 1;"
Add-Check 'E10' 'estoque.abertura-tipada-inicial' (($estA.code -eq 200) -and ($erowA.text -eq '100|Arroz C1S6')) ("POST /api/educacao/merenda/estoques/lancar ABERTURA 100 produto A -> $($estA.code); banco saldo|nome: $($erowA.text) (C2S2 BR-2.2: porta tipada)")
$estB1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pb.text + ',"operacao":"ABERTURA","quantidade":10}')
$estB2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pb.text + ',"operacao":"AJUSTE","quantidade":5,"motivo":"c2s2-ajuste-soma"}')
$erowB = Get-Psql "select round(coalesce(s.saldo,-1))::text from sigov.educacao_merenda_estoque s where s.tenant_id=910001 and s.produto_id=(select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-B') order by s.id desc limit 1;"
Add-Check 'E10' 'estoque.ajuste-soma-sobre-linha-existente' (($estB1.code -eq 200) -and ($estB2.code -eq 200) -and ($erowB.text -eq '15')) ("ABERTURA 10 + AJUSTE +5 (motivo c2s2-ajuste-soma) -> codes $($estB1.code)/$($estB2.code); banco saldo=15 (AJUSTE soma sobre a linha existente, nao duplica)")
$estX = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body '{"produtoId":9999999999,"operacao":"ABERTURA","quantidade":1}'
Add-Check 'E10' 'estoque.produto-inexistente-404-nomeada' (($estX.code -eq 404) -and ($estX.body -like '*MERENDA_PRODUTO_INEXISTENTE*')) ("POST /estoques/lancar ABERTURA produto 9999999999 -> $($estX.code) (404 MERENDA_PRODUTO_INEXISTENTE nomeada)")
$cardS6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s6esc.text + ',"inicio":"2026-11-02","fim":"2026-11-30","produtos":[' + $pa.text + ',' + $pb.text + ']}')
$cardS6j = $null; try { $cardS6j = $cardS6.body | ConvertFrom-Json } catch {}
$cs6 = 0; if ($cardS6j -and $null -ne $cardS6j.data) { $cs6 = [long]$cardS6j.data }
$actS6 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_cardapio/$cs6/status?status=ATIVA&justificativa=c1-s6-ativa" -Base $ApiBase
$cstatS6 = Get-Psql "select status from sigov.educacao_merenda_cardapio where tenant_id=910001 and id=$cs6;"
Add-Check 'E10' 'cardapio.ativo-para-distribuicao' (($cardS6.code -eq 200) -and ($actS6.code -eq 200) -and ($cstatS6.text -eq 'ATIVA')) ("cardapios/completo [A,B] -> $($cardS6.code) id=$cs6; status ATIVA -> $($actS6.code); banco status=$($cstatS6.text)")
$dist1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-10","itens":[{"produtoId":' + $pa.text + ',"quantidade":50},{"produtoId":' + $pb.text + ',"quantidade":4}]}')
$drow = Get-Psql "select d.status||'|'||coalesce(d.competencia::text,'null')||'|'||(select count(*) from sigov.educacao_merenda_distribuicao_item i where i.referencia_id=d.id and i.is_deleted=false)||'|'||(select count(*) from sigov.educacao_merenda_movimento m where m.referencia_id=d.id and m.tipo='SAIDA' and m.is_deleted=false)||'|'||round(coalesce((select sum(s.saldo) from sigov.educacao_merenda_estoque s where s.tenant_id=910001 and s.produto_id in (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo in ('C1S6-PROD-A','C1S6-PROD-B'))),-1))::text from sigov.educacao_merenda_distribuicao d where d.tenant_id=910001 and d.referencia_id=$cs6 and d.competencia=date '2026-11-10' order by d.id desc limit 1;"
Add-Check 'E10' 'distribuicao.confirmada-com-consumo-real' (($dist1.code -eq 200) -and ($drow.text -eq 'ATIVA|2026-11-10|2|2|61')) ("POST distribuicoes/confirmar (A:50,B:4) -> $($dist1.code); banco status|data|itens|mov-Saida|saldo-total: $($drow.text) (100-50 + 15-4 = 61; tudo na mesma transação)")
$dist2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-11","itens":[{"produtoId":' + $pa.text + ',"quantidade":999}]}')
Add-Check 'E10' 'distribuicao.estoque-insuficiente-409-nomeada' (($dist2.code -eq 409) -and ($dist2.body -like '*ESTOQUE_INSUFICIENTE*')) ("itens A:999 com saldo 50 -> $($dist2.code) (409 ESTOQUE_INSUFICIENTE nomeada; transação revertida)")
$dist3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-10","itens":[{"produtoId":' + $pa.text + ',"quantidade":1}]}')
Add-Check 'E10' 'distribuicao.dia-ocupado-409-nomeada' (($dist3.code -eq 409) -and ($dist3.body -like '*DISTRIBUICAO_DIA_EXISTENTE*')) ("mesmo cardapio+data já distribuída -> $($dist3.code) (409 DISTRIBUICAO_DIA_EXISTENTE; backstop transacional: indice ux_merenda_distribuicao_tenant_cardapio_data)")
$dist4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-12-05","itens":[{"produtoId":' + $pa.text + ',"quantidade":1}]}')
Add-Check 'E10' 'distribuicao.data-fora-do-periodo-409' (($dist4.code -eq 409) -and ($dist4.body -like '*DATA_FORA_DO_PERIODO*')) ("data 2026-12-05 fora do período do cardápio (até 2026-11-30) -> $($dist4.code) (409 DATA_FORA_DO_PERIODO)")
$dist5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-12","itens":[{"produtoId":' + $pc.text + ',"quantidade":1}]}')
Add-Check 'E10' 'distribuicao.produto-fora-do-menu-400' (($dist5.code -eq 400) -and ($dist5.body -like '*PRODUTO_FORA_DO_MENU*')) ("item com produto C (nao consta no cardapio) -> $($dist5.code) (400 PRODUTO_FORA_DO_MENU)")
$cardS6b = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/cardapios/completo' -Base $ApiBase -Body ('{"escolaId":' + $s6esc.text + ',"inicio":"2026-12-01","fim":"2026-12-31","produtos":[' + $pa.text + ',' + $pb.text + ']}')
$cardS6bj = $null; try { $cardS6bj = $cardS6b.body | ConvertFrom-Json } catch {}
$cs6b = 0; if ($cardS6bj -and $null -ne $cardS6bj.data) { $cs6b = [long]$cardS6bj.data }
$dist6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6b + ',"data":"2026-12-15","itens":[{"produtoId":' + $pa.text + ',"quantidade":1}]}')
Add-Check 'E10' 'distribuicao.cardapio-rascunho-409' (($dist6.code -eq 409) -and ($dist6.body -like '*CARDAPIO_NAO_APROVADO*')) ("cardápio ainda em RASCUNHO -> $($dist6.code) (409 CARDAPIO_NAO_APROVADO; distribuições exigem cardápio ATIVO)")

# --- E11 (Sprint 7): biblioteca circulacao — emprestimo/devolucao/reserva por exemplar ---
# POST /api/educacao/biblioteca/emprestimos/lancar {exemplarId,alunoId,servidorId,prazo}:
# exemplar existe/ativo/no escopo (404/409), tomador exatamente um de aluno/servidor
# (400 se nenhum ou ambos), prazo >= data de lancamento (400); um emprestimo vigente
# por exemplar (409 nomeada + indice ux_biblioteca_emprestimo_tenant_exemplar_vigencia
# como backstop). POST /api/educacao/biblioteca/emprestimos/{id}/devolver {observacao}:
# so ATIVOS podem ser devolvidos (senao 409 nomeada); encerra o emprestimo e registra
# a devolucao na mesma transacao. POST /api/educacao/biblioteca/reservas/lancar: uma
# reserva vigente por exemplar (fila de tamanho 1). GET /api/educacao/biblioteca/dashboard:
# metricas dedicadas (titulos/exemplares disponiveis/emprestimos ativos/atrasos).
# Falhas nomeadas (regra 13); multi-esfera (regra 24).
$e11fix = Get-Psql @"
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,1,'c1e26-s7-esc','Fixtures C1 Escola S7',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s7-esc');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,1,'F','Fixtures C1 S7 Aluno','999999000301','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000301');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,1,p.id,'C1S7-ALA','ATIVO',910301 from sigov.pessoa p where p.documento='999999000301'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S7-ALA');
select 'ok';
"@
Add-Check 'E11' 'fixture.s7-aplicado' ($e11fix.code -eq 0) ("fixture S7 (escola c1e26-s7-esc + pessoa/aluno ficticio C1S7-ALA, tenant 910001) exit=$($e11fix.code)")
$e11al = Get-Psql "select coalesce(id,-1) from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S7-ALA';"
$e11acv = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/acervo' -Base $ApiBase -Body '{"codigo":"C1S7-ACV","nome":"Livro C1S7 Fisica","tipo":"NAO_FICCIONADO","status":"ATIVA"}'
$e11exA = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/exemplares' -Base $ApiBase -Body '{"codigo":"C1S7-EXA","nome":"Exemplar C1S7 A","status":"DISPONIVEL"}'
$e11exB = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/exemplares' -Base $ApiBase -Body '{"codigo":"C1S7-EXB","nome":"Exemplar C1S7 B","status":"DISPONIVEL"}'
$e11idA = Get-Psql "select coalesce(id,-1) from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo='C1S7-EXA' order by id desc limit 1;"
$e11idB = Get-Psql "select coalesce(id,-1) from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo='C1S7-EXB' order by id desc limit 1;"
Add-Check 'E11' 'cadastro.acervo-e-exemplares-via-api' (($e11acv.code -eq 200) -and ($e11exA.code -eq 200) -and ($e11exB.code -eq 200) -and ([long]$e11idA.text -gt 0) -and ([long]$e11idB.text -gt 0)) ("POST acervo $($e11acv.code) + exemplares DISPONIVEL x2 $($e11exA.code)/$($e11exB.code); ids: $($e11idA.text)/$($e11idB.text)")
# S7 fica no relogio LOCAL do host de proposito: a camada de biblioteca valida o prazo contra
# DateTime.Today do processo (local), nao contra current_date do banco — alinhar ao banco causou
# skew dentro da janela 21h-24h BRT (corrida 4: 'prazo anterior' aceito + competencia divergente)
$e11hoje = (Get-Date).ToString('yyyy-MM-dd')
$e11prazo = (Get-Date).AddDays(14).ToString('yyyy-MM-dd')
$e11prazoPasse = (Get-Date).AddDays(-1).ToString('yyyy-MM-dd')
$e11l1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idA.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
$e11db1 = Get-Psql ("select status||'|'||to_char(competencia,'YYYY-MM-DD')||'|'||to_char(data_fim,'YYYY-MM-DD')||'|'||coalesce(aluno_id,-1) from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=" + $e11idA.text + " order by id desc limit 1;")
Add-Check 'E11' 'emprestimo.lancado-com-tomador-aluno' (($e11l1.code -eq 200) -and ($e11db1.text -like ("ATIVA|" + $e11hoje + "|" + $e11prazo + "|" + $e11al.text))) ("POST emprestimos/lancar (exemplar A + aluno + prazo +14d) -> $($e11l1.code); banco status|competencia|data_fim|aluno: $($e11db1.text)")
$e11l2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idA.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
Add-Check 'E11' 'emprestimo.duplicado-409-nomeada' (($e11l2.code -eq 409) -and ($e11l2.body -like '*EXEMPLAR_INDISPONIVEL*')) ("segundo emprestimo do mesmo exemplar vigente -> $($e11l2.code) (409 EXEMPLAR_INDISPONIVEL nomeada; checagem previa)")
$e11raw = Get-Psql ("insert into sigov.educacao_biblioteca_emprestimo(tenant_id,entidade_id,exemplar_id,tipo,status,dados,auditoria,correlation_id,ativo) select 910001,1," + $e11idA.text + ",'FISICO','ATIVA','{}'::jsonb,'[]'::jsonb,gen_random_uuid(),true;")
Add-Check 'E11' 'emprestimo.indice-backstop-unicidade' (($e11raw.code -eq 3) -and ($e11raw.text -like '*ux_biblioteca_emprestimo_tenant_exemplar_vigencia*')) ("insert SQL de 2o emprestimo ATIVO do mesmo exemplar -> exit=$($e11raw.code) (indice unico parcial como backstop transacional; o comando tipado falha antes com 409 nomeada)")
$e11l3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazoPasse + '"}')
Add-Check 'E11' 'emprestimo.prazo-anterior-400-nomeada' (($e11l3.code -eq 400) -and ($e11l3.body -like '*PRAZO_EMPRESTIMO_INVALIDO*')) ("prazo ontem ($($e11prazoPasse)) -> $($e11l3.code) (400 PRAZO_EMPRESTIMO_INVALIDO nomeada; nada gravado)")
$e11l4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"prazo":"' + $e11prazo + '"}')
Add-Check 'E11' 'emprestimo.sem-tomador-400-nomeada' (($e11l4.code -eq 400) -and ($e11l4.body -like '*EMPRESTIMO_TOMADOR_INVALIDO*')) ("sem alunoId/servidorId -> $($e11l4.code) (400 EMPRESTIMO_TOMADOR_INVALIDO nomeada; exatamente um tomador)")
$e11l5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":9999999999,"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
Add-Check 'E11' 'emprestimo.exemplar-inexistente-404-nomeada' (($e11l5.code -eq 404) -and ($e11l5.body -like '*BIBLIOTECA_EXEMPLAR_INEXISTENTE*')) ("exemplar 9999999999 -> $($e11l5.code) (404 BIBLIOTECA_EXEMPLAR_INEXISTENTE nomeada)")
$e11emb1 = Get-Psql ("select id from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=" + $e11idA.text + " order by id desc limit 1;")
$e11d1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e11emb1.text)/devolver" -Base $ApiBase -Body '{"observacao":"devolvido c1s7"}'
$e11d1j = $null; try { $e11d1j = ($e11d1.body | ConvertFrom-Json) } catch {}
$e11d1id = 0; if ($e11d1j -and $null -ne $e11d1j.data) { $e11d1id = [long]$e11d1j.data }
$e11devrow = Get-Psql ("select coalesce(min(id),0) from sigov.educacao_biblioteca_devolucao where tenant_id=910001 and referencia_id=" + $(if($e11emb1.text){$e11emb1.text}else{0}) + ";")
$e11db2 = Get-Psql "select (select status from sigov.educacao_biblioteca_emprestimo m where m.tenant_id=910001 and m.exemplar_id=$($e11idA.text) order by m.id desc limit 1)||'|'||(select count(1) from sigov.educacao_biblioteca_devolucao d where d.tenant_id=910001 and d.referencia_id=$(if($e11emb1.text){$e11emb1.text}else{0}))||'|'||(select d.dados->>'em_dia' from sigov.educacao_biblioteca_devolucao d where d.tenant_id=910001 and d.referencia_id=$(if($e11emb1.text){$e11emb1.text}else{0}) limit 1);"
Add-Check 'E11' 'devolucao.registrada-transacional-evento' (($e11d1.code -eq 200) -and ($e11db2.text -eq 'ENCERRADA|1|true') -and ($e11d1id -gt 0) -and ($e11d1id -eq [long]$e11devrow.text)) ("POST emprestimos/{id}/devolver -> $($e11d1.code); banco emprestimo|devolucoes|em_dia: $($e11db2.text); resposta data=id do evento de devolucao $e11d1id == banco $($e11devrow.text) (BR-3.2: o comando retorna o evento, mesma transacao; exemplar liberado)")
$e11d2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e11emb1.text)/devolver" -Base $ApiBase -Body '{}'
$e11d2j = $null; try { $e11d2j = ($e11d2.body | ConvertFrom-Json) } catch {}
$e11d2id = 0; if ($e11d2j -and $null -ne $e11d2j.data) { $e11d2id = [long]$e11d2j.data }
Add-Check 'E11' 'devolucao.repetida-idempotente-evento-original' (($e11d2.code -eq 200) -and ($e11d2id -gt 0) -and ($e11d2id -eq $e11d1id)) ("devolver de novo o mesmo emprestimo -> $($e11d2.code) data=$($e11d2id) (BR-3.2: idempotente — retorna o id do evento de devolucao original $e11d1id em vez de 409; nao duplica)")
$e11d3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/9999999999/devolver' -Base $ApiBase -Body '{}'
Add-Check 'E11' 'devolucao.inexistente-404-nomeada' (($e11d3.code -eq 404) -and ($e11d3.body -like '*BIBLIOTECA_EMPRESTIMO_INEXISTENTE*')) ("emprestimo 9999999999 -> $($e11d3.code) (404 BIBLIOTECA_EMPRESTIMO_INEXISTENTE nomeada)")
$e11l6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idA.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
Add-Check 'E11' 'circulo.nova-vigencia-apos-devolucao' (($e11l6.code -eq 200)) ("novo emprestimo do exemplar apos devolucao -> $($e11l6.code) (estado terminal liberou a vigencia para nova circulacao)")
$e11r1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/reservas/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + '}')
$e11rid = Get-Psql ("select id from sigov.educacao_biblioteca_reserva where tenant_id=910001 and exemplar_id=" + $e11idB.text + " order by id desc limit 1;")
$e11r2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/reservas/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + '}')
Add-Check 'E11' 'reserva.lancada-e-duplicada-409-nomeada' (($e11r1.code -eq 200) -and ($e11r2.code -eq 409) -and ($e11r2.body -like '*RESERVA_VIGENTE_EXISTENTE*') -and ($e11rid.text -ne '')) ("reserva exemplar B -> $($e11r1.code) (id=$($e11rid.text)); 2a reserva -> $($e11r2.code) (409 RESERVA_VIGENTE_EXISTENTE nomeada; fila de tamanho 1; backstop ux_biblioteca_reserva_tenant_exemplar_vigencia)")
# fixture de atraso: empurra o prazo do emprestimo ativo para ontem (ajuste declarado de fixture p/ evidenciar a metrica)
$e11up = Get-Psql ("update sigov.educacao_biblioteca_emprestimo set data_fim=current_date-1 where tenant_id=910001 and exemplar_id=" + $e11idA.text + " and ativo and is_deleted=false and status='ATIVA';")
$e11dash = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/biblioteca/dashboard' -Base $ApiBase
$e11dj = $null; try { $e11dj = ($e11dash.body | ConvertFrom-Json) } catch {}
$e11dT = 0; $e11dDisp = 0; $e11dAt = 0; $e11dAtr = 0; $e11dRP = 0; $e11dBloq = 0
if ($e11dj -and $null -ne $e11dj.data) { $e11dT = [long]$e11dj.data.titulos; $e11dDisp = [long]$e11dj.data.disponiveis; $e11dAt = [long]$e11dj.data.emprestimosAtivos; $e11dAtr = [long]$e11dj.data.atrasos; $e11dRP = [long]$e11dj.data.reservasPendentes; $e11dBloq = [long]$e11dj.data.bloqueados }
Add-Check 'E11' 'dashboard.metricas-dedicadas-disponibilidade-unica' (($e11dash.code -eq 200) -and ($e11dT -ge 1) -and ($e11dDisp -eq 0) -and ($e11dAt -ge 1) -and ($e11dAtr -ge 1) -and ($e11dRP -ge 1) -and ($e11dBloq -eq 0)) ("GET biblioteca/dashboard -> $($e11dash.code); metricas titulos/disponiveis/ativos/atrasos/reservasPendentes/bloqueados: $e11dT/$e11dDisp/$e11dAt/$e11dAtr/$e11dRP/$e11dBloq (BR-3.6 disponibilidade unica: exemplar A emprestado e B com reserva pendente NAO contam como disponiveis; BR-3.2/BR-3.4 novas metricas reservasPendentes/bloqueados; atraso provado com fixture data_fim=ontem)")
$e11rc = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/operacoes/educacao_biblioteca_reserva/$($e11rid.text)/status?status=CANCELADA&justificativa=c1-s7-cancela-reserva" -Base $ApiBase
$e11r3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/reservas/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + '}')
Add-Check 'E11' 'reserva.cancelada-reabre-fila' (($e11rc.code -eq 200) -and ($e11r3.code -eq 200)) ("reserva CANCELADA via maquina de estados -> $($e11rc.code); nova reserva no exemplar -> $($e11r3.code) (terminal libera a fila)")

# --- E17 (Ciclo 2 Sprint 3): biblioteca circulacao completa (BR-3.1 a BR-3.8) ---
# Estado herdado do E11: exemplar A emprestado (emprestimo ativo, data_fim empurrada p/ ontem
# pela fixture de atraso) e exemplar B livre com reserva ativa do aluno C1S7-ALA. Parametros
# por tenant via parametro_modulo/parametro_modulo_valor (regra 11), sem override no tenant:
# EMPRESTIMO_MAX_VIGENTES_TOMADOR=2, RENOVACAO_MAX_POR_EMPRESTIMO=1, RENOVACAO_DIAS_ESTENDIMENTO=14.
$e17ren0 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e11emb1.text)/renovar" -Base $ApiBase
Add-Check 'E17' 'renovacao.emprestimo-nao-vigente-409' (($e17ren0.code -eq 409) -and ($e17ren0.body -like '*RENOVAR_EMPRESTIMO_NAO_VIGENTE*')) ("renovar emprestimo ja ENCERRADO -> $($e17ren0.code) (409 RENOVAR_EMPRESTIMO_NAO_VIGENTE nomeada; somente ATIVOS renovam)")
$e17embA = Get-Psql ("select id from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=" + $e11idA.text + " order by id desc limit 1;")
$e17ren1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embA.text)/renovar" -Base $ApiBase
$e17fimEsperado = (Get-Date).AddDays(28).ToString('yyyy-MM-dd')
$e17dbren = Get-Psql ("select renovacoes||'|'||to_char(data_fim,'YYYY-MM-DD')||'|'||to_char(prazo_original,'YYYY-MM-DD')||'|'||status from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and id=" + $e17embA.text + ";")
$e17evren = Get-Psql ("select count(1) from sigov.educacao_biblioteca_evento where tenant_id=910001 and referencia_id=" + $e17embA.text + " and tipo='RENOVACAO';")
Add-Check 'E17' 'renovacao.vigente-prazo-original-e-evento' (($e17ren1.code -eq 200) -and ($e17dbren.text -eq ("1|" + $e17fimEsperado + "|" + $e11prazo + "|ATIVA")) -and ($e17evren.text -eq '1')) ("renovar emprestimo ativo (data_fim = ontem pela fixture) -> $($e17ren1.code); banco renovacoes|data_fim|prazo_original|status: $($e17dbren.text) (BR-3.3: novo prazo = PRAZO_ORIGINAL($e11prazo) + (renovacoes+1)*dias = $e17fimEsperado, nao soma sobre o prazo corrompido); evento RENOVACAO append-only gravado: $($e17evren.text)")
$e17ren2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embA.text)/renovar" -Base $ApiBase
Add-Check 'E17' 'renovacao.limite-excedido-409' (($e17ren2.code -eq 409) -and ($e17ren2.body -like '*RENOVAR_LIMITE_EXCEDIDO*')) ("2a renovacao do mesmo emprestimo -> $($e17ren2.code) (409 RENOVAR_LIMITE_EXCEDIDO; maximo pelo tenant RENOVACAO_MAX_POR_EMPRESTIMO=1)")
$e17lB = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
$e17embB = Get-Psql ("select id from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=" + $e11idB.text + " order by id desc limit 1;")
Add-Check 'E17' 'emprestimo.vigente-com-reserva-pendente-fila' (($e17lB.code -eq 200) -and ([long]$e17embB.text -gt 0)) ("emprestar exemplar B que possui reserva ativa -> $($e17lB.code) id=$($e17embB.text) (reserva e fila: nao impede emprestimo; sera consumida na devolucao, BR-3.2)")
$e17exC = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/exemplares' -Base $ApiBase -Body '{"codigo":"C1S7-EXC","nome":"Exemplar C1S7 C","status":"DISPONIVEL"}'
$e17idC = Get-Psql "select coalesce(id,-1) from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo='C1S7-EXC' order by id desc limit 1;"
$e17lC = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e17idC.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
Add-Check 'E17' 'emprestimo.limite-tomador-padrao-409' (($e17lC.code -eq 409) -and ($e17lC.body -like '*TOMADOR_EMPRESTIMOS_MAX_ATINGIDO*')) ("3o emprestimo do mesmo aluno (ja tem 2 vigentes: A + B) -> $($e17lC.code) (409 TOMADOR_EMPRESTIMOS_MAX_ATINGIDO; BR-3.1 padrao EMPRESTIMO_MAX_VIGENTES_TOMADOR=2 parametrizado por tenant)")
$e17renB = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embB.text)/renovar" -Base $ApiBase
Add-Check 'E17' 'renovacao.reserva-pendente-409' (($e17renB.code -eq 409) -and ($e17renB.body -like '*RENOVAR_RESERVA_VIGENTE*')) ("renovar emprestimo de exemplar com reserva pendente -> $($e17renB.code) (409 RENOVAR_RESERVA_VIGENTE; BR-3.3 renovacao so ocorre sem reserva no exemplar)")
$e17idR = Get-Psql ("select id from sigov.educacao_biblioteca_reserva where tenant_id=910001 and exemplar_id=" + $e11idB.text + " order by id desc limit 1;")
$e17devB = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embB.text)/devolver" -Base $ApiBase -Body '{}'
$e17devBj = $null; try { $e17devBj = ($e17devB.body | ConvertFrom-Json) } catch {}
$e17devBid = 0; if ($e17devBj -and $null -ne $e17devBj.data) { $e17devBid = [long]$e17devBj.data }
$e17trans = Get-Psql ("select (select coalesce(referencia_id,-1)||'|'||coalesce(dados->>'origem','')||'|'||status from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=" + $e11idB.text + " order by id desc limit 1)||'|'||(select status from sigov.educacao_biblioteca_reserva where tenant_id=910001 and id=" + $e17idR.text + ")||'|'||(select status from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and id=" + $e17embB.text + ");")
Add-Check 'E17' 'devolucao.transfere-reserva-automatica' (($e17devB.code -eq 200) -and ($e17devBid -gt 0) -and ($e17trans.text -eq ($e17idR.text + '|RESERVA|ATIVA|CONSUMIDA|ENCERRADA'))) ("devolver emprestimo do B -> $($e17devB.code) (evento de devolucao $e17devBid); na MESMA transacao a reserva pendente virou emprestimo automatico: referencia_id|origem|status + reserva + emprestimo antigo = $($e17trans.text) (BR-3.2 transferencia com consumo da reserva, tomador abaixo do maximo)")
$e17fut = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
$e17dvf = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embA.text)/devolver" -Base $ApiBase -Body ('{"dataDevolucao":"' + $e17fut + '"}')
$e17dvfs = Get-Psql ("select status from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and id=" + $e17embA.text + ";")
Add-Check 'E17' 'devolucao.data-efetiva-futura-400' (($e17dvf.code -eq 400) -and ($e17dvf.body -like '*DATA_DEVOLUCAO_INVALIDA*') -and ($e17dvfs.text -eq 'ATIVA')) ("devolver com data efetiva amanha ($e17fut) -> $($e17dvf.code) (400 DATA_DEVOLUCAO_INVALIDA; atraso mede a data EFETIVA da devolucao, BR-3.2; nada gravado, emprestimo segue $($e17dvfs.text))")
$e17oc0a = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e17idC.text)/ocorrencia" -Base $ApiBase -Body '{"tipo":"DANO","motivo":""}'
Add-Check 'E17' 'ocorrencia.sem-motivo-400' (($e17oc0a.code -eq 400) -and ($e17oc0a.body -like '*OCORRENCIA_MOTIVO_OBRIGATORIO*')) ("ocorrencia DANO sem motivo -> $($e17oc0a.code) (400 OCORRENCIA_MOTIVO_OBRIGATORIO; BR-3.4 toda ocorrencia exige motivo)")
$e17oc0b = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e17idC.text)/ocorrencia" -Base $ApiBase -Body '{"tipo":"TROCA","motivo":"troca c2s3"}'
Add-Check 'E17' 'ocorrencia.tipo-invalido-400' (($e17oc0b.code -eq 400) -and ($e17oc0b.body -like '*OCORRENCIA_TIPO_INVALIDO*')) ("ocorrencia tipo TROCA -> $($e17oc0b.code) (400 OCORRENCIA_TIPO_INVALIDO; tipos canonicos PERDA/DANO/EXTRAVIO/BLOQUEIO)")
$e17embB2 = Get-Psql ("select id from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=" + $e11idB.text + " order by id desc limit 1;")
$e17oc1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e11idB.text)/ocorrencia" -Base $ApiBase -Body '{"tipo":"DANO","motivo":"capa rasgada c2s3"}'
$e17oc1j = $null; try { $e17oc1j = ($e17oc1.body | ConvertFrom-Json) } catch {}
$e17oid = 0; if ($e17oc1j -and $null -ne $e17oc1j.data) { $e17oid = [long]$e17oc1j.data }
$e17oc1db = Get-Psql ("select (select coalesce(tipo,'')||'|'||status||'|'||coalesce(descricao,'') from sigov.educacao_biblioteca_ocorrencia where tenant_id=910001 and id=" + $e17oid + ")||'|'||(select status from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e11idB.text + ")||'|'||(select status||'|'||coalesce(justificativa,'') from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and id=" + $e17embB2.text + ");")
Add-Check 'E17' 'ocorrencia.dano-encerra-emprestimo-vigente' (($e17oc1.code -eq 200) -and ([long]$e17oid -gt 0) -and ($e17oc1db.text -like 'DANO|ATIVA|capa rasgada c2s3|DANIFICADO|ENCERRADA|OCORRENCIA:*')) ("DANO no exemplar B -> $($e17oc1.code) id=$e17oid; banco ocorrencia|exemplar|emprestimo: $($e17oc1db.text) (BR-3.4 encerra o emprestimo vigente com justificativa OCORRENCIA:<id> e DANIFICADO o exemplar)")
$e17dvoc = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embB2.text)/devolver" -Base $ApiBase -Body '{}'
Add-Check 'E17' 'devolucao.encerrado-por-ocorrencia-409' (($e17dvoc.code -eq 409) -and ($e17dvoc.body -like '*EMPRESTIMO_ENCERRADO_POR_OCORRENCIA*')) ("devolver emprestimo encerrado pela ocorrencia -> $($e17dvoc.code) (409 EMPRESTIMO_ENCERRADO_POR_OCORRENCIA nomeada; estorne a ocorrencia antes)")
$e17st0 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/ocorrencias/$($e17oid)/estornar" -Base $ApiBase -Body '{}'
Add-Check 'E17' 'estorno.sem-motivo-400' (($e17st0.code -eq 400) -and ($e17st0.body -like '*ESTORNO_OCORRENCIA_MOTIVO_OBRIGATORIO*')) ("estorno sem motivo -> $($e17st0.code) (400 ESTORNO_OCORRENCIA_MOTIVO_OBRIGATORIO; reversao auditada)")
$e17st1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/ocorrencias/$($e17oid)/estornar" -Base $ApiBase -Body '{"motivo":"dano inexistente c2s3"}'
$e17st1j = $null; try { $e17st1j = ($e17st1.body | ConvertFrom-Json) } catch {}
$e17st1id = 0; if ($e17st1j -and $null -ne $e17st1j.data) { $e17st1id = [long]$e17st1j.data }
$e17st1db = Get-Psql ("select (select status from sigov.educacao_biblioteca_ocorrencia where tenant_id=910001 and id=" + $e17oid + ")||'|'||(select status from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e11idB.text + ")||'|'||(select status from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and id=" + $e17embB2.text + ");")
$e17st2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/ocorrencias/$($e17oid)/estornar" -Base $ApiBase -Body '{"motivo":"repete c2s3"}'
$e17st2j = $null; try { $e17st2j = ($e17st2.body | ConvertFrom-Json) } catch {}
$e17st2id = 0; if ($e17st2j -and $null -ne $e17st2j.data) { $e17st2id = [long]$e17st2j.data }
Add-Check 'E17' 'estorno.unico-restaura-estado' (($e17st1.code -eq 200) -and ($e17st1id -eq $e17oid) -and ($e17st1db.text -eq 'CANCELADA|DISPONIVEL|ATIVA') -and ($e17st2.code -eq 200) -and ($e17st2id -eq $e17st1id)) ("estornar ocorrencia -> $($e17st1.code) id=$e17st1id; banco ocorrencia|exemplar|emprestimo: $($e17st1db.text) (BR-3.4: ocorrencia CANCELADA, exemplar volta DISPONIVEL, emprestimo reaberto ATIVA); estorno repetido -> $($e17st2.code) id=$e17st2id (compensacao unica, idempotente, mesma identificacao)")
$e17stx = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/ocorrencias/9999999999/estornar' -Base $ApiBase -Body '{"motivo":"x c2s3"}'
Add-Check 'E17' 'ocorrencia.inexistente-404' (($e17stx.code -eq 404) -and ($e17stx.body -like '*BIBLIOTECA_OCORRENCIA_INEXISTENTE*')) ("estornar ocorrencia 9999999999 -> $($e17stx.code) (404 BIBLIOTECA_OCORRENCIA_INEXISTENTE nomeada)")
$e17dv2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e17embB2.text)/devolver" -Base $ApiBase -Body '{"observacao":"devolucao pos estorno c2s3"}'
$e17libB = Get-Psql ("select status||'|'||(select count(1) from sigov.educacao_biblioteca_emprestimo e where e.tenant_id=910001 and e.exemplar_id=" + $e11idB.text + " and e.ativo and not e.is_deleted and e.status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')) from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e11idB.text + ";")
Add-Check 'E17' 'devolucao.pos-estorno-libera-exemplar' (($e17dv2.code -eq 200) -and ($e17libB.text -eq 'DISPONIVEL|0')) ("devolver o emprestimo reaberto pelo estorno -> $($e17dv2.code); exemplar B|emprestimos-vigentes: $($e17libB.text) (exemplar volta a ficar livre para nova circulacao apos o ciclo ocorrencia+estorno; fluxo E16 consome esse estado)")
$e17oc2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e17idC.text)/ocorrencia" -Base $ApiBase -Body '{"tipo":"BLOQUEIO","motivo":"verificacao c2s3"}'
$e17bloq = Get-Psql ("select status from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e17idC.text + ";")
$e17lD = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e17idC.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e11prazo + '"}')
$e17desb0 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e17idC.text)/desbloquear" -Base $ApiBase -Body '{}'
$e17desb1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e17idC.text)/desbloquear" -Base $ApiBase -Body '{"justificativa":"verificada ok c2s3"}'
$e17liber = Get-Psql ("select status from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e17idC.text + ";")
$e17desb2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/exemplares/$($e17idC.text)/desbloquear" -Base $ApiBase -Body '{"justificativa":"repete c2s3"}'
Add-Check 'E17' 'ocorrencia.bloqueio-trava-e-desbloqueio-justificado' (($e17oc2.code -eq 200) -and ($e17bloq.text -eq 'BLOQUEADO') -and ($e17lD.code -eq 409) -and ($e17lD.body -like '*EXEMPLAR_BLOQUEADO*') -and ($e17desb0.code -eq 400) -and ($e17desb0.body -like '*DESBLOQUEIO_JUSTIFICATIVA_OBRIGATORIA*') -and ($e17desb1.code -eq 200) -and ($e17liber.text -eq 'DISPONIVEL') -and ($e17desb2.code -eq 409) -and ($e17desb2.body -like '*EXEMPLAR_NAO_BLOQUEADO*')) ("BLOQUEIO no C: ocorrencia $($e17oc2.code) + exemplar BLOQUEADO + emprestimo 409 EXEMPLAR_BLOQUEADO ($($e17lD.code)); desbloquear sem justificativa 400 ($($e17desb0.code)); com justificativa 200 -> $($e17liber.text) ($($e17desb1.code)); repetir 409 EXEMPLAR_NAO_BLOQUEADO ($($e17desb2.code)) (BR-3.4 gate de bloqueio impede emprestimo; desbloqueio auditado)")
$e17inv = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/inventarios/lancar' -Base $ApiBase -Body '{}'
$e17invj = $null; try { $e17invj = ($e17inv.body | ConvertFrom-Json) } catch {}
$e17invId = 0; if ($e17invj -and $null -ne $e17invj.data) { $e17invId = [long]$e17invj.data }
$e17invdb = Get-Psql ("select (select status from sigov.educacao_biblioteca_inventario where tenant_id=910001 and id=" + $e17invId + ")||'|'||(select count(1) from sigov.educacao_biblioteca_inventario_item where referencia_id=" + $e17invId + ")||'|'||(select coalesce(substr(codigo,1,3),'') from sigov.educacao_biblioteca_inventario where tenant_id=910001 and id=" + $e17invId + ");")
Add-Check 'E17' 'inventario.snapshot-aberto' (($e17inv.code -eq 200) -and ([long]$e17invId -gt 0) -and ($e17invdb.text -like 'ABERTO|*|INV')) ("lancar inventario -> $($e17inv.code) id=$e17invId; status|itens-no-snapshot|prefixo-codigo: $($e17invdb.text) (BR-3.5 snapshot da posicao de todos os exemplares ativos na abertura, codigo INV-)")
$e17ctg0 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/contar" -Base $ApiBase -Body ('{"exemplarId":' + $e17idC.text + ',"quantidade":-1}')
Add-Check 'E17' 'inventario.quantidade-negativa-400' (($e17ctg0.code -eq 400) -and ($e17ctg0.body -like '*INVENTARIO_QUANTIDADE_INVALIDA*')) ("contar qtd=-1 -> $($e17ctg0.code) (400 INVENTARIO_QUANTIDADE_INVALIDA; contagem fisica e inteiro nao negativo)")
$e17ctA = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/contar" -Base $ApiBase -Body ('{"exemplarId":' + $e11idA.text + ',"quantidade":1}')
$e17jsA = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/justificar" -Base $ApiBase -Body ('{"exemplarId":' + $e11idA.text + ',"desfecho":"AJUSTE","justificativa":"x c2s3"}')
Add-Check 'E17' 'inventario.contar-conforme-sem-divergencia' (($e17ctA.code -eq 200) -and ($e17jsA.code -eq 409) -and ($e17jsA.body -like '*INVENTARIO_ITEM_SEM_DIVERGENCIA*')) ("contar A qtd=1 (presente, conforme a posicao) -> $($e17ctA.code); justificar item sem divergencia -> $($e17jsA.code) (409 INVENTARIO_ITEM_SEM_DIVERGENCIA)")
$e17ctC = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/contar" -Base $ApiBase -Body ('{"exemplarId":' + $e17idC.text + ',"quantidade":0}')
$e17jsCi = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/justificar" -Base $ApiBase -Body ('{"exemplarId":' + $e17idC.text + ',"desfecho":"TROCA","justificativa":"x c2s3"}')
$e17ctB = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/contar" -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"quantidade":1,"statusContado":"DANIFICADO"}')
Add-Check 'E17' 'inventario.divergencias-contadas-e-desfecho-invalido' (($e17ctC.code -eq 200) -and ($e17ctB.code -eq 200) -and ($e17jsCi.code -eq 400) -and ($e17jsCi.body -like '*INVENTARIO_DESFECHO_INVALIDO*')) ("contar C qtd=0 (ausente na contagem) -> $($e17ctC.code); contar B qtd=1 statusContado=DANIFICADO (divergencia de status) -> $($e17ctB.code); desfecho TROCA -> $($e17jsCi.code) (400 INVENTARIO_DESFECHO_INVALIDO; canonicos AJUSTE/DISPENSA)")
$e17ctX = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/contar" -Base $ApiBase -Body '{"exemplarId":9999999999,"quantidade":1}'
$e17jsX = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/justificar" -Base $ApiBase -Body '{"exemplarId":9999999999,"desfecho":"AJUSTE","justificativa":"x c2s3"}'
Add-Check 'E17' 'inventario.exemplar-fora-da-base-404' (($e17ctX.code -eq 404) -and ($e17ctX.body -like '*BIBLIOTECA_EXEMPLAR_INEXISTENTE*') -and ($e17jsX.code -eq 404) -and ($e17jsX.body -like '*BIBLIOTECA_INVENTARIO_ITEM_INEXISTENTE*')) ("contar exemplar 9999999999 -> $($e17ctX.code) (404 exemplar inexistente no escopo); justificar exemplar fora do snapshot -> $($e17jsX.code) (404 BIBLIOTECA_INVENTARIO_ITEM_INEXISTENTE)")
$e17fe0 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/fechar?justificativa=c2s3-tentativa" -Base $ApiBase
Add-Check 'E17' 'inventario.fechar-com-divergencias-409' (($e17fe0.code -eq 409) -and ($e17fe0.body -like '*INVENTARIO_DIVERGENCIAS_EM_ABERTO*')) ("fechar com divergencias abertas (C ausente + B status) -> $($e17fe0.code) (409 INVENTARIO_DIVERGENCIAS_EM_ABERTO; cada divergencia exige desfecho justificavel)")
$e17jsC = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/justificar" -Base $ApiBase -Body ('{"exemplarId":' + $e17idC.text + ',"desfecho":"AJUSTE","justificativa":"perda confirmada c2s3"}')
$e17cdb = Get-Psql ("select status from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e17idC.text + ";")
$e17jsB = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/justificar" -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"desfecho":"DISPENSA","justificativa":"recontado presente c2s3"}')
$e17bdb = Get-Psql ("select status from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and id=" + $e11idB.text + ";")
Add-Check 'E17' 'inventario.desfechos-aplicados-exemplares' (($e17jsC.code -eq 200) -and ($e17cdb.text -eq 'PERDIDO') -and ($e17jsB.code -eq 200) -and ($e17bdb.text -eq 'DISPONIVEL')) ("AJUSTE C (qtd=0) -> $($e17jsC.code) e exemplar C PERDIDO; DISPENSA B -> $($e17jsB.code) e exemplar B segue $($e17bdb.text) (BR-3.5 AJUSTE reflete no exemplar, DISPENSA preserva; justificativas auditadas)")
$e17fe1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/fechar?justificativa=c2s3-fechamento" -Base $ApiBase
$e17fes = Get-Psql ("select status from sigov.educacao_biblioteca_inventario where tenant_id=910001 and id=" + $e17invId + ";")
$e17ctG = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/itens/contar" -Base $ApiBase -Body ('{"exemplarId":' + $e11idA.text + ',"quantidade":1}')
$e17fe2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/inventarios/$($e17invId)/fechar?justificativa=repete" -Base $ApiBase
Add-Check 'E17' 'inventario.fechado-terminal' (($e17fe1.code -eq 200) -and ($e17fes.text -eq 'FECHADO') -and ($e17ctG.code -eq 409) -and ($e17fe2.code -eq 409) -and ($e17ctG.body -like '*INVENTARIO_JA_FECHADO*') -and ($e17fe2.body -like '*INVENTARIO_JA_FECHADO*')) ("fechar -> $($e17fe1.code) status FECHADO; contar apos fechar -> $($e17ctG.code) e re-fechar -> $($e17fe2.code) (409 INVENTARIO_JA_FECHADO; estado terminal da maquina)")
$e17fe3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/inventarios/9999999999/fechar?justificativa=x' -Base $ApiBase
Add-Check 'E17' 'inventario.inexistente-404' (($e17fe3.code -eq 404) -and ($e17fe3.body -like '*BIBLIOTECA_INVENTARIO_INEXISTENTE*')) ("fechar inventario 9999999999 -> $($e17fe3.code) (404 BIBLIOTECA_INVENTARIO_INEXISTENTE nomeada)")
$e17hA = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/biblioteca/historico/exemplar/$($e11idA.text)" -Base $ApiBase
$e17hAj = $null; try { $e17hAj = ($e17hA.body | ConvertFrom-Json) } catch {}
$e17hAcount = 0; $e17hAtipos = @()
if ($e17hAj -and $null -ne $e17hAj.data) { $e17hAcount = @($e17hAj.data).Count; $e17hAtipos = @($e17hAj.data | ForEach-Object { $_.tipo }) }
$e17hT = Invoke-Api -Bearer $tk.token -Method GET -Path "/api/educacao/biblioteca/historico/tomador?alunoId=$($e11al.text)" -Base $ApiBase
$e17hTj = $null; try { $e17hTj = ($e17hT.body | ConvertFrom-Json) } catch {}
$e17hTcount = 0; if ($e17hTj -and $null -ne $e17hTj.data) { $e17hTcount = @($e17hTj.data).Count }
Add-Check 'E17' 'historico.append-only-multi-tabelas' (($e17hA.code -eq 200) -and ($e17hAcount -ge 3) -and ($e17hAtipos -contains 'EMPRESTIMO') -and ($e17hAtipos -contains 'DEVOLUCAO') -and ($e17hAtipos -contains 'EVENTO') -and ($e17hT.code -eq 200) -and ($e17hTcount -ge 5)) ("GET historico/exemplar/A -> $($e17hA.code) eventos=$e17hAcount (inclui EMPRESTIMO+DEVOLUCAO+EVENTO de renovacao); GET historico/tomador?alunoId=ALA -> $($e17hT.code) eventos=$e17hTcount (BR-3.7 leitura unificada append-only das tabelas tipadas, teto 200, sem update/delete na leitura)")
$e17hT0 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/biblioteca/historico/tomador' -Base $ApiBase
$e17hA0 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/biblioteca/historico/exemplar/9999999999' -Base $ApiBase
Add-Check 'E17' 'historico.falhas-nomeadas-filtro' (($e17hT0.code -eq 400) -and ($e17hT0.body -like '*EMPRESTIMO_TOMADOR_INVALIDO*') -and ($e17hA0.code -eq 404) -and ($e17hA0.body -like '*BIBLIOTECA_EXEMPLAR_INEXISTENTE*')) ("historico/tomador sem nenhum tomador -> $($e17hT0.code) (400 EMPRESTIMO_TOMADOR_INVALIDO via filtro EducacaoFalhasNomeadasFiltro); historico/exemplar/9999999999 -> $($e17hA0.code) (404 nomeada via filtro; regra 13 nas leituras)")
$e17acv = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/acervo' -Base $ApiBase -Body '{"codigo":"C2S3-ACV","nome":"Livro FNLI C2S3","tipo":"FICCIONADO","status":"ATIVA","dados":{"origem":"FNLI"}}'
$e17acvid = Get-Psql "select coalesce(id,-1) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo='C2S3-ACV' order by id desc limit 1;"
$e17acvdb = Get-Psql ("select coalesce(dados->>'origem','')||'|'||(case when origem is null then 'NULL' else coalesce(origem,'') end) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and id=" + $e17acvid.text + ";")
Add-Check 'E17' 'acervo.origem-fnli-persistida' (($e17acv.code -eq 200) -and ($e17acvdb.text -eq 'FNLI|NULL')) ("POST acervo com dados.origem=FNLI -> $($e17acv.code); banco dados->>origem | coluna origem: $($e17acvdb.text) (BR-3.8: coluna origem criada pela migration C2S3 no acervo e origem FNLI registrada em dados)")

# ===========================================================================
# E12 — Sprint 8: educacao/transporte escolar (rota/ponto/aluno)
# ---------------------------------------------------------------------------
# POST /api/educacao/transporte/rotas/lancar {nome,turno,escolaId?,regiao?,codigo}:
# exatamente uma de escolaId ou regiao (400 nomeada; multi-esfera regra 24),
# escola no escopo institucional (404/409 reutilizados), rota criada ATIVA
# (simplificacao declarada). POST /api/educacao/transporte/rotas/{id}/pontos/lancar
# {nome,ordem}: ordem > 0 (400) e unica por rota x tenant vigente (409 nomeada +
# indice ux_transporte_ponto_tenant_rota_ordem como backstop). POST
# /api/educacao/transporte/rotas/{id}/alunos/vincular {alunoId,pontoId?,turno?}:
# aluno valido em sigov.aluno (404 nomeada), turno herdado da rota (divergente =
# 400), ponto deve pertencer a rota (404/409), um vinculo vigente por
# aluno x rota x turno (409 nomeada + indice ux_educacao_transporte_aluno_rota_turno
# do rc50_48 como backstop). GET /api/educacao/transporte/dashboard: DTO dedicado
# EducacaoTransporteDashboardDto (rotasAtivas/pontosAtivos/alunosVinculados/
# escolasAtendidas). Maquina de estados S2: rota ATIVA -> CANCELADA bloqueia
# novas operacoes (409 nomeada). Falhas nomeadas (regra 13); multi-esfera (regra 24).
# ===========================================================================
$e12fix = Get-Psql @"
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,1,'c1e26-s8-esc','Fixtures C1 Escola S8',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s8-esc');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,1,'F','Fixtures C1 S8 Aluno A','999999000402','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000402');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,1,'F','Fixtures C1 S8 Aluno B','999999000403','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000403');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,1,p.id,'C1S8-ALA','ATIVO',910301 from sigov.pessoa p where p.documento='999999000402'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S8-ALA');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,1,p.id,'C1S8-ALB','ATIVO',910301 from sigov.pessoa p where p.documento='999999000403'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S8-ALB');
select 'ok';
"@
Add-Check 'E12' 'fixture.s8-aplicado' ($e12fix.code -eq 0) ("fixture S8 (escola c1e26-s8-esc + pessoas/alunos ficticios C1S8-ALA/C1S8-ALB, tenant 910001) exit=$($e12fix.code)")
$e12esc = Get-Psql "select coalesce(id,-1) from sigov.escola where tenant_id=910001 and codigo='c1e26-s8-esc';"
$e12alA = Get-Psql "select coalesce(id,-1) from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S8-ALA';"
$e12alB = Get-Psql "select coalesce(id,-1) from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S8-ALB';"
$e12l1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/rotas/lancar' -Base $ApiBase -Body ('{"nome":"Rota C1S8 Bairro Central","turno":"MANHA","escolaId":' + $e12esc.text + ',"codigo":"C1S8-ROTA"}')
$e12r1 = Get-Psql "select coalesce(id,-1) from sigov.educacao_transporte_rota where tenant_id=910001 and codigo='C1S8-ROTA' order by id desc limit 1;"
$e12db1 = Get-Psql ("select status||'|'||coalesce(escola_id,-1)||'|'||turno||'|'||tipo from sigov.educacao_transporte_rota where tenant_id=910001 and id=" + $e12r1.text + " limit 1;")
Add-Check 'E12' 'rota.lancada-com-escola-e-turno' (($e12l1.code -eq 200) -and ($e12db1.text -like ("ATIVA|" + $e12esc.text + "|MANHA|ESCOLAR"))) ("POST transporte/rotas/lancar (escola + turno MANHA) -> $($e12l1.code); banco status|escola|turno|tipo: $($e12db1.text)")
$e12l2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/rotas/lancar' -Base $ApiBase -Body '{"nome":"Rota sem vinculo","turno":"MANHA"}'
Add-Check 'E12' 'rota.sem-escola-nem-regiao-400' (($e12l2.code -eq 400) -and ($e12l2.body -like '*ROTA_ESCOLA_OU_REGIAO_INVALIDA*')) ("sem escolaId e sem regiao -> $($e12l2.code) (400 ROTA_ESCOLA_OU_REGIAO_INVALIDA nomeada; exatamente uma das duas)")
$e12l3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/rotas/lancar' -Base $ApiBase -Body ('{"nome":"Rota dupla","turno":"MANHA","escolaId":' + $e12esc.text + ',"regiao":"REGIONAL-ALFA"}')
Add-Check 'E12' 'rota.com-escola-e-regiao-400' (($e12l3.code -eq 400) -and ($e12l3.body -like '*ROTA_ESCOLA_OU_REGIAO_INVALIDA*')) ("escolaId e regiao juntos -> $($e12l3.code) (400 ROTA_ESCOLA_OU_REGIAO_INVALIDA nomeada)")
$e12l4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/rotas/lancar' -Base $ApiBase -Body '{"nome":"Rota C1S8 Regional Alfa","turno":"TARDE","regiao":"REGIONAL-ALFA","codigo":"C1S8-REG"}'
$e12r2 = Get-Psql "select coalesce(id,-1) from sigov.educacao_transporte_rota where tenant_id=910001 and codigo='C1S8-REG' order by id desc limit 1;"
$e12db2 = Get-Psql ("select coalesce(escola_id,-1)||'|'||coalesce(dados->>'regiao','(null)')||'|'||status from sigov.educacao_transporte_rota where tenant_id=910001 and id=" + $e12r2.text + " limit 1;")
Add-Check 'E12' 'rota.regional-sem-escola-200-multi-esfera' (($e12l4.code -eq 200) -and ($e12db2.text -eq "-1|REGIONAL-ALFA|ATIVA")) ("rota por regiao sem escola -> $($e12l4.code); banco escola|regiao|status: $($e12db2.text) (regra 24: orgao/superintendencia/regiao atendidos sem unidade escolar)")
$e12l5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/rotas/lancar' -Base $ApiBase -Body '{"nome":"Rota fantasma","turno":"MANHA","escolaId":9999999999}'
Add-Check 'E12' 'rota.escola-inexistente-404' (($e12l5.code -eq 404) -and ($e12l5.body -like '*ESCOLA_INEXISTENTE*')) ("escola 9999999999 -> $($e12l5.code) (404 ESCOLA_INEXISTENTE nomeada; validacao de escopo institucional reutilizada)")
$e12p1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/pontos/lancar" -Base $ApiBase -Body '{"nome":"Parada A - Unidade escolar","ordem":1,"descricao":"Embarque na unidade"}'
$e12pid1 = Get-Psql ("select id from sigov.educacao_transporte_rota_ponto where tenant_id=910001 and rota_id=" + $e12r1.text + " order by id desc limit 1;")
$e12db3 = Get-Psql ("select coalesce(numero,0)||'|'||coalesce(rota_id,-1)||'|'||status||'|'||coalesce(turno,'(null)') from sigov.educacao_transporte_rota_ponto where tenant_id=910001 and id=" + $e12pid1.text + " limit 1;")
Add-Check 'E12' 'ponto.lancado-com-ordem' (($e12p1.code -eq 200) -and ($e12db3.text -like ("1|" + $e12r1.text + "|ATIVA|MANHA"))) ("POST rotas/{id}/pontos/lancar (ordem 1) -> $($e12p1.code); banco numero|rota|status|turno: $($e12db3.text) (turno herdado da rota)")
$e12p2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/pontos/lancar" -Base $ApiBase -Body '{"nome":"Parada A bis","ordem":1}'
Add-Check 'E12' 'ponto.ordem-duplicada-409-nomeada' (($e12p2.code -eq 409) -and ($e12p2.body -like '*PONTO_ORDENACAO_EXISTENTE*')) ("segundo ponto na ordem 1 da mesma rota -> $($e12p2.code) (409 PONTO_ORDENACAO_EXISTENTE nomeada; checagem previa)")
$e12bsp = Get-Psql ("insert into sigov.educacao_transporte_rota_ponto(tenant_id,entidade_id,rota_id,nome,numero,turno,status) select 910001,1," + $e12r1.text + ",'C1S8-BS',2,'MANHA','ATIVA' union all select 910001,1," + $e12r1.text + ",'C1S8-BS',2,'MANHA','ATIVA';")
Add-Check 'E12' 'ponto.indice-backstop-unicidade' (($e12bsp.code -eq 3) -and ($e12bsp.text -like '*ux_transporte_ponto_tenant_rota_ordem*')) ("insert SQL bruto de 2 pontos na ordem 2 da mesma rota -> exit=$($e12bsp.code) citando ux_transporte_ponto_tenant_rota_ordem (indice unico parcial como backstop transacional)")
$e12p3 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/pontos/lancar" -Base $ApiBase -Body '{"nome":"Ordem zero","ordem":0}'
Add-Check 'E12' 'ponto.ordem-invalida-400' (($e12p3.code -eq 400) -and ($e12p3.body -like '*PONTO_ORDEM_INVALIDA*')) ("ordem 0 -> $($e12p3.code) (400 PONTO_ORDEM_INVALIDA nomeada; ordens sao inteiros positivos)")
$e12p4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/rotas/9999999999/pontos/lancar' -Base $ApiBase -Body '{"nome":"Fantasma","ordem":1}'
Add-Check 'E12' 'ponto.rota-inexistente-404' (($e12p4.code -eq 404) -and ($e12p4.body -like '*TRANSPORTE_ROTA_INEXISTENTE*')) ("rota 9999999999 -> $($e12p4.code) (404 TRANSPORTE_ROTA_INEXISTENTE nomeada)")
$e12v1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/alunos/vincular" -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + '}')
$e12db4 = Get-Psql ("select coalesce(aluno_id,-1)||'|'||coalesce(rota_id,-1)||'|'||turno||'|'||coalesce(referencia_id,-1)||'|'||status from sigov.educacao_transporte_aluno where tenant_id=910001 and rota_id=" + $e12r1.text + " order by id desc limit 1;")
Add-Check 'E12' 'vinculo.aluno-a-rota-ativa' (($e12v1.code -eq 200) -and ($e12db4.text -like ($e12alA.text + "|" + $e12r1.text + "|MANHA|-1|ATIVA"))) ("POST rotas/{id}/alunos/vincular (aluno A; turno herdado da rota) -> $($e12v1.code); banco aluno|rota|turno|referencia|status: $($e12db4.text)")
$e12v2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/alunos/vincular" -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + '}')
Add-Check 'E12' 'vinculo.duplicado-409-nomeada' (($e12v2.code -eq 409) -and ($e12v2.body -like '*ALUNO_JA_VINCULADO_A_ROTA*')) ("vincular o mesmo aluno na mesma rota/turno -> $($e12v2.code) (409 ALUNO_JA_VINCULADO_A_ROTA nomeada; checagem previa)")
$e12bsa = Get-Psql ("insert into sigov.educacao_transporte_aluno(tenant_id,entidade_id,aluno_id,rota_id,turno,status) select 910001,1," + $e12alA.text + "," + $e12r1.text + ",'MANHA','ATIVA';")
Add-Check 'E12' 'vinculo.indice-backstop-aluno-rota-turno' (($e12bsa.code -eq 3) -and ($e12bsa.text -like '*ux_educacao_transporte_aluno_rota_turno*')) ("insert SQL bruto duplicando o vinculo vigente -> exit=$($e12bsa.code) citando ux_educacao_transporte_aluno_rota_turno (indice rc50_48 como backstop transacional)")
$e12v3 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/alunos/vincular" -Base $ApiBase -Body ('{"alunoId":' + $e12alB.text + ',"turno":"NOITE"}')
Add-Check 'E12' 'vinculo.turno-divergente-400' (($e12v3.code -eq 400) -and ($e12v3.body -like '*TURNO_VINCULO_DIVERGENTE*')) ("aluno B com turno NOITE na rota MANHA -> $($e12v3.code) (400 TURNO_VINCULO_DIVERGENTE nomeada)")
$e12v4 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/alunos/vincular" -Base $ApiBase -Body '{"alunoId":9999999999}'
Add-Check 'E12' 'vinculo.aluno-inexistente-404' (($e12v4.code -eq 404) -and ($e12v4.body -like '*TRANSPORTE_ALUNO_INEXISTENTE*')) ("aluno 9999999999 -> $($e12v4.code) (404 TRANSPORTE_ALUNO_INEXISTENTE nomeada)")
$e12p5 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r2.text)/pontos/lancar" -Base $ApiBase -Body '{"nome":"Parada Regional","ordem":1}'
$e12pid2 = Get-Psql ("select id from sigov.educacao_transporte_rota_ponto where tenant_id=910001 and rota_id=" + $e12r2.text + " order by id desc limit 1;")
$e12v5 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/alunos/vincular" -Base $ApiBase -Body ('{"alunoId":' + $e12alB.text + ',"pontoId":' + $e12pid2.text + '}')
Add-Check 'E12' 'vinculo.ponto-de-outra-rota-409' (($e12p5.code -eq 200) -and ($e12v5.code -eq 409) -and ($e12v5.body -like '*PONTO_ROTA_DIVERGENTE*')) ("ponto lancado na rota regional e usado no vinculo da rota escolar -> $($e12v5.code) (409 PONTO_ROTA_DIVERGENTE nomeada)")
$e12v6 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r1.text)/alunos/vincular" -Base $ApiBase -Body ('{"alunoId":' + $e12alB.text + ',"pontoId":' + $e12pid1.text + '}')
$e12db5 = Get-Psql ("select coalesce(aluno_id,-1)||'|'||coalesce(referencia_id,-1)||'|'||status from sigov.educacao_transporte_aluno where tenant_id=910001 and rota_id=" + $e12r1.text + " order by id desc limit 1;")
Add-Check 'E12' 'vinculo.com-ponto-da-propria-rota-200' (($e12v6.code -eq 200) -and ($e12db5.text -like ($e12alB.text + "|" + $e12pid1.text + "|ATIVA"))) ("aluno B na rota com o ponto da propria rota -> $($e12v6.code); banco aluno|referencia(ponto)|status: $($e12db5.text) (referencia_id guarda o ponto)")
$e12dash = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/transporte/dashboard' -Base $ApiBase
$e12dj = $null; try { $e12dj = ($e12dash.body | ConvertFrom-Json) } catch {}
$e12dR = 0; $e12dP = 0; $e12dV = 0; $e12dE = 0
if ($e12dj -and $null -ne $e12dj.data) { $e12dR = [long]$e12dj.data.rotasAtivas; $e12dP = [long]$e12dj.data.pontosAtivos; $e12dV = [long]$e12dj.data.alunosVinculados; $e12dE = [long]$e12dj.data.escolasAtendidas }
Add-Check 'E12' 'dashboard.metricas-dedicadas' (($e12dash.code -eq 200) -and ($e12dR -ge 2) -and ($e12dP -ge 2) -and ($e12dV -ge 2) -and ($e12dE -ge 1)) ("GET transporte/dashboard -> $($e12dash.code); rotasAtivas/pontosAtivos/alunosVinculados/escolasAtendidas: $e12dR/$e12dP/$e12dV/$e12dE (DTO dedicado EducacaoTransporteDashboardDto sobre dados reais do tenant)")
$e12rc = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/operacoes/educacao_transporte_rota/$($e12r2.text)/status?status=CANCELADA&justificativa=c1-s8-cancela-rota-regional" -Base $ApiBase
$e12p6 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/transporte/rotas/$($e12r2.text)/pontos/lancar" -Base $ApiBase -Body '{"nome":"Ponto apos cancelamento","ordem":2}'
Add-Check 'E12' 'maquina.rota-cancelada-bloqueia-operacoes' (($e12rc.code -eq 200) -and ($e12p6.code -eq 409) -and ($e12p6.body -like '*TRANSPORTE_ROTA_NAO_VIGENTE*')) ("rota CANCELADA via maquina de estados S2 -> $($e12rc.code); novo ponto na rota cancelada -> $($e12p6.code) (409 TRANSPORTE_ROTA_NAO_VIGENTE nomeada)")

# ===========================================================================
# E13 — Sprint 9: educacao/fundeb_custos_indicadores (FDEB + centro de custo)
# ---------------------------------------------------------------------------
# POST /api/educacao/fundeb/parametros/lancar {codigo,valor}: um codigo vigente
# por tenant (409 nomeada + indice ux_fundeb_parametro_tenant_codigo backstop);
# a taxa minima de aplicacao (multi-esfera, regra 24) vem do banco, nunca
# hardcoded. POST /api/educacao/fundeb/receitas/lancar {competencia,fonte,valor}:
# uma receita vigente por competencia/fonte/tenant (409 nomeada + indice
# ux_fundeb_receita_tenant_competencia_fonte backstop). POST
# /api/educacao/fundeb/aplicacoes/lancar {centroCustoId,valor}: centro de custo
# ATIVO exigido (404/409 nomeadas). POST /api/educacao/custos/centros/lancar
# {codigo,nome}: um codigo vigente por tenant (409 nomeada + indice
# ux_custo_centro_tenant_codigo backstop). GET /api/educacao/indicadores/
# dashboard: DTO dedicado EducacaoIndicadoresDashboardDto com conformidade
# (taxa de aplicacao x taxa minima lida do parametro do banco). Maquina de
# estados S2: receita CANCELADA sai da soma e libera a unicidade; centro
# CANCELADO bloqueia novas aplicacoes. Falhas nomeadas (regra 13); multi-esfera
# (regra 24).
# ===========================================================================
$e13fix = Get-Psql @"
delete from sigov.educacao_fundeb_aplicacao where tenant_id=910001;
delete from sigov.educacao_fundeb_receita where tenant_id=910001;
delete from sigov.educacao_fundeb_parametro where tenant_id=910001;
delete from sigov.educacao_custo_lancamento where tenant_id=910001;
delete from sigov.educacao_custo_centro where tenant_id=910001;
select 'ok';
"@
Add-Check 'E13' 'fixture.s9-reset' ($e13fix.code -eq 0) ("reset das tabelas fundeb/custo do tenant 910001 (vazias antes do C1; garante estado limpo para re-execucao) exit=$($e13fix.code)")
$e13p1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/parametros/lancar' -Base $ApiBase -Body '{"codigo":"taxa_minima_aplicacao","valor":0.5}'
$e13db1 = Get-Psql "select codigo||'|'||valor::text||'|'||status from sigov.educacao_fundeb_parametro where tenant_id=910001 and upper(codigo)='TAXA_MINIMA_APLICACAO' order by id desc limit 1;"
Add-Check 'E13' 'parametro.lancado-taxa-minima-multi-esfera' (($e13p1.code -eq 200) -and ($e13db1.text -eq 'TAXA_MINIMA_APLICACAO|0.50|ATIVA')) ("POST fundeb/parametros/lancar (codigo minusculo normalizado p/ maiusculo) -> $($e13p1.code); banco codigo|valor|status: $($e13db1.text) (taxa minima da esfera vem do banco, nunca hardcoded - regra 24)")
$e13p2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/parametros/lancar' -Base $ApiBase -Body '{"codigo":"TAXA_MINIMA_APLICACAO","valor":0.7}'
Add-Check 'E13' 'parametro.duplicado-codigo-409-nomeada' (($e13p2.code -eq 409) -and ($e13p2.body -like '*FUNDEB_PARAMETRO_CODIGO_EXISTENTE*')) ("segundo parametro com o mesmo codigo (case-insensitive) -> $($e13p2.code) (409 FUNDEB_PARAMETRO_CODIGO_EXISTENTE nomeada; checagem previa)")
$e13bsp = Get-Psql "insert into sigov.educacao_fundeb_parametro(tenant_id,codigo,nome,tipo,valor,status) select 910001,'C1S9-PBS','Backstop C1S9','FDEB_PARAMETRO',1,'ATIVA' union all select 910001,'C1S9-PBS','Backstop C1S9','FDEB_PARAMETRO',1,'ATIVA';"
Add-Check 'E13' 'parametro.indice-backstop-codigo' (($e13bsp.code -eq 3) -and ($e13bsp.text -like '*ux_fundeb_parametro_tenant_codigo*')) ("insert SQL bruto duplicando o codigo C1S9-PBS -> exit=$($e13bsp.code) citando ux_fundeb_parametro_tenant_codigo (indice unico parcial como backstop transacional)")
$e13r1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body '{"competencia":"2026-01-15","fonte":"fpe","valor":10000}'
$e13rid = Get-Psql "select coalesce(id,-1) from sigov.educacao_fundeb_receita where tenant_id=910001 and tipo='FPE' order by id desc limit 1;"
$e13db2 = Get-Psql ("select to_char(competencia,'YYYY-MM-DD')||'|'||tipo||'|'||valor::text||'|'||status from sigov.educacao_fundeb_receita where tenant_id=910001 and id=" + $e13rid.text + ";")
Add-Check 'E13' 'receita.lancada-fpe-competencia' (($e13r1.code -eq 200) -and ($e13db2.text -eq '2026-01-15|FPE|10000.00|ATIVA')) ("POST fundeb/receitas/lancar (competencia 2026-01-15 + fonte fpe normalizada) -> $($e13r1.code); banco competencia|tipo|valor|status: $($e13db2.text)")
$e13r2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body '{"competencia":"2026-01-15","fonte":"FPE","valor":9999}'
Add-Check 'E13' 'receita.duplicada-competencia-fonte-409-nomeada' (($e13r2.code -eq 409) -and ($e13r2.body -like '*FUNDEB_RECEITA_DUPLICADA*')) ("mesma competencia/fonte (case-insensitive) -> $($e13r2.code) (409 FUNDEB_RECEITA_DUPLICADA nomeada; uma receita vigente por competencia/fonte/tenant)")
$e13bsr = Get-Psql "insert into sigov.educacao_fundeb_receita(tenant_id,entidade_id,competencia,tipo,valor,status) select 910001,1,date '2026-02-10','FNDE',100,'ATIVA' union all select 910001,1,date '2026-02-10','FNDE',100,'ATIVA';"
Add-Check 'E13' 'receita.indice-backstop-competencia-fonte' (($e13bsr.code -eq 3) -and ($e13bsr.text -like '*ux_fundeb_receita_tenant_competencia_fonte*')) ("insert SQL bruto duplicando competencia/fonte -> exit=$($e13bsr.code) citando ux_fundeb_receita_tenant_competencia_fonte (backstop transacional)")
$e13r3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body '{"competencia":"2026-01-15","fonte":"","valor":100}'
Add-Check 'E13' 'receita.sem-fonte-400' (($e13r3.code -eq 400) -and ($e13r3.body -like '*exige fonte*')) ("fonte vazia -> $($e13r3.code) (400 validacao explicita; fonte identifica a origem do recurso: FPE/FNDE/receita propria...)")
$e13r4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body '{"competencia":"0001-01-01","fonte":"FPE","valor":100}'
Add-Check 'E13' 'receita.competencia-invalida-400' (($e13r4.code -eq 400) -and ($e13r4.body -like '*Receita FDEB exige*')) ("competencia 0001-01-01 -> $($e13r4.code) (400 validacao explicita de data)")
$e13c1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/centros/lancar' -Base $ApiBase -Body '{"codigo":"c1s9-cc1","nome":"Centro C1S9 Manutencao e Desenvolvimento"}'
$e13cc1 = Get-Psql "select coalesce(id,-1) from sigov.educacao_custo_centro where tenant_id=910001 and upper(codigo)='C1S9-CC1' order by id desc limit 1;"
$e13db3 = Get-Psql ("select codigo||'|'||status||'|'||tipo from sigov.educacao_custo_centro where tenant_id=910001 and id=" + $e13cc1.text + " limit 1;")
Add-Check 'E13' 'centro.lancado-ativo-com-codigo' (($e13c1.code -eq 200) -and ($e13db3.text -eq 'C1S9-CC1|ATIVA|CUSTO_EDUCACAO')) ("POST custos/centros/lancar (codigo minusculo normalizado) -> $($e13c1.code); banco codigo|status|tipo: $($e13db3.text)")
$e13c2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/centros/lancar' -Base $ApiBase -Body '{"codigo":"C1S9-CC1","nome":"Centro C1S9 bis"}'
Add-Check 'E13' 'centro.duplicado-codigo-409-nomeada' (($e13c2.code -eq 409) -and ($e13c2.body -like '*CUSTO_CENTRO_CODIGO_EXISTENTE*')) ("segundo centro com o mesmo codigo (case-insensitive) -> $($e13c2.code) (409 CUSTO_CENTRO_CODIGO_EXISTENTE nomeada; checagem previa)")
$e13bsc = Get-Psql "insert into sigov.educacao_custo_centro(tenant_id,codigo,nome,tipo,status) select 910001,'C1S9-CBS','Backstop C1S9','CUSTO_EDUCACAO','ATIVA' union all select 910001,'C1S9-CBS','Backstop C1S9','CUSTO_EDUCACAO','ATIVA';"
Add-Check 'E13' 'centro.indice-backstop-codigo' (($e13bsc.code -eq 3) -and ($e13bsc.text -like '*ux_custo_centro_tenant_codigo*')) ("insert SQL bruto duplicando o codigo C1S9-CBS -> exit=$($e13bsc.code) citando ux_custo_centro_tenant_codigo (backstop transacional)")
$e13frIns = Get-Psql "insert into sigov.educacao_custo_centro(tenant_id,codigo,nome,tipo,status) values(910001,'C1S9-RASC','Centro C1S9 Rascunho','CUSTO_EDUCACAO','RASCUNHO');"
$e13ccR = Get-Psql "select coalesce(id,-1) from sigov.educacao_custo_centro where tenant_id=910001 and upper(codigo)='C1S9-RASC';"
$e13a1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e13ccR.text + ',"valor":500}')
Add-Check 'E13' 'aplicacao.centro-rascunho-409-nomeada' (($e13frIns.code -eq 0) -and ($e13a1.code -eq 409) -and ($e13a1.body -like '*CUSTO_CENTRO_NAO_VIGENTE*')) ("aplicacao em centro RASCUNHO (fixture SQL) -> $($e13a1.code) (409 CUSTO_CENTRO_NAO_VIGENTE nomeada; apenas centros ativos aceitam aplicacao)")
$e13a2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body '{"centroCustoId":9999999999,"valor":500}'
Add-Check 'E13' 'aplicacao.centro-inexistente-404-nomeada' (($e13a2.code -eq 404) -and ($e13a2.body -like '*CUSTO_CENTRO_INEXISTENTE*')) ("centro 9999999999 -> $($e13a2.code) (404 CUSTO_CENTRO_INEXISTENTE nomeada)")
$e13a3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e13cc1.text + ',"valor":6000}')
$e13db4 = Get-Psql "select coalesce(centro_custo_id,-1)||'|'||valor::text||'|'||status from sigov.educacao_fundeb_aplicacao where tenant_id=910001 order by id desc limit 1;"
Add-Check 'E13' 'aplicacao.lancada-centro-ativo' (($e13a3.code -eq 200) -and ($e13db4.text -like ($e13cc1.text + '|6000.00|ATIVA'))) ("POST fundeb/aplicacoes/lancar (centro ativo, valor 6000) -> $($e13a3.code); banco centro_custo_id|valor|status: $($e13db4.text)")
$e13a4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e13cc1.text + ',"valor":0}')
Add-Check 'E13' 'aplicacao.valor-invalido-400' (($e13a4.code -eq 400) -and ($e13a4.body -like '*positivo*')) ("valor 0 -> $($e13a4.code) (400 validacao explicita: valor deve ser positivo)")
$e13dash1 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/indicadores/dashboard' -Base $ApiBase
$e13dj1 = $null; try { $e13dj1 = ($e13dash1.body | ConvertFrom-Json) } catch {}
$e13d1Rec = 0; $e13d1Apl = 0; $e13d1Taxa = 0; $e13d1Min = $null; $e13d1Ok = $false; $e13d1Cpa = 0; $e13d1Inc = 0
if ($e13dj1 -and $null -ne $e13dj1.data) { $e13d1Rec = [decimal]$e13dj1.data.receitasFundeb; $e13d1Apl = [decimal]$e13dj1.data.aplicacoesFundeb; $e13d1Taxa = [decimal]$e13dj1.data.taxaAplicacao; if ($null -ne $e13dj1.data.taxaMinima) { $e13d1Min = [decimal]$e13dj1.data.taxaMinima }; $e13d1Ok = [bool]$e13dj1.data.emConformidade; $e13d1Cpa = [decimal]$e13dj1.data.custoPorAluno; $e13d1Inc = [long]$e13dj1.data.inconsistencias }
Add-Check 'E13' 'dashboard.conformidade-fundeb-acima-do-minimo' (($e13dash1.code -eq 200) -and ($e13d1Rec -eq 10000) -and ($e13d1Apl -eq 6000) -and ($e13d1Taxa -eq 0.6) -and ($null -ne $e13d1Min) -and ($e13d1Min -eq 0.5) -and $e13d1Ok -and ($e13d1Cpa -eq 0)) ("GET indicadores/dashboard -> $($e13dash1.code); receitasFundeb/aplicacoesFundeb/taxaAplicacao/taxaMinima/emConformidade/custoPorAluno/inconsistencias: $e13d1Rec/$e13d1Apl/$e13d1Taxa/$e13d1Min/$e13d1Ok/$e13d1Cpa/$e13d1Inc (DTO dedicado EducacaoIndicadoresDashboardDto; taxa minima lida do parametro do banco - regra 24)")
$e13rc = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/operacoes/educacao_fundeb_receita/" + $e13rid.text + "/status?status=CANCELADA&justificativa=c1-s9-receita-fpe-cancelada") -Base $ApiBase
$e13dash2 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/indicadores/dashboard' -Base $ApiBase
$e13dj2 = $null; try { $e13dj2 = ($e13dash2.body | ConvertFrom-Json) } catch {}
$e13d2Rec = 0; $e13d2Apl = 0; $e13d2Taxa = 0; $e13d2Ok = $true
if ($e13dj2 -and $null -ne $e13dj2.data) { $e13d2Rec = [decimal]$e13dj2.data.receitasFundeb; $e13d2Apl = [decimal]$e13dj2.data.aplicacoesFundeb; $e13d2Taxa = [decimal]$e13dj2.data.taxaAplicacao; $e13d2Ok = [bool]$e13dj2.data.emConformidade }
Add-Check 'E13' 'maquina.receita-cancelada-sai-da-soma' (($e13rc.code -eq 200) -and ($e13dash2.code -eq 200) -and ($e13d2Rec -eq 0) -and ($e13d2Apl -eq 6000) -and ($e13d2Taxa -eq 0) -and (-not $e13d2Ok)) ("receita CANCELADA via maquina de estados S2 -> $($e13rc.code); dashboard receitas/aplicacoes/taxa/emConformidade: $e13d2Rec/$e13d2Apl/$e13d2Taxa/$e13d2Ok (estado terminal sai da soma vigente; sem receita vigente a conformidade fica falsa)")
$e13rl = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body '{"competencia":"2026-01-15","fonte":"FPE","valor":10000}'
Add-Check 'E13' 'unicidade.receita-cancelada-libera-o-indice' ($e13rl.code -eq 200) ("novo lancamento com a mesma competencia/fonte apos o cancelamento -> $($e13rl.code) (estado terminal liberou a unicidade parcial do indice)")
$e13ccc = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/operacoes/educacao_custo_centro/" + $e13cc1.text + "/status?status=CANCELADA&justificativa=c1-s9-centro-manutencao-cancelado") -Base $ApiBase
$e13a5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e13cc1.text + ',"valor":700}')
Add-Check 'E13' 'maquina.centro-cancelado-bloqueia-aplicacao' (($e13ccc.code -eq 200) -and ($e13a5.code -eq 409) -and ($e13a5.body -like '*CUSTO_CENTRO_NAO_VIGENTE*')) ("centro CANCELADO via maquina de estados S2 -> $($e13ccc.code); nova aplicacao no centro cancelado -> $($e13a5.code) (409 CUSTO_CENTRO_NAO_VIGENTE nomeada)")

# ===========================================================================
# E14 — Sprint 10: educacao/indicadores + educacenso + projecao de matricula
# ---------------------------------------------------------------------------
# POST /api/educacao/indicadores/catalogo/lancar {codigo,nome,tipo?,descricao?}:
# um indicador vigente por codigo/tenant (409 nomeada + indice
# ux_indicador_tenant_codigo backstop; codigo normalizado p/ maiusculo). POST
# /api/educacao/indicadores/valores/lancar {indicadorId,ano,valor}: indicador
# ATIVO exigido (404/409 nomeadas; ano em [2000,2100]); um valor vigente por
# indicador x ano x tenant (409 nomeada + indice ux_indicador_valor_tenant_
# ref_ano backstop; referencia_id guarda o indicador, ano_letivo o ano). POST
# /api/educacao/projecoes-matricula/lancar {escolaId,ano,quantidade}: escopo
# institucional reutilizado (S2); uma projecao vigente por escola x ano x
# tenant (409 + backstop ux_projecao_matricula_tenant_escola_ano). POST
# /api/educacao/educacenso/lotes/lancar {ano}: um lote vigente por ano x
# tenant (409 + backstop ux_educacenso_lote_tenant_ano). POST
# /api/educacao/educacenso/inconsistencias/lancar {loteId,gravidade,origem,
# recomendacao?}: lote ATIVO exigido (404/409 nomeadas); gravidade restrita a
# BAIXA/MEDIA/ALTA (400); tipo=gravidade e nome=origem (mapeamento declarado),
# criada ABERTA. GET /api/educacao/educacenso/dashboard: DTO dedicado
# EducacaoEducacensoDashboardDto (lotesAtivos/inconsistenciasAbertas/
# gravidadeAlta/eventos). Maquina S2: lote CANCELADA libera o ano para novo
# lote. Falhas nomeadas (regra 13); multi-esfera (regra 24).
# ===========================================================================
$e14fix = Get-Psql @"
delete from sigov.educacao_educacenso_inconsistencia where tenant_id=910001;
delete from sigov.educacao_educacenso_lote where tenant_id=910001;
delete from sigov.educacao_indicador_valor where tenant_id=910001;
delete from sigov.educacao_projecao_matricula where tenant_id=910001;
delete from sigov.educacao_indicador where tenant_id=910001;
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao)
select 910001,1,'c1e26-s10-esc','Fixtures C1 Escola S10',coalesce((select tipo_escola from sigov.escola where is_deleted=false limit 1),'OURO'),coalesce((select situacao from sigov.escola where is_deleted=false limit 1),'ATIVA')
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-s10-esc');
select 'ok';
"@
Add-Check 'E14' 'fixture.s10-reset' ($e14fix.code -eq 0) ("reset das tabelas indicadores/educacenso do tenant 910001 (vazias antes do C1; garante estado limpo para re-execucao) + escola c1e26-s10-esc idempotente exit=$($e14fix.code)")
$e14esc = Get-Psql "select coalesce(id,-1) from sigov.escola where tenant_id=910001 and codigo='c1e26-s10-esc';"
$e14i1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/catalogo/lancar' -Base $ApiBase -Body '{"codigo":"matr_por_escola","nome":"Matriculas por escola","tipo":"planejamento"}'
$e14ind = Get-Psql "select coalesce(id,-1) from sigov.educacao_indicador where tenant_id=910001 and upper(codigo)='MATR_POR_ESCOLA' order by id desc limit 1;"
$e14db1 = Get-Psql ("select codigo||'|'||status||'|'||tipo from sigov.educacao_indicador where tenant_id=910001 and id=" + $e14ind.text + " limit 1;")
Add-Check 'E14' 'indicador.lancado-codigo-normalizado' (($e14i1.code -eq 200) -and ($e14db1.text -ceq 'MATR_POR_ESCOLA|ATIVA|PLANEJAMENTO')) ("POST indicadores/catalogo/lancar (codigo e tipo minusculos normalizados p/ maiusculos; assertao case-sensitive) -> $($e14i1.code); banco codigo|status|tipo: $($e14db1.text)")
$e14i2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/catalogo/lancar' -Base $ApiBase -Body '{"codigo":"MATR_POR_ESCOLA","nome":"Matriculas por escola bis","tipo":"PLANEJAMENTO"}'
Add-Check 'E14' 'indicador.duplicado-codigo-409-nomeada' (($e14i2.code -eq 409) -and ($e14i2.body -like '*INDICADOR_CODIGO_EXISTENTE*')) ("segundo indicador com o mesmo codigo (case-insensitive) -> $($e14i2.code) (409 INDICADOR_CODIGO_EXISTENTE nomeada)")
$e14bsi = Get-Psql "insert into sigov.educacao_indicador(tenant_id,codigo,nome,tipo,status) select 910001,'C1S10-IBS','Backstop C1S10','PLANEJAMENTO','ATIVA' union all select 910001,'C1S10-IBS','Backstop C1S10','PLANEJAMENTO','ATIVA';"
Add-Check 'E14' 'indicador.indice-backstop-codigo' (($e14bsi.code -eq 3) -and ($e14bsi.text -like '*ux_indicador_tenant_codigo*')) ("insert SQL bruto duplicando o codigo C1S10-IBS -> exit=$($e14bsi.code) citando ux_indicador_tenant_codigo (backstop transacional)")
$e14v1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/valores/lancar' -Base $ApiBase -Body ('{"indicadorId":' + $e14ind.text + ',"ano":2026,"valor":150}')
$e14db2 = Get-Psql "select coalesce(referencia_id,-1)||'|'||coalesce(ano_letivo,-1)||'|'||tipo||'|'||valor::text||'|'||status from sigov.educacao_indicador_valor where tenant_id=910001 order by id desc limit 1;"
Add-Check 'E14' 'valor.lancado-indicador-ano' (($e14v1.code -eq 200) -and ($e14db2.text -like ($e14ind.text + '|2026|INDICADOR_VALOR|150.00|ATIVA'))) ("POST indicadores/valores/lancar (indicador ATIVO, ano 2026) -> $($e14v1.code); banco referencia(indicador)|ano|tipo|valor|status: $($e14db2.text)")
$e14v2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/valores/lancar' -Base $ApiBase -Body ('{"indicadorId":' + $e14ind.text + ',"ano":2026,"valor":999}')
Add-Check 'E14' 'valor.duplicado-indicador-ano-409-nomeada' (($e14v2.code -eq 409) -and ($e14v2.body -like '*VALOR_INDICADOR_EXISTENTE*')) ("segundo valor do mesmo indicador no mesmo ano -> $($e14v2.code) (409 VALOR_INDICADOR_EXISTENTE nomeada; um valor vigente por indicador x ano x tenant)")
$e14bsv = Get-Psql ("insert into sigov.educacao_indicador_valor(tenant_id,referencia_id,ano_letivo,tipo,valor,status) select 910001," + $e14ind.text + ",2027,'INDICADOR_VALOR',1,'ATIVA' union all select 910001," + $e14ind.text + ",2027,'INDICADOR_VALOR',1,'ATIVA';")
Add-Check 'E14' 'valor.indice-backstop-ref-ano' (($e14bsv.code -eq 3) -and ($e14bsv.text -like '*ux_indicador_valor_tenant_ref_ano*')) ("insert SQL bruto duplicando indicador x ano 2027 -> exit=$($e14bsv.code) citando ux_indicador_valor_tenant_ref_ano (backstop transacional)")
$e14indRIns = Get-Psql "insert into sigov.educacao_indicador(tenant_id,codigo,nome,tipo,status) values(910001,'C1S10-RASC','Indicador Rascunho','PLANEJAMENTO','RASCUNHO');"
$e14indR = Get-Psql "select coalesce(id,-1) from sigov.educacao_indicador where tenant_id=910001 and upper(codigo)='C1S10-RASC';"
$e14v3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/valores/lancar' -Base $ApiBase -Body ('{"indicadorId":' + $e14indR.text + ',"ano":2026,"valor":10}')
Add-Check 'E14' 'valor.indicador-rascunho-409-nomeada' (($e14indRIns.code -eq 0) -and ($e14v3.code -eq 409) -and ($e14v3.body -like '*INDICADOR_NAO_VIGENTE*')) ("valor em indicador RASCUNHO (fixture SQL) -> $($e14v3.code) (409 INDICADOR_NAO_VIGENTE nomeada; apenas indicadores ativos aceitam valor)")
$e14v4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/valores/lancar' -Base $ApiBase -Body '{"indicadorId":9999999999,"ano":2026,"valor":10}'
Add-Check 'E14' 'valor.indicador-inexistente-404-nomeada' (($e14v4.code -eq 404) -and ($e14v4.body -like '*INDICADOR_INEXISTENTE*')) ("indicador 9999999999 -> $($e14v4.code) (404 INDICADOR_INEXISTENTE nomeada)")
$e14v5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/indicadores/valores/lancar' -Base $ApiBase -Body ('{"indicadorId":' + $e14ind.text + ',"ano":1999,"valor":10}')
Add-Check 'E14' 'valor.ano-invalido-400' (($e14v5.code -eq 400) -and ($e14v5.body -like '*entre 2000 e 2100*')) ("ano 1999 -> $($e14v5.code) (400 validacao explicita de janela de ano)")
$e14pr1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/projecoes-matricula/lancar' -Base $ApiBase -Body ('{"escolaId":' + $e14esc.text + ',"ano":2026,"quantidade":30}')
$e14db3 = Get-Psql "select coalesce(escola_id,-1)||'|'||coalesce(ano_letivo,-1)||'|'||round(quantidade,0)::text||'|'||status from sigov.educacao_projecao_matricula where tenant_id=910001 order by id desc limit 1;"
Add-Check 'E14' 'projecao.lancada-escola-ano' (($e14pr1.code -eq 200) -and ($e14db3.text -like ($e14esc.text + '|2026|30|ATIVA'))) ("POST projecoes-matricula/lancar (escola no escopo institucional, ano 2026, 30 vagas) -> $($e14pr1.code); banco escola|ano|quantidade|status: $($e14db3.text) (planejamento de oferta valido para qualquer esfera - regra 24)")
$e14pr2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/projecoes-matricula/lancar' -Base $ApiBase -Body ('{"escolaId":' + $e14esc.text + ',"ano":2026,"quantidade":45}')
Add-Check 'E14' 'projecao.duplicada-escola-ano-409-nomeada' (($e14pr2.code -eq 409) -and ($e14pr2.body -like '*PROJECAO_MATRICULA_EXISTENTE*')) ("segunda projecao da mesma escola no mesmo ano -> $($e14pr2.code) (409 PROJECAO_MATRICULA_EXISTENTE nomeada)")
$e14bspr = Get-Psql ("insert into sigov.educacao_projecao_matricula(tenant_id,escola_id,ano_letivo,tipo,quantidade,status) select 910001," + $e14esc.text + ",2029,'PROJECAO_MATRICULA',1,'ATIVA' union all select 910001," + $e14esc.text + ",2029,'PROJECAO_MATRICULA',1,'ATIVA';")
Add-Check 'E14' 'projecao.indice-backstop-escola-ano' (($e14bspr.code -eq 3) -and ($e14bspr.text -like '*ux_projecao_matricula_tenant_escola_ano*')) ("insert SQL bruto duplicando escola x ano 2029 -> exit=$($e14bspr.code) citando ux_projecao_matricula_tenant_escola_ano (backstop transacional)")
$e14lt1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/lotes/lancar' -Base $ApiBase -Body '{"ano":2026}'
$e14lote = Get-Psql "select coalesce(id,-1) from sigov.educacao_educacenso_lote where tenant_id=910001 and ano_letivo=2026 order by id desc limit 1;"
$e14db4 = Get-Psql ("select coalesce(ano_letivo,-1)||'|'||tipo||'|'||status from sigov.educacao_educacenso_lote where tenant_id=910001 and id=" + $e14lote.text + ";")
Add-Check 'E14' 'lote.lancado-ano-2026' (($e14lt1.code -eq 200) -and ($e14db4.text -eq '2026|EDUCACENSO|ATIVA')) ("POST educacenso/lotes/lancar (ano 2026) -> $($e14lt1.code); banco ano|tipo|status: $($e14db4.text)")
$e14lt2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/lotes/lancar' -Base $ApiBase -Body '{"ano":2026}'
Add-Check 'E14' 'lote.duplicado-ano-409-nomeada' (($e14lt2.code -eq 409) -and ($e14lt2.body -like '*LOTE_EDUCACENSO_ANO_EXISTENTE*')) ("segundo lote do mesmo ano -> $($e14lt2.code) (409 LOTE_EDUCACENSO_ANO_EXISTENTE nomeada; um lote vigente por ano x tenant)")
$e14bslt = Get-Psql "insert into sigov.educacao_educacenso_lote(tenant_id,ano_letivo,tipo,status) select 910001,2030,'EDUCACENSO','ATIVA' union all select 910001,2030,'EDUCACENSO','ATIVA';"
Add-Check 'E14' 'lote.indice-backstop-ano' (($e14bslt.code -eq 3) -and ($e14bslt.text -like '*ux_educacenso_lote_tenant_ano*')) ("insert SQL bruto duplicando o ano 2030 -> exit=$($e14bslt.code) citando ux_educacenso_lote_tenant_ano (backstop transacional)")
$e14in1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/inconsistencias/lancar' -Base $ApiBase -Body ('{"loteId":' + $e14lote.text + ',"gravidade":"alta","origem":"matricula","recomendacao":"corrigir registro no censo"}')
$e14db5 = Get-Psql "select coalesce(referencia_id,-1)||'|'||tipo||'|'||nome||'|'||status from sigov.educacao_educacenso_inconsistencia where tenant_id=910001 order by id desc limit 1;"
Add-Check 'E14' 'inconsistencia.lancada-lote-vigente' (($e14in1.code -eq 200) -and ($e14db5.text -like ($e14lote.text + '|ALTA|MATRICULA|ABERTA'))) ("POST educacenso/inconsistencias/lancar (gravidade e origem minusculas normalizadas) -> $($e14in1.code); banco referencia(lote)|tipo(gravidade)|nome(origem)|status: $($e14db5.text)")
$e14in2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/inconsistencias/lancar' -Base $ApiBase -Body '{"loteId":9999999999,"gravidade":"ALTA","origem":"X"}'
Add-Check 'E14' 'inconsistencia.lote-inexistente-404-nomeada' (($e14in2.code -eq 404) -and ($e14in2.body -like '*LOTE_EDUCACENSO_INEXISTENTE*')) ("lote 9999999999 -> $($e14in2.code) (404 LOTE_EDUCACENSO_INEXISTENTE nomeada)")
$e14in3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/inconsistencias/lancar' -Base $ApiBase -Body ('{"loteId":' + $e14lote.text + ',"gravidade":"CRITICA","origem":"X"}')
Add-Check 'E14' 'inconsistencia.gravidade-invalida-400' (($e14in3.code -eq 400) -and ($e14in3.body -like '*MEDIA*')) ("gravidade CRITICA fora do conjunto BAIXA/MEDIA/ALTA -> $($e14in3.code) (400 validacao explicita)")
$e14dash = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/educacenso/dashboard' -Base $ApiBase
$e14dj = $null; try { $e14dj = ($e14dash.body | ConvertFrom-Json) } catch {}
$e14dLotes = 0; $e14dAbertas = 0; $e14dAlta = 0; $e14dEventos = 0
if ($e14dj -and $null -ne $e14dj.data) { $e14dLotes = [long]$e14dj.data.lotesAtivos; $e14dAbertas = [long]$e14dj.data.inconsistenciasAbertas; $e14dAlta = [long]$e14dj.data.gravidadeAlta; $e14dEventos = [long]$e14dj.data.eventos }
Add-Check 'E14' 'censo-dashboard.metricas-dedicadas' (($e14dash.code -eq 200) -and ($e14dLotes -eq 1) -and ($e14dAbertas -eq 1) -and ($e14dAlta -eq 1) -and ($e14dEventos -ge 0)) ("GET educacenso/dashboard -> $($e14dash.code); lotesAtivos/inconsistenciasAbertas/gravidadeAlta/eventos: $e14dLotes/$e14dAbertas/$e14dAlta/$e14dEventos (DTO dedicado EducacaoEducacensoDashboardDto sobre dados reais do tenant)")
$e14rc = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/operacoes/educacao_educacenso_lote/" + $e14lote.text + "/status?status=CANCELADA&justificativa=c1-s10-lote-2026-cancelado") -Base $ApiBase
$e14rl = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/lotes/lancar' -Base $ApiBase -Body '{"ano":2026}'
Add-Check 'E14' 'maquina.lote-cancelada-libera-o-ano' (($e14rc.code -eq 200) -and ($e14rl.code -eq 200)) ("lote 2026 CANCELADA via maquina de estados S2 -> $($e14rc.code); novo lote 2026 apos o cancelamento -> $($e14rl.code) (estado terminal liberou a unicidade parcial do indice)")

# -----------------------------------------------------------------------------
# E15 (Sprint 1 · Ciclo 2 · Merenda integridade transacional): lancamentos
# tipados ABERTURA/ENTRADA/AJUSTE (movimento+saldo na mesma transacao, uma linha
# de saldo por tenant x produto), chave de idempotencia (reenvio = efeito unico,
# conteudo divergente = CONFLITO), estorno de movimento/distribuicao com
# compensacao unica, cancelamento generico bloqueado quando ha efeitos, race
# legado -> uma linha, conciliacao movimento x saldo no encerramento.
# -----------------------------------------------------------------------------
function Get-E15Data([object]$Resp){ $j = $null; try { $j = $Resp.body | ConvertFrom-Json } catch {}; if ($j -and $null -ne $j.data) { return $j.data }; return 0 }
$e15p1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"c2s1-nolinha","nome":"Fixture C2S1 SemLinha","tipo":"ALIMENTO","status":"ATIVA"}'
$e15p2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"c2s1-semestoque","nome":"Fixture C2S1 SemEstoque","tipo":"ALIMENTO","status":"ATIVA"}'
$e15p3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"c2s1-race","nome":"Fixture C2S1 Race","tipo":"ALIMENTO","status":"ATIVA"}'
$pbn = Get-Psql "select coalesce(max(id),-1)::text from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='c2s1-nolinha';"
$pes = Get-Psql "select coalesce(max(id),-1)::text from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='c2s1-semestoque';"
$prc = Get-Psql "select coalesce(max(id),-1)::text from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='c2s1-race';"
Add-Check 'E15' 'produto.fixture-criado' (($e15p1.code -eq 200) -and ($e15p2.code -eq 200) -and ($e15p3.code -eq 200) -and ($pbn.text -ne '-1')) ("POST produtos x3 c2s1-* -> $($e15p1.code)/$($e15p2.code)/$($e15p3.code); ids $($pbn.text)/$($pes.text)/$($prc.text)")
$e15ab = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"ABERTURA","quantidade":5}')
Add-Check 'E15' 'lancamento.abertura' ($e15ab.code -eq 200) ("lancamento ABERTURA 5 -> $($e15ab.code) $($e15ab.body)")
$e15ab2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"ABERTURA","quantidade":5}')
Add-Check 'E15' 'lancamento.abertura-duplicada-rejeitada' (($e15ab2.code -eq 409) -and ($e15ab2.body -like '*ESTOQUE_ABERTURA_EXISTENTE*')) ("segunda ABERTURA -> $($e15ab2.code) $($e15ab2.body)")
$e15en = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"ENTRADA","quantidade":10,"chaveIdempotencia":"C2S1-E15-K2"}')
$idEn = Get-E15Data $e15en
Add-Check 'E15' 'lancamento.entrada-com-chave' (($e15en.code -eq 200) -and ($idEn -ne 0)) ("ENTRADA 10 chave K2 -> $($e15en.code) mov=$idEn")
$e15enr = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"ENTRADA","quantidade":10,"chaveIdempotencia":"C2S1-E15-K2"}')
$idEnr = Get-E15Data $e15enr
Add-Check 'E15' 'lancamento.reenvio-mesma-chave-efeito-unico' (($e15enr.code -eq 200) -and ($idEn -ne 0) -and ($idEnr -eq $idEn)) ("reenvio mesma chave/conteudo -> $($e15enr.code) id=$idEnr (esperado igual a $idEn; sem novo lancamento)")
$e15ecx = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"ENTRADA","quantidade":20,"chaveIdempotencia":"C2S1-E15-K2"}')
Add-Check 'E15' 'lancamento.chave-conteudo-diferente-conflicto' (($e15ecx.code -eq 409) -and ($e15ecx.body -like '*CHAVE_IDEMPOTENCIA_CONFLITO*')) ("mesma chave conteudo diferente -> $($e15ecx.code) $($e15ecx.body)")
$e15aj0 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"AJUSTE","quantidade":-3}')
Add-Check 'E15' 'ajuste.sem-motivo-rejeitado' (($e15aj0.code -eq 400) -and ($e15aj0.body -like '*ESTORNO_MOTIVO_OBRIGATORIO*')) ("AJUSTE sem motivo -> $($e15aj0.code) $($e15aj0.body)")
$e15aj = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"AJUSTE","quantidade":-3,"motivo":"Ajuste de inventario fixture C2S1"}')
$idAj = Get-E15Data $e15aj
Add-Check 'E15' 'ajuste.com-motivo' (($e15aj.code -eq 200) -and ($idAj -ne 0)) ("AJUSTE -3 com motivo -> $($e15aj.code) mov=$idAj")
$e15ajn = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pbn.text + ',"operacao":"AJUSTE","quantidade":-99,"motivo":"Sobreponto de teste C2S1"}')
Add-Check 'E15' 'ajuste.saldo-negativo-rejeitado' (($e15ajn.code -eq 409) -and ($e15ajn.body -like '*SALDO_NEGATIVO_REJEITADO*')) ("AJUSTE -99 -> $($e15ajn.code) $($e15ajn.body)")
$e15na = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $pes.text + ',"operacao":"ENTRADA","quantidade":1}')
Add-Check 'E15' 'entrada.sem-linha-aberta' (($e15na.code -eq 404) -and ($e15na.body -like '*ESTOQUE_NAO_ABERTO*')) ("ENTRADA sem ABERTURA previa -> $($e15na.code) $($e15na.body)")
# C2S2 BR-2.2: porta legado /estoques restrita (409 COMANDO_TIPADO_OBRIGATORIO) — a race migra para a porta tipada:
# 8 ABERTURAS paralelas com chaves distintas => exatamente 1 abertura vitoriosa (demais 409 ESTOQUE_ABERTURA_EXISTENTE)
$raceE15 = 1..8 | ForEach-Object { Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques/lancar' -Base $ApiBase -Body ('{"produtoId":' + $prc.text + ',"operacao":"ABERTURA","quantidade":1,"chaveIdempotencia":"C2S2-RACE-' + $_ + '"}') }
$raceOkE15 = @($raceE15 | Where-Object { $_.code -eq 200 }).Count
$raceDb = Get-Psql "select count(*)::int||'|'||coalesce(sum(saldo),0)::int from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id=$($prc.text) and is_deleted=false;"
$raceLinE15 = ($raceDb.text -split '\|')[0]; $raceSalE15 = ($raceDb.text -split '\|')[1]
$raceNokE15 = @($raceE15 | Where-Object { $_.code -eq 409 }).Count
Add-Check 'E15' 'abertura.race-paralela-abertura-unica' (($raceOkE15 -eq 1) -and ($raceNokE15 -eq 7) -and ($raceLinE15 -eq '1') -and ($raceSalE15 -eq '1')) ("8 ABERTURAS paralelas na porta tipada: OK=$raceOkE15 409s=$raceNokE15 linhas=$raceLinE15 saldo=$raceSalE15 (abertura unica; perdedores 409 ESTOQUE_ABERTURA_EXISTENTE, nunca linha duplicada)")
$salPa0 = Get-Psql "select round(coalesce(saldo,-1),4)::text from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id=$($pa.text) and is_deleted=false order by id desc limit 1;"
$e15d1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-13","itens":[{"produtoId":' + $pa.text + ',"quantidade":3}],"chaveIdempotencia":"C2S1-E15-KD1"}')
$did1 = 0; $jD1 = $null; try { $jD1 = $e15d1.body | ConvertFrom-Json } catch {}; if ($jD1 -and $null -ne $jD1.data) { $did1 = [long]$jD1.data }
Add-Check 'E15' 'distribuicao.com-chave' ($e15d1.code -eq 200) ("distribuicao A:3 em 2026-11-13 chave KD1 -> $($e15d1.code) dist=$did1 (saldo antes: $($salPa0.text))")
$e15d1r = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-13","itens":[{"produtoId":' + $pa.text + ',"quantidade":3}],"chaveIdempotencia":"C2S1-E15-KD1"}')
$did1r = 0; $jD1r = $null; try { $jD1r = $e15d1r.body | ConvertFrom-Json } catch {}; if ($jD1r -and $null -ne $jD1r.data) { $did1r = [long]$jD1r.data }
$baixaD1 = Get-Psql "select count(*)::int from sigov.educacao_merenda_movimento where tenant_id=910001 and referencia_id=$did1 and tipo='SAIDA' and is_deleted=false;"
Add-Check 'E15' 'distribuicao.reenvio-mesma-chave-efeito-unico' (($e15d1r.code -eq 200) -and ($did1r -eq $did1) -and ($baixaD1.text -eq '1')) ("reenvio mesma chave/conteudo -> $($e15d1r.code) id=$did1r (esperado $did1); baixas_SAIDA=$($baixaD1.text) (sem dupla baixa)")
$e15est1 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/distribuicoes/$did1/estornar" -Base $ApiBase -Body '{"motivo":"Estorno fixture C2S1 (devoluicao da merenda)"}'
$salPa1 = Get-Psql "select round(coalesce(saldo,-1),4)::text from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id=$($pa.text) and is_deleted=false order by id desc limit 1;"
$stD1 = Get-Psql "select status from sigov.educacao_merenda_distribuicao where tenant_id=910001 and id=$did1;"
$estCnt1 = Get-Psql "select count(*)::int from sigov.educacao_merenda_movimento where tenant_id=910001 and referencia_id=$did1 and tipo='ESTORNO' and is_deleted=false;"
Add-Check 'E15' 'estorno.distribuicao-restaura-saldo-e-cancela' (($e15est1.code -eq 200) -and ($salPa1.text -eq $salPa0.text) -and ($stD1.text -ceq 'CANCELADA') -and ($estCnt1.text -eq '1')) ("estorno distribuicao -> $($e15est1.code); saldo restaurado=$($salPa1.text) (esperado $($salPa0.text)); status=$($stD1.text); estornos=$($estCnt1.text) (esperado 1; documento/itens preservados)")
$e15est1r = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/distribuicoes/$did1/estornar" -Base $ApiBase -Body '{"motivo":"Estorno fixture C2S1 (devoluicao da merenda)"}'
$salPa2 = Get-Psql "select round(coalesce(saldo,-1),4)::text from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id=$($pa.text) and is_deleted=false order by id desc limit 1;"
$estCnt1b = Get-Psql "select count(*)::int from sigov.educacao_merenda_movimento where tenant_id=910001 and referencia_id=$did1 and tipo='ESTORNO' and is_deleted=false;"
Add-Check 'E15' 'estorno.distribuicao-repetido-efeito-unico' (($e15est1r.code -eq 200) -and ($estCnt1b.text -eq '1') -and ($salPa2.text -eq $salPa0.text)) ("estorno repetido -> $($e15est1r.code); estornos=$($estCnt1b.text) saldo=$($salPa2.text) (compensacao unica, sem reestouro)")
$e15d2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes/confirmar' -Base $ApiBase -Body ('{"cardapioId":' + $cs6 + ',"data":"2026-11-14","itens":[{"produtoId":' + $pa.text + ',"quantidade":1}]}')
$did2 = 0; $jD2 = $null; try { $jD2 = $e15d2.body | ConvertFrom-Json } catch {}; if ($jD2 -and $null -ne $jD2.data) { $did2 = [long]$jD2.data }
Add-Check 'E15' 'distribuicao.sem-chave' ($e15d2.code -eq 200) ("distribuicao A:1 em 2026-11-14 sem chave -> $($e15d2.code) dist=$did2")
$e15canc = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_distribuicao/$did2/status?status=CANCELADA&justificativa=c2s1-cancelamento-generico" -Base $ApiBase
Add-Check 'E15' 'cancelamento-generico.com-efeito-bloqueado' (($e15canc.code -eq 409) -and ($e15canc.body -like '*DISTRIBUICAO_ESTORNO_OBRIGATORIO*')) ("cancelamento generico CANCELADA com baixa de estoque -> $($e15canc.code) $($e15canc.body)")
$e15est2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/distribuicoes/$did2/estornar" -Base $ApiBase -Body '{"motivo":"Estorno fixture C2S1 (dia seguinte)"}'
$idEst2 = Get-E15Data $e15est2
Add-Check 'E15' 'estorno.distribuicao.apos-bloqueio-generico' (($e15est2.code -eq 200) -and ($idEst2 -ne 0)) ("estorno tipado apos bloqueio do generico -> $($e15est2.code) id=$idEst2")
$movAbE15 = Get-Psql "select min(id)::text from sigov.educacao_merenda_movimento where tenant_id=910001 and produto_id=$($pbn.text) and tipo='ENTRADA' and is_deleted=false;"
$e15em = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/movimentos/$($movAbE15.text)/estornar" -Base $ApiBase -Body '{"motivo":"Estorno fixture C2S1 (abertura)"}'
$estMovId = 0; $jEm = $null; try { $jEm = $e15em.body | ConvertFrom-Json } catch {}; if ($jEm -and $null -ne $jEm.data) { $estMovId = [long]$jEm.data }
Add-Check 'E15' 'estorno.movimento-compensacao' ($e15em.code -eq 200) ("estorno do movimento de abertura -> $($e15em.code) estorno=$estMovId")
$e15emr = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/movimentos/$($movAbE15.text)/estornar" -Base $ApiBase -Body '{"motivo":"Estorno fixture C2S1 (abertura)"}'
$estMovIdR = 0; $jEmr = $null; try { $jEmr = $e15emr.body | ConvertFrom-Json } catch {}; if ($jEmr -and $null -ne $jEmr.data) { $estMovIdR = [long]$jEmr.data }
Add-Check 'E15' 'estorno.movimento-repetido-mesmo-id' (($e15emr.code -eq 200) -and ($estMovId -ne 0) -and ($estMovIdR -eq $estMovId)) ("estorno repetido -> $($e15emr.code) id=$estMovIdR (esperado $estMovId; compensacao unica)")
$e15eme = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/movimentos/$estMovId/estornar" -Base $ApiBase -Body '{"motivo":"Estorno de estorno C2S1"}'
Add-Check 'E15' 'estorno.de-estorno-bloqueado' (($e15eme.code -eq 409) -and ($e15eme.body -like '*ESTORNO_BASE_INVALIDA*')) ("estorno do estorno -> $($e15eme.code) $($e15eme.body)")
$e15emi = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/movimentos/99999999/estornar' -Base $ApiBase -Body '{"motivo":"Base inexistente C2S1"}'
Add-Check 'E15' 'estorno.base-inexistente' (($e15emi.code -eq 404) -and ($e15emi.body -like '*ESTORNO_BASE_INEXISTENTE*')) ("estorno de movimento inexistente -> $($e15emi.code) $($e15emi.body)")
$recE15 = Get-Psql "select coalesce(e.s,-1)::text||'|'||coalesce(m.s,-1)::text from (select sum(saldo) as s from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id=$($pbn.text) and is_deleted=false) e, (select sum(quantidade) as s from sigov.educacao_merenda_movimento where tenant_id=910001 and produto_id=$($pbn.text) and is_deleted=false) m;"
$recSalE15 = ($recE15.text -split '\|')[0]; $recSomE15 = ($recE15.text -split '\|')[1]
Add-Check 'E15' 'conciliacao.movimento-x-saldo-fechados' ($recSalE15 -eq $recSomE15) ("saldo=$recSalE15 = soma dos movimentos=$recSomE15 (lancamentos tipados: movimento+saldo na mesma transacao; conciliacao fechada apos estorno unico)")

# -----------------------------------------------------------------------------
# E16 (Ciclo 2 Sprint 2): maquina de estados com efeitos declarados (BR-2.1/2.4),
# fim dos bypasses genericos - comando tipado unico (BR-2.2), versao otimista
# obrigatoria apos a primeira atualizacao (BR-2.3), relogio operacional unico
# (BR-2.5) e diagnostico uniforme de schema ausente nos quatro caminhos (BR-2.6).
# -----------------------------------------------------------------------------
# ---- BR-2.2: criacao generica restrita => 409 COMANDO_TIPADO_OBRIGATORIO com a rota tipada na mensagem
$b16mv = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/movimentos' -Base $ApiBase -Body '{"status":"ATIVA","quantidade":5}'
Add-Check 'E16' 'bypass.movimento-comando-tipado-409' (($b16mv.code -eq 409) -and ($b16mv.body -like '*COMANDO_TIPADO_OBRIGATORIO*') -and ($b16mv.body -like '*estoques/lancar*')) ("POST generico /merenda/movimentos -> $($b16mv.code) (rota tipada na mensagem)")
$b16dt = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/distribuicoes' -Base $ApiBase -Body '{"status":"RASCUNHO"}'
Add-Check 'E16' 'bypass.distribuicao-comando-tipado-409' (($b16dt.code -eq 409) -and ($b16dt.body -like '*COMANDO_TIPADO_OBRIGATORIO*') -and ($b16dt.body -like '*distribuicoes/confirmar*')) ("POST generico /merenda/distribuicoes -> $($b16dt.code)")
$b16st = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques' -Base $ApiBase -Body ('{"produtoId":' + $pa.text + ',"saldoInicial":1}')
Add-Check 'E16' 'bypass.estoque-legado-restrito-409' (($b16st.code -eq 409) -and ($b16st.body -like '*COMANDO_TIPADO_OBRIGATORIO*') -and ($b16st.body -like '*estoques/lancar*')) ("POST legado /merenda/estoques -> $($b16st.code) (porta legado restrita)")
$b16em = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos' -Base $ApiBase -Body '{"status":"DISPONIVEL"}'
Add-Check 'E16' 'bypass.emprestimo-comando-tipado-409' (($b16em.code -eq 409) -and ($b16em.body -like '*COMANDO_TIPADO_OBRIGATORIO*') -and ($b16em.body -like '*emprestimos/lancar*')) ("POST generico /biblioteca/emprestimos -> $($b16em.code)")
$b16rs = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/reservas' -Base $ApiBase -Body '{"status":"ATIVA"}'
Add-Check 'E16' 'bypass.reserva-comando-tipado-409' (($b16rs.code -eq 409) -and ($b16rs.body -like '*COMANDO_TIPADO_OBRIGATORIO*') -and ($b16rs.body -like '*reservas/lancar*')) ("POST generico /biblioteca/reservas -> $($b16rs.code)")
$b16ap = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes' -Base $ApiBase -Body '{"status":"ATIVA","valor":1}'
Add-Check 'E16' 'bypass.fundeb-aplicacao-comando-tipado-409' (($b16ap.code -eq 409) -and ($b16ap.body -like '*COMANDO_TIPADO_OBRIGATORIO*') -and ($b16ap.body -like '*aplicacoes/lancar*')) ("POST generico /fundeb/aplicacoes -> $($b16ap.code)")
$b16pr = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C2S2-LIVRE","nome":"Produto C2S2 Livre","tipo":"ALIMENTO","status":"ATIVA"}'
Add-Check 'E16' 'livre.produto-segue-generico-200' (($b16pr.code -eq 200)) ("POST generico /merenda/produtos -> $($b16pr.code) (recurso livre segue o caminho generico)")
# ---- BR-2.1: origem desconhecida => LEGADO_NAO_REGULARIZADO (visivel, tratavel, nao silenciosa)
$lg1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body '{"codigo":"c2s2-leg1","nome":"Requisicao Legada 1","status":"LEGADO_HISTORICO"}'
$lg1id = Get-Psql "select id from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo='c2s2-leg1' order by id desc limit 1;"
$lg1ok = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$($lg1id.text)/status?status=ATIVA&justificativa=c2s2-regularizacao-legada" -Base $ApiBase
$lg1db = Get-Psql "select status from sigov.educacao_merenda_requisicao where tenant_id=910001 and id=$($lg1id.text);"
Add-Check 'E16' 'legado.regularizacao-para-canonico-200' (($lg1.code -eq 200) -and ($lg1ok.code -eq 200) -and ($lg1db.text -eq 'ATIVA')) ("origem LEGADO_HISTORICO + justificativa -> ATIVA $($lg1ok.code); banco=$($lg1db.text) (estado LEGADO_NAO_REGULARIZADO tratavel)")
$lg2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body '{"codigo":"c2s2-leg2","nome":"Requisicao Legada 2","status":"LEGADO_HISTORICO"}'
$lg2id = Get-Psql "select id from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo='c2s2-leg2' order by id desc limit 1;"
$lg2nao = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$($lg2id.text)/status?status=ATIVA" -Base $ApiBase
Add-Check 'E16' 'legado.sem-justificativa-409' (($lg2nao.code -eq 409) -and ($lg2nao.body -like '*REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA*')) ("origem legada sem justificativa -> $($lg2nao.code) (transicao nao silenciosa)")
$lg3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/requisicoes' -Base $ApiBase -Body '{"codigo":"c2s2-leg3","nome":"Requisicao Legada 3","status":"LEGADO_OUTRO"}'
$lg3id = Get-Psql "select id from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo='c2s2-leg3' order by id desc limit 1;"
$lg3z = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$($lg3id.text)/status?status=ZULU_DESCONHECIDO&justificativa=x" -Base $ApiBase
Add-Check 'E16' 'legado.destino-nao-canonical-409' (($lg3z.code -eq 409) -and ($lg3z.body -like '*TRANSICAO_INVALIDA*')) ("origem legada -> destino nao canonical ZULU -> $($lg3z.code) ('_ => true' eliminado)")
$lg3t = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$($lg3id.text)/status?status=ENCERRADA&justificativa=x" -Base $ApiBase
Add-Check 'E16' 'legado.salto-direto-terminal-409' (($lg3t.code -eq 409) -and ($lg3t.body -like '*TRANSICAO_INVALIDA*')) ("origem legada -> ENCERRADA direto -> $($lg3t.code) (sem salto direto a terminal)")
# ---- BR-2.3: registro ja atualizado exige updatedAt (CONFLITO_VERSAO quando omitido ou obsoleto)
$v1nao = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$($lg1id.text)/status?status=SUSPENSA" -Base $ApiBase
Add-Check 'E16' 'versao.exigida-apos-primeira-atualizacao-409' (($v1nao.code -eq 409) -and ($v1nao.body -like '*CONFLITO_VERSAO*')) ("segunda transicao sem updatedAt -> $($v1nao.code) (registro ja atualizado exige versao)")
$lg1u = Get-Psql "select coalesce(updated_at,created_at)::text from sigov.educacao_merenda_requisicao where tenant_id=910001 and id=$($lg1id.text);"
$v1sim = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/merenda/operacoes/educacao_merenda_requisicao/$($lg1id.text)/status?status=SUSPENSA&updatedAt=" + [Uri]::EscapeDataString($lg1u.text)) -Base $ApiBase
Add-Check 'E16' 'versao.correta-aceita-200' (($v1sim.code -eq 200)) ("mesma transicao com updatedAt correto -> $($v1sim.code)")
# ---- BR-2.6: schema ausente => 404 RECURSO_INDISPONIVEL identico em listar/criar/status/dashboard
$uPre = Get-Psql "select count(*)::int from sigov.educacao_merenda_produto where tenant_id=910001;"
Get-Psql "alter table sigov.educacao_merenda_produto rename to c1t2_probe_s2;" | Out-Null
$uList = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/merenda/produtos?pagina=1&tamanho=20' -Base $ApiBase
$uCria = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/produtos' -Base $ApiBase -Body '{"codigo":"C2S2-PROBE","nome":"Probe Schema","tipo":"ALIMENTO","status":"ATIVA"}'
$uStat = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_produto/1/status?status=ATIVA&justificativa=probe-schema" -Base $ApiBase
$uDash = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/merenda/dashboard' -Base $ApiBase
$m16 = @{}
foreach ($pv in @(@('List',$uList),@('Cria',$uCria),@('Stat',$uStat),@('Dash',$uDash))) { try { $jj = $pv[1].body | ConvertFrom-Json; $m16[$pv[0]] = [string]$jj.message } catch { $m16[$pv[0]] = 'PARSE_ERR' } }
$sameMsg = ($m16['List'] -ne 'PARSE_ERR') -and ($m16['List'] -eq $m16['Cria']) -and ($m16['List'] -eq $m16['Stat']) -and ($m16['List'] -eq $m16['Dash'])
$c404 = 0
foreach ($pv2 in @($uList,$uCria,$uStat,$uDash)) { if ($pv2.code -eq 404) { $c404++ } }
Add-Check 'E16' 'schema.uniforme-quatro-caminhos-identicos' (($c404 -eq 4) -and $sameMsg -and ($m16['List'] -like '*RECURSO_INDISPONIVEL*') -and ($m16['List'] -like '*schema ausente*')) ("listar=$($uList.code) criar=$($uCria.code) status=$($uStat.code) dashboard=$($uDash.code); mensagem padrao identica nos 4 caminhos (BR-2.6)")
Get-Psql "alter table sigov.c1t2_probe_s2 rename to educacao_merenda_produto;" | Out-Null
$uPos = Get-Psql "select count(*)::int from sigov.educacao_merenda_produto where tenant_id=910001;"
Add-Check 'E16' 'probe.tabela-restaurada-intacta' (($uPos.text -eq $uPre.text)) ("restore ok; linhas antes=$($uPre.text) depois=$($uPos.text)")
# ---- BR-2.5: relogio operacional unico (data local do host governa elegibilidade e persistencia)
$e16prazo = (Get-Date).Date.AddDays(14).ToString('yyyy-MM-dd')
$e16clk = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/emprestimos/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + ',"prazo":"' + $e16prazo + '"}')
$e16dbk = Get-Psql "select to_char(data_inicio,'YYYY-MM-DD')||'|'||to_char(competencia,'YYYY-MM-DD') from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id=$($e11idB.text) order by id desc limit 1;"
Add-Check 'E16' 'tempo.fonte-operacional-host-unico' (($e16clk.code -eq 200) -and ($e16dbk.text -eq ($e11hoje + '|' + $e11hoje))) ("emprestimo exemplar B prazo=" + $e16prazo + " -> $($e16clk.code); data_inicio|competencia=$($e16dbk.text) vs host local=$e11hoje (fonte unica C#; SQL UTC nao governa)")

# ===========================================================================
# E18 — Ciclo 2 Sprint 4: transporte escolar da rota a viagem (BR-4.1 a BR-4.8)
# ---------------------------------------------------------------------------
# POST veiculos/lancar {placa,capacidade,classe[,situacaoDocumental,
# inspecaoValidade]}: placa unica no tenant (409 VEICULO_PLACA_EXISTENTE + indice
# parcial backstop); situacao canonica REGULAR/VENCIDA/BAIXA (400 nomeada;
# VENCIDA/BAIXA tornam o veiculo inapto p/ viagem); sem data de inspecao, a
# validade nasce em N meses parametrizaveis pelo tenant (BR-4.1). POST
# motoristas/lancar {nome,habilitacaoCategoria,habilitacaoValidade}: vencida na
# data da viagem (MOTORISTA_HABILITACAO_VENCIDA) e categoria compativel com a
# classe do veiculo via catalogo configuravel
# sigov.transporte_categoria_compatibilidade (BR-4.2). POST viagens/lancar
# {rotaId,data,veiculoId,motoristaId[,monitorIds,km/custo]}: backstops parciais
# de sobreposicao veiculo x data e motorista x data nas viagens vigentes;
# capacidade x vinculados ativos da rota; monitor obrigatorio quando o
# parametro do tenant exige (override no banco testado); carona parametrica de
# ocorrencia grave do veiculo apos desfecho (BR-4.3/BR-4.5); estimativa de custo
# gravada no planejamento (BR-4.6). Ciclo PROGRAMADA -> EM_ANDAMENTO (inicio c/
# hora) -> CONCLUIDA (fim c/ hora e km); cancelamento pre-inicio gratuito com
# justificativa, pos-inicio exige ocorrencia vinculada; substituicao gera nova
# versao (antiga SUBSTITUIDA, presencas/custos seguem) (BR-4.7). Presenca por
# viagem por aluno vinculado a rota: idempotente no mesmo status, correcao com a
# viagem aberta, fechamento impede novo lancamento, estorno unitario com
# justificativa desbloqueia um registro por vez (BR-4.4). Ocorrencia de viagem:
# tipos/gravidades validados, ALTA torna a viagem OCORRENCIA_GRAVE (unica
# vigente sem desfecho), desfecho unico idempotente com relatorio obrigatorio
# restaura o status anterior (BR-4.5). Custo realizado unico por viagem,
# imutavel, nao altera o estimado (BR-4.6). GET viagens/{id}/relatorio: presenca
# + ocorrencias + custos estimado x realizado com desvio absoluto e percentual
# (BR-4.8). Falhas nomeadas (regra 13); multi-esfera (regra 24).
# ===========================================================================
function Get-E18Data { param([string]$Body) try { return [long](($Body | ConvertFrom-Json).data) } catch { return -1 } }
$e18fix = Get-Psql @"
delete from sigov.educacao_transporte_frequencia where tenant_id=910001 and tipo='PRESENCA_TRANSPORTE';
delete from sigov.educacao_transporte_ocorrencia where tenant_id=910001;
delete from sigov.educacao_transporte_custo where tenant_id=910001;
delete from sigov.educacao_transporte_viagem where tenant_id=910001;
delete from sigov.educacao_transporte_veiculo where tenant_id=910001 and placa like 'C2S4-%';
delete from sigov.educacao_transporte_motorista where tenant_id=910001 and nome like 'C2S4-%';
delete from sigov.parametro_modulo_valor where tenant_id=910001 and parametro_id=(select id from sigov.parametro_modulo where modulo='EDUCACAO' and codigo='TRANSPORTE_MONITOR_OBRIGATORIO' limit 1);
select 'ok';
"@
Add-Check 'E18' 'fixture.s4-reset' ($e18fix.code -eq 0) ("reset das linhas C2S4 de transporte do tenant 910001 (viagens/presencas/ocorrencias/custos/veiculos/motoristas/override-monitor) exit=$($e18fix.code)")
$e18d0 = (Get-Date).Date.ToString('yyyy-MM-dd'); $e18d1 = (Get-Date).Date.AddDays(1).ToString('yyyy-MM-dd'); $e18d2 = (Get-Date).Date.AddDays(2).ToString('yyyy-MM-dd'); $e18d3 = (Get-Date).Date.AddDays(3).ToString('yyyy-MM-dd'); $e18d4 = (Get-Date).Date.AddDays(4).ToString('yyyy-MM-dd'); $e18d5 = (Get-Date).Date.AddDays(5).ToString('yyyy-MM-dd'); $e18d6 = (Get-Date).Date.AddDays(6).ToString('yyyy-MM-dd'); $e18d7 = (Get-Date).Date.AddDays(7).ToString('yyyy-MM-dd'); $e18d8 = (Get-Date).Date.AddDays(8).ToString('yyyy-MM-dd'); $e18d9 = (Get-Date).Date.AddDays(9).ToString('yyyy-MM-dd'); $e18d10 = (Get-Date).Date.AddDays(10).ToString('yyyy-MM-dd'); $e18d11 = (Get-Date).Date.AddDays(11).ToString('yyyy-MM-dd')
# ---- BR-4.1: veiculo regular; sem data de inspecao, validade = N meses do parametro do banco
$e18v1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI01","capacidade":20,"classe":"ONIBUS"}'
$e18idV1 = Get-E18Data $e18v1.body
$e18inspEsp = (Get-Date).Date.AddMonths(6).ToString('yyyy-MM-dd')
$e18dbV1 = Get-Psql ("select coalesce(placa,'(nil)')||'|'||coalesce(situacao_documental,'(nil)')||'|'||coalesce(to_char(inspecao_validade,'YYYY-MM-DD'),'(nil)') from sigov.educacao_transporte_veiculo where tenant_id=910001 and id=" + $e18idV1 + " limit 1;")
Add-Check 'E18' 'veiculo.lancado-regular-inspecao-parametrica' (($e18v1.code -eq 200) -and ($e18dbV1.text -eq ('C2S4-VEI01|REGULAR|' + $e18inspEsp))) ("POST veiculos/lancar (placa/capacidade/classe, sem data de inspecao) -> $($e18v1.code); banco placa|situacao|inspecao: $($e18dbV1.text) (BR-4.1: sem validade informada nasce vencendo em 6 meses pelo parametro TRANSPORTE_INSPECCAO_VALIDADE_MESES do banco, nunca hardcoded)")
$e18v1dup = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI01","capacidade":20,"classe":"ONIBUS"}'
Add-Check 'E18' 'veiculo.placa-duplicada-409-nomeada' (($e18v1dup.code -eq 409) -and ($e18v1dup.body -like '*VEICULO_PLACA_EXISTENTE*')) ("segundo veiculo com a mesma placa -> $($e18v1dup.code) (409 VEICULO_PLACA_EXISTENTE nomeada; checagem previa + indice parcial ux como backstop transacional)")
$e18vSit = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI99","capacidade":10,"classe":"ONIBUS","situacaoDocumental":"OUTRA"}'
Add-Check 'E18' 'veiculo.situacao-documental-invalida-400' (($e18vSit.code -eq 400) -and ($e18vSit.body -like '*VEICULO_SITUACAO_INVALIDA*')) ("situacao 'OUTRA' -> $($e18vSit.code) (400 VEICULO_SITUACAO_INVALIDA nomeada; conjunto canonico REGULAR/VENCIDA/BAIXA)")
$e18vCap = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI98","capacidade":0,"classe":"ONIBUS"}'
Add-Check 'E18' 'veiculo.capacidade-invalida-400' (($e18vCap.code -eq 400) -and ($e18vCap.body -like '*VEICULO_CAPACIDADE_INVALIDA*')) ("capacidade 0 -> $($e18vCap.code) (400 VEICULO_CAPACIDADE_INVALIDA nomeada; capacidade e inteiro positivo)")
# fixture de recursos auxiliares (todos ficticios, prefixo C2S4)
$e18v2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI02","capacidade":20,"classe":"ONIBUS"}'
$e18v3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI03","capacidade":1,"classe":"ONIBUS"}'
$e18v4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body ('{"placa":"C2S4-VEI04","capacidade":20,"classe":"ONIBUS","inspecaoValidade":"' + (Get-Date).Date.AddDays(-1).ToString('yyyy-MM-dd') + '"}')
$e18v5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI05","capacidade":20,"classe":"ONIBUS","situacaoDocumental":"BAIXA"}'
$e18v6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/veiculos/lancar' -Base $ApiBase -Body '{"placa":"C2S4-VEI06","capacidade":20,"classe":"MICRO_ONIBUS"}'
$e18m1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/motoristas/lancar' -Base $ApiBase -Body ('{"nome":"C2S4-MOT01 Ficticio A","habilitacaoCategoria":"E","habilitacaoValidade":"' + (Get-Date).Date.AddDays(365).ToString('yyyy-MM-dd') + '"}')
$e18m2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/motoristas/lancar' -Base $ApiBase -Body ('{"nome":"C2S4-MOT02 Ficticio B","habilitacaoCategoria":"E","habilitacaoValidade":"' + (Get-Date).Date.AddDays(365).ToString('yyyy-MM-dd') + '"}')
$e18m3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/motoristas/lancar' -Base $ApiBase -Body ('{"nome":"C2S4-MOT03 Ficticio C","habilitacaoCategoria":"E","habilitacaoValidade":"' + (Get-Date).Date.AddDays(-1).ToString('yyyy-MM-dd') + '"}')
$e18m4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/motoristas/lancar' -Base $ApiBase -Body ('{"nome":"C2S4-MOT04 Ficticio D","habilitacaoCategoria":"C","habilitacaoValidade":"' + (Get-Date).Date.AddDays(365).ToString('yyyy-MM-dd') + '"}')
$e18m5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/motoristas/lancar' -Base $ApiBase -Body ('{"nome":"C2S4-MOT05 Ficticio E","habilitacaoCategoria":"D","habilitacaoValidade":"' + (Get-Date).Date.AddDays(365).ToString('yyyy-MM-dd') + '"}')
$e18okFix = @($e18v2,$e18v3,$e18v4,$e18v5,$e18v6,$e18m1,$e18m2,$e18m3,$e18m4,$e18m5) | Where-Object { $_.code -eq 200 } | Measure-Object
$e18idV2 = Get-E18Data $e18v2.body; $e18idV3 = Get-E18Data $e18v3.body; $e18idV4 = Get-E18Data $e18v4.body; $e18idV5 = Get-E18Data $e18v5.body; $e18idV6 = Get-E18Data $e18v6.body
$e18idM1 = Get-E18Data $e18m1.body; $e18idM2 = Get-E18Data $e18m2.body; $e18idM3 = Get-E18Data $e18m3.body; $e18idM4 = Get-E18Data $e18m4.body; $e18idM5 = Get-E18Data $e18m5.body
Add-Check 'E18' 'fixture.recursos-aplicados' ($e18okFix.Count -eq 10) ("recursos auxiliares ficticios: V2 (ONIBUS reserva), V3 (capacidade 1), V4 (inspecao vencida ontem), V5 (situacao BAIXA), V6 (MICRO_ONIBUS); M1/M2 (categoria E valida), M3 (E vencida), M4 (C), M5 (D) -> todos 200 (compatibilidade E x ONIBUS e D x MICRO_ONIBUS vem da tabela configuravel sigov.transporte_categoria_compatibilidade, regra 11)")
# ---- BR-4.3: viagem lancada PROGRAMADA com estimativa gravada (BR-4.6)
$e18t1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d0 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM1 + ',"kmEstimado":50,"tarifaKm":2.5,"combustivelEstimado":200,"pedagioPrevisto":50}')
$e18t1id = Get-E18Data $e18t1.body
$e18dbT1 = Get-Psql ("select coalesce(v.status,'(nil)')||'|'||case when exists(select 1 from sigov.educacao_transporte_custo ct where ct.tenant_id=910001 and ct.referencia_id=v.id and ct.tipo='ESTIMADO' and ct.ativo and not ct.is_deleted and abs(ct.valor-375.0)<0.001) then '375ok' else '375diff' end from sigov.educacao_transporte_viagem v where v.tenant_id=910001 and v.id=" + $e18t1id + " limit 1;")
Add-Check 'E18' 'viagem.lancada-programada-com-estimativa' (($e18t1.code -eq 200) -and ($e18dbT1.text -eq 'PROGRAMADA|375ok')) ("POST viagens/lancar (rota E12 + data + veiculo + motorista + km/tarifa/combustivel/pedagio) -> $($e18t1.code); banco status|custoEstimado: $($e18dbT1.text) (BR-4.3 nasce PROGRAMADA; BR-4.6 estimativa 50x2.5+200+50=375 gravada no planejamento, imutavel)")
$e18sv = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d0 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM2 + '}')
Add-Check 'E18' 'viagem.sobreposicao-veiculo-409' (($e18sv.code -eq 409) -and ($e18sv.body -like '*VIAGEM_VEICULO_SOBREPOSICAO*')) ("mesmo veiculo na mesma data com outro motorista livre -> $($e18sv.code) (409 VIAGEM_VEICULO_SOBREPOSICAO nomeada; backstop parcial ux sobre vigentes PROGRAMADA/EM_ANDAMENTO)")
$e18sm = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d0 + '","veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM1 + '}')
Add-Check 'E18' 'viagem.sobreposicao-motorista-409' (($e18sm.code -eq 409) -and ($e18sm.body -like '*VIAGEM_MOTORISTA_SOBREPOSICAO*')) ("mesmo motorista na mesma data com outro veiculo livre -> $($e18sm.code) (409 VIAGEM_MOTORISTA_SOBREPOSICAO nomeada; backstop parcial ux separado do do veiculo)")
$e18sc = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d1 + '","veiculoId":' + $e18idV3 + ',"motoristaId":' + $e18idM2 + '}')
Add-Check 'E18' 'viagem.capacidade-excedida-409' (($e18sc.code -eq 409) -and ($e18sc.body -like '*VIAGEM_CAPACIDADE_EXCEDIDA*')) ("veiculo capacidade 1 contra 2 alunos vinculados ativos da rota -> $($e18sc.code) (409 VIAGEM_CAPACIDADE_EXCEDIDA nomeada; capacidade x vinculados validada no lancamento)")
$e18si = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d2 + '","veiculoId":' + $e18idV4 + ',"motoristaId":' + $e18idM1 + '}')
Add-Check 'E18' 'viagem.inspecao-vencida-bloqueio-409' (($e18si.code -eq 409) -and ($e18si.body -like '*VEICULO_INSPECCAO_VENCIDA*')) ("veiculo com inspecao vencida ontem e modo BLOQUEIO do tenant -> $($e18si.code) (409 VEICULO_INSPECCAO_VENCIDA nomeada; BR-4.1 modo ALERTA|BLOQUEIO parametrizavel)")
$e18sa = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d2 + '","veiculoId":' + $e18idV5 + ',"motoristaId":' + $e18idM2 + '}')
Add-Check 'E18' 'viagem.veiculo-inapto-situacao-baixa-409' (($e18sa.code -eq 409) -and ($e18sa.body -like '*VEICULO_INAPTO*')) ("veiculo com situacao documental BAIXA -> $($e18sa.code) (409 VEICULO_INAPTO nomeada; apenas REGULARES podem ser alocados, BR-4.1)")
$e18sh = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d1 + '","veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM3 + '}')
Add-Check 'E18' 'viagem.habilitacao-vencida-na-data-da-viagem-409' (($e18sh.code -eq 409) -and ($e18sh.body -like '*MOTORISTA_HABILITACAO_VENCIDA*')) ("motorista com habilitacao vencida antes da data da viagem -> $($e18sh.code) (409 MOTORISTA_HABILITACAO_VENCIDA nomeada; validade comparada contra a data da viagem, BR-4.2)")
$e18scat = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d3 + '","veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM4 + '}')
Add-Check 'E18' 'viagem.categoria-incompativel-com-classe-409' (($e18scat.code -eq 409) -and ($e18scat.body -like '*MOTORISTA_CATEGORIA_INCOMPATIVEL*')) ("categoria C em veiculo classe ONIBUS -> $($e18scat.code) (409 MOTORISTA_CATEGORIA_INCOMPATIVEL nomeada; consulta ao catalogo configuravel sigov.transporte_categoria_compatibilidade, regra 11)")
$e18s404v = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d4 + '","veiculoId":9999999999,"motoristaId":' + $e18idM1 + '}')
Add-Check 'E18' 'viagem.veiculo-inexistente-404' (($e18s404v.code -eq 404) -and ($e18s404v.body -like '*VEICULO_INEXISTENTE*')) ("veiculo 9999999999 -> $($e18s404v.code) (404 VEICULO_INEXISTENTE nomeada)")
$e18s404m = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d4 + '","veiculoId":' + $e18idV6 + ',"motoristaId":9999999999}')
Add-Check 'E18' 'viagem.motorista-inexistente-404' (($e18s404m.code -eq 404) -and ($e18s404m.body -like '*MOTORISTA_INEXISTENTE*')) ("motorista 9999999999 -> $($e18s404m.code) (404 MOTORISTA_INEXISTENTE nomeada)")
# ---- BR-4.3: monitor obrigatorio quando o parametro do tenant exige (banco = autoridade, regra 11)
$e18monIns = Get-Psql @'
insert into sigov.parametro_modulo_valor(tenant_id,parametro_id,valor,correlation_id,created_by,is_deleted)
select 910001,id,'"TRUE"'::jsonb,'c2s4-monitor-override-e2e',910301,false
from sigov.parametro_modulo where modulo='EDUCACAO' and codigo='TRANSPORTE_MONITOR_OBRIGATORIO' limit 1;
'@
$e18mon1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d4 + '","veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM1 + '}')
Add-Check 'E18' 'viagem.monitor-obrigatorio-parametro-do-banco-409' (([int]$e18monIns.code -eq 0) -and ($e18mon1.code -eq 409) -and ($e18mon1.body -like '*VIAGEM_MONITOR_OBRIGATORIO*')) ("override do tenant TRANSPORTE_MONITOR_OBRIGATORIO=TRUE direto no banco -> viagem sem monitores -> $($e18mon1.code) (409 VIAGEM_MONITOR_OBRIGATORIO nomeada; exigencia parametrizada por tenant, nada hardcoded)")
$e18mon2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d4 + '","veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM1 + ',"monitorIds":[1]}')
$e18monDel = Get-Psql 'delete from sigov.parametro_modulo_valor where tenant_id=910001 and parametro_id=(select id from sigov.parametro_modulo where modulo=''EDUCACAO'' and codigo=''TRANSPORTE_MONITOR_OBRIGATORIO'' limit 1);'
Add-Check 'E18' 'viagem.monitor-presente-libera-lancamento-200' (($e18mon2.code -eq 200) -and ([int]$e18monDel.code -eq 0)) ("mesma viagem com monitorIds -> $($e18mon2.code) (200; override removido do banco apos o teste)")
# ---- BR-4.7: ciclo PROGRAMADA -> EM_ANDAMENTO (hora) -> CONCLUIDA (hora + km)
$e18in1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/iniciar") -Base $ApiBase -Body ('{"horaInicio":"' + $e18d0 + 'T08:00:00"}')
$e18dbIn = Get-Psql ("select status||'|'||coalesce(to_char(timezone('UTC',hora_inicio),'HH24:MI'),'(nil)') from sigov.educacao_transporte_viagem where tenant_id=910001 and id=" + $e18t1id + " limit 1;")
Add-Check 'E18' 'ciclo.iniciar-em-andamento-com-hora' (($e18in1.code -eq 200) -and ($e18dbIn.text -eq 'EM_ANDAMENTO|08:00')) ("POST viagens/{id}/iniciar (hora 08:00) -> $($e18in1.code); banco status|hora_inicio(UTC): $($e18dbIn.text) (BR-4.7 inicio sempre registra a hora)")
$e18co1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/concluir") -Base $ApiBase -Body ('{"horaFim":"' + $e18d0 + 'T10:00:00","kmPercorridos":55}')
$e18dbCo = Get-Psql ("select status||'|'||coalesce(to_char(timezone('UTC',hora_fim),'HH24:MI'),'(nil)')||'|'||case when km_percorridos=55 then 'kmok' else 'kmdiff:'||coalesce(km_percorridos::text,'(nil)') end from sigov.educacao_transporte_viagem where tenant_id=910001 and id=" + $e18t1id + " limit 1;")
Add-Check 'E18' 'ciclo.concluir-hora-fim-e-km' (($e18co1.code -eq 200) -and ($e18dbCo.text -eq 'CONCLUIDA|10:00|kmok')) ("POST viagens/{id}/concluir (hora 10:00 + 55 km) -> $($e18co1.code); banco status|hora_fim(UTC)|km_percorridos: $($e18dbCo.text)")
$e18in2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/iniciar") -Base $ApiBase -Body ('{"horaInicio":"' + $e18d0 + 'T08:30:00"}')
Add-Check 'E18' 'ciclo.iniciar-de-novo-estado-invalido-409' (($e18in2.code -eq 409) -and ($e18in2.body -like '*VIAGEM_ESTADO_INVALIDO*')) ("iniciar viagem ja CONCLUIDA -> $($e18in2.code) (409 VIAGEM_ESTADO_INVALIDO nomeada; o ciclo nao volta atraves)")
$e18caX = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/cancelar") -Base $ApiBase -Body '{"justificativa":"cancelar concluida"}'
Add-Check 'E18' 'ciclo.cancelar-concluida-estado-invalido-409' (($e18caX.code -eq 409) -and ($e18caX.body -like '*VIAGEM_ESTADO_INVALIDO*')) ("cancelar viagem ja CONCLUIDA -> $($e18caX.code) (409 VIAGEM_ESTADO_INVALIDO nomeada; estados terminais nao sao cancelaveis)")
# ---- BR-4.7: cancelamento pre-inicio gratuito c/ justificativa; pos-inicio exige ocorrencia
$e18t3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d5 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM1 + '}')
$e18t3id = Get-E18Data $e18t3.body
$e18cj0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t3id + "/cancelar") -Base $ApiBase -Body '{"justificativa":""}'
Add-Check 'E18' 'cancelamento.sem-justificativa-400' (($e18t3.code -eq 200) -and ($e18cj0.code -eq 400) -and ($e18cj0.body -like '*VIAGEM_CANCELAMENTO_JUSTIFICATIVA*')) ("cancelar_PROGRAMADA sem justificativa -> $($e18cj0.code) (400 nomeada; todo cancelamento entra na cronologia com motivo)")
$e18cj1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t3id + "/cancelar") -Base $ApiBase -Body '{"justificativa":"roteiro reprogramado pela demanda"}'
$e18dbCj = Get-Psql ("select status||'|'||coalesce(justificativa,'(nil)') from sigov.educacao_transporte_viagem where tenant_id=910001 and id=" + $e18t3id + " limit 1;")
Add-Check 'E18' 'cancelamento.pre-inicio-gratuito-200' (($e18cj1.code -eq 200) -and ($e18dbCj.text -eq 'CANCELADA|roteiro reprogramado pela demanda')) ("cancelar_PROGRAMADA com justificativa -> $($e18cj1.code); banco status|justificativa: $($e18dbCj.text) (libera veiculo/motorista da data para novas programacoes)")
$e18t4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d5 + '","veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM2 + '}')
$e18t4id = Get-E18Data $e18t4.body
$e18in4 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t4id + "/iniciar") -Base $ApiBase -Body ('{"horaInicio":"' + $e18d5 + 'T08:00:00"}')
$e18cj2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t4id + "/cancelar") -Base $ApiBase -Body '{"justificativa":"chuva forte"}'
Add-Check 'E18' 'cancelamento.pos-inicio-exige-ocorrencia-409' (($e18t4.code -eq 200) -and ($e18in4.code -eq 200) -and ($e18cj2.code -eq 409) -and ($e18cj2.body -like '*VIAGEM_CANCELAMENTO_EXIGE_OCORRENCIA*')) ("viajar iniciada (EM_ANDAMENTO) e cancelada apenas com justificativa -> $($e18cj2.code) (409 VIAGEM_CANCELAMENTO_EXIGE_OCORRENCIA nomeada; pos-inicio exige ocorrenciaId vinculada)")
# ---- BR-4.7: substituicao gera nova versao (antiga SUBSTITUIDA; presencas/custos seguem)
$e18t6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d7 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM1 + ',"kmEstimado":40,"tarifaKm":2}')
$e18t6id = Get-E18Data $e18t6.body
$e18sub0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t6id + "/substituir") -Base $ApiBase -Body '{"veiculoId":null,"motoristaId":null,"justificativa":"sem recurso novo"}'
Add-Check 'E18' 'substituicao.sem-recurso-novo-400' (($e18t6.code -eq 200) -and ($e18sub0.code -eq 400) -and ($e18sub0.body -like '*VIAGEM_SUBSTITUICAO_SEM_RECURSO*')) ("substituir sem veiculoId nem motoristaId -> $($e18sub0.code) (400 VIAGEM_SUBSTITUICAO_SEM_RECURSO nomeada; ao menos um recurso novo e a justificativa sao exigidos)")
$e18t5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d6 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM2 + ',"kmEstimado":40,"tarifaKm":2}')
$e18t5id = Get-E18Data $e18t5.body
$e18pA1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t5id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"PRESENTE"}')
$e18pB1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t5id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alB.text + ',"status":"FALTANTE"}')
$e18sub1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t5id + "/substituir") -Base $ApiBase -Body ('{"veiculoId":' + $e18idV2 + ',"motoristaId":' + $e18idM1 + ',"justificativa":"troca de veiculo por revisao"}')
$e18t5bid = Get-E18Data $e18sub1.body
$e18dbSub = Get-Psql ("select coalesce(o.status,'(nil)')||'|'||coalesce(n.status,'(nil)')||'|'||coalesce(n.versao,(0)::bigint)||'|'||coalesce(n.veiculo_id,-1)||'|'||coalesce(n.motorista_id,-1) from sigov.educacao_transporte_viagem o join sigov.educacao_transporte_viagem n on n.id=" + $e18t5bid + " where o.tenant_id=910001 and o.id=" + $e18t5id + ";")
Add-Check 'E18' 'substituicao.nova-versao-programada-recursos-trocados' (($e18pA1.code -eq 200) -and ($e18pB1.code -eq 200) -and ($e18sub1.code -eq 200) -and ($e18dbSub.text -eq ('SUBSTITUIDA|PROGRAMADA|2|' + $e18idV2 + '|' + $e18idM1))) ("substituir (novos veiculo+motorista aptos, exclusos dos backstops via id<>self) -> $($e18sub1.code); banco antiga|nova(status)|versao|veiculo|motorista: $($e18dbSub.text) (BR-4.7: nova versao PROGRAMADA mantendo data/hora, original SUBSTITUIDA)")
$e18dbSub2 = Get-Psql ("select (select count(1) from sigov.educacao_transporte_frequencia where tenant_id=910001 and referencia_id=" + $e18t5bid + " and tipo='PRESENCA_TRANSPORTE' and ativo and not is_deleted)||'|'||(select count(1) from sigov.educacao_transporte_custo where tenant_id=910001 and referencia_id=" + $e18t5bid + " and ativo and not is_deleted)||'|'||(select count(1) from sigov.educacao_transporte_custo where tenant_id=910001 and referencia_id=" + $e18t5id + " and ativo and not is_deleted);")
Add-Check 'E18' 'substituicao.presencas-e-custos-seguidos-para-nova-versao' (($e18dbSub2.text -like ('2|1|0*'))) ("apos substituicao: presencas referenciando a nova versao|custos na nova|custos restantes na original: $($e18dbSub2.text) (linha SUBSTITUIDA fica como cronologia; os dados operacionais migram)")
# ---- BR-4.4: presenca individual, fechamento e estorno unitario
$e18t7 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d8 + '","veiculoId":' + $e18idV6 + ',"motoristaId":' + $e18idM5 + '}')
$e18t7id = Get-E18Data $e18t7.body
$e18pA = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"PRESENTE"}')
$e18pAid = Get-E18Data $e18pA.body
$e18pA2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"PRESENTE"}')
$e18pA2id = Get-E18Data $e18pA2.body
$e18dbPA = Get-Psql ("select count(1) from sigov.educacao_transporte_frequencia where tenant_id=910001 and referencia_id=" + $e18t7id + " and aluno_id=" + $e12alA.text + " and tipo='PRESENCA_TRANSPORTE' and ativo and not is_deleted;")
Add-Check 'E18' 'presenca.idempotente-mesmo-status-mesmo-id' (($e18t7.code -eq 200) -and ($e18pA.code -eq 200) -and ($e18pA2.code -eq 200) -and ($e18pAid -eq $e18pA2id) -and ($e18dbPA.text -eq '1')) ("lancar PRESENTE e relancar o mesmo status -> ambos 200 com o MESMO id e 1 unica linha no banco (idempotencia de registro)")
$e18pB = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alB.text + ',"status":"FALTANTE"}')
$e18pBad = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"FOO"}')
Add-Check 'E18' 'presenca.faltante-ok-status-invalido-400' (($e18pB.code -eq 200) -and ($e18pBad.code -eq 400) -and ($e18pBad.body -like '*PRESENCIA_STATUS_INVALIDO*')) ("FALTANTE do segundo aluno -> 200; status 'FOO' -> $($e18pBad.code) (400 PRESENCIA_STATUS_INVALIDO nomeada; canonico PRESENTE/FALTANTE/DESLIGADO)")
$e18pGhost = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body '{"alunoId":9999999999,"status":"PRESENTE"}'
Add-Check 'E18' 'presenca.aluno-inexistente-404' (($e18pGhost.code -eq 404) -and ($e18pGhost.body -like '*TRANSPORTE_ALUNO_INEXISTENTE*')) ("aluno 9999999999 -> $($e18pGhost.code) (404 TRANSPORTE_ALUNO_INEXISTENTE nomeada)")
$e18al7 = Get-Psql "select coalesce((select a.id from sigov.aluno a where a.tenant_id=910001 and a.codigo_aluno like 'C1S3-%' and a.ativo and not a.is_deleted and a.entidade_id=(select entidade_id from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S8-ALA' limit 1) and not exists(select 1 from sigov.educacao_transporte_aluno ta where ta.tenant_id=910001 and ta.aluno_id=a.id and ta.ativo and not ta.is_deleted) order by a.id limit 1),-1);"
$e18pOut = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e18al7.text + ',"status":"PRESENTE"}')
Add-Check 'E18' 'presenca.aluno-sem-vinculo-na-rota-409' (($e18pOut.code -eq 409) -and ($e18pOut.body -like '*PRESENCA_ALUNO_FORA_DA_ROTA*')) ("aluno valido do tenant (fixture C1S3) sem vinculo ativo em rota de transporte -> $($e18pOut.code) (409 PRESENCA_ALUNO_FORA_DA_ROTA nomeada; o dominio de origem da presenca e o vinculo a rota)")
$e18fech1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/fechar") -Base $ApiBase
$e18dbFech = Get-Psql ("select coalesce(presenca_fechada,false) from sigov.educacao_transporte_viagem where tenant_id=910001 and id=" + $e18t7id + " limit 1;")
Add-Check 'E18' 'presencas.fechar-integro-200' (($e18fech1.code -eq 200) -and ($e18dbFech.text -eq 't')) ("fechar com todos os 2 vinculados registrados -> $($e18fech1.code); banco presenca_fechada: $($e18dbFech.text) (BR-4.4 fechamento impede novos lancamentos)")
$e18pFech = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"DESLIGADO"}')
Add-Check 'E18' 'presencas.lancamento-pos-fechamento-exige-estorno-409' (($e18pFech.code -eq 409) -and ($e18pFech.body -like '*PRESENCAS_FECHADAS_ESTORNO_OBRIGATORIO*')) ("novo lancamento na viagem fechada -> $($e18pFech.code) (409 PRESENCAS_FECHADAS_ESTORNO_OBRIGATORIO nomeada)")
$e18est0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/" + $e18pAid + "/estornar") -Base $ApiBase -Body '{"justificativa":""}'
Add-Check 'E18' 'presencas.estorno-sem-justificativa-400' (($e18est0.code -eq 400) -and ($e18est0.body -like '*PRESENCIA_ESTORNO_JUSTIFICATIVA_OBRIGATORIA*')) ("estorno sem justificativa -> $($e18est0.code) (400 nomeada; compensacao sempre justificada)")
$e18est1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/" + $e18pAid + "/estornar") -Base $ApiBase -Body '{"justificativa":"erro de lancamento pelo monitor"}'
$e18pRe = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"PRESENTE"}')
$e18pReid = Get-E18Data $e18pRe.body
$e18dbEst = Get-Psql ("select coalesce(status,'(nil)')||'|'||coalesce(nullif(justificativa,''),'(nil)') from sigov.educacao_transporte_frequencia where tenant_id=910001 and id=" + $e18pAid + " limit 1;")
Add-Check 'E18' 'presencas.estorno-unitario-desbloqueia-relancamento' (($e18est1.code -eq 200) -and ($e18pRe.code -eq 200) -and ($e18pReid -eq $e18pAid) -and ($e18dbEst.text -eq 'PRESENTE|erro de lancamento pelo monitor')) ("estorno com justificativa -> 200 e relancamento reabre o MESMO registro (id $e18pAid); banco status|justificativa: $($e18dbEst.text) (estorno desbloqueia um registro por vez)")
$e18fech2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t7id + "/presencas/fechar") -Base $ApiBase
Add-Check 'E18' 'presencas.ja-fechadas-409' (($e18fech2.code -eq 409) -and ($e18fech2.body -like '*PRESENCAS_JA_FECHADAS*')) ("segundo fechamento -> $($e18fech2.code) (409 PRESENCAS_JA_FECHADAS nomeada; fechamento e unico)")
$e18t8 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d9 + '","veiculoId":' + $e18idV6 + ',"motoristaId":' + $e18idM5 + '}')
$e18t8id = Get-E18Data $e18t8.body
$e18pA8 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t8id + "/presencas/lancar") -Base $ApiBase -Body ('{"alunoId":' + $e12alA.text + ',"status":"PRESENTE"}')
$e18fech3 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t8id + "/presencas/fechar") -Base $ApiBase
Add-Check 'E18' 'presencas.fechar-com-vinculado-sem-registro-409' (($e18t8.code -eq 200) -and ($e18pA8.code -eq 200) -and ($e18fech3.code -eq 409) -and ($e18fech3.body -like '*PRESENCAS_INCOMPLETAS*')) ("fechar com apenas 1 dos 2 vinculados registrado -> $($e18fech3.code) (409 PRESENCAS_INCOMPLETAS nomeada; o fechamento exige cobertura integral da rota)")
# ---- BR-4.5: ocorrencias com gravidade, ALTA => OCORRENCIA_GRAVE, desfecho e carona
$e18t9 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d10 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM1 + '}')
$e18t9id = Get-E18Data $e18t9.body
$e18oc1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/lancar") -Base $ApiBase -Body '{"tipo":"QUEBRA","gravidade":"BAIXA","descricao":"avario de pneu traseiro em rodovia"}'
Add-Check 'E18' 'ocorrencia.baixa-lancada-200' (($e18t9.code -eq 200) -and ($e18oc1.code -eq 200)) ("ocorrencia QUEBRA/BAIXA com descricao -> $($e18oc1.code) (BAIXA/MEDIA nao alteram a situacao da viagem)")
$e18oc2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/lancar") -Base $ApiBase -Body '{"tipo":"ACIDENTE","gravidade":"ALTA","descricao":"colisao leve em curva, danos materiais"}'
$e18oc2id = Get-E18Data $e18oc2.body
$e18dbOc = Get-Psql ("select status from sigov.educacao_transporte_viagem where tenant_id=910001 and id=" + $e18t9id + " limit 1;")
Add-Check 'E18' 'ocorrencia.alta-grava-ocorrencia-grave-na-viagem' (($e18oc2.code -eq 200) -and ($e18dbOc.text -eq 'OCORRENCIA_GRAVE')) ("ocorrencia ACIDENTE/ALTA -> $($e18oc2.code); banco status da viagem: $($e18dbOc.text) (BR-4.5 ALTA muda a situacao da viagem e guarda o status anterior para o desfecho)")
$e18oc3 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/lancar") -Base $ApiBase -Body '{"tipo":"ACIDENTE","gravidade":"ALTA","descricao":"segunda ocorrencia alta"}'
Add-Check 'E18' 'ocorrencia.alta-duplicada-sem-desfecho-409' (($e18oc3.code -eq 409) -and ($e18oc3.body -like '*OCORRENCIA_ALTA_VIGENTE*')) ("segunda ALTA com a primeira ainda sem desfecho -> $($e18oc3.code) (409 OCORRENCIA_ALTA_VIGENTE nomeada; backstop ux da ALTA vigente por viagem)")
$e18ocTipo = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/lancar") -Base $ApiBase -Body '{"tipo":"OUTRO","gravidade":"BAIXA","descricao":"tipo invalido"}'
Add-Check 'E18' 'ocorrencia.tipo-invalido-400' (($e18ocTipo.code -eq 400) -and ($e18ocTipo.body -like '*OCORRENCIA_TRANSPORTE_TIPO_INVALIDO*')) ("tipo 'OUTRO' -> $($e18ocTipo.code) (400 nomeada; canonico ACIDENTE/QUEBRA/CLIMA/ROTINEIRA_OUTRA)")
$e18ocGrav = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/lancar") -Base $ApiBase -Body '{"tipo":"QUEBRA","gravidade":"SUPER","descricao":"gravidade invalida"}'
Add-Check 'E18' 'ocorrencia.gravidade-invalida-400' (($e18ocGrav.code -eq 400) -and ($e18ocGrav.body -like '*OCORRENCIA_TRANSPORTE_GRAVIDADE_INVALIDA*')) ("gravidade 'SUPER' -> $($e18ocGrav.code) (400 nomeada; canonico BAIXA/MEDIA/ALTA)")
$e18ocDesc = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/lancar") -Base $ApiBase -Body '{"tipo":"QUEBRA","gravidade":"BAIXA"}'
Add-Check 'E18' 'ocorrencia.sem-descricao-400' (($e18ocDesc.code -eq 400) -and ($e18ocDesc.body -like '*OCORRENCIA_TRANSPORTE_DESCRICAO_OBRIGATORIA*')) ("sem descricao -> $($e18ocDesc.code) (400 nomeada; descricao do fato e obrigatoria)")
$e18desf0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/" + $e18oc2id + "/desfechar") -Base $ApiBase -Body '{"relatorio":""}'
Add-Check 'E18' 'desfecho.sem-relatorio-400' (($e18desf0.code -eq 400) -and ($e18desf0.body -like '*OCORRENCIA_DESFECHO_RELATORIO_OBRIGATORIO*')) ("desfechar ALTA sem relatorio -> $($e18desf0.code) (400 nomeada; o relatorio autoriza a liberacao do veiculo)")
$e18desf1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/" + $e18oc2id + "/desfechar") -Base $ApiBase -Body '{"relatorio":"relatorio tecnico: danos materiais leves, sem feridos, veiculo liberado apos reparo"}'
$e18dbDesf = Get-Psql ("select v.status||'|'||case when o.desfecho_em is null then '(nil)' else 'ok' end||'|'||coalesce(o.desfecho_relatorio,'(nil)' )||'|'||coalesce(o.dados->>'status_anterior','(nil)') from sigov.educacao_transporte_viagem v join sigov.educacao_transporte_ocorrencia o on o.id=" + $e18oc2id + " where v.tenant_id=910001 and v.id=" + $e18t9id + " limit 1;")
Add-Check 'E18' 'desfecho.com-relatorio-restaura-status-anterior' (($e18desf1.code -eq 200) -and ($e18dbDesf.text -eq 'PROGRAMADA|ok|relatorio tecnico: danos materiais leves, sem feridos, veiculo liberado apos reparo|PROGRAMADA')) ("desfecho com relatorio -> $($e18desf1.code); banco statusViagem|desfechoEm|relatorio|statusAnterior: $($e18dbDesf.text) (desfecho restaura o status anterior guardado na ocorrencia)")
$e18desf2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t9id + "/ocorrencias/" + $e18oc2id + "/desfechar") -Base $ApiBase -Body '{"relatorio":"repeticao do desfecho"}'
Add-Check 'E18' 'desfecho.repetido-e-idempotente-200' (($e18desf2.code -eq 200) -and ([long](($e18desf2.body | ConvertFrom-Json).data) -eq $e18oc2id)) ("segundo desfecho da mesma ocorrencia -> $($e18desf2.code) com o mesmo id (desfecho unico: repeticao e idempotente, nao erro)")
$e18car = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/transporte/viagens/lancar' -Base $ApiBase -Body ('{"rotaId":' + $e12r1.text + ',"data":"' + $e18d11 + '","veiculoId":' + $e18idV1 + ',"motoristaId":' + $e18idM2 + '}')
Add-Check 'E18' 'carona.veiculo-com-grave-recente-bloqueia-nova-viagem-409' (($e18car.code -eq 409) -and ($e18car.body -like '*VIAGEM_OCORRENCIA_GRAVE_CARONA*')) ("nova viagem do mesmo veiculo dentro da janela parametrica de 24h apos o desfecho da ALTA -> $($e18car.code) (409 VIAGEM_OCORRENCIA_GRAVE_CARONA nomeada; janela TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS do banco)")
# ---- BR-4.6: custo realizado unico, imutavel, nao altera o estimado
$e18cr1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/custos/realizar") -Base $ApiBase -Body '{"valor":400,"documento":"NF-C2S4-0001"}'
$e18dbCr = Get-Psql ("select case when exists(select 1 from sigov.educacao_transporte_custo where tenant_id=910001 and referencia_id=" + $e18t1id + " and tipo='REALIZADO' and ativo and not is_deleted and abs(valor-400.0)<0.001) then 'ok' else 'diff' end||'|'||case when exists(select 1 from sigov.educacao_transporte_custo where tenant_id=910001 and referencia_id=" + $e18t1id + " and tipo='ESTIMADO' and ativo and not is_deleted and abs(valor-375.0)<0.001) then 'ok' else 'diff' end||'|'||coalesce((select descricao from sigov.educacao_transporte_custo where tenant_id=910001 and referencia_id=" + $e18t1id + " and tipo='REALIZADO' limit 1),'(nil)') from (select 1 as x) base;")
Add-Check 'E18' 'custo.realizado-registrado-com-estimado-intacto' (($e18cr1.code -eq 200) -and ($e18dbCr.text -eq 'ok|ok|NF-C2S4-0001')) ("comando proprio custos/realizar (400, NF-C2S4-0001) na viagem CONCLUIDA -> $($e18cr1.code); banco realizado|estimado|documento: $($e18dbCr.text) (BR-4.6: realizado e imutavel; estimado do planejamento permanece intacto)")
$e18cr2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/custos/realizar") -Base $ApiBase -Body '{"valor":410,"documento":"NF-C2S4-0002"}'
Add-Check 'E18' 'custo.realizado-duplicado-409' (($e18cr2.code -eq 409) -and ($e18cr2.body -like '*CUSTO_REALIZADO_EXISTENTE*')) ("segundo custo realizado na mesma viagem -> $($e18cr2.code) (409 CUSTO_REALIZADO_EXISTENTE nomeada; sem edicao manual do realizado)")
$e18cr0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/custos/realizar") -Base $ApiBase -Body '{"valor":0,"documento":"NF-C2S4-0000"}'
Add-Check 'E18' 'custo.realizado-valor-invalido-400' (($e18cr0.code -eq 400) -and ($e18cr0.body -like '*CUSTO_REALIZADO_VALOR_INVALIDO*')) ("valor 0 -> $($e18cr0.code) (400 nomeada; o custo efetivo deve ser positivo)")
# ---- BR-4.8: relatorio de viagem (presenca + ocorrencias + custos c/ desvio)
$e18rel = Invoke-Api -Bearer $tk.token -Method GET -Path ("/api/educacao/transporte/viagens/" + $e18t1id + "/relatorio") -Base $ApiBase
$relj = $null; try { $relj = ($e18rel.body | ConvertFrom-Json) } catch {}
$e18relOk = ($e18rel.code -eq 200) -and $null -ne $relj -and ($relj.data.status -eq 'CONCLUIDA') -and ([long]$relj.data.versao -eq 1) -and ([long]$relj.data.alunosVinculados -eq 2) -and ([long]$relj.data.custoEstimado -eq 375) -and ([long]$relj.data.custoRealizado -eq 400) -and ([long]$relj.data.desvioCustoAbsoluto -eq 25) -and ($relj.data.desvioCustoPct -eq 6.67) -and (@($relj.data.alunos).Count -eq 2) -and (@($relj.data.ocorrencias).Count -eq 0) -and ($relj.data.origem -like '*sigov.educacao_transporte_viagem*')
Add-Check 'E18' 'relatorio.viagem-completa-com-desvio-de-custo' $e18relOk ("GET viagens/{id}/relatorio -> $($e18rel.code); status CONCLUIDA versao 1, 2 alunos vinculados, custoEstimado 375 x custoRealizado 400 -> desvioAbsoluto 25 / desvioPct 6.67%, ocorrencias 0; origem verificavel declarada no DTO (BR-4.8)")
$e18rel404 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/transporte/viagens/9999999999/relatorio' -Base $ApiBase
Add-Check 'E18' 'relatorio.viagem-inexistente-404' (($e18rel404.code -eq 404) -and ($e18rel404.body -like '*TRANSPORTE_VIAGEM_INEXISTENTE*')) ("relatorio da viagem 9999999999 -> $($e18rel404.code) (404 TRANSPORTE_VIAGEM_INEXISTENTE nomeada)")

# =============================================================================
# --- E19 (Ciclo 2 Sprint 5): FUNDEB/custos/rateio + avaliacao do magisterio + Educacenso
# =============================================================================
# BR-5.1 parametro FUNDEB com vigencia anual (ano_letivo persistida); BR-5.2 competencia
# fechada bloqueia receita/aplicacao/lancamento/rateio (COMPETENCIA_FUNDEB_FECHADA) e
# aplicacao unica por competencia x centro (legado sem competencia = sentinela 2999-12-31);
# BR-5.3 conformidade = aplicacoes MAGISTERIO / total vs minimo do BANCO (parametro tenant
# TAXA_MINIMA_APLICACAO prevalece sobre FDEB_CONFORMIDADE_MINIMO_PCT; regra 11); BR-5.4 rateio
# MATRICULAS|RECEITA|PESO_MANUAL com soma EXATA (residual no maior peso), idempotencia origem x
# criterio x competencia (RATEIO_DUPLICADO) e estorno comandado; BR-5.5 avaliacao com resultado
# unico por servidor x avaliacao, fechamento com consolidado imutavel e reabertura justificada;
# BR-5.6 decisao documentada das inconsistencias e encerramento do lote so com ALTA zerada e
# eventos de sincronizacao registrados; BR-5.7 projecao de matricula unica vigente por
# escola x ano (alimenta custo/aluno projetado na S20). Fixture propria (marcadores
# c1e26-c2s5-% / C2S5-% / 9999990006xx); estado fundeb/custo/avaliacao/educacenso do tenant
# zerado no inicio para determinismo (bloco final da suíte; CX re-limpa o tenant).
$fix19 = Get-Psql @"
delete from sigov.educacao_fundeb_competencia where tenant_id=910001;
delete from sigov.educacao_custo_rateio where tenant_id=910001;
delete from sigov.educacao_custo_lancamento where tenant_id=910001;
delete from sigov.educacao_avaliacao_resultado where tenant_id=910001;
delete from sigov.educacao_avaliacao_magisterio where tenant_id=910001;
delete from sigov.educacao_educacenso_evento where tenant_id=910001;
delete from sigov.educacao_educacenso_inconsistencia where tenant_id=910001;
delete from sigov.educacao_educacenso_lote where tenant_id=910001;
delete from sigov.educacao_projecao_matricula where tenant_id=910001;
delete from sigov.educacao_fundeb_aplicacao where tenant_id=910001;
delete from sigov.educacao_fundeb_receita where tenant_id=910001;
delete from sigov.educacao_fundeb_parametro where tenant_id=910001;
delete from sigov.educacao_custo_centro where tenant_id=910001;
insert into sigov.escola(tenant_id,entidade_id,codigo,nome,tipo_escola,situacao,created_by)
select 910001,$($e7ent.text),'c1e26-c2s5-esc','Fixture C2 S5 Escola','OURO','ATIVA',910301
where not exists(select 1 from sigov.escola where tenant_id=910001 and codigo='c1e26-c2s5-esc');
insert into sigov.ano_letivo(tenant_id,entidade_id,escola_id,ano,data_inicio,data_fim,status,created_by)
select 910001,$($e7ent.text),e.id,2026,'2026-02-02','2027-01-10','ABERTO',910301
from sigov.escola e where e.tenant_id=910001 and e.codigo='c1e26-c2s5-esc'
and not exists(select 1 from sigov.ano_letivo a where a.tenant_id=910001 and a.escola_id=e.id and a.ano=2026);
insert into sigov.curso(tenant_id,entidade_id,codigo,nome,etapa_ensino,modalidade,created_by)
select 910001,$($e7ent.text),'C2S5-CURSO','Fixture C2 S5 Curso','ENSINO_FUNDAMENTAL','REGULAR',910301
where not exists(select 1 from sigov.curso where tenant_id=910001 and codigo='C2S5-CURSO');
insert into sigov.serie_ano(tenant_id,entidade_id,curso_id,codigo,nome,ordem,created_by)
select 910001,$($e7ent.text),c.id,'C2S5-SERIE','Fixture C2 S5 Serie',1,910301
from sigov.curso c where c.tenant_id=910001 and c.codigo='C2S5-CURSO'
and not exists(select 1 from sigov.serie_ano where tenant_id=910001 and codigo='C2S5-SERIE');
insert into sigov.turma(tenant_id,entidade_id,escola_id,ano_letivo_id,curso_id,serie_ano_id,codigo,nome,turno,capacidade,vagas_ocupadas,status,created_by)
select 910001,$($e7ent.text),e.id,l.id,c.id,s.id,'c1e26-c2s5-turma','Fixture C2 S5 Turma','MATUTINO',3,0,'ABERTA',910301
from sigov.escola e join sigov.ano_letivo l on l.tenant_id=e.tenant_id and l.escola_id=e.id and l.ano=2026
join sigov.curso c on c.tenant_id=910001 and c.codigo='C2S5-CURSO'
join sigov.serie_ano s on s.tenant_id=910001 and s.codigo='C2S5-SERIE'
where e.tenant_id=910001 and e.codigo='c1e26-c2s5-esc'
and not exists(select 1 from sigov.turma where tenant_id=910001 and codigo='c1e26-c2s5-turma');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixture C2 S5 Aluno A','999999000601','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000601');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixture C2 S5 Aluno B','999999000602','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000602');
insert into sigov.pessoa(tenant_id,entidade_id,tipo_pessoa,nome,documento,classificacao_lgpd,created_by)
select 910001,$($e7ent.text),'F','Fixture C2 S5 Aluno C','999999000603','DADO_PESSOAL',910301
where not exists(select 1 from sigov.pessoa where documento='999999000603');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C2S5-ALA','ATIVO',910301 from sigov.pessoa p where p.documento='999999000601'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C2S5-ALA');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C2S5-ALB','ATIVO',910301 from sigov.pessoa p where p.documento='999999000602'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C2S5-ALB');
insert into sigov.aluno(tenant_id,entidade_id,pessoa_id,codigo_aluno,situacao,created_by)
select 910001,$($e7ent.text),p.id,'C2S5-ALC','ATIVO',910301 from sigov.pessoa p where p.documento='999999000603'
and not exists(select 1 from sigov.aluno where tenant_id=910001 and codigo_aluno='C2S5-ALC');
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,t.id,'C2S5-MAT-A',current_date,'ATIVA',true,910301
from sigov.aluno a join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-c2s5-esc'
join sigov.ano_letivo l on l.tenant_id=910001 and l.escola_id=e.id and l.ano=2026
join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-c2s5-turma'
where a.tenant_id=910001 and a.codigo_aluno='C2S5-ALA'
and not exists(select 1 from sigov.matricula where numero_matricula='C2S5-MAT-A');
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,t.id,'C2S5-MAT-B',current_date,'ATIVA',true,910301
from sigov.aluno a join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-c2s5-esc'
join sigov.ano_letivo l on l.tenant_id=910001 and l.escola_id=e.id and l.ano=2026
join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-c2s5-turma'
where a.tenant_id=910001 and a.codigo_aluno='C2S5-ALB'
and not exists(select 1 from sigov.matricula where numero_matricula='C2S5-MAT-B');
insert into sigov.matricula(tenant_id,entidade_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,ativo,created_by)
select 910001,$($e7ent.text),a.id,e.id,l.id,t.id,'C2S5-MAT-C',current_date,'ATIVA',true,910301
from sigov.aluno a join sigov.escola e on e.tenant_id=910001 and e.codigo='c1e26-c2s5-esc'
join sigov.ano_letivo l on l.tenant_id=910001 and l.escola_id=e.id and l.ano=2026
join sigov.turma t on t.tenant_id=910001 and t.codigo='c1e26-c2s5-turma'
where a.tenant_id=910001 and a.codigo_aluno='C2S5-ALC'
and not exists(select 1 from sigov.matricula where numero_matricula='C2S5-MAT-C');
insert into sigov.servidor(tenant_id,dados,created_by)
select 910001,'{"matricula":"C2S5-SV1"}',910301 where not exists(select 1 from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV1');
insert into sigov.servidor(tenant_id,dados,created_by)
select 910001,'{"matricula":"C2S5-SV2"}',910301 where not exists(select 1 from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV2');
insert into sigov.servidor(tenant_id,dados,created_by)
select 910001,'{"matricula":"C2S5-SV3"}',910301 where not exists(select 1 from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV3');
insert into sigov.servidor(tenant_id,dados,created_by)
select 910001,'{"matricula":"C2S5-SV4"}',910301 where not exists(select 1 from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV4');
insert into sigov.educacao_custo_centro(tenant_id,entidade_id,codigo,nome,categoria,escola_id,status,created_by)
select 910001,$($e7ent.text),'C2S5-CENTRO-MAG','C2S5 Centro Magisterio','MAGISTERIO',e.id,'ATIVA',910301
from sigov.escola e where e.tenant_id=910001 and e.codigo='c1e26-c2s5-esc'
and not exists(select 1 from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-MAG');
insert into sigov.educacao_custo_centro(tenant_id,entidade_id,codigo,nome,categoria,status,created_by)
select 910001,$($e7ent.text),'C2S5-CENTRO-OUTRO','C2S5 Centro Material','MATERIAL','ATIVA',910301
where not exists(select 1 from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-OUTRO');
insert into sigov.educacao_custo_centro(tenant_id,entidade_id,codigo,nome,status,created_by)
select 910001,$($e7ent.text),'C2S5-CENTRO-TERCEIRO','C2S5 Centro Sem Categoria','ATIVA',910301
where not exists(select 1 from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-TERCEIRO');
insert into sigov.educacao_custo_centro(tenant_id,entidade_id,codigo,nome,categoria,status,created_by)
select 910001,$($e7ent.text),'C2S5-CENTRO-ORIGEM','C2S5 Centro Origem Rateio','INFRAESTRUTURA','ATIVA',910301
where not exists(select 1 from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-ORIGEM');
select count(*)::text from sigov.educacao_custo_centro where tenant_id=910001 and codigo like 'C2S5-%';
"@
$e19esc = Get-Psql "select id from sigov.escola where tenant_id=910001 and codigo='c1e26-c2s5-esc';"
$e19cMag = Get-Psql "select id from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-MAG';"
$e19cOutro = Get-Psql "select id from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-OUTRO';"
$e19cTerceiro = Get-Psql "select id from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-TERCEIRO';"
$e19cOrigem = Get-Psql "select id from sigov.educacao_custo_centro where tenant_id=910001 and codigo='C2S5-CENTRO-ORIGEM';"
$e19sv1 = Get-Psql "select id from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV1';"
$e19sv2 = Get-Psql "select id from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV2';"
$e19sv3 = Get-Psql "select id from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV3';"
$e19sv4 = Get-Psql "select id from sigov.servidor where tenant_id=910001 and dados->>'matricula'='C2S5-SV4';"
function Get-E19Data { param([string]$Body) try { return ($Body | ConvertFrom-Json).data } catch { return $null } }
Add-Check 'E19' 'fixture.c2s5-pronto' (($fix19.code -eq 0) -and [long]$e19esc.text -gt 0 -and [long]$e19cMag.text -gt 0 -and [long]$e19cOutro.text -gt 0 -and [long]$e19cTerceiro.text -gt 0 -and [long]$e19cOrigem.text -gt 0 -and [long]$e19sv1.text -gt 0 -and [long]$e19sv4.text -gt 0) ("escola+ano+curso+serie+turma+3 matriculas ATIVA+4 servidores+4 centros (MAGISTERIO/MATERIAL/sem categoria/origem) + estado fundeb/custo/avaliacao/educacenso/projecao zerado -> escola=$($e19esc.text) mag=$($e19cMag.text) outro=$($e19cOutro.text) terceiro=$($e19cTerceiro.text) origem=$($e19cOrigem.text) sv=$($e19sv1.text)..$($e19sv4.text) (dados ficticios, multi-esfera pela entidade do contexto, regras 21-24)")

# --- E19.A: BR-5.1 parametro FUNDEB com vigencia anual -------------------------
$e19p1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/parametros/lancar' -Base $ApiBase -Body '{"codigo":"TAXA_MINIMA_APLICACAO","valor":60,"ano":2026}'
$e19pDb = Get-Psql "select coalesce(ano_letivo,-1)||'|'||valor::text from sigov.educacao_fundeb_parametro where tenant_id=910001 and upper(coalesce(codigo,''))='TAXA_MINIMA_APLICACAO' limit 1;"
Add-Check 'E19' 'parametro.vigencia-anual-200' (($e19p1.code -eq 200) -and ($e19pDb.text -eq '2026|60.00')) ("POST parametros/lancar {TAXA_MINIMA_APLICACAO,60,2026} -> $($e19p1.code); banco ano_letivo|valor: $($e19pDb.text) (BR-5.1: vigencia anual persistida; minimo parametrizado != default 70 => parametrizavel pelo banco)")
$e19p2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/parametros/lancar' -Base $ApiBase -Body '{"codigo":"TAXA_MINIMA_APLICACAO","valor":61,"ano":2026}'
Add-Check 'E19' 'parametro.duplicado-vigente-409' (($e19p2.code -eq 409) -and ($e19p2.body -like '*FUNDEB_PARAMETRO_CODIGO_EXISTENTE*')) ("segundo parametro vigente com mesmo codigo -> $($e19p2.code) (unicidade vigente por codigo; trocar a vigencia exige encerrar o existente)")
$e19p3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/parametros/lancar' -Base $ApiBase -Body '{"codigo":"C2S5-PARAANO","valor":5,"ano":1999}'
Add-Check 'E19' 'parametro.ano-invalido-400' (($e19p3.code -eq 400)) ("ano 1999 fora da faixa canonica 2000-2100 -> $($e19p3.code) (validacao explicita, sem sucesso simulado - regra 13)")

# --- E19.B: BR-5.2 competencia fechada + aplicacao unica por competencia x centro
$e19a1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cMag.text + ',"valor":4000}')
Add-Check 'E19' 'aplicacao.sem-competencia-legado-200' (($e19a1.code -eq 200)) ("aplicacao 4000 no centro MAGISTERIO sem competencia (caminho legado, bucket sentinela 2999-12-31) -> $($e19a1.code)")
$e19a2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cMag.text + ',"valor":4000}')
Add-Check 'E19' 'aplicacao.duplicada-mesmo-centro-409' (($e19a2.code -eq 409) -and ($e19a2.body -like '*FUNDEB_APLICACAO_DUPLICADA*')) ("segunda aplicacao vigente no mesmo centro/bucket -> $($e19a2.code) (backstop ux_fundeb_aplicacao_tenant_comp_centro + falha nomeada)")
$e19r1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","fonte":"FPE","valor":10000}')
$e19r2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","fonte":"FPE","valor":1}')
Add-Check 'E19' 'receita.competencia-aberta-200-duplicada-409' (($e19r1.code -eq 200) -and ($e19r2.code -eq 409) -and ($e19r2.body -like '*FUNDEB_RECEITA_DUPLICADA*')) ("receita FPE@$hoje 10000 aberta -> $($e19r1.code); duplicada mesma competencia/fonte -> $($e19r2.code)")
$e19a3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cOutro.text + ',"valor":4000,"competencia":"' + $hoje + '"}')
$e19a4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cOutro.text + ',"valor":4000,"competencia":"' + $hoje + '"}')
Add-Check 'E19' 'aplicacao.competencia-200-duplicada-409' (($e19a3.code -eq 200) -and ($e19a4.code -eq 409) -and ($e19a4.body -like '*FUNDEB_APLICACAO_DUPLICADA*')) ("aplicacao 4000 centro OUTRO @ $hoje -> $($e19a3.code); segunda na mesma competencia x centro -> $($e19a4.code) (braco com competencia do indice parcial)")
$e19f1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/fechar' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","justificativa":"Fechamento competencia homologacao C2S5"}')
$e19fechId = Get-E19Data $e19f1.body
$e19f2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/fechar' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '"}')
Add-Check 'E19' 'competencia.fechar-idempotente-200' (([long]$e19f1.code -eq 200) -and ($e19f2.code -eq 200) -and ((Get-E19Data $e19f2.body) -eq $e19fechId)) ("fechar $hoje -> $($e19f1.code) id=$e19fechId; repetir sem justificativa -> mesmo id $($e19fechId) (fechamento idempotente e auditoravel em educacao_fundeb_competencia)")
$e19r3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/receitas/lancar' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","fonte":"FPE","valor":500}')
$e19a5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cMag.text + ',"valor":100,"competencia":"' + $hoje + '"}')
Add-Check 'E19' 'lancamentos.competencia-fechada-409' (($e19r3.code -eq 409) -and ($e19r3.body -like '*COMPETENCIA_FUNDEB_FECHADA*') -and ($e19a5.code -eq 409) -and ($e19a5.body -like '*COMPETENCIA_FUNDEB_FECHADA*')) ("receita e aplicacao novas na competencia fechada -> $($e19r3.code)/$($e19a5.code) (COMPETENCIA_FUNDEB_FECHADA nomeada; BR-5.2)")
$e19rb1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/reabrir' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '"}')
$e19rb2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/reabrir' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","justificativa":"Retificacao homologacao C2S5"}')
$e19rb3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/reabrir' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","justificativa":"x"}')
$e19a6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/aplicacoes/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cMag.text + ',"valor":4000,"competencia":"' + $hoje + '"}')
Add-Check 'E19' 'competencia.reabrir-justificado-libera-200' (($e19rb1.code -eq 400) -and ($e19rb1.body -like '*COMPETENCIA_REABERTURA_JUSTIFICATIVA_OBRIGATORIA*') -and ($e19rb2.code -eq 200) -and ($e19rb3.code -eq 409) -and ($e19rb3.body -like '*COMPETENCIA_FUNDEB_NAO_FECHADA*') -and ($e19a6.code -eq 200)) ("reabrir sem justificativa -> $($e19rb1.code); com -> $($e19rb2.code); reabrir ja reaberta -> $($e19rb3.code); nova aplicacao apos reabertura -> $($e19a6.code) (magisterio total agora 8000)")

# --- E19.C: BR-5.3 painel de conformidade -------------------------------------
$e19cf1 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/fundeb/conformidade' -Base $ApiBase
$jCf1 = $null; try { $jCf1 = ($e19cf1.body | ConvertFrom-Json).data } catch {}
$dbCf = Get-Psql "select (select coalesce(sum(a.valor),0) from sigov.educacao_fundeb_aplicacao a inner join sigov.educacao_custo_centro cc on cc.id=a.centro_custo_id where a.tenant_id=910001 and a.ativo=true and a.is_deleted=false and upper(coalesce(cc.categoria,''))='MAGISTERIO')::text||'|'||(select coalesce(sum(valor),0) from sigov.educacao_fundeb_aplicacao where tenant_id=910001 and ativo=true and is_deleted=false)::text;"
Add-Check 'E19' 'conformidade.magisterio-vs-minimo-banco-200' (($e19cf1.code -eq 200) -and $null -ne $jCf1 -and ([math]::Abs([double]$jCf1.aplicacoesMagisterio - 8000) -lt 0.001) -and ([math]::Abs([double]$jCf1.aplicacoesTotal - 12000) -lt 0.001) -and ([math]::Abs([double]$jCf1.receitasTotal - 10000) -lt 0.001) -and ([math]::Abs([double]$jCf1.percentual - 66.67) -lt 0.005) -and ([double]$jCf1.minimoPct -eq 60) -and ($jCf1.emConformidade -eq $true)) ("percentual=" + $jCf1.percentual + " minimo=" + $jCf1.minimoPct + " (parametro TENANT TAXA_MINIMA_APLICACAO=60 prevalece sobre o modulo 70; regra 11); mag|total no banco: " + $dbCf.text + " (BR-5.3: 8000/12000=66.67% >= 60% => em conformidade; LC 114/2021)")
$e19cf2 = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/fundeb/conformidade?competencia=2026-01-01' -Base $ApiBase
$jCf2 = $null; try { $jCf2 = ($e19cf2.body | ConvertFrom-Json).data } catch {}
Add-Check 'E19' 'conformidade.recorte-vazio-zero-por-cento-200' (($e19cf2.code -eq 200) -and $null -ne $jCf2 -and ([double]$jCf2.percentual -eq 0) -and ($jCf2.emConformidade -eq $false) -and ([string]$jCf2.nota -like '*sem aplicacoes*')) ("recorte 2026-01-01 sem aplicacoes -> percentual 0% com nota explicita e NAO indisponibilidade; nota=" + $jCf2.nota)
$e19cf3 = Invoke-Api -Bearer $tk.token -Method GET -Path ("/api/educacao/fundeb/conformidade?unidadeEscolaId=" + $e19esc.text) -Base $ApiBase
$jCf3 = $null; try { $jCf3 = ($e19cf3.body | ConvertFrom-Json).data } catch {}
Add-Check 'E19' 'conformidade.recorte-unidade-escolar-200' (($e19cf3.code -eq 200) -and $null -ne $jCf3 -and ([math]::Abs([double]$jCf3.aplicacoesTotal - 8000) -lt 0.001) -and ([math]::Abs([double]$jCf3.percentual - 100) -lt 0.005) -and ($jCf3.emConformidade -eq $true)) ("recorte por unidade escolar: somente centros vinculados a escola (MAGISTERIO 8000) -> total=8000 pct=" + $jCf3.percentual + " (BR-5.3 recorte unidade escolar x competencia)")

# --- E19.D: BR-5.4 lancamento tipado de custo + rateio com estorno -------------
$e19lc1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/lancamentos/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","valor":100.04}')
$e19lc2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/lancamentos/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","valor":-50}')
Add-Check 'E19' 'lancamento.custo-tipado-200-valor-invalido-400' (($e19lc1.code -eq 200) -and ($e19lc2.code -eq 400)) ("lancamento 100.04 origem@$hoje -> $($e19lc1.code) (fecha o residuo S10: total do rateio auditavel por origem x competencia); valor -50 -> $($e19lc2.code)")
$pmPesos = '{"' + $e19cMag.text + '":1,"' + $e19cOutro.text + '":1,"' + $e19cTerceiro.text + '":1}'
$e19rm1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"PESO_MANUAL","pesosManual":' + $pmPesos + '}')
$e19rmId = Get-E19Data $e19rm1.body
$dbRm = Get-Psql "select (select count(*) from sigov.educacao_custo_lancamento where tenant_id=910001 and referencia_id=$e19rmId and tipo='RATEIO_DERIVADO' and status='ATIVA')||'|'||(select coalesce(sum(valor),0)::numeric(18,2) from sigov.educacao_custo_lancamento where tenant_id=910001 and referencia_id=$e19rmId and tipo='RATEIO_DERIVADO' and status='ATIVA')::text||'|'||(select status||'|'||tipo||'|'||valor::text from sigov.educacao_custo_rateio where tenant_id=910001 and id=$e19rmId);"
Add-Check 'E19' 'rateio.peso-manual-soma-exata-200' (($e19rm1.code -eq 200) -and ($dbRm.text -eq '3|100.04|ATIVA|PESO_MANUAL|100.04')) ("3 lancamentos derivados somam EXATAMENTE o total 100.04 (residual absorvido no maior peso); cabecalho ATIVA PESO_MANUAL 100.04 atomicamente (BR-5.4); banco: " + $dbRm.text)
$e19rm2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"PESO_MANUAL","pesosManual":' + $pmPesos + '}')
$e19rm3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"OUTRA","pesosManual":' + $pmPesos + '}')
$e19rm4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"2026-01-01","criterio":"PESO_MANUAL","pesosManual":' + $pmPesos + '}')
Add-Check 'E19' 'rateio.duplicado-criterio-total-4xx' (($e19rm2.code -eq 409) -and ($e19rm2.body -like '*RATEIO_DUPLICADO*') -and ($e19rm3.code -eq 400) -and ($e19rm3.body -like '*RATEIO_CRITERIO_INVALIDO*') -and ($e19rm4.code -eq 400) -and ($e19rm4.body -like '*RATEIO_TOTAL_INVALIDO*')) ("rateio repetido origem x PESO_MANUAL x $hoje -> $($e19rm2.code); criterio OUTRA -> $($e19rm3.code); sem lancamentos @2026-01-01 -> $($e19rm4.code) (BR-5.4 falhas nomeadas)")
$e19rmt = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"MATRICULAS"}')
$e19rmtId = Get-E19Data $e19rmt.body
$dbRt = Get-Psql "select (select count(*)||'|'||coalesce(sum(valor),0)::numeric(18,2)::text from sigov.educacao_custo_lancamento where tenant_id=910001 and referencia_id=$e19rmtId and tipo='RATEIO_DERIVADO' and status='ATIVA')||'|'||(select centro_custo_id from sigov.educacao_custo_lancamento where tenant_id=910001 and referencia_id=$e19rmtId and tipo='RATEIO_DERIVADO' and status='ATIVA' order by id limit 1);"
Add-Check 'E19' 'rateio.matriculas-peso-por-escola-200' (($e19rmt.code -eq 200) -and ($dbRt.text -eq ('1|100.04|' + $e19cMag.text))) ("critério MATRICULAS: somente o centro com escola que tem matriculas ATIVA recebe o total 100.04 (3 matriculas); banco: " + $dbRt.text)
$e19rr = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"RECEITA"}')
$e19rrId = Get-E19Data $e19rr.body
$dbRr = Get-Psql "select count(*)||'|'||coalesce(sum(valor),0)::numeric(18,2)::text||'|'||count(distinct centro_custo_id) from sigov.educacao_custo_lancamento where tenant_id=910001 and referencia_id=$e19rrId and tipo='RATEIO_DERIVADO' and status='ATIVA';"
Add-Check 'E19' 'rateio.receita-aplicacoes-fundeb-200' (($e19rr.code -eq 200) -and ($dbRr.text -eq '2|100.04|2')) ("critério RECEITA: pesos pelas aplicacoes FUNDEB dos destinos (mag 8000 x outro 4000; terceiro sem aplicacao fica fora); 2 derivados somam 100.04; banco: " + $dbRr.text)
$e19est0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/custos/rateios/" + $e19rmId + "/estornar") -Base $ApiBase -Body '{}'
$e19est1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/custos/rateios/" + $e19rmId + "/estornar") -Base $ApiBase -Body '{"justificativa":"Estorno homologacao C2S5"}'
$dbEst = Get-Psql "select (select status from sigov.educacao_custo_rateio where tenant_id=910001 and id=$e19rmId)||'|'||(select count(*) from sigov.educacao_custo_lancamento where tenant_id=910001 and referencia_id=$e19rmId and tipo='RATEIO_DERIVADO' and status='CANCELADA' and coalesce(justificativa,'') like 'ESTORNO_RATEIO:%')"
$e19est2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/custos/rateios/" + $e19rmId + "/estornar") -Base $ApiBase -Body '{"justificativa":"idempotente"}'
Add-Check 'E19' 'rateio.estorno-comandado-idempotente-200' (($e19est0.code -eq 400) -and ($e19est0.body -like '*RATEIO_ESTORNO_JUSTIFICATIVA_OBRIGATORIA*') -and ($e19est1.code -eq 200) -and ($dbEst.text -eq 'ESTORNADO|3') -and ($e19est2.code -eq 200)) ("estorno sem justificativa -> $($e19est0.code); comandado -> cabecalho ESTORNADO + 3 derivados CANCELADOS com ESTORNO_RATEIO:$($e19rmId) (banco: " + $dbEst.text + "); repetir -> 200 idempotente (BR-5.4)")
$e19rm5 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"PESO_MANUAL","pesosManual":' + $pmPesos + '}')
Add-Check 'E19' 'rateio.estorno-libera-unicidade-200' (($e19rm5.code -eq 200) -and ((Get-E19Data $e19rm5.body) -ne $e19rmId)) ("mesma origem x PESO_MANUAL x competencia relancada apos estorno -> 200 com novo id (ESTORNADO sai do indice parcial; estado terminal libera a posicao)")
$e19est404 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/9999999999/estornar' -Base $ApiBase -Body '{"justificativa":"probe"}'
Add-Check 'E19' 'rateio.inexistente-404' (($e19est404.code -eq 404) -and ($e19est404.body -like '*RATEIO_CUSTO_INEXISTENTE*')) ("estornar rateio 9999999999 -> $($e19est404.code) (404 nomeada)")
$e19f3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/fechar' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '"}')
$e19lc3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/lancamentos/lancar' -Base $ApiBase -Body ('{"centroCustoId":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","valor":10}')
$e19rm6 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/custos/rateios/lancar' -Base $ApiBase -Body ('{"centroCustoIdOrigem":' + $e19cOrigem.text + ',"competencia":"' + $hoje + '","criterio":"MATRICULAS"}')
$e19rb4 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/fundeb/competencias/reabrir' -Base $ApiBase -Body ('{"competencia":"' + $hoje + '","justificativa":"Reabre para secao de avaliacao C2S5"}')
Add-Check 'E19' 'rateio.competencia-fechada-bloqueia-409' (($e19f3.code -eq 200) -and ($e19lc3.code -eq 409) -and ($e19lc3.body -like '*COMPETENCIA_FUNDEB_FECHADA*') -and ($e19rm6.code -eq 409) -and ($e19rm6.body -like '*COMPETENCIA_FUNDEB_FECHADA*') -and ($e19rb4.code -eq 200)) ("re-fechada: lancamento de custo -> $($e19lc3.code) e novo rateio -> $($e19rm6.code) ambos COMPETENCIA_FUNDEB_FECHADA; reaberta de novo -> 200 (BR-5.2 x BR-5.4)")

# --- E19.E: BR-5.5 avaliacao do magisterio ------------------------------------
$e19av0 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/avaliacao-magisterio/lancar' -Base $ApiBase -Body ('{"nome":"C2S5 Aval Sem Instrumento","periodoInicio":"' + $hoje + '","periodoFim":"' + $hoje + '"}')
$e19av0b = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/avaliacao-magisterio/lancar' -Base $ApiBase -Body '{"nome":"C2S5 Aval Periodo Inverso","periodoInicio":"2026-06-01","periodoFim":"2026-01-01","instrumento":"INSTRUMENTO_C2S5"}'
$e19av1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/avaliacao-magisterio/lancar' -Base $ApiBase -Body ('{"nome":"C2S5 Avaliacao Magisterio 2026","periodoInicio":"' + $hoje + '","periodoFim":"' + $hoje + '","instrumento":"INSTRUMENTO_C2S5","unidadeId":' + $e19esc.text + ',"ano":2026}')
$e19av1Id = Get-E19Data $e19av1.body
Add-Check 'E19' 'avaliacao.lancar-instrumento-obrigatorio-200' (($e19av0.code -eq 400) -and ($e19av0.body -like '*AVALIACAO_INSTRUMENTO_OBRIGATORIA*') -and ($e19av0b.code -eq 400) -and ($e19av0b.body -like '*AVALIACAO_PERIODO_INVALIDO*') -and ($e19av1.code -eq 200)) ("sem instrumento -> $($e19av0.code); periodo invertido -> $($e19av0b.code); valido (nome+periodo+instrumento+unidade+ano, escopo validado) -> $($e19av1.code) id=$e19av1Id (BR-5.5)")
$e19rt0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv1.text + ',"nota":11}')
$e19rt404 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body '{"servidorId":9999999999,"nota":5}'
$e19rt1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv1.text + ',"nota":9}')
$e19rt2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv1.text + ',"nota":8}')
Add-Check 'E19' 'avaliacao.resultado-validacao-servidor-200' (($e19rt0.code -eq 400) -and ($e19rt0.body -like '*AVALIACAO_NOTA_INVALIDO*') -and ($e19rt404.code -eq 404) -and ($e19rt404.body -like '*AVALIACAO_SERVIDOR_INEXISTENTE*') -and ($e19rt1.code -eq 200) -and ($e19rt2.code -eq 409) -and ($e19rt2.body -like '*AVALIACAO_RESULTADO_EXISTENTE*')) ("nota 11 fora da escala 0..10 -> $($e19rt0.code); servidor 9999999999 -> $($e19rt404.code); sv1 nota 9 -> 200; sv1 de novo -> 409 (1 resultado vigente por servidor x avaliacao; backstop + nomeada)")
$e19rt3 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv2.text + ',"nota":7}')
$e19rt4 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv3.text + ',"nota":5}')
$e19fe1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/fechar") -Base $ApiBase -Body '{}'
$e19fe2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/fechar") -Base $ApiBase -Body '{}'
$dbCons = Get-Psql "select status||'|'||round((dados->'consolidado'->>'media')::numeric,2)::text||'|'||coalesce(dados->'consolidado'->>'servidoresAvaliados','?')||'|'||((dados->'consolidado'->>'participacaoPct')::numeric between 0 and 100)::text from sigov.educacao_avaliacao_magisterio where tenant_id=910001 and id=$e19av1Id;"
Add-Check 'E19' 'avaliacao.fechamento-consolidado-imutavel-200' (($e19rt3.code -eq 200) -and ($e19rt4.code -eq 200) -and ($e19fe1.code -eq 200) -and ($e19fe2.code -eq 200) -and ((Get-E19Data $e19fe2.body) -eq $e19av1Id) -and ($dbCons.text -eq 'FECHADA|7.00|3|true')) ("sv2 nota 7 e sv3 nota 5 -> 200; fechar -> FECHADA com consolidado imutavel: media 7.00=(9+7+5)/3, 3 servidores avaliados, participacaoPct dentro de 0..100 (escopo = servidores ativos do tenant); banco: " + $dbCons.text + " (BR-5.5)")
$e19rt5 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv2.text + ',"nota":6}')
Add-Check 'E19' 'avaliacao.fechada-bloqueia-resultados-409' (($e19rt5.code -eq 409) -and ($e19rt5.body -like '*AVALIACAO_FECHADA_RESULTADOS_BLOQUEADOS*')) ("resultado na avaliacao FECHADA -> $($e19rt5.code) (trava do fechamento; retificacao so via reabertura)")
$e19ra0 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/reabrir") -Base $ApiBase -Body '{}'
$e19ra1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/reabrir") -Base $ApiBase -Body '{"justificativa":"Retificacao homologacao C2S5"}'
$e19rt6 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv1.text + ',"nota":9.5}')
$e19rt7 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/resultados/lancar") -Base $ApiBase -Body ('{"servidorId":' + $e19sv4.text + ',"nota":8}')
$e19fe3 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av1Id + "/fechar") -Base $ApiBase -Body '{}'
$dbCons2 = Get-Psql "select status||'|'||round((dados->'consolidado'->>'media')::numeric,2)::text||'|'||coalesce(dados->'consolidado'->>'servidoresAvaliados','?') from sigov.educacao_avaliacao_magisterio where tenant_id=910001 and id=$e19av1Id;"
Add-Check 'E19' 'avaliacao.reabertura-retificacao-200' (($e19ra0.code -eq 400) -and ($e19ra0.body -like '*AVALIACAO_REABERTURA_JUSTIFICATIVA_OBRIGATORIA*') -and ($e19ra1.code -eq 200) -and ($e19rt6.code -eq 409) -and ($e19rt6.body -like '*AVALIACAO_RESULTADO_EXISTENTE*') -and ($e19rt7.code -eq 200) -and ($e19fe3.code -eq 200) -and ($dbCons2.text -eq 'FECHADA|7.25|4')) ("reabrir sem just -> $($e19ra0.code); com just -> $($e19ra1.code); ja avaliado -> $($e19rt6.code) [$(if($e19rt6.body.Length -gt 90){$e19rt6.body.Substring(0,90)}else{$e19rt6.body})]; sv4 -> $($e19rt7.code) [$(if($e19rt7.body.Length -gt 90){$e19rt7.body.Substring(0,90)}else{$e19rt7.body})]; re-fechar -> $($e19fe3.code); banco: " + $dbCons2.text + " (esperado FECHADA|7.25|4)")
$e19av2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/avaliacao-magisterio/lancar' -Base $ApiBase -Body ('{"nome":"C2S5 Aval Segunda Aberta","periodoInicio":"' + $hoje + '","periodoFim":"' + $hoje + '","instrumento":"INSTRUMENTO_C2S5"}')
$e19av2Id = Get-E19Data $e19av2.body
$e19ra2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/avaliacao-magisterio/" + $e19av2Id + "/reabrir") -Base $ApiBase -Body '{"justificativa":"nao estava fechada"}'
Add-Check 'E19' 'avaliacao.estado-invalido-reabrir-aberta-409' (($e19av2.code -eq 200) -and ($e19ra2.code -eq 409) -and ($e19ra2.body -like '*AVALIACAO_ESTADO_INVALIDO*')) ("reabrir avaliacao ABERTA -> $($e19ra2.code) (ciclo canonico ABERTA -> FECHADA -> REABERTA; operacao em estado inesperado e nomeada)")

# --- E19.F: BR-5.6 Educacenso decisoes + encerramento + sincronizacao ----------
$e19lo1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/lotes/lancar' -Base $ApiBase -Body '{"ano":2026}'
$e19loId = Get-E19Data $e19lo1.body
$e19lo2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/lotes/lancar' -Base $ApiBase -Body '{"ano":2026}'
$e19ic1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/inconsistencias/lancar' -Base $ApiBase -Body ('{"loteId":' + $e19loId + ',"gravidade":"ALTA","origem":"C2S5-HOMOLOGACAO","recomendacao":"Revisar duvida ficticia alta"}')
$e19ic1Id = Get-E19Data $e19ic1.body
$e19ic2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/inconsistencias/lancar' -Base $ApiBase -Body ('{"loteId":' + $e19loId + ',"gravidade":"MEDIA","origem":"C2S5-HOMOLOGACAO"}')
$e19ic2Id = Get-E19Data $e19ic2.body
$e19ic3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/inconsistencias/lancar' -Base $ApiBase -Body ('{"loteId":' + $e19loId + ',"gravidade":"BAIXA","origem":"C2S5-HOMOLOGACAO"}')
$e19ic3Id = Get-E19Data $e19ic3.body
Add-Check 'E19' 'educacenso.lote-ano-unico-200' (($e19lo1.code -eq 200) -and ($e19lo2.code -eq 409) -and ($e19lo2.body -like '*LOTE_EDUCACENSO_ANO_EXISTENTE*') -and ($e19ic1.code -eq 200) -and ($e19ic2.code -eq 200) -and ($e19ic3.code -eq 200)) ("lote 2026 -> 200 id=$e19loId; segundo lote mesmo ano -> 409; inconsistencias ALTA/MEDIA/BAIXA ligadas ao lote -> 200 (BR-5.6)")
$e19en1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/lotes/" + $e19loId + "/encerrar") -Base $ApiBase -Body '{}'
$e19dc1 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/inconsistencias/" + $e19ic1Id + "/decidir") -Base $ApiBase -Body '{"desfecho":"OUTRO"}'
$e19dc2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/inconsistencias/" + $e19ic1Id + "/decidir") -Base $ApiBase -Body '{"desfecho":"ACEITE_COM_MOTIVO"}'
Add-Check 'E19' 'educacenso.encerramento-alta-aberta-409' (($e19en1.code -eq 409) -and ($e19en1.body -like '*EDUCACENSO_LOTE_ALTA_ABERTA*') -and ($e19dc1.code -eq 400) -and ($e19dc1.body -like '*EDUCACENSO_DESFECHO_INVALIDO*') -and ($e19dc2.code -eq 400) -and ($e19dc2.body -like '*EDUCACENSO_MOTIVO_OBRIGATORIO*')) ("encerrar com ALTA aberta -> $($e19en1.code); desfecho OUTRA -> $($e19dc1.code); ACEITE sem motivo -> $($e19dc2.code) (desfechos canonicos CORRIGIDA | ACEITE_COM_MOTIVO com motivo)")
$e19dc3 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/inconsistencias/" + $e19ic1Id + "/decidir") -Base $ApiBase -Body '{"desfecho":"CORRIGIDA"}'
$e19dc4 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/inconsistencias/" + $e19ic1Id + "/decidir") -Base $ApiBase -Body '{"desfecho":"CORRIGIDA"}'
$e19en2 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/lotes/" + $e19loId + "/encerrar") -Base $ApiBase -Body '{}'
$e19dc5 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/inconsistencias/" + $e19ic2Id + "/decidir") -Base $ApiBase -Body '{"desfecho":"ACEITE_COM_MOTIVO","motivo":"Aceite documentado homologacao C2S5"}'
$e19dc6 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/inconsistencias/" + $e19ic3Id + "/decidir") -Base $ApiBase -Body '{"desfecho":"CORRIGIDA"}'
Add-Check 'E19' 'educacenso.decisao-documentada-200' (($e19dc3.code -eq 200) -and ($e19dc4.code -eq 409) -and ($e19dc4.body -like '*EDUCACENSO_INCONSISTENCIA_JA_DECIDIDA*') -and ($e19en2.code -eq 409) -and ($e19en2.body -like '*EDUCACENSO_LOTE_INCONSISTENCIAS_ABERTAS*') -and ($e19dc5.code -eq 200) -and ($e19dc6.code -eq 200)) ("ALTA CORRIGIDA -> $($e19dc3.code); de novo -> $($e19dc4.code) [$(if($e19dc4.body.Length -gt 90){$e19dc4.body.Substring(0,90)}else{$e19dc4.body})]; encerrar medias abertas -> $($e19en2.code) [$(if($e19en2.body.Length -gt 90){$e19en2.body.Substring(0,90)}else{$e19en2.body})]; MEDIA aceite -> $($e19dc5.code); BAIXA corrigida -> $($e19dc6.code) (BR-5.6)")
$e19en3 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/lotes/" + $e19loId + "/encerrar") -Base $ApiBase -Body '{"justificativa":"Lote 2026 homologacao encerrado"}'
$dbEn = Get-Psql "select (select status from sigov.educacao_educacenso_lote where tenant_id=910001 and id=$e19loId)||'|'||(select count(*) from sigov.educacao_educacenso_evento where tenant_id=910001 and referencia_id=$e19loId and tipo='ENCERRAMENTO_LOTE')||'|'||(select string_agg(status||':'||(dados->>'desfecho'),',' order by id) from sigov.educacao_educacenso_inconsistencia where tenant_id=910001 and referencia_id=$e19loId);"
$e19en4 = Invoke-Api -Bearer $tk.token -Method POST -Path ("/api/educacao/educacenso/lotes/" + $e19loId + "/encerrar") -Base $ApiBase -Body '{}'
Add-Check 'E19' 'educacenso.lote-encerrado-consistente-200' (($e19en3.code -eq 200) -and ($dbEn.text -eq 'ENCERRADA|1|CORRIGIDA:CORRIGIDA,ACEITA:ACEITE_COM_MOTIVO,CORRIGIDA:CORRIGIDA') -and ($e19en4.code -eq 409) -and ($e19en4.body -like '*EDUCACENSO_LOTE_ESTADO_INVALIDO*')) ("encerrar -> ENCERRADA + evento ENCERRAMENTO_LOTE registrado + decisoes finais (ALTA CORRIGIDA, MEDIA ACEITA, BAIXA CORRIGIDA) no banco: " + $dbEn.text + "; encerrar de novo -> 409 (BR-5.6)")
$e19sin = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/educacenso/eventos/sincronizar' -Base $ApiBase -Body '{"detalhe":"Sincronizacao lote 2026 homologacao C2S5"}'
$e19dbd = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/educacenso/dashboard' -Base $ApiBase
$jDbd = $null; try { $jDbd = ($e19dbd.body | ConvertFrom-Json).data } catch {}
Add-Check 'E19' 'educacenso.sincronizacao-registrada-200' (($e19sin.code -eq 200) -and ($e19dbd.code -eq 200) -and $null -ne $jDbd -and ([string]$jDbd.ultimaSincronizacao -ne '')) ("evento SINCRONIZACAO registrado -> 200; painel educacenso/dashboard agora exibe ultimaSincronizacao=" + $jDbd.ultimaSincronizacao + " (BR-5.6 eventos de sincronizacao)")

# --- E19.G: BR-5.7 projecao de matricula unica por escola x ano -----------------
$e19pr1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/projecoes-matricula/lancar' -Base $ApiBase -Body ('{"escolaId":' + $e19esc.text + ',"ano":2026,"quantidade":120}')
$e19pr2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/projecoes-matricula/lancar' -Base $ApiBase -Body ('{"escolaId":' + $e19esc.text + ',"ano":2026,"quantidade":130}')
Add-Check 'E19' 'projecao.matricula-unica-escola-ano-200' (($e19pr1.code -eq 200) -and ($e19pr2.code -eq 409) -and ($e19pr2.body -like '*PROJECAO_MATRICULA_EXISTENTE*')) ("projeção 120 matriculas 2026 -> 200; segunda projeção mesma escola x ano -> 409 (1 vigente por escola x ano; alimenta custo/aluno projetado na S20; BR-5.7)")

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
delete from sigov.educacao_historico_escolar_item where historico_id in (select id from sigov.educacao_historico_escolar where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%'));
delete from sigov.educacao_historico_escolar where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%');
delete from sigov.educacao_portal_vinculo where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%');
delete from sigov.responsavel_aluno where pessoa_id=(select id from sigov.pessoa where documento='999999000401');
update sigov.usuario set pessoa_id=null where id=910301 and pessoa_id=(select id from sigov.pessoa where documento='999999000401');
delete from sigov.diario_frequencia where turma_id in (select id from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%');
delete from sigov.professor_turma where turma_id in (select id from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%');
delete from sigov.matricula where tenant_id=910001 and numero_matricula like 'C1S3-%';
delete from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%';
delete from sigov.serie_ano where tenant_id=910001 and codigo='C1S3-SERIE';
delete from sigov.curso where tenant_id=910001 and codigo='C1S3-CURSO';
delete from sigov.ano_letivo where tenant_id=910001 and escola_id=(select id from sigov.escola where tenant_id=910001 and codigo='c1e26-s3-esc');
delete from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%';
delete from sigov.professor where tenant_id=910001 and codigo_professor='C1S3-PROF';
delete from sigov.pessoa where documento in ('999999000101','999999000102','999999000103','999999000201','999999000401','999999000104');
delete from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c1e26-%';
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%';
delete from sigov.escola where tenant_id=910001 and codigo like 'c1e26-esc-%';
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s3-esc';
delete from sigov.educacao_merenda_cardapio_item where referencia_id in (select id from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s5-%'));
delete from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s5-%');
delete from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C1S5-%';
delete from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s5-%';
delete from sigov.educacao_merenda_movimento where referencia_id in (select id from sigov.educacao_merenda_distribuicao where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%'));
delete from sigov.educacao_merenda_distribuicao_item where referencia_id in (select id from sigov.educacao_merenda_distribuicao where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%'));
delete from sigov.educacao_merenda_distribuicao where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%');
delete from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id in (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C1S6-%');
delete from sigov.educacao_merenda_cardapio_item where referencia_id in (select id from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%'));
delete from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%');
delete from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C1S6-%';
delete from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%';
with c2s1p as (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'c2s1-%')
delete from sigov.educacao_merenda_movimento where produto_id in (select id from c2s1p);
with c2s1p as (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'c2s1-%')
delete from sigov.educacao_merenda_estoque where produto_id in (select id from c2s1p);
with c2s1p as (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'c2s1-%')
delete from sigov.educacao_merenda_produto where id in (select id from c2s1p);
delete from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c2s2-%';
delete from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C2S2-%';
delete from sigov.educacao_biblioteca_devolucao where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%');
delete from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%');
delete from sigov.educacao_biblioteca_reserva where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%');
delete from sigov.educacao_biblioteca_ocorrencia where tenant_id=910001;
delete from sigov.educacao_biblioteca_evento where tenant_id=910001;
delete from sigov.educacao_biblioteca_inventario_item where referencia_id in (select id from sigov.educacao_biblioteca_inventario where tenant_id=910001);
delete from sigov.educacao_biblioteca_inventario where tenant_id=910001;
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C2S3-%';
delete from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%';
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C1S7-%';
delete from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S7-ALA';
delete from sigov.pessoa where documento='999999000301';
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s7-esc';
delete from sigov.educacao_transporte_frequencia where tenant_id=910001 and tipo='PRESENCA_TRANSPORTE';
delete from sigov.educacao_transporte_ocorrencia where tenant_id=910001;
delete from sigov.educacao_transporte_custo where tenant_id=910001;
delete from sigov.educacao_transporte_viagem where tenant_id=910001;
delete from sigov.educacao_transporte_veiculo where tenant_id=910001 and placa like 'C2S4-%';
delete from sigov.educacao_transporte_motorista where tenant_id=910001 and nome like 'C2S4-%';
delete from sigov.parametro_modulo_valor where tenant_id=910001 and parametro_id=(select id from sigov.parametro_modulo where modulo='EDUCACAO' and codigo='TRANSPORTE_MONITOR_OBRIGATORIO' limit 1);
delete from sigov.educacao_transporte_aluno where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S8-%');
delete from sigov.educacao_transporte_rota_ponto where tenant_id=910001 and rota_id in (select id from sigov.educacao_transporte_rota where tenant_id=910001 and codigo like 'C1S8-%');
delete from sigov.educacao_transporte_rota where tenant_id=910001 and codigo like 'C1S8-%';
delete from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S8-%';
delete from sigov.pessoa where documento in ('999999000402','999999000403');
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s8-esc';
delete from sigov.entidade where cnpj in ('99999999000191','99999999000209','99999999000301') and not exists(select 1 from sigov.escola where entidade_id=sigov.entidade.id);
delete from sigov.educacao_fundeb_aplicacao where tenant_id=910001;
delete from sigov.educacao_fundeb_receita where tenant_id=910001;
delete from sigov.educacao_fundeb_parametro where tenant_id=910001;
delete from sigov.educacao_custo_lancamento where tenant_id=910001;
delete from sigov.educacao_custo_centro where tenant_id=910001;
delete from sigov.educacao_educacenso_inconsistencia where tenant_id=910001;
delete from sigov.educacao_educacenso_lote where tenant_id=910001;
delete from sigov.educacao_fundeb_competencia where tenant_id=910001;
delete from sigov.educacao_custo_rateio where tenant_id=910001;
delete from sigov.educacao_avaliacao_resultado where tenant_id=910001;
delete from sigov.educacao_avaliacao_magisterio where tenant_id=910001;
delete from sigov.educacao_educacenso_evento where tenant_id=910001;
delete from sigov.matricula where tenant_id=910001 and numero_matricula like 'C2S5-%';
delete from sigov.turma where tenant_id=910001 and codigo='c1e26-c2s5-turma';
delete from sigov.serie_ano where tenant_id=910001 and codigo='C2S5-SERIE';
delete from sigov.curso where tenant_id=910001 and codigo='C2S5-CURSO';
delete from sigov.ano_letivo where tenant_id=910001 and escola_id=(select id from sigov.escola where tenant_id=910001 and codigo='c1e26-c2s5-esc');
delete from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C2S5-%';
delete from sigov.pessoa where documento in ('999999000601','999999000602','999999000603');
delete from sigov.servidor where tenant_id=910001 and dados->>'matricula' like 'C2S5-%';
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-c2s5-esc';
delete from sigov.educacao_indicador_valor where tenant_id=910001;
delete from sigov.educacao_projecao_matricula where tenant_id=910001;
delete from sigov.educacao_indicador where tenant_id=910001;
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s10-esc';
select (select count(*) from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c1e26-%')
     + (select count(*) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%')
     + (select count(*) from sigov.escola where tenant_id=910001 and (codigo like 'c1e26-esc-%' or codigo='c1e26-s3-esc' or codigo='c1e26-s7-esc' or codigo='c1e26-s8-esc'))
     + (select count(*) from sigov.entidade where cnpj in ('99999999000191','99999999000209','99999999000301'))
     + (select count(*) from sigov.matricula where tenant_id=910001 and numero_matricula like 'C1S3-%')
     + (select count(*) from sigov.turma where tenant_id=910001 and codigo like 'c1e26-s3-%')
     + (select count(*) from sigov.curso where tenant_id=910001 and codigo='C1S3-CURSO')
     + (select count(*) from sigov.aluno where tenant_id=910001 and (codigo_aluno like 'C1S3-%' or codigo_aluno='C1S7-ALA' or codigo_aluno like 'C1S8-%'))
     + (select count(*) from sigov.educacao_historico_escolar where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%'))
     + (select count(*) from sigov.educacao_portal_vinculo where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S3-%'))
     + (select count(*) from sigov.responsavel_aluno where pessoa_id=(select id from sigov.pessoa where documento='999999000401'))
     + (select count(*) from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s5-%'))
     + (select count(*) from sigov.educacao_merenda_cardapio_item where referencia_id in (select id from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s5-%')))
     + (select count(*) from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C1S5-%')
     + (select count(*) from sigov.educacao_merenda_distribuicao where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%'))
     + (select count(*) from sigov.educacao_merenda_estoque where tenant_id=910001 and produto_id in (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C1S6-%'))
     + (select count(*) from sigov.educacao_merenda_cardapio where tenant_id=910001 and escola_id in (select id from sigov.escola where tenant_id=910001 and codigo like 'c1e26-s6-%'))
     + (select count(*) from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C1S6-%')
     + (select count(*) from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%')
     + (select count(*) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C1S7-%')
     + (select count(*) from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%'))
     + (select count(*) from sigov.educacao_biblioteca_reserva where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%'))
     + (select count(*) from sigov.educacao_biblioteca_devolucao where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%'))
     + (select count(*) from sigov.pessoa where documento in ('999999000101','999999000102','999999000103','999999000201','999999000401','999999000104','999999000301','999999000402','999999000403'))
      + (select count(*) from sigov.educacao_transporte_rota where tenant_id=910001 and codigo like 'C1S8-%')
      + (select count(*) from sigov.educacao_transporte_rota_ponto where tenant_id=910001 and rota_id in (select id from sigov.educacao_transporte_rota where tenant_id=910001 and codigo like 'C1S8-%'))
      + (select count(*) from sigov.educacao_transporte_aluno where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S8-%'))
      + (select count(*) from sigov.educacao_fundeb_parametro where tenant_id=910001)
      + (select count(*) from sigov.educacao_fundeb_receita where tenant_id=910001)
      + (select count(*) from sigov.educacao_fundeb_aplicacao where tenant_id=910001)
      + (select count(*) from sigov.educacao_custo_centro where tenant_id=910001)
      + (select count(*) from sigov.educacao_custo_lancamento where tenant_id=910001)
       + (select count(*) from sigov.educacao_indicador where tenant_id=910001)
       + (select count(*) from sigov.educacao_indicador_valor where tenant_id=910001)
       + (select count(*) from sigov.educacao_projecao_matricula where tenant_id=910001)
       + (select count(*) from sigov.educacao_educacenso_lote where tenant_id=910001)
       + (select count(*) from sigov.educacao_educacenso_inconsistencia where tenant_id=910001)
       + (select count(*) from sigov.escola where tenant_id=910001 and codigo='c1e26-s10-esc')
        + (select count(*) from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'c2s1-%')
        + (select count(*) from sigov.educacao_merenda_estoque where produto_id in (select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'c2s1-%'))
         + (select count(*) from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c2s2-%')
         + (select count(*) from sigov.educacao_merenda_produto where tenant_id=910001 and codigo like 'C2S2-%')
          + (select count(*) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C2S3-%')
          + (select count(*) from sigov.educacao_biblioteca_ocorrencia where tenant_id=910001)
          + (select count(*) from sigov.educacao_biblioteca_evento where tenant_id=910001)
          + (select count(*) from sigov.educacao_biblioteca_inventario where tenant_id=910001)
          + (select count(*) from sigov.educacao_biblioteca_inventario_item where referencia_id in (select id from sigov.educacao_biblioteca_inventario where tenant_id=910001))
            + (select count(*) from sigov.educacao_transporte_veiculo where tenant_id=910001 and placa like 'C2S4-%')
            + (select count(*) from sigov.educacao_transporte_motorista where tenant_id=910001 and nome like 'C2S4-%')
            + (select count(*) from sigov.educacao_transporte_viagem where tenant_id=910001)
            + (select count(*) from sigov.educacao_transporte_custo where tenant_id=910001)
            + (select count(*) from sigov.educacao_transporte_ocorrencia where tenant_id=910001)
            + (select count(*) from sigov.educacao_transporte_frequencia where tenant_id=910001 and tipo='PRESENCA_TRANSPORTE')
            + (select count(*) from sigov.parametro_modulo_valor where tenant_id=910001 and parametro_id=(select id from sigov.parametro_modulo where modulo='EDUCACAO' and codigo='TRANSPORTE_MONITOR_OBRIGATORIO' limit 1))
             + (select count(*) from sigov.educacao_fundeb_competencia where tenant_id=910001)
             + (select count(*) from sigov.educacao_custo_rateio where tenant_id=910001)
             + (select count(*) from sigov.educacao_avaliacao_resultado where tenant_id=910001)
             + (select count(*) from sigov.educacao_avaliacao_magisterio where tenant_id=910001)
             + (select count(*) from sigov.educacao_educacenso_evento where tenant_id=910001)
             + (select count(*) from sigov.matricula where tenant_id=910001 and numero_matricula like 'C2S5-%')
             + (select count(*) from sigov.turma where tenant_id=910001 and codigo='c1e26-c2s5-turma')
             + (select count(*) from sigov.curso where tenant_id=910001 and codigo='C2S5-CURSO')
             + (select count(*) from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C2S5-%')
             + (select count(*) from sigov.pessoa where documento in ('999999000601','999999000602','999999000603'))
             + (select count(*) from sigov.servidor where tenant_id=910001 and dados->>'matricula' like 'C2S5-%')
             + (select count(*) from sigov.escola where tenant_id=910001 and codigo='c1e26-c2s5-esc') as restantes;
"@
Add-Check 'CX' 'fixture.removido' ($cl.code -eq 0 -and $cl.text -eq '0') ("delete c1e26-%/C1S3-%/C1S7-%/C1S8-% (diario classe+aulas+conteudo+frequencia+fechamento+historico, frequencias, S4 historico-escolar+itens+vinculos+responsavel+pessoa do usuario, professor_turma, matriculas, turmas, ano, alunos, professor, pessoas, escola + S5 cardapio+itens+produtos + S6 distribuicoes+itens+movimentos+estoque + S7 biblioteca acervo/exemplares/emprestimos/reservas/devolucoes/aluno/escola + S8 transporte vinculos/pontos/rotas/alunos/escola + S9 fundeb parametros/receitas/aplicacoes + custo centros/lancamentos + S10 indicadores/valores/projecoes/lotescenso/inconsistencias/escola (tenant) + C2S2 requisicoes-legadas/produto-livre + C2S3 acervo-FNLI/ocorrencias/eventos/inventarios/items-inventario + C2S4 transporte veiculos/motoristas/viagens/custos/ocorrencias/presencas/parametro-monitor + C2S5 fundeb-competencia/rateios/avaliacoes/educacenso-eventos/matriculas/turma/curso/alunos/servidores/escola + merenda/biblioteca/entidade) tenant 910001 -> restantes=" + $cl.text)

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
