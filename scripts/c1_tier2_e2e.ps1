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
$dF2 = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
$dF3 = (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
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
$t92 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_cardapio/$mact/status?status=ENCERRADA&justificativa=c1-s5-encerra" -Base $ApiBase
$t93 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/merenda/operacoes/educacao_merenda_cardapio/$mact/status?status=ATIVA&justificativa=c1-s5-reabrir" -Base $ApiBase
Add-Check 'E9' 'cardapio.maquina-ativa-terminal-e-final' (($t91.code -eq 200) -and ($t92.code -eq 200) -and ($t93.code -eq 409) -and ($t93.body -like '*TRANSICAO_INVALIDA*')) ("maquina S2 aplicada ao cardapio: RASCUNHO->ATIVA $($t91.code) ->ENCERRADA $($t92.code) ->ATIVA $($t93.code) (terminal nao reabre; TRANSICAO_INVALIDA nomeada)")

# --- E10 (Sprint 6): merenda operação — registro de estoque + distribuição com consumo real ---
# POST /api/educacao/merenda/estoques {produtoId,saldoInicial}: cria a linha de estoque
# espelhando o produto ou ajusta o saldo da linha existente (falha nomeada se o produto
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
$estA = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques' -Base $ApiBase -Body ('{"produtoId":' + $pa.text + ',"saldoInicial":100}')
$erowA = Get-Psql "select round(coalesce(s.saldo,-1))::text||'|'||coalesce(s.nome,'null') from sigov.educacao_merenda_estoque s where s.tenant_id=910001 and s.produto_id=(select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-A') order by s.id desc limit 1;"
Add-Check 'E10' 'estoque.registro-inicial' (($estA.code -eq 200) -and ($erowA.text -eq '100|Arroz C1S6')) ("POST /api/educacao/merenda/estoques produto A saldoInicial=100 -> $($estA.code); banco saldo|nome: $($erowA.text)")
$estB1 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques' -Base $ApiBase -Body ('{"produtoId":' + $pb.text + ',"saldoInicial":10}')
$estB2 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques' -Base $ApiBase -Body ('{"produtoId":' + $pb.text + ',"saldoInicial":5}')
$erowB = Get-Psql "select round(coalesce(s.saldo,-1))::text from sigov.educacao_merenda_estoque s where s.tenant_id=910001 and s.produto_id=(select id from sigov.educacao_merenda_produto where tenant_id=910001 and codigo='C1S6-PROD-B') order by s.id desc limit 1;"
Add-Check 'E10' 'estoque.ajuste-soma-sobre-linha-existente' (($estB1.code -eq 200) -and ($estB2.code -eq 200) -and ($erowB.text -eq '15')) ("POST estoques produto B 10 e depois +5 -> codes $($estB1.code)/$($estB2.code); banco saldo=15 (ajuste soma sobre a linha existente, nao duplica)")
$estX = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/merenda/estoques' -Base $ApiBase -Body '{"produtoId":9999999999,"saldoInicial":1}'
Add-Check 'E10' 'estoque.produto-inexistente-404-nomeada' (($estX.code -eq 404) -and ($estX.body -like '*MERENDA_PRODUTO_INEXISTENTE*')) ("POST /api/educacao/merenda/estoques produto 9999999999 -> $($estX.code) (404 MERENDA_PRODUTO_INEXISTENTE nomeada)")
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
$e11db2 = Get-Psql "select (select status from sigov.educacao_biblioteca_emprestimo m where m.tenant_id=910001 and m.exemplar_id=$($e11idA.text) order by m.id desc limit 1)||'|'||(select count(1) from sigov.educacao_biblioteca_devolucao d where d.tenant_id=910001 and d.referencia_id=$(if($e11emb1.text){$e11emb1.text}else{0}))||'|'||(select d.dados->>'em_dia' from sigov.educacao_biblioteca_devolucao d where d.tenant_id=910001 and d.referencia_id=$(if($e11emb1.text){$e11emb1.text}else{0}) limit 1);"
Add-Check 'E11' 'devolucao.registrada-transacional' (($e11d1.code -eq 200) -and ($e11db2.text -eq 'ENCERRADA|1|true')) ("POST emprestimos/{id}/devolver -> $($e11d1.code); banco emprestimo|devolucoes|em_dia: $($e11db2.text) (mesma transacao; exemplar liberado)")
$e11d2 = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/emprestimos/$($e11emb1.text)/devolver" -Base $ApiBase -Body '{}'
Add-Check 'E11' 'devolucao.repetida-409-nomeada' (($e11d2.code -eq 409) -and ($e11d2.body -like '*EMPRESTIMO_JA_DEVOLVIDO*')) ("devolver de novo o mesmo emprestimo -> $($e11d2.code) (409 EMPRESTIMO_JA_DEVOLVIDO nomeada)")
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
$e11dT = 0; $e11dDisp = 0; $e11dAt = 0; $e11dAtr = 0
if ($e11dj -and $null -ne $e11dj.data) { $e11dT = [long]$e11dj.data.titulos; $e11dDisp = [long]$e11dj.data.disponiveis; $e11dAt = [long]$e11dj.data.emprestimosAtivos; $e11dAtr = [long]$e11dj.data.atrasos }
Add-Check 'E11' 'dashboard.metricas-dedicadas' (($e11dash.code -eq 200) -and ($e11dT -ge 1) -and ($e11dDisp -ge 1) -and ($e11dAt -ge 1) -and ($e11dAtr -ge 1)) ("GET biblioteca/dashboard -> $($e11dash.code); metricas titulos/disponiveis/ativos/atrasos: $e11dT/$e11dDisp/$e11dAt/$e11dAtr (DTO dedicado EducacaoBibliotecaDashboardDto; atraso provado com fixture data_fim=ontem)")
$e11rc = Invoke-Api -Bearer $tk.token -Method POST -Path "/api/educacao/biblioteca/operacoes/educacao_biblioteca_reserva/$($e11rid.text)/status?status=CANCELADA&justificativa=c1-s7-cancela-reserva" -Base $ApiBase
$e11r3 = Invoke-Api -Bearer $tk.token -Method POST -Path '/api/educacao/biblioteca/reservas/lancar' -Base $ApiBase -Body ('{"exemplarId":' + $e11idB.text + ',"alunoId":' + $e11al.text + '}')
Add-Check 'E11' 'reserva.cancelada-reabre-fila' (($e11rc.code -eq 200) -and ($e11r3.code -eq 200)) ("reserva CANCELADA via maquina de estados -> $($e11rc.code); nova reserva no exemplar -> $($e11r3.code) (terminal libera a fila)")

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
delete from sigov.educacao_biblioteca_devolucao where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%');
delete from sigov.educacao_biblioteca_emprestimo where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%');
delete from sigov.educacao_biblioteca_reserva where tenant_id=910001 and exemplar_id in (select id from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%');
delete from sigov.educacao_biblioteca_exemplar where tenant_id=910001 and codigo like 'C1S7-%';
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C1S7-%';
delete from sigov.aluno where tenant_id=910001 and codigo_aluno='C1S7-ALA';
delete from sigov.pessoa where documento='999999000301';
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s7-esc';
delete from sigov.educacao_transporte_aluno where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S8-%');
delete from sigov.educacao_transporte_rota_ponto where tenant_id=910001 and rota_id in (select id from sigov.educacao_transporte_rota where tenant_id=910001 and codigo like 'C1S8-%');
delete from sigov.educacao_transporte_rota where tenant_id=910001 and codigo like 'C1S8-%';
delete from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S8-%';
delete from sigov.pessoa where documento in ('999999000402','999999000403');
delete from sigov.escola where tenant_id=910001 and codigo='c1e26-s8-esc';
delete from sigov.entidade where cnpj in ('99999999000191','99999999000209','99999999000301') and not exists(select 1 from sigov.escola where entidade_id=sigov.entidade.id);
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
      + (select count(*) from sigov.educacao_transporte_aluno where tenant_id=910001 and aluno_id in (select id from sigov.aluno where tenant_id=910001 and codigo_aluno like 'C1S8-%')) as restantes;
"@
Add-Check 'CX' 'fixture.removido' ($cl.code -eq 0 -and $cl.text -eq '0') ("delete c1e26-%/C1S3-%/C1S7-%/C1S8-% (diario classe+aulas+conteudo+frequencia+fechamento+historico, frequencias, S4 historico-escolar+itens+vinculos+responsavel+pessoa do usuario, professor_turma, matriculas, turmas, ano, alunos, professor, pessoas, escola + S5 cardapio+itens+produtos + S6 distribuicoes+itens+movimentos+estoque + S7 biblioteca acervo/exemplares/emprestimos/reservas/devolucoes/aluno/escola + S8 transporte vinculos/pontos/rotas/alunos/escola + merenda/biblioteca/entidade) tenant 910001 -> restantes=" + $cl.text)

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
