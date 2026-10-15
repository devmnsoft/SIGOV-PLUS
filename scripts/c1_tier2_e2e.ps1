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
# -OnlyS12: pula os blocos E1..S11 (ja validados na rodada oficial anterior) e executa
#           somente a secao SPRINT 12 + limpeza/auditoria final (iteracao rapida sob
#           ambiente compartilhado; fixture S12 e self-contained/reentravel).
# =============================================================================
param([switch]$OnlyS12,[switch]$OnlyS13)
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
  Ensure-SigovUp | Out-Null   # self-heal antes do login (host pode ter sido cortado no meio da rodada)
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
# --- SELF-HEAL (mitigacao de ambiente compartilhado) --------------------------
# Outra sessao desta maquina (projeto externo) ja matou os processos dotnet
# durante a rodada (corte abrupto, sem excecao na log). Todo o estado da
# prova vive em Postgres (sessoes/tokens/fixtures), entao um restart dos hosts
# SIGOV e transparente ao fluxo: em HTTP 0, garantir hosts de pe e tentar a
# chamada uma unica vez. O restart e ALVO (apenas Sigov.Api.dll/Sigov.Web.dll).
$script:selfhealN = 0
function Ensure-SigovUp {
  $hApi = ''
  try { $hApi = (& curl.exe -s -o NUL -w '%{http_code}' --max-time 6 'http://localhost:5001/health') } catch { $hApi = '000' }
  $hWeb = ''
  try { $hWeb = (& curl.exe -s -o NUL -w '%{http_code}' --max-time 6 'http://localhost:5099/Auth/Login') } catch { $hWeb = '000' }
  if ("$hApi" -ne '000' -and "$hWeb" -ne '000') { return $true }
  $script:selfhealN++
  Write-Output ('[SELFHEAL] hosts baixos (api=' + $hApi + ' web=' + $hWeb + '); restart alvo n=' + $script:selfhealN)
  try {
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -and ($_.CommandLine -like '*Sigov.Api.dll*' -or $_.CommandLine -like '*Sigov.Web.dll*') } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  } catch { Write-Output ('[SELFHEAL] WMI/clean parcial: ' + $_.Exception.Message) }
  Start-Sleep -Seconds 2
  $cmdApi = @'
Set-Location C:\MNSOFT\SIGOV-PLUS\src\Sigov.Api\bin\Release\net10.0
$env:ASPNETCORE_URLS = 'http://localhost:5100;http://localhost:5001'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=p1_dev;Username=postgres;Password=123456;Search Path=sigov;Application Name=sigov.api'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Sigov__Database__MigrationMode = 'Disabled'
& dotnet exec 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Api\bin\Release\net10.0\Sigov.Api.dll' *>&1 | Out-File -FilePath 'C:\MNSOFT\SIGOV-PLUS\artifacts\c1_tier2_e2e\s6_api_selfheal.log' -Append
'@
  $cmdWeb = @'
Set-Location C:\MNSOFT\SIGOV-PLUS\src\Sigov.Web\bin\Release\net10.0
$env:ASPNETCORE_URLS = 'http://localhost:5099'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=p1_dev;Username=postgres;Password=123456;Search Path=sigov;Application Name=sigov.web'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& dotnet exec 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Web\bin\Release\net10.0\Sigov.Web.dll' *>&1 | Out-File -FilePath 'C:\MNSOFT\SIGOV-PLUS\artifacts\c1_tier2_e2e\s6_web_selfheal.log' -Append
'@
  Start-Process powershell -WindowStyle Hidden -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($cmdApi)))
  Start-Process powershell -WindowStyle Hidden -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($cmdWeb)))
  for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 4
    $h = '000'
    try { $h = (& curl.exe -s -o NUL -w '%{http_code}' --max-time 5 'http://localhost:5001/health') } catch {}
    if ("$h" -ne '000') { Write-Output ('[SELFHEAL] api de pe apos ~' + (($i + 1) * 4) + 's (health=' + $h + ')'); return $true }
  }
  Write-Output '[SELFHEAL] FALHA: api nao respondeu em 120s'
  return $false
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
  $codeInt = 0; try { $codeInt = [int]$code } catch {}
  if ($codeInt -eq 0) {
    # HTTP 0 = falha de transporte: host cortado por interferencia externa da maquina
    # compartilhada (ja ocorreu em rodada oficial: corte abrupto sem excecao).
    # Self-heal com restart alvo + um unico retry da mesma chamada.
    if (Ensure-SigovUp) {
      Start-Sleep -Seconds 2
      $raw = & curl.exe @w 2> $ef
      $cexit = $LASTEXITCODE
      $code = ("$raw").Trim()
      $codeInt = 0; try { $codeInt = [int]$code } catch {}
    }
  }
  # ReadAllText (UTF-8): Get-Content sem encoding leria ANSI e quebraria acesos nas falhas nomeadas
  $body = if (Test-Path $of) { [IO.File]::ReadAllText($of) } else { '' }
  $errTxt = ''
  try { if (Test-Path $ef) { $errTxt = ([IO.File]::ReadAllText($ef)).Trim() } } catch {}
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
if (-not $OnlyS12 -and -not $OnlyS13) {
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

# E20 — Ciclo 2 Sprint 6: jornadas pelas telas reais (BR-6.1 a BR-6.6). A fachada estatica
# Avancada.cshtml foi substituida por views por area ligadas aos endpoints tipados: cada rota de
# area renderiza o shell de area com data-edu-area/data-edu-page, o api base da area embutido na
# config do motor, sem toast falso "Painel atualizado" e sem a marca da fachada estatica.
function Get-WebPage { param([string]$Jar,[string]$Url)
  $of = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_" + [guid]::NewGuid().ToString('N') + ".html")
  $ef = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_err_" + [guid]::NewGuid().ToString('N') + ".txt")
  $raw = & curl.exe -s --max-time 45 -b $Jar -L -o $of -w "%{http_code}" $Url 2> $ef
  $codeInt = 0; try { $codeInt = [int]("$raw".Trim()) } catch {}
  $body = if (Test-Path $of) { [IO.File]::ReadAllText($of) } else { '' }
  return [pscustomobject]@{ code=$codeInt; body="$body" }
}
foreach ($e20a in @(
  @('merenda','/Educacao/Merenda/Dashboard','api/educacao/merenda'),
  @('biblioteca','/Educacao/Biblioteca/Dashboard','api/educacao/biblioteca'),
  @('transporte','/Educacao/TransporteEscolar/Dashboard','api/educacao/transporte'),
  @('indicadores','/Educacao/Indicadores/Dashboard','"api/educacao",')
)) {
  $e20p = Get-WebPage $lg.jar ("$WebBase" + $e20a[1])
  Add-Check 'E20' ("telasreais.$($e20a[0]).dash-200") ($e20p.code -eq 200) ("GET $($e20a[1]) com sessao web -> $($e20p.code) (200; area renderiza com dados reais da API, nao shell estatico)")
  Add-Check 'E20' ("telasreais.$($e20a[0]).marker-area-e-pagina") (($e20p.body -like ('*data-edu-area="' + $e20a[0] + '"*')) -and ($e20p.body -like '*data-edu-page="Dashboard"*')) ("marca da area + pagina presentes no HTML (view por area ligada aos endpoints tipados, BR-6.1)")
  Add-Check 'E20' ("telasreais.$($e20a[0]).config-api-base") ($e20p.body -like ("*" + $e20a[2] + "*")) ("config do motor contem o api base tipado da area ($($e20a[2]); KPIs/tabela/acoes consomem esses endpoints)")
  Add-Check 'E20' ("telasreais.$($e20a[0]).sem-toast-falso") (-not ($e20p.body -like '*data-sigov-toast="Painel atualizado"*')) ("nenhum toast falso 'Painel atualizado' no HTML (BR-6.2: toda acao chama API real e so entao notifica)")
  Add-Check 'E20' ("telasreais.$($e20a[0]).sem-fachada-estatica") (-not ($e20p.body -like '*education-premium-header*')) ("marca da fachada estatica Avancada.cshtml ausente (substituida pela view da area)")
}
$e20deep = @(
  @('movimentos','/Educacao/Merenda/Movimentos','Movimentos'),
  @('emprestimos','/Educacao/Biblioteca/Emprestimos','Emprestimos'),
  @('viagens','/Educacao/TransporteEscolar/Viagens','Viagens'),
  @('fundeb','/Educacao/Indicadores/Fundeb','Fundeb')
)
foreach ($e20d in $e20deep) {
  $e20q = Get-WebPage $lg.jar ("$WebBase" + $e20d[1])
  Add-Check 'E20' ("telasreais.rota-profunda.$($e20d[0])") (($e20q.code -eq 200) -and ($e20q.body -like ('*data-edu-page="' + $e20d[2] + '"*'))) ("GET $($e20d[1]) -> $($e20q.code) com data-edu-page=$($e20d[2]) (jornada menu -> pagina -> lista -> acao sobre a mesma view da area)")
  Add-Check 'E20' ("telasreais.rota-profunda.$($e20d[0]).componientes") (($e20q.body -like '*id="eduKpis"*') -and ($e20q.body -like '*sigov-filter-bar*') -and ($e20q.body -like '*_loading*' -or $e20q.body -like '*sigov-loading*')) ("compartilhados presentes no HTML (KPIs com origem + _FilterBar + _Loading/_EmptyState, BR-6.3)")
}

# =============================================================================
# --- S8 (Ciclo 2 Sprint 8): Compras integradas (1/2) — demanda -> contratacao -> contrato ---
# BR-8.1 a BR-8.6 sobre o schema generico compras_* (migration 20261013140000; banco =
# autoridade, regras 11/12: modalidades/dispensas art. 75 e parametros lidos do catalogo):
#   BR-8.1 demanda tipada (qtd>0/unidade conhecida/preco>0 ou pesquisa/centro de custo
#           ativo; urgencia com dupla aprovacao por autoridade distinta; imutavel apos
#           aprovacao; repelida -> reaberta -> cancelada com justificativas);
#   BR-8.2 processo unico da contratacao (Lei 14.133 art. 128) garantido na aprovacao;
#   BR-8.3 procedimento com modalidade do catalogo (consulta previa limitada pelo teto
#           parametrico; dispensa exige base legal do catalogo art. 75), habilitacao por
#           tipo, lances decrescentes (1 lance = 1 registro, chave idempotente) e zona
#           ME/EPP com preferencia dentro da margem parametrica (LC 123/2006) documentada;
#   BR-8.4 julgamento com classificacao (zona aberta bloqueia), rejeicao de proposta
#           inexequivel entre classificacao e adjudicacao, adjudicacao com valor derivado
#           dos itens e homologacao separada (autoridade distinta + prazo parametrico);
#   BR-8.5 contrato nascendo da homologacao (itens espelhados, valor DERIVADO, GED/FISICA),
#           execucao com teto parametrico, prorrogação art. 106 (teto de anos e maximo de
#           vezes parametrizados, rastro em historico) e rescisao com ato (unilateral exige
#           notificacao previa);
#   BR-8.6 cadeia direta DISPENSA -> adjudicacao com fornecedor -> homologacao -> contrato.
# Multi-esfera (regras 21-24): entidade fixture com esfera 'estadual' e usuario dedicado
# c2s8.admin com a MESMA cadeia usuario_grupo/grupo/perfil do admin (autorizacao resolvida
# pelo banco, regra 11). Marcadores C2S8-%; o bloco se auto-limpa ao final.
$s8fix = Get-Psql @"
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910001,'C2S8 Entidade Homologacao','00.000.000/0001-91','C2S8-ENT','estadual','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910001 and codigo_externo='C2S8-ENT');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S8 Administrador','c2s8.admin','c2s8.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S8-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s8.admin');
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,910401,910001,true,now(),910301
from sigov.usuario u
where u.tenant_id=910001 and u.login='c2s8.admin'
and not exists(select 1 from sigov.usuario_grupo g where g.usuario_id=u.id and g.grupo_acesso_id=910401 and g.ativo);
-- Grant das 15 chaves canonicas S7 ao perfil da cadeia copiada (ADMIN_TENANT), marcadas com
-- correlation_id sentinel C2S8 para limpeza cirurgica; DO NOTHING preserva vinculos preexistentes.
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000001'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.id=(select gp.perfil_acesso_id from sigov.grupo_perfil gp where gp.grupo_acesso_id=910401 limit 1)
  and p.modulo='compras' and p.ativo and not p.is_deleted
  and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir','compras.licitacao.abrir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar','compras.julgamento.executar','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar')
on conflict (perfil_acesso_id,permissao_id) do nothing;
insert into sigov.financeiro_centro_custo(tenant_id,codigo,nome,descricao,ativo,created_at,updated_at)
select 910001,'C2S8-CC1','CC C2S8 Ativo','centro de custo ficticio ativo (homologacao)',true,now(),now()
where not exists(select 1 from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S8-CC1');
insert into sigov.financeiro_centro_custo(tenant_id,codigo,nome,descricao,ativo,created_at,updated_at)
select 910001,'C2S8-CC2','CC C2S8 Inativo','centro de custo ficticio inativo (homologacao)',false,now(),now()
where not exists(select 1 from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S8-CC2');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910001,e.id,'Forneadora C2S8 Alfa','JURIDICA','99.999.000/0001-01','ATIVO',null,now(),now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S8-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910001 and f.documento='99.999.000/0001-01');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910001,e.id,'Forneadora C2S8 Beta Micro','JURIDICA','99.999.000/0002-02','ATIVO','ME',now(),now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S8-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910001 and f.documento='99.999.000/0002-02');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910001,e.id,'Forneadora C2S8 Gama','JURIDICA','99.999.000/0003-03','ATIVO','OUTROS',now(),now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S8-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910001 and f.documento='99.999.000/0003-03');
select coalesce((select id::text from sigov.entidade where tenant_id=910001 and codigo_externo='C2S8-ENT'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s8.admin'),'0') || '|'
     || coalesce((select id::text from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S8-CC1'),'0') || '|'
     || coalesce((select id::text from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S8-CC2'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910001 and documento='99.999.000/0001-01'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910001 and documento='99.999.000/0002-02'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910001 and documento='99.999.000/0003-03'),'0');
"@
$s8p = if ($s8fix.code -eq 0 -and ($s8fix.text -ne '')) { @($s8fix.text.Trim() -split '\|') } else { @('0','0','0','0','0','0','0') }
$s8ent=[long]$s8p[0]; $s8uid=[long]$s8p[1]; $s8cc1=[long]$s8p[2]; $s8cc2=[long]$s8p[3]; $s8f1=[long]$s8p[4]; $s8f2=[long]$s8p[5]; $s8f3=[long]$s8p[6]
Add-Check 'S8' 'fixture.aplicado-entidade-estadual-usuario-fornecedores' (($s8fix.code -eq 0) -and ($s8ent -gt 0) -and ($s8uid -gt 0) -and ($s8f2 -gt 0)) ("fixture S8 (entidade esfera estadual + usuario c2s8.admin com a mesma cadeia de perfil 910401 do admin + 2 centros de custo + 3 fornecedores (NULL/ME/OUTROS), marcador C2S8-%, tenant 910001) exit=$($s8fix.code); ent=$s8ent uid=$s8uid cc1=$s8cc1 cc2=$s8cc2 f1=$s8f1 f2=$s8f2 f3=$s8f3")
$lgS8 = New-SigovLogin -User 'c2s8.admin' -Pass $Password -Base $WebBase
$tkS8 = if ($lgS8.ok) { Get-ApiToken -Jar $lgS8.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
Add-Check 'S8' 'auth.login-dedicado-token-mint' (($lgS8.ok -eq $true) -and ($tkS8.token.Length -eq 64)) ("c2s8.admin POST /Auth/Login -> $($lgS8.post); ApiToken len=$($tkS8.token.Length) (usuario da entidade estadual C2S8-ENT com a cadeia de autorizacao copiada do admin: toda a frente resolve permissoes pelo banco, regra 11)")

# --- BR-8.1: demanda tipada --------------------------------------------------------
$s8d1b = ('{"objeto":"C2S8-Aquisicao material","justificativa":"necessidade operacional homologacao c2s8","itens":[{"descricao":"Papel A4","quantidade":10,"unidade":"pct","tipoObjeto":"BEM","precoEstimado":25.5,"centroCustoId":' + $s8cc1 + '}],"urgencia":false}')
$s8d1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s8d1b
$s8d1j = $null; try { $s8d1j = ($s8d1.body | ConvertFrom-Json) } catch {}
$s8d1id = 0; if ($s8d1j -and $null -ne $s8d1j.data) { $s8d1id = [long]$s8d1j.data.id }
$s8d1db = Get-Psql ("select status||'|'||to_char(valor_estimado,'FM999990.00')||'|'||urgencia||'|'||prioridade from sigov.compras_demanda where id=" + $s8d1id + ";")
$null = Get-Psql ("update sigov.compras_demanda set created_by=910301 where id=" + $s8d1id + ";")
Add-Check 'S8' 'demanda.criada-tipada-valor-derivado' (($s8d1.code -eq 200) -and ($s8d1id -gt 0) -and ($s8d1db.text -eq 'SOLICITADA|255.00|false|NORMAL')) ("POST /api/compras/integradas/demandas (10 pct x 25.50, tipo BEM, centro de custo ATIVO) -> $($s8d1.code) id=$s8d1id; banco status|valor_estimado|urgencia|prioridade: $($s8d1db.text) (BR-8.1: valor derivado da soma dos itens; created_by reposicionado para 910301 = solicitante DISTINCTO do decisor, como em contratacao real)")
$s8dq = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-Qtd zero","justificativa":"validacao c2s8","itens":[{"descricao":"Item qtd 0","quantidade":0,"unidade":"un","precoEstimado":10,"centroCustoId":' + $s8cc1 + '}],"urgencia":false}')
$s8dp = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-Sem preco","justificativa":"validacao c2s8","itens":[{"descricao":"Item sem preco e sem pesquisa","quantidade":1,"unidade":"un","precoEstimado":0,"centroCustoId":' + $s8cc1 + '}],"urgencia":false}')
$s8dc = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-CC inativo","justificativa":"validacao c2s8","itens":[{"descricao":"Item CC inativo","quantidade":1,"unidade":"un","precoEstimado":10,"centroCustoId":' + $s8cc2 + '}],"urgencia":false}')
Add-Check 'S8' 'demanda.itens-invalidos-400-nomeados' (($s8dq.code -eq 400) -and ($s8dq.body -like '*DEMANDA_ITEM_INVALIDO*') -and ($s8dp.code -eq 400) -and ($s8dp.body -like '*DEMANDA_ITEM_INVALIDO*') -and ($s8dc.code -eq 400) -and ($s8dc.body -like '*DEMANDA_ITEM_INVALIDO*')) ("qtd=0 -> $($s8dq.code); preco<=0 sem pesquisaPrecoId -> $($s8dp.code); centro de custo INATIVO -> $($s8dc.code) (todos 400 DEMANDA_ITEM_INVALIDO nomeada: qtd>0/unidade conhecida/preco>0 OU pesquisa referenciada/centro de custo valido e ativo — BR-8.1; nada gravado)")
$s8dn = Invoke-Api -Bearer $tkS8.token -Method GET -Path '/api/compras/integradas/demandas/9999999999' -Base $ApiBase
Add-Check 'S8' 'demanda.inexistente-404-nomeada' (($s8dn.code -eq 404) -and ($s8dn.body -like '*DEMANDA_INEXISTENTE*')) ("GET /demandas/9999999999 -> $($s8dn.code) (404 DEMANDA_INEXISTENTE nomeada; escopo tenant — regra 13)")
$s8duj = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-Urgente sem just","justificativa":"urgencia sem justificativa","itens":[{"descricao":"Kit C2S8","quantidade":5,"unidade":"un","precoEstimado":40,"centroCustoId":' + $s8cc1 + '}],"urgencia":true}')
Add-Check 'S8' 'demanda.urgencia-sem-justificativa-400' (($s8duj.code -eq 400) -and ($s8duj.body -like '*DEMANDA_URGENCIA_SEM_JUSTIFICATIVA*')) ("urgencia=true sem justificativaUrgencia -> $($s8duj.code) (400 DEMANDA_URGENCIA_SEM_JUSTIFICATIVA nomeada — BR-8.1)")
$s8d2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-Kit urgente","justificativa":"rompimento de estoque do programa estadual","itens":[{"descricao":"Kit C2S8","quantidade":5,"unidade":"un","precoEstimado":40,"centroCustoId":' + $s8cc1 + '}],"urgencia":true,"justificativaUrgencia":"rompimento de estoque do programa estadual c2s8"}')
$s8d2j = $null; try { $s8d2j = ($s8d2.body | ConvertFrom-Json) } catch {}
$s8d2id = 0; if ($s8d2j -and $null -ne $s8d2j.data) { $s8d2id = [long]$s8d2j.data.id }
$null = Get-Psql ("update sigov.compras_demanda set created_by=910301 where id=" + $s8d2id + ";")
$s8a1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d2id + '/aprovar') -Base $ApiBase
$s8a1db = Get-Psql ("select status||'|'||coalesce(aprovado_por::text,'NULL')||'|'||coalesce(dupla_aprovado_por::text,'NULL') from sigov.compras_demanda where id=" + $s8d2id + ";")
$s8dbgLike = [bool]($s8a1db.text -like ("APROVADA_PARCIAL|$s8uid|NULL"))
$s8dbgBody = $s8a1.body.Substring(0,[Math]::Min(160,$s8a1.body.Length))
$s8dbgA = ('code=[' + $s8a1.code + '] like=[' + $s8dbgLike + '] uid=' + $s8uid + ' body=[' + $s8dbgBody + ']')
Add-Check 'S8' 'demanda.urgencia-primeira-aprovacao-parcial' (($s8a1.code -eq 200) -and ($s8a1db.text -like ("APROVADA_PARCIAL|$s8uid|NULL"))) ("aprovar 1a vez (urgencia) -> $($s8a1.code); banco status|aprovado_por|dupla_aprovado_por: $($s8a1db.text) (BR-8.1: urgencia exige DUPLA aprovacao por autoridades distintas — primeira apenas parcial; separacao de funcoes ja validada: o solicitante 910301 nao e o decisor; DBG ' + $s8dbgA + '")
$s8ab1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d2id + ',"modalidadeCodigo":"PREGAO_ELETRONICO"}')
Add-Check 'S8' 'demanda.urgencia-abrir-bloqueado-sem-dupla' (($s8ab1.code -eq 409) -and ($s8ab1.body -like '*DEMANDA_URGENCIA_SEM_DUPLA_APROVACAO*')) ("abrir procedimento com APROVADA_PARCIAL -> $($s8ab1.code) (409 DEMANDA_URGENCIA_SEM_DUPLA_APROVACAO nomeada: o processo so nasce apos a dupla aprovacao completa)")
$s8a2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d2id + '/aprovar') -Base $ApiBase
Add-Check 'S8' 'demanda.urgencia-mesma-autoridade-409' (($s8a2.code -eq 409) -and ($s8a2.body -like '*SEPARACAO_FUNCOES_VIOLADA*')) ("segunda aprovacao pela MESMA autoridade da primeira -> $($s8a2.code) (409 SEPARACAO_FUNCOES_VIOLADA nomeada — BR-8.1)")
$null = Get-Psql ("update sigov.compras_demanda set aprovado_por=910402 where id=" + $s8d2id + ";")
$s8a3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d2id + '/aprovar') -Base $ApiBase
$s8a3j = $null; try { $s8a3j = ($s8a3.body | ConvertFrom-Json) } catch {}
$s8a3pid = 0; if ($s8a3j -and $null -ne $s8a3j.data -and $null -ne $s8a3j.data.processoId) { $s8a3pid = [long]$s8a3j.data.processoId }
$s8a3db = Get-Psql ("select status||'|'||coalesce(dupla_aprovado_por::text,'NULL')||'|'||coalesce((select p.id::text from sigov.compras_processo p where p.tenant_id=910001 and p.demanda_id=" + $s8d2id + " limit 1),'SEM_PROCESSO') from sigov.compras_demanda where id=" + $s8d2id + ";")
Add-Check 'S8' 'demanda.urgencia-dupla-aprovacao-completa-processo-unico' (($s8a3.code -eq 200) -and ($s8a3pid -gt 0) -and ($s8a3db.text.StartsWith('APROVADA|' + $s8uid + '|') -eq $true)) ("segunda aprovacao por autoridade DISTINTA (fixture reposiciona a 1a para 910402) -> $($s8a3.code) processoId=$s8a3pid; banco status|dupla_aprovado_por|processo_id: $($s8a3db.text) (BR-8.1: dupla aprovacao completa registrada; BR-8.2: processo unico nasce na mesma transacao da segunda aprovacao)")
$s8a4 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d1id + '/aprovar') -Base $ApiBase
$s8a4db = Get-Psql ("select count(*)||'|'||min(status)||'|'||case when min(numero) like 'PI-%' then 'PI' else 'NAO' end||'|'||coalesce(min(demanda_id::text),'0') from sigov.compras_processo where tenant_id=910001 and demanda_id=" + $s8d1id + ";")
Add-Check 'S8' 'demanda.nao-urgente-aprovada-processo-unico-criado' (($s8a4.code -eq 200) -and ($s8a4db.text -eq ('1|ABERTO|PI|' + $s8d1id))) ("aprovar demanda nao urgente -> $($s8a4.code); banco processos count|status|numero|demanda: $($s8a4db.text) (BR-8.2: processo unico da contratacao criado na mesma transacao, numero PI-YYYY-<demanda>, Lei 14.133 art. 128)")
$s8rp = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d1id + '/repelir') -Base $ApiBase -Body '{"justificativa":"tentativa pos aprovacao"}'
$s8cx = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d1id + '/cancelar') -Base $ApiBase -Body '{"justificativa":"tentativa pos aprovacao"}'
Add-Check 'S8' 'demanda.imutavel-pos-aprovacao-409' (($s8rp.code -eq 409) -and ($s8rp.body -like '*DEMANDA_APOS_APROVACAO_IMUTAVEL*') -and ($s8cx.code -eq 409) -and ($s8cx.body -like '*DEMANDA_APOS_APROVACAO_IMUTAVEL*')) ("repelir -> $($s8rp.code) e cancelar -> $($s8cx.code) sobre APROVADA (ambos 409 DEMANDA_APOS_APROVACAO_IMUTAVEL nomeada — BR-8.1: imutavel apos aprovacao)")
$s8d3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-Terceira chamada","justificativa":"ciclo de decisao c2s8","itens":[{"descricao":"Item C2S8 T","quantidade":2,"unidade":"un","precoEstimado":10,"centroCustoId":' + $s8cc1 + '}],"urgencia":false}')
$s8d3j = $null; try { $s8d3j = ($s8d3.body | ConvertFrom-Json) } catch {}
$s8d3id = 0; if ($s8d3j -and $null -ne $s8d3j.data) { $s8d3id = [long]$s8d3j.data.id }
$null = Get-Psql ("update sigov.compras_demanda set created_by=910301 where id=" + $s8d3id + ";")
$s8r1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d3id + '/repelir') -Base $ApiBase -Body '{"justificativa":"sem disponibilidade orcamentaria neste ciclo"}'
$s8rb = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d3id + '/reabrir') -Base $ApiBase -Body '{"justificativa":"nova programacao orcamentaria confirmada"}'
$s8rbdb = Get-Psql ("select status||'|'||coalesce(aprovado_em::text,'NULL')||'|'||coalesce(justificativa_decisao::text,'NULL') from sigov.compras_demanda where id=" + $s8d3id + ";")
Add-Check 'S8' 'demanda.repelida-reaberta-ciclo-com-justificativa' (($s8r1.code -eq 200) -and ($s8rb.code -eq 200) -and ($s8rbdb.text -like 'SOLICITADA|NULL|nova programacao*')) ("repelir -> $($s8r1.code) (REPELIDA); reabrir -> $($s8rb.code); banco status|aprovado_em|decisao: $($s8rbdb.text) (BR-8.1: REPELIDA -> SOLICITADA com nova justificativa e campos de aprovacao zerados para novo ciclo)")
$s8rb2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d3id + '/reabrir') -Base $ApiBase -Body '{"justificativa":"reabrir de novo"}'
$s8cx2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d3id + '/cancelar') -Base $ApiBase -Body '{"justificativa":"desnecessario apos reprogramacao"}'
Add-Check 'S8' 'demanda.estado-invalido-e-cancelamento-final' (($s8rb2.code -eq 409) -and ($s8rb2.body -like '*DEMANDA_ESTADO_INVALIDO*') -and ($s8cx2.code -eq 200)) ("reabrir sobre SOLICITADA -> $($s8rb2.code) (409 DEMANDA_ESTADO_INVALIDO nomeada); cancelar sobre SOLICITADA -> $($s8cx2.code) (CANCELADA terminal; BR-8.1 maquina SOLICITADA/APROVADA_PARCIAL -> APROVADA|REPELIDA|CANCELADA)")

# --- BR-8.2/8.6: abertura do procedimento (modalidade do catalogo) ------------------
$s8m1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d1id + ',"modalidadeCodigo":"LEILAO"}')
Add-Check 'S8' 'procedimento.modalidade-fora-do-catalogo-404' (($s8m1.code -eq 404) -and ($s8m1.body -like '*MODALIDADE_FORA_DO_CATALOGO*')) ("abrir com modalidade LEILAO (ausente do catalogo do banco) -> $($s8m1.code) (404 MODALIDADE_FORA_DO_CATALOGO nomeada: nada fora do banco vira modalidade — regras 11/12)")
$s8m2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d1id + ',"modalidadeCodigo":"CONSULTA_PREVIA","orcamentoEstimado":200000.37}')
Add-Check 'S8' 'procedimento.consulta-previa-acima-do-teto-409' (($s8m2.code -eq 409) -and ($s8m2.body -like '*CONSULTA_PREVIA_ACIMA_TETO*')) ("consulta previa com R$ 200.000,37 (teto parametrico COMPRAS_CONSULTA_PREVIA_TETO=118704.36 no banco) -> $($s8m2.code) (409 CONSULTA_PREVIA_ACIMA_TETO nomeada: limite Lei 14.133 art. 75 II parametrizado, regra 11)")
$s8m3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d1id + ',"modalidadeCodigo":"PREGAO_ELETRONICO","fonteRecurso":"Fundo Estadual de Manutencao C2S8"}')
$s8m3j = $null; try { $s8m3j = ($s8m3.body | ConvertFrom-Json) } catch {}
$s8proc1 = 0; $s8lic1 = 0
if ($s8m3j -and $null -ne $s8m3j.data) { $s8proc1 = [long]$s8m3j.data.processoId; $s8lic1 = [long]$s8m3j.data.licitacaoId }
$s8lic1db = Get-Psql ("select status||'|'||zona_me_epp_status||'|'||case when numero like 'LIT-%' then 'LIT' else 'NAO' end||'|'||coalesce((select cfg.modalidade from sigov.compras_modalidade_config cfg where cfg.id=l.modalidade_config_id),'NAO') from sigov.compras_licitacao l where id=" + $s8lic1 + ";")
Add-Check 'S8' 'procedimento.aberto-pregao-do-catalogo' (($s8m3.code -eq 200) -and ($s8lic1 -gt 0) -and ($s8m3j.data.criterio -eq 'MENOR_PRECO') -and ($s8lic1db.text -eq 'ABERTA|NAO_APLICAVEL|LIT|PREGAO_ELETRONICO')) ("abrir PREGAO_ELETRONICO -> $($s8m3.code) processo=$s8proc1 licitacao=$s8lic1 criterio=$($s8m3j.data.criterio); banco lic status|zona|numero|modalidade: $($s8lic1db.text) (BR-8.2/8.3: licitacao ABERTA vinculada ao processo unico; zona ME/EPP inicia NAO_APLICAVEL; metadados do ato anexados ao processo jsonb)")
$s8m4 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d1id + ',"modalidadeCodigo":"PREGAO_ELETRONICO"}')
$s8m4j = $null; try { $s8m4j = ($s8m4.body | ConvertFrom-Json) } catch {}
$s8m5 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d1id + ',"modalidadeCodigo":"CONCORRENCIA","orcamentoEstimado":500000}')
Add-Check 'S8' 'procedimento.idempotente-e-unico-por-demanda' (($s8m4.code -eq 200) -and ([long]$s8m4j.data.licitacaoId -eq $s8lic1) -and ($s8m4j.data.jaExiste -eq $true) -and ($s8m5.code -eq 409) -and ($s8m5.body -like '*PROCESSO_JA_VINCULADO_A_DEMANDA*')) ("reabrir MESMA modalidade -> $($s8m4.code) jaExiste=true mesmo licid=$($s8m4j.data.licitacaoId); modalidade DIFERENTE (CONCORRENCIA) -> $($s8m5.code) (409 PROCESSO_JA_VINCULADO_A_DEMANDA nomeada: um processo, um procedimento — BR-8.2 art. 128)")

# --- BR-8.3: habilitacao por tipo ----------------------------------------------------
$s8h1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/habilitacoes') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f1 + ',"tipo":"JURIDICO","documentoRef":"DOC-C2S8-J1","status":"APROVADO"}')
$s8h2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/habilitacoes') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f1 + ',"tipo":"TECNICO","documentoRef":"DOC-C2S8-T1","status":"APROVADO"}')
$s8h3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/habilitacoes') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f3 + ',"tipo":"JURIDICO","documentoRef":"DOC-C2S8-J3","status":"REPROVADO"}')
$s8h4 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/habilitacoes') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f3 + ',"tipo":"JURIDICO","documentoRef":"DOC-C2S8-J3","status":"REPROVADO","justificativa":"alvara vencido conforme conferencia c2s8"}')
$s8hdb = Get-Psql ("select (select count(*) from sigov.compras_habilitacao where tenant_id=910001 and licitacao_id=" + $s8lic1 + " and fornecedor_id=" + $s8f1 + ")||'|'||coalesce((select status from sigov.compras_habilitacao where tenant_id=910001 and licitacao_id=" + $s8lic1 + " and fornecedor_id=" + $s8f3 + " and tipo='JURIDICO'),'NAO')||'|'||(select count(*) from sigov.compras_habilitacao where tenant_id=910001 and licitacao_id=" + $s8lic1 + " and fornecedor_id=" + $s8f3 + ");")
Add-Check 'S8' 'habilitacao.por-tipo-reprovacao-com-justificativa' (($s8h1.code -eq 200) -and ($s8h2.code -eq 200) -and ($s8h3.code -eq 400) -and ($s8h3.body -like '*HABILITACAO_REPROVACAO_SEM_JUSTIFICATIVA*') -and ($s8h4.code -eq 200) -and ($s8hdb.text -eq '2|REPROVADO|1')) ("JURIDICO APROVADO -> $($s8h1.code); TECNICO APROVADO -> $($s8h2.code); REPROVADO sem justificativa -> $($s8h3.code) (400 nomeada); REPROVADO com justificativa -> $($s8h4.code); banco linhas-F1|status-F3|linhas-F3: $($s8hdb.text) (BR-8.3: conferencia por tipo JURIDICO/FISCAL_TRABALHISTA/TECNICO com documento registrado; 1 linha vigente por tipo)")

# --- BR-8.3: lances decrescentes + idempotencia + zona ME/EPP -------------------------
$s8l1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f1 + ',"valor":1000}')
$s8l2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f3 + ',"valor":1200}')
$s8l3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f1 + ',"valor":950}')
Add-Check 'S8' 'lance.primeiro-e-necessariamente-decrescente' (($s8l1.code -eq 200) -and ($s8l2.code -eq 409) -and ($s8l2.body -like '*LANCE_NAO_DECRESCENTE*') -and ($s8l3.code -eq 200)) ("F1 1000.00 -> $($s8l1.code); F3 1200.00 (>= melhor 1000) -> $($s8l2.code) (409 LANCE_NAO_DECRESCENTE nomeada); F1 950.00 -> $($s8l3.code) (BR-8.3: sessao eletrônica de lances decrescentes; 1 registro por autor x sequencia)")
$s8k1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f3 + ',"valor":949,"chaveIdempotencia":"C2S8-K1"}')
$s8k1j = $null; try { $s8k1j = ($s8k1.body | ConvertFrom-Json) } catch {}
$s8kid = 0; if ($s8k1j -and $null -ne $s8k1j.data) { $s8kid = [long]$s8k1j.data.id }
$s8k2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f3 + ',"valor":949,"chaveIdempotencia":"C2S8-K1"}')
$s8k2j = $null; try { $s8k2j = ($s8k2.body | ConvertFrom-Json) } catch {}
$s8k2id = 0; if ($s8k2j -and $null -ne $s8k2j.data) { $s8k2id = [long]$s8k2j.data.id }
$s8k3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f3 + ',"valor":948,"chaveIdempotencia":"C2S8-K1"}')
Add-Check 'S8' 'lance.chave-idempotencia-1-lance-1-registro' (($s8k1.code -eq 200) -and ($s8kid -gt 0) -and ($s8k2.code -eq 200) -and ($s8k2id -eq $s8kid) -and ($s8k3.code -eq 409) -and ($s8k3.body -like '*LANCE_CHAVE_IDEMPOTENCIA_CONFLITO*')) ("F3 949 chave C2S8-K1 -> $($s8k1.code) id=$s8kid; replay mesmo conteudo -> $($s8k2.code) id=$s8k2id (VOLTA o efeito original, nao duplica); mesmo conteudo-chave valor diferente -> $($s8k3.code) (409 LANCE_CHAVE_IDEMPOTENCIA_CONFLITO nomeada; BR-8.3: 1 lance = 1 registro)")
$s8l4 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f2 + ',"valor":930}')
$s8zdb = Get-Psql ("select zona_me_epp_status from sigov.compras_licitacao where id=" + $s8lic1 + ";")
Add-Check 'S8' 'zona.me-epp-aberta-ao-primeiro-lance-me' (($s8l4.code -eq 200) -and ($s8zdb.text -eq 'ABERTA')) ("lance do fornecedor ME (categoria cadastrada no banco) 930.00 -> $($s8l4.code); banco zona_me_epp_status: $($s8zdb.text) (BR-8.3/LC 123/2006: a zona de preferencias ABRE automaticamente com o primeiro lancista ME/EPP)")
$s8jz = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/julgamento') -Base $ApiBase -Body '{}'
Add-Check 'S8' 'julgamento.bloqueado-zona-aberta-409' (($s8jz.code -eq 409) -and ($s8jz.body -like '*PREFERENCIA_ME_EPP_NAO_DOCUMENTADA*')) ("julgamento com zona ainda ABERTA -> $($s8jz.code) (409 PREFERENCIA_ME_EPP_NAO_DOCUMENTADA nomeada: o desfecho da preferencia deve ser documentado antes de classificar)")
$s8p1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/preferencia') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f1 + ',"documentoRef":"DOC-C2S8-P0"}')
Add-Check 'S8' 'preferencia.fornecedor-fora-da-zona-409' (($s8p1.code -eq 409) -and ($s8p1.body -like '*FORNECEDOR_FORA_DA_ZONA_ME_EPP*')) ("exercer preferencia como fornecedor NAO ME/EPP -> $($s8p1.code) (409 FORNECEDOR_FORA_DA_ZONA_ME_EPP nomeada; somente ME/EPP exerce, LC 123/2006)")
$s8l5 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/lances') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f2 + ',"valor":900}')
$s8p2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/preferencia') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f2 + ',"documentoRef":"DOC-C2S8-PREF"}')
$s8p2j = $null; try { $s8p2j = ($s8p2.body | ConvertFrom-Json) } catch {}
$s8pdb = Get-Psql ("select zona_me_epp_status||'|'||case when zona_motivo like '%DOC-C2S8-PREF%' then 'DOC' else 'NAO' end from sigov.compras_licitacao where id=" + $s8lic1 + ";")
Add-Check 'S8' 'preferencia.dentro-da-margem-parametrica-exercida' (($s8l5.code -eq 200) -and ($s8p2.code -eq 200) -and ($s8p2j.data.dentroDaMargem -eq $true) -and ($s8p2j.data.zonaStatus -eq 'ENCERRADA_POR_PREFERENCIA') -and ([decimal]$s8p2j.data.melhorLanceMeEpp -eq 900) -and ([decimal]$s8p2j.data.melhorLanceForaMeEpp -eq 949) -and ([int]$s8p2j.data.margemPct -eq 5) -and ($s8pdb.text -eq 'ENCERRADA_POR_PREFERENCIA|DOC')) ("ME lana 900.00 (melhor fora zona 949.00; margem parametrica LICITACAO_MARGEM_ME_EPP_PCT=5 -> limite 901.55) -> preferencia $($s8p2.code) desfecho=$($s8p2j.data.desfecho) dentroDaMargem=$($s8p2j.data.dentroDaMargem); banco zona|motivo-doc: $($s8pdb.text) (BR-8.3/LC 123: preferencia EXERCIDA por estar a 5% do melhor nao ME/EPP; desfecho documentado com o documento de referencia)")
$s8p3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/preferencia') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f2 + ',"documentoRef":"DOC-C2S8-PREF2"}')
Add-Check 'S8' 'preferencia.ja-resolvida-409' (($s8p3.code -eq 409) -and ($s8p3.body -like '*ZONA_ME_EPP_JA_RESOLVIDA*')) ("2a documentacao da zona -> $($s8p3.code) (409 ZONA_ME_EPP_JA_RESOLVIDA nomeada: o desfecho e um ato unico e traceavel)")
$s8j1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/julgamento') -Base $ApiBase -Body '{}'
$s8j1j = $null; try { $s8j1j = ($s8j1.body | ConvertFrom-Json) } catch {}
$s8jdb = Get-Psql ("select string_agg(ordem_classificacao::text||':'||fornecedor_id::text||':'||to_char(valor,'FM999990.00'), ',' order by ordem_classificacao)||'|'||(select status from sigov.compras_licitacao where id=" + $s8lic1 + ") from sigov.compras_proposta where tenant_id=910001 and licitacao_id=" + $s8lic1 + ";")
$s8jesp = ('1:' + $s8f2 + ':900.00,2:' + $s8f3 + ':949.00,3:' + $s8f1 + ':950.00|JULGADA')
Add-Check 'S8' 'julgamento.classificacao-automatica-melhores-lances' (($s8j1.code -eq 200) -and ($s8jdb.text -eq $s8jesp)) ("julgamento -> $($s8j1.code); banco classificacao|status-licitacao: $($s8jdb.text) esperado=$s8jesp (BR-8.4: classificacao automatica pelos MELHORES lances de cada fornecedor, menor primeiro; o ME vence na frente apos preferencia exercida; licitacao JULGADA)")
$s8j2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/julgamento') -Base $ApiBase -Body '{}'
Add-Check 'S8' 'julgamento.repetido-409' (($s8j2.code -eq 409) -and ($s8j2.body -like '*JULGAMENTO_JA_REALIZADO*')) ("julgamento novamente -> $($s8j2.code) (409 JULGAMENTO_JA_REALIZADO nomeada; o ato e unico por licitacao)")
$s8pf3 = Get-Psql ("select id from sigov.compras_proposta where tenant_id=910001 and licitacao_id=" + $s8lic1 + " and fornecedor_id=" + $s8f3 + " limit 1;")
$s8pr = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/propostas/' + $s8pf3.text + '/rejeitar') -Base $ApiBase -Body '{"motivo":"memoria de calculo nao cobre o preco unitario apresentado"}'
$s8pr2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/propostas/' + $s8pf3.text + '/rejeitar') -Base $ApiBase -Body '{"motivo":"de novo"}'
Add-Check 'S8' 'proposta.rejeitada-inexequivel-entre-julgamento-e-adjudicacao' (($s8pr.code -eq 200) -and ($s8pr.body -like '*REJEITADA_INEXEQUIVEL*') -and ($s8pr2.code -eq 409) -and ($s8pr2.body -like '*PROPOSTA_JA_REJEITADA*')) ("rejeitar proposta F3 (ordem 2) por inexequibilidade -> $($s8pr.code) (status REJEITADA_INEXEQUIVEL com motivo registrado); rejeitar de novo -> $($s8pr2.code) (409 PROPOSTA_JA_REJEITADA nomeada; BR-8.4: rejeicao so entre classificacao e adjudicacao)")
$s8ad0 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/adjudicar') -Base $ApiBase -Body '{}'
Add-Check 'S8' 'adjudicacao.sem-itens-licitados-400' (($s8ad0.code -eq 400) -and ($s8ad0.body -like '*ADJUDICACAO_SEM_ITENS_LICITADOS*')) ("adjudicar antes de haver itens na licitacao -> $($s8ad0.code) (400 ADJUDICACAO_SEM_ITENS_LICITADOS nomeada: o objeto adjudicado espelha os itens licitados)")
$s8li1 = Get-Psql ("insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910001," + $s8ent + "," + $s8lic1 + ",1,'Papel A4',10,'pct',25.50,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s8lic1 + " and i.numero=1); insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910001," + $s8ent + "," + $s8lic1 + ",2,'Toner C2S8',2,'un',375.00,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s8lic1 + " and i.numero=2); select count(*) from sigov.compras_licitacao_item where licitacao_id=" + $s8lic1 + ";")
$s8ad1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/adjudicar') -Base $ApiBase -Body '{}'
$s8ad1j = $null; try { $s8ad1j = ($s8ad1.body | ConvertFrom-Json) } catch {}
$s8addb = Get-Psql ("select to_char(a.valor_total,'FM999990.00')||'|'||to_char((select coalesce(sum(i.total),0) from sigov.compras_adjudicacao_item i where i.adjudicacao_id=a.id),'FM999990.00')||'|'||(select status from sigov.compras_proposta p where p.id=a.proposta_vencedora_id)||'|'||(select status from sigov.compras_licitacao where id=" + $s8lic1 + ")||'|'||(select count(*) from sigov.compras_adjudicacao_item i join sigov.compras_licitacao_item li on li.id=i.licitacao_item_id where i.adjudicacao_id=a.id) from sigov.compras_adjudicacao a where tenant_id=910001 and licitacao_id=" + $s8lic1 + ";")
Add-Check 'S8' 'adjudicacao.executada-valor-derivado-vencedora-me' (($s8li1.text -eq '2') -and ($s8ad1.code -eq 200) -and ([long]$s8ad1j.data.fornecedorId -eq $s8f2) -and ([decimal]$s8ad1j.data.valorTotal -eq 900) -and ([int]$s8ad1j.data.nItens -eq 2) -and ($s8addb.text -eq '900.00|900.00|VENCEDORA|ADJUDICADA|2')) ("2 itens licitados (fixture) exit=$($s8li1.code); adjudicar -> $($s8ad1.code) fornecedor=$($s8ad1j.data.fornecedorId) valorTotal=$($s8ad1j.data.valorTotal) nItens=$($s8ad1j.data.nItens); banco adj.valor_total|sum-itens|proposta-vencedora|lic|items-espelhados: $($s8addb.text) (BR-8.4: vencedor = menor ordem nao rejeitada (ME apos preferencia); valor DERIVADO do lance distribuido aos itens espelhados; licitacao ADJUDICADA)")
$s8ad2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/adjudicar') -Base $ApiBase -Body '{}'
Add-Check 'S8' 'adjudicacao.repetida-409' (($s8ad2.code -eq 409) -and ($s8ad2.body -like '*ADJUDICACAO_JA_REALIZADA*')) ("adjudicar de novo -> $($s8ad2.code) (409 ADJUDICACAO_JA_REALIZADA nomeada; um ato por licitacao)")
$s8ho0 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/homologar') -Base $ApiBase
Add-Check 'S8' 'homologacao.mesma-autoridade-do-adjudicador-409' (($s8ho0.code -eq 409) -and ($s8ho0.body -like '*HOMOLOGADOR_E_ADJUDICADOR_IGUAIS*')) ("homologar pela MESMA autoridade que adjudicou -> $($s8ho0.code) (409 HOMOLOGADOR_E_ADJUDICADOR_IGUAIS nomeada: atos SEPARADOS com autoridade distinta — BR-8.4)")
$null = Get-Psql ("update sigov.compras_adjudicacao set responsavel_id=910402 where tenant_id=910001 and licitacao_id=" + $s8lic1 + ";")
$s8ho1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/homologar') -Base $ApiBase
$s8ho1j = $null; try { $s8ho1j = ($s8ho1.body | ConvertFrom-Json) } catch {}
$s8hoid = 0; if ($s8ho1j -and $null -ne $s8ho1j.data) { $s8hoid = [long]$s8ho1j.data.id }
$s8hodb = Get-Psql ("select l.status||'|'||h.id||'|'||h.responsavel_id from sigov.compras_licitacao l join sigov.compras_homologacao h on h.licitacao_id=l.id where l.id=" + $s8lic1 + ";")
Add-Check 'S8' 'homologacao.homologada-por-autoridade-distinta' (($s8ho1.code -eq 200) -and ($s8hoid -gt 0) -and ($s8hodb.text -like ("HOMOLOGADA|" + $s8hoid + "|" + $s8uid))) ("homologar com autoridade distinta do adjudicador (fixture reposiciona adjudicador para 910402) -> $($s8ho1.code) id=$s8hoid; banco licitacao|homologacao|responsavel: $($s8hodb.text) (BR-8.4: homologacao dentro do prazo parametrico HOMOLOGACAO_PRAZO_MAX_DIAS=30 contado da adjudicacao)")
$s8ho2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic1 + '/homologar') -Base $ApiBase
Add-Check 'S8' 'homologacao.repetida-409' (($s8ho2.code -eq 409) -and ($s8ho2.body -like '*HOMOLOGACAO_JA_REALIZADA*')) ("homologar de novo -> $($s8ho2.code) (409 HOMOLOGACAO_JA_REALIZADA nomeada)")
$s8ct0 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body '{"homologacaoId":9999999999,"dataEficacia":"2026-11-01","dataFim":"2027-10-31","assinaturaModo":"GED"}'
Add-Check 'S8' 'contrato.homologacao-inexistente-404' (($s8ct0.code -eq 404) -and ($s8ct0.body -like '*HOMOLOGACAO_INEXISTENTE*')) ("gerar contrato da homologacao 9999999999 -> $($s8ct0.code) (404 HOMOLOGACAO_INEXISTENTE nomeada; BR-8.5: contrato NAO nasce sem procedimento homologado)")
$s8ct1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body ('{"homologacaoId":' + $s8hoid + ',"dataEficacia":"2026-11-01","dataFim":"2027-10-31","assinaturaModo":"GED","gestor":"Gestor C2S8","fiscal":"Fiscal C2S8"}')
$s8ct1j = $null; try { $s8ct1j = ($s8ct1.body | ConvertFrom-Json) } catch {}
$s8ct1id = 0; if ($s8ct1j -and $null -ne $s8ct1j.data) { $s8ct1id = [long]$s8ct1j.data.contratoId }
$s8ctdb = Get-Psql ("select status||'|'||to_char(valor_contratado,'FM999990.00')||'|'||homologacao_id::text||'|'||assinatura_modo||'|'||to_char((select coalesce(sum(i.total),0) from sigov.compras_contrato_item i where i.contrato_id=c.id),'FM999990.00')||'|'||(select count(*) from sigov.compras_contrato_item i where i.contrato_id=c.id) from sigov.compras_contrato c where id=" + $s8ct1id + ";")
Add-Check 'S8' 'contrato.gerado-da-homologacao-valor-derivado-ged' (($s8ct1.code -eq 200) -and ([decimal]$s8ct1j.data.valorContratado -eq 900) -and ([int]$s8ct1j.data.nItens -eq 2) -and ($s8ctdb.text -like ("VIGENTE|900.00|" + $s8hoid + "|GED|900.00|2"))) ("gerar contrato da homologacao $s8hoid (GED, gestor+fiscal) -> $($s8ct1.code) id=$s8ct1id; banco status|valor|homologacao|assinatura|sum-items|n-items: $($s8ctdb.text) (BR-8.5: contrato NASCE da homologacao com valor DERIVADO da adjudicacao 900.00 (sem edicao livre), itens espelhados 1:1 da adjudicacao e vínculo homologacao_id univoco)")
$s8ct2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body ('{"homologacaoId":' + $s8hoid + ',"dataEficacia":"2026-11-01","dataFim":"2027-10-31","assinaturaModo":"GED"}')
Add-Check 'S8' 'contrato.duplicado-para-a-mesma-homologacao-409' (($s8ct2.code -eq 409) -and ($s8ct2.body -like '*CONTRATO_JA_GERADO_PARA_HOMOLOGACAO*')) ("2o contrato da mesma homologacao -> $($s8ct2.code) (409 CONTRATO_JA_GERADO_PARA_HOMOLOGACAO nomeada + indice unico vigente como backstop)")
$s8rec = Get-Psql ("insert into sigov.compras_processo_item(tenant_id,entidade_id,processo_id,numero,descricao,quantidade,unidade,tipo_material_servico) select 910001," + $s8ent + "," + $s8proc1 + ",1,'ITEM-C2S8-1',10,'UN','MATERIAL' where not exists(select 1 from sigov.compras_processo_item pi where pi.tenant_id=910001 and pi.processo_id=" + $s8proc1 + " and pi.descricao='ITEM-C2S8-1'); insert into sigov.compras_recebimento(tenant_id,entidade_id,processo_id,contrato_id,documento,data_recebimento,status,created_at,created_by) select 910001," + $s8ent + "," + $s8proc1 + "," + $s8ct1id + ",'AT-C2S8-1',current_date,'PENDENTE',now(),910301 where not exists(select 1 from sigov.compras_recebimento r where r.tenant_id=910001 and r.documento='AT-C2S8-1'); insert into sigov.compras_recebimento_item(recebimento_id,tenant_id,entidade_id,processo_item_id,quantidade,valor_unitario,tipo_material) select r.id,910001," + $s8ent + ",pi.id,10,90.00,'CONSUMO' from sigov.compras_recebimento r join sigov.compras_processo_item pi on pi.tenant_id=r.tenant_id and pi.processo_id=r.processo_id and pi.descricao='ITEM-C2S8-1' where r.tenant_id=910001 and r.documento='AT-C2S8-1'; select count(*) from sigov.compras_recebimento where tenant_id=910001 and documento='AT-C2S8-1';")
$s8ex = Invoke-Api -Bearer $tkS8.token -Method GET -Path ('/api/compras/integradas/contratos/' + $s8ct1id + '/execucao') -Base $ApiBase
$s8exj = $null; try { $s8exj = ($s8ex.body | ConvertFrom-Json) } catch {}
Add-Check 'S8' 'execucao.teto-parametrico-bloqueia-novo-fornecimento' (($s8rec.text -eq '1') -and ($s8ex.code -eq 200) -and ([decimal]$s8exj.data.totalFornecido -eq 900) -and ([decimal]$s8exj.data.pctExecutado -eq 100) -and ([int]$s8exj.data.tetoPct -eq 100) -and ($s8exj.data.bloqueiaNovoFornecimento -eq $true)) ("recebimento fixture 10 x 90.00 = 900.00 (100% do contratado); GET execucao -> $($s8ex.code) totalFornecido=$($s8exj.data.totalFornecido) pct=$($s8exj.data.pctExecutado) teto=$($s8exj.data.tetoPct)% bloqueia=$($s8exj.data.bloqueiaNovoFornecimento) (BR-8.5: teto de execucao parametrizado CONTRATO_EXECUCAO_TETO_PCT=100 lido do banco; sinaliza bloqueio de novo fornecimento para a etapa 2/2)")
$s8pg1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s8ct1id + '/prorrogar') -Base $ApiBase -Body '{"novoFim":"2031-11-02","justificativa":"prorrogo anual c2s8"}'
$s8pg2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s8ct1id + '/prorrogar') -Base $ApiBase -Body '{"novoFim":"2031-11-01","justificativa":"prorrogo anual c2s8 dentro do teto art 106"}'
$s8pgdb = Get-Psql ("select to_char(data_fim,'YYYY-MM-DD')||'|'||(select count(*) from sigov.compras_contrato_historico h where h.contrato_id=" + $s8ct1id + " and h.status_novo='PRORROGACAO')||'|'||coalesce((select h.justificativa from sigov.compras_contrato_historico h where h.contrato_id=" + $s8ct1id + " and h.status_novo='PRORROGACAO' limit 1),'NAO') from sigov.compras_contrato where id=" + $s8ct1id + ";")
$s8dbgB = ('pg1body=[' + $s8pg1.body.Substring(0,[Math]::Min(150,$s8pg1.body.Length)) + '] pg2body=[' + $s8pg2.body.Substring(0,[Math]::Min(150,$s8pg2.body.Length)) + ']')
Add-Check 'S8' 'prorrogacao.teto-art106-parametrico-e-rastro-historico' (($s8pg1.code -eq 409) -and ($s8pg1.body -like '*PRORROGACAO_EXCEDE_TETO_ART_106*') -and ($s8pg2.code -eq 200) -and ($s8pgdb.text -like '2031-11-01|1|PRORROGACAO ate 2031-11-01:*')) ("prorrogar ate 2031-11-02 (> inicio 2026-11-01 + PRORROGACAO_TETO_ANOS=5) -> $($s8pg1.code) (409 PRORROGACAO_EXCEDE_TETO_ART_106 nomeada); prorrogar ate 2031-11-01 -> $($s8pg2.code); banco data_fim|historico|justificativa: $($s8pgdb.text) (BR-8.5/art. 106: teto de anos E maximo de vezes parametrizados no banco; todo ato deixa rastro append-only em contrato_historico; DBG ' + $s8dbgB + '")
$s8rs1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s8ct1id + '/rescindir') -Base $ApiBase -Body '{"tipo":"UNILATERAL_INADIMPLEMENTO","justificativa":"inadimplemento documentado c2s8"}'
$s8rs2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s8ct1id + '/rescindir') -Base $ApiBase -Body '{"tipo":"UNILATERAL_INADIMPLEMENTO","justificativa":"inadimplemento documentado c2s8","notificacaoRef":"NOT-C2S8-77"}'
$s8rsdb = Get-Psql ("select status||'|'||(select count(*) from sigov.compras_contrato_historico h where h.contrato_id=" + $s8ct1id + " and h.status_novo='RESCINDIDO')||'|'||case when coalesce((select h.justificativa from sigov.compras_contrato_historico h where h.contrato_id=" + $s8ct1id + " and h.status_novo='RESCINDIDO' limit 1),'') like '%NOT-C2S8-77%' then 'NOTIF' else 'NAO' end from sigov.compras_contrato where id=" + $s8ct1id + ";")
Add-Check 'S8' 'rescisao.unilateral-exige-notificacao-previa-e-atu' (($s8rs1.code -eq 400) -and ($s8rs1.body -like '*RESCISAO_SEM_ATO_JUSTIFICATIVA*') -and ($s8rs2.code -eq 200) -and ($s8rsdb.text -eq 'RESCIDO|1|NOTIF')) ("rescisao unilateral SEM notificacaoRef -> $($s8rs1.code) (400 RESCISAO_SEM_ATO_JUSTIFICATIVA nomeada: art. 129 exige notificação prévia); COM notificacaoRef NOT-C2S8-77 -> $($s8rs2.code); banco status|historico-rescisao|notif-registrada: $($s8rsdb.text) (BR-8.5: toda rescisao e um ATO com justificativa e rastro)")
$s8pg3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s8ct1id + '/prorrogar') -Base $ApiBase -Body '{"novoFim":"2032-11-01","justificativa":"pos rescisao"}'
Add-Check 'S8' 'contrato.resciso-nao-prorroga-409' (($s8pg3.code -eq 409) -and ($s8pg3.body -like '*CONTRATO_NAO_VIGENTE*')) ("prorrogar contrato RESCIDO -> $($s8pg3.code) (409 CONTRATO_NAO_VIGENTE nomeada: estado terminal do contrato)")

# --- BR-8.6: cadeia direta DISPENSA -> adjudicacao -> homologacao -> contrato ----------
$s8d5 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body ('{"objeto":"C2S8-Servico especial","justificativa":"contratacao direta c2s8","itens":[{"descricao":"Servico C2S8","quantidade":5,"unidade":"un","precoEstimado":100,"tipoObjeto":"SERVICO","centroCustoId":' + $s8cc1 + '}],"urgencia":false}')
$s8d5j = $null; try { $s8d5j = ($s8d5.body | ConvertFrom-Json) } catch {}
$s8d5id = 0; if ($s8d5j -and $null -ne $s8d5j.data) { $s8d5id = [long]$s8d5j.data.id }
$null = Get-Psql ("update sigov.compras_demanda set created_by=910301 where id=" + $s8d5id + ";")
$null = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s8d5id + '/aprovar') -Base $ApiBase
$s8dp1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d5id + ',"modalidadeCodigo":"DISPENSA"}')
$s8dp2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d5id + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_ZZZ"}')
Add-Check 'S8' 'dispensa.base-legal-obrigatoria-do-catalogo' (($s8d5.code -eq 200) -and ($s8dp1.code -eq 400) -and ($s8dp1.body -like '*DISPENSA_BASE_LEGAL_OBRIGATORIA*') -and ($s8dp2.code -eq 404) -and ($s8dp2.body -like '*DISPENSA_BASE_LEGAL_INEXISTENTE*')) ("demanda servico aprovada -> criada id=$s8d5id; abrir DISPENSA sem base legal -> $($s8dp1.code) (400 DISPENSA_BASE_LEGAL_OBRIGATORIA nomeada); com codigo INVENTADO (ART_75_ZZZ) -> $($s8dp2.code) (404 DISPENSA_BASE_LEGAL_INEXISTENTE nomeada: a base legal vem do CATALOGO art. 75 seed no banco, regra 11/12)")
$s8dp3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s8d5id + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_I"}')
$s8dp3j = $null; try { $s8dp3j = ($s8dp3.body | ConvertFrom-Json) } catch {}
$s8lic2 = 0; $s8proc2 = 0
if ($s8dp3j -and $null -ne $s8dp3j.data) { $s8proc2 = [long]$s8dp3j.data.processoId; $s8lic2 = [long]$s8dp3j.data.licitacaoId }
$s8dbmet = Get-Psql ("select coalesce(documentos_metadados->>'dispensa_base_legal','NAO')||'|'||coalesce(documentos_metadados->>'modalidade','NAO') from sigov.compras_processo where id=" + $s8proc2 + ";")
Add-Check 'S8' 'dispensa.aberta-com-base-legal-art75-rastro-no-processo' (($s8dp3.code -eq 200) -and ($s8lic2 -gt 0) -and ($s8dbmet.text -eq 'ART_75_I|DISPENSA')) ("abrir DISPENSA com ART_75_I -> $($s8dp3.code) licitacao=$s8lic2; metadados do processo base_legal|modalidade: $($s8dbmet.text) (BR-8.6: contratacao direta registrada com a base legal escolhida do catalogo, rastreada no processo unico)")
$s8li2 = Get-Psql ("insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910001," + $s8ent + "," + $s8lic2 + ",1,'Servico especial C2S8',5,'un',100.00,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s8lic2 + "); select count(*) from sigov.compras_licitacao_item where licitacao_id=" + $s8lic2 + ";")
$s8da1 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic2 + '/adjudicar') -Base $ApiBase -Body '{}'
Add-Check 'S8' 'adjudicacao.direta-sem-fornecedor-400' (($s8li2.text -eq '1') -and ($s8da1.code -eq 400) -and ($s8da1.body -like '*ADJUDICACAO_DIRETA_SEM_FORNECEDOR*')) ("adjudicar modalidade direta (DISPENSA) sem indicar fornecedor -> $($s8da1.code) (400 ADJUDICACAO_DIRETA_SEM_FORNECEDOR nomeada: sem sessao de lances, o ato SELECIONA o fornecedor)")
$s8da2 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic2 + '/adjudicar') -Base $ApiBase -Body ('{"fornecedorId":' + $s8f1 + ',"observacao":"contratacao direta c2s8"}')
$s8da2j = $null; try { $s8da2j = ($s8da2.body | ConvertFrom-Json) } catch {}
$s8dadb = Get-Psql ("select to_char(a.valor_total,'FM999990.00')||'|'||(select status from sigov.compras_licitacao where id=" + $s8lic2 + ")||'|'||(select p.fornecedor_id::text from sigov.compras_proposta p where p.id=a.proposta_vencedora_id) from sigov.compras_adjudicacao a where tenant_id=910001 and licitacao_id=" + $s8lic2 + ";")
Add-Check 'S8' 'adjudicacao.direta-executada-valor-derivado-dos-itens' (($s8da2.code -eq 200) -and ([decimal]$s8da2j.data.valorTotal -eq 500) -and ([long]$s8da2j.data.fornecedorId -eq $s8f1) -and ($s8dadb.text -like ("500.00|ADJUDICADA|" + $s8f1))) ("adjudicar direta indicando F1 -> $($s8da2.code) valorTotal=$($s8da2j.data.valorTotal) (5 un x 100.00 dos itens licitados); banco valor_total|status|fornecedor-vencedor: $($s8dadb.text) (BR-8.6: proposta criada e valor DERIVADO da planilha da licitacao)")
$null = Get-Psql ("update sigov.compras_adjudicacao set responsavel_id=910402 where tenant_id=910001 and licitacao_id=" + $s8lic2 + ";")
$s8ho3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s8lic2 + '/homologar') -Base $ApiBase
$s8ho3j = $null; try { $s8ho3j = ($s8ho3.body | ConvertFrom-Json) } catch {}
$s8ho3id = 0; if ($s8ho3j -and $null -ne $s8ho3j.data) { $s8ho3id = [long]$s8ho3j.data.id }
$s8ct3 = Invoke-Api -Bearer $tkS8.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body ('{"homologacaoId":' + $s8ho3id + ',"dataEficacia":"2026-11-05","dataFim":"2027-11-04","assinaturaModo":"FISICA","gestor":"Gestor C2S8","fiscal":"Fiscal C2S8"}')
$s8ct3j = $null; try { $s8ct3j = ($s8ct3.body | ConvertFrom-Json) } catch {}
$s8ct3db = Get-Psql ("select status||'|'||to_char(valor_contratado,'FM999990.00')||'|'||assinatura_modo||'|'||fornecedor_id::text from sigov.compras_contrato c where c.tenant_id=910001 and c.homologacao_id=" + $s8ho3id + " limit 1;")
Add-Check 'S8' 'dispensa.cadeia-completa-ate-contrato-assinatura-fisica' (($s8ho3.code -eq 200) -and ($s8ct3.code -eq 200) -and ([decimal]$s8ct3j.data.valorContratado -eq 500) -and ($s8ct3db.text -like ("VIGENTE|500.00|FISICA|" + $s8f1))) ("homologar dispensa -> $($s8ho3.code) id=$s8ho3id; gerar contrato ASSINATURA FISICA -> $($s8ct3.code) valorContratado=$($s8ct3j.data.valorContratado); banco status|valor|assinatura|fornecedor: $($s8ct3db.text) (BR-8.6: cadeia direta completa demanda -> DISPENSA(art.75) -> adjudicacao -> homologacao -> contrato; sem sessao de lances)")

# --- limpeza do bloco S8 (auto-limpeza; regra 9) ---------------------------------------
$s8cl = Get-Psql @"
create temp table if not exists s8_alvo_d (id bigint); truncate table s8_alvo_d;
insert into s8_alvo_d select id from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S8-%';
create temp table if not exists s8_alvo_p (id bigint); truncate table s8_alvo_p;
insert into s8_alvo_p select id from sigov.compras_processo where tenant_id=910001 and demanda_id in (select id from s8_alvo_d);
create temp table if not exists s8_alvo_l (id bigint); truncate table s8_alvo_l;
insert into s8_alvo_l select id from sigov.compras_licitacao where tenant_id=910001 and processo_id in (select id from s8_alvo_p);
delete from sigov.compras_recebimento_item where recebimento_id in (select id from sigov.compras_recebimento where tenant_id=910001 and contrato_id in (select id from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s8_alvo_l))));
delete from sigov.compras_recebimento where tenant_id=910001 and contrato_id in (select id from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s8_alvo_l)));
delete from sigov.compras_contrato_item where contrato_id in (select id from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s8_alvo_l)));
delete from sigov.compras_contrato_historico where contrato_id in (select id from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s8_alvo_l)));
delete from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s8_alvo_l));
delete from sigov.compras_homologacao where licitacao_id in (select id from s8_alvo_l);
delete from sigov.compras_adjudicacao_item where adjudicacao_id in (select id from sigov.compras_adjudicacao where licitacao_id in (select id from s8_alvo_l));
delete from sigov.compras_adjudicacao where licitacao_id in (select id from s8_alvo_l);
delete from sigov.compras_proposta where licitacao_id in (select id from s8_alvo_l);
delete from sigov.compras_habilitacao where licitacao_id in (select id from s8_alvo_l);
delete from sigov.compras_lance where licitacao_id in (select id from s8_alvo_l);
delete from sigov.compras_licitacao_item where licitacao_id in (select id from s8_alvo_l);
delete from sigov.compras_licitacao where id in (select id from s8_alvo_l);
delete from sigov.compras_processo_item where tenant_id=910001 and processo_id in (select id from s8_alvo_p);
delete from sigov.compras_processo where id in (select id from s8_alvo_p);
delete from sigov.compras_demanda_item where demanda_id in (select id from s8_alvo_d);
delete from sigov.compras_demanda where id in (select id from s8_alvo_d);
delete from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S8 %';
delete from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S8-%';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.perfil_permissao where correlation_id='cf58c2ab-1c20-4a90-88f8-0c2000000001';
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s8.admin');
delete from sigov.usuario where tenant_id=910001 and login='c2s8.admin';
delete from sigov.entidade where tenant_id=910001 and codigo_externo='C2S8-ENT';
select
 (select count(*) from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S8-%')
 + (select count(*) from sigov.compras_licitacao where tenant_id=910001 and objeto like 'C2S8-%')
 + (select count(*) from sigov.compras_contrato where tenant_id=910001 and objeto like 'C2S8-%')
 + (select count(*) from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S8 %')
 + (select count(*) from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S8-%')
 + (select count(*) from sigov.usuario where tenant_id=910001 and login='c2s8.admin')
 + (select count(*) from sigov.entidade where tenant_id=910001 and codigo_externo='C2S8-ENT')
  + (select count(*) from sigov.compras_processo_item where tenant_id=910001 and descricao like 'ITEM-C2S8-%');
"@
Add-Check 'S8' 'fixture.removido-auto-limpeza' (($s8cl.code -eq 0) -and ($s8cl.text -eq '0')) ("cascade delete S8 (recebimento+items, contratos+items+historico, homologacoes, adjudicacoes+items, propostas, habilitacoes, lances, licitacao_items, licitacoes, processos+items, demandas+items, fornecedores, centros de custo, sessoes/auditorias do usuario (identidade_sessao/auditoria_evento/autorizacao_decisao/sessao_contexto/contexto_operacional/trilha/historico_login/mfa), grants sentinel cf58c2ab, usuario c2s8.admin+grupo, entidade C2S8-ENT) tenant 910001 -> restantes=" + $s8cl.text)

# ============================================================================
# BLOCO S9 - Compras integradas (2/2): fornecimento -> recebimento -> fatura
# -> destinacao em estoque/patrimonio -> obrigacao financeira unica
# (BR-7.5/BR-7.6/BR-7.7 do contrato docs/CONTRATOS-FRENTE-ADMINISTRATIVA-C2S7-MULTIESFERA.md)
# Invariantes validadas ao vivo: recebido <= fornecido; rejeicao com motivo + prazo de
# substituicao parametrico (RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS=5, lido do banco)
# e desfecho obrigatorio antes do fechamento; fatura unica por fornecimento com
# conferencia (CNPJ x contratado, valor x pedido); cada destino = movimento
# referenciado unico idempotente por referencia (estoque: local inexistente/inativo
# bloqueia; patrimonio: ficha gerada no ato); 1 fornecimento CONCLUIDO = 1 obrigacao
# idempotente por referencia (valor = aprovado x unitario); sem NF-e vinculada nao
# liquida; estorno por compensacao sem delete fisico; execucao contratual (endpoint S8)
# continua consistente com o espelho legado dos recebimentos integrados.
# Multi-esfera (regras 21-24): entidade fixture com esfera 'federal' (complemento do
# estadual da S8) e usuario c2s9.admin com a MESMA cadeia usuario_grupo/grupo/perfil;
# grants sentinel cf58c2ab cobrindo as chaves canonicas C2S7 da frente + as reutilizadas
# almoxarifado.recebimento.confirmar / patrimonio.bem.criar (banco = autoridade, regra 11).
# Marcadores C2S9-%; o bloco se auto-limpa ao final (regra 9).
# ============================================================================
$s9fix = Get-Psql @"
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910001,'C2S9 Entidade Homologacao','00.000.000/0001-92','C2S9-ENT','federal','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910001 and codigo_externo='C2S9-ENT');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S9 Administrador','c2s9.admin','c2s9.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S9-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s9.admin');
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,910401,910001,true,now(),910301
from sigov.usuario u
where u.tenant_id=910001 and u.login='c2s9.admin'
and not exists(select 1 from sigov.usuario_grupo g where g.usuario_id=u.id and g.grupo_acesso_id=910401 and g.ativo);
-- Grant das 24 chaves (15 canonicas S7 + 7 da frente S9 + 2 reutilizadas almoxarifado/patrimonio),
-- marcadas com correlation_id sentinel para limpeza cirurgica; DO NOTHING preserva vinculos preexistentes.
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000001'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.id=(select gp.perfil_acesso_id from sigov.grupo_perfil gp where gp.grupo_acesso_id=910401 limit 1)
  and p.ativo and not p.is_deleted
  and ((p.modulo='compras' and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir','compras.licitacao.abrir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar','compras.julgamento.executar','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar','compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular','compras.obrigacao.visualizar','compras.obrigacao.liquidar'))
    or (p.chave='almoxarifado.recebimento.confirmar' and p.modulo='almoxarifado')
    or (p.chave='patrimonio.bem.criar' and p.modulo='patrimonio'))
on conflict (perfil_acesso_id,permissao_id) do nothing;
do `$`$
begin
  if (select count(1) from sigov.permissao p where p.ativo and not p.is_deleted
      and ((p.modulo='compras' and p.chave in ('compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular','compras.obrigacao.visualizar','compras.obrigacao.liquidar'))
        or (p.chave in ('almoxarifado.recebimento.confirmar','patrimonio.bem.criar')))) <> 9 then
    raise exception 'C2S9-FIXTURE: catalogo de permissoes incompleto (esperado 9 chaves ativas da frente no banco)';
  end if;
end
`$`$;
insert into sigov.financeiro_centro_custo(tenant_id,codigo,nome,descricao,ativo,created_at,updated_at)
select 910001,'C2S9-CC1','CC C2S9 Ativo','centro de custo ficticio ativo (homologacao)',true,now(),now()
where not exists(select 1 from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S9-CC1');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910001,e.id,'Forneadora C2S9 Delta','JURIDICA','99.999.000/0004-04','ATIVO',null,now(),now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S9-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910001 and f.documento='99.999.000/0004-04');
insert into sigov.almoxarifado_local(tenant_id,entidade_id,codigo,nome,ativo,created_at,updated_at)
select 910001,e.id,'C2S9-LOC1','Local C2S9 Ativo',true,now(),now()
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S9-ENT'
and not exists(select 1 from sigov.almoxarifado_local l where l.tenant_id=910001 and l.codigo='C2S9-LOC1');
insert into sigov.almoxarifado_local(tenant_id,entidade_id,codigo,nome,ativo,created_at,updated_at)
select 910001,e.id,'C2S9-LOC2','Local C2S9 Inativo',false,now(),now()
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S9-ENT'
and not exists(select 1 from sigov.almoxarifado_local l where l.tenant_id=910001 and l.codigo='C2S9-LOC2');
insert into sigov.almoxarifado_material(tenant_id,entidade_id,codigo,descricao,tipo_material,unidade_medida,estoque_minimo,controla_lote,controla_validade,ativo,created_at,updated_at)
select 910001,e.id,'C2S9-MAT','Material C2S9 homologacao','CONSUMO','un',0,false,false,true,now(),now()
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S9-ENT'
and not exists(select 1 from sigov.almoxarifado_material m where m.tenant_id=910001 and m.codigo='C2S9-MAT');
select coalesce((select id::text from sigov.entidade where tenant_id=910001 and codigo_externo='C2S9-ENT'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s9.admin'),'0') || '|'
     || coalesce((select id::text from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S9-CC1'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910001 and documento='99.999.000/0004-04'),'0') || '|'
     || coalesce((select id::text from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S9-MAT'),'0') || '|'
     || coalesce((select id::text from sigov.almoxarifado_local where tenant_id=910001 and codigo='C2S9-LOC1'),'0') || '|'
     || coalesce((select id::text from sigov.almoxarifado_local where tenant_id=910001 and codigo='C2S9-LOC2'),'0');
"@
$s9p = if ($s9fix.code -eq 0 -and ($s9fix.text -ne '')) { @($s9fix.text.Trim() -split '\|') } else { @('0','0','0','0','0','0','0') }
$s9ent=[long]$s9p[0]; $s9uid=[long]$s9p[1]; $s9cc1=[long]$s9p[2]; $s9f9=[long]$s9p[3]; $s9mat=[long]$s9p[4]; $s9loc1=[long]$s9p[5]; $s9loc2=[long]$s9p[6]
Add-Check 'S9' 'fixture.aplicado-entidade-federal-usuario-catalogo-completo' (($s9fix.code -eq 0) -and ($s9ent -gt 0) -and ($s9uid -gt 0) -and ($s9f9 -gt 0) -and ($s9mat -gt 0) -and ($s9loc1 -gt 0) -and ($s9loc2 -gt 0)) ("fixture S9 (entidade esfera FEDERAL c2s9 + usuario c2s9.admin com a mesma cadeia de perfil 910401 do admin + CC ativo + fornecedor 99.999.000/0004-04 + locais ativo/inativo + material C2S9-MAT; postcond do fixture exige as 9 chaves ativas da frente no banco — falha explicita, regras 11/13) exit=$($s9fix.code); ent=$s9ent uid=$s9uid cc=$s9cc1 forn=$s9f9 mat=$s9mat loc1=$s9loc1 loc2=$s9loc2")
$lgS9 = New-SigovLogin -User 'c2s9.admin' -Pass $Password -Base $WebBase
$tkS9 = if ($lgS9.ok) { Get-ApiToken -Jar $lgS9.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
Add-Check 'S9' 'auth.login-dedicado-token-mint' (($lgS9.ok -eq $true) -and ($tkS9.token.Length -eq 64)) ("c2s9.admin POST /Auth/Login -> $($lgS9.post); ApiToken len=$($tkS9.token.Length) (usuario da entidade FEDERAL c2s9 — regra 22: cobertura multi-esfera complementando o estadual da S8; autorizacao toda resolvida pelo banco, regra 11)")

# --- cadeia direta DISPENSA -> contrato (monta a base vigente da etapa 2/2) --------
$s9d1b = ('{"objeto":"C2S9-Aquisicao patrimonial","justificativa":"necessidade operacional homologacao c2s9","itens":[{"descricao":"Equipamento C2S9","quantidade":18,"unidade":"un","tipoObjeto":"BEM","precoEstimado":100,"centroCustoId":' + $s9cc1 + '}],"urgencia":false}')
$s9d1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s9d1b
$s9d1j = $null; try { $s9d1j = ($s9d1.body | ConvertFrom-Json) } catch {}
$s9d1id = 0; if ($s9d1j -and $null -ne $s9d1j.data) { $s9d1id = [long]$s9d1j.data.id }
$s9d1db = Get-Psql ("select status||'|'||to_char(valor_estimado,'FM999990.00')||'|'||urgencia||'|'||prioridade from sigov.compras_demanda where id=" + $s9d1id + ";")
$null = Get-Psql ("update sigov.compras_demanda set created_by=910301 where id=" + $s9d1id + ";")
$s9ap1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s9d1id + '/aprovar') -Base $ApiBase
$s9apdb = Get-Psql ("select count(*)||'|'||min(status)||'|'||case when min(numero) like 'PI-%' then 'PI' else 'NAO' end from sigov.compras_processo where tenant_id=910001 and demanda_id=" + $s9d1id + ";")
Add-Check 'S9' 'chain.demanda-aprovada-processo-unico' (($s9d1.code -eq 200) -and ($s9d1id -gt 0) -and ($s9d1db.text -eq 'SOLICITADA|1800.00|false|NORMAL') -and ($s9ap1.code -eq 200) -and ($s9apdb.text -eq ('1|ABERTO|PI'))) ("demanda 18 un x 100.00 (BEM) -> $($s9d1.code) id=$s9d1id banco SOLICITADA|1800.00; aprovar nao urgente -> $($s9ap1.code) banco processos count|status|numero: $($s9apdb.text) (mesmo fluxo BR-8.1/8.2 da S8 criando a base da etapa 2/2)")
$s9dp1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s9d1id + ',"modalidadeCodigo":"DISPENSA"}')
$s9dp2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s9d1id + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_ZZZ"}')
$s9dp3 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s9d1id + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_I"}')
$s9dp3j = $null; try { $s9dp3j = ($s9dp3.body | ConvertFrom-Json) } catch {}
$s9proc = 0; $s9lic = 0
if ($s9dp3j -and $null -ne $s9dp3j.data) { $s9proc = [long]$s9dp3j.data.processoId; $s9lic = [long]$s9dp3j.data.licitacaoId }
$s9li = Get-Psql ("insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910001," + $s9ent + "," + $s9lic + ",1,'Equipamento especial C2S9',18,'un',100.00,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s9lic + "); select count(*) from sigov.compras_licitacao_item where licitacao_id=" + $s9lic + ";")
$s9da1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s9lic + '/adjudicar') -Base $ApiBase -Body '{}'
$s9da2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s9lic + '/adjudicar') -Base $ApiBase -Body ('{"fornecedorId":' + $s9f9 + ',"observacao":"contratacao direta c2s9"}')
$s9da2j = $null; try { $s9da2j = ($s9da2.body | ConvertFrom-Json) } catch {}
$s9dadb = Get-Psql ("select to_char(a.valor_total,'FM999990.00')||'|'||(select status from sigov.compras_licitacao where id=" + $s9lic + ")||'|'||(select p.fornecedor_id::text from sigov.compras_proposta p where p.id=a.proposta_vencedora_id) from sigov.compras_adjudicacao a where tenant_id=910001 and licitacao_id=" + $s9lic + ";")
$null = Get-Psql ("update sigov.compras_adjudicacao set responsavel_id=910402 where tenant_id=910001 and licitacao_id=" + $s9lic + ";")
$s9ho = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s9lic + '/homologar') -Base $ApiBase
$s9hoj = $null; try { $s9hoj = ($s9ho.body | ConvertFrom-Json) } catch {}
$s9hoid = 0; if ($s9hoj -and $null -ne $s9hoj.data) { $s9hoid = [long]$s9hoj.data.id }
$s9ct = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body ('{"homologacaoId":' + $s9hoid + ',"dataEficacia":"2026-11-10","dataFim":"2027-11-09","assinaturaModo":"FISICA","gestor":"Gestor C2S9","fiscal":"Fiscal C2S9"}')
$s9ctj = $null; try { $s9ctj = ($s9ct.body | ConvertFrom-Json) } catch {}
$s9ctid = 0; if ($s9ctj -and $null -ne $s9ctj.data) { $s9ctid = [long]$s9ctj.data.contratoId }
$s9ctdb = Get-Psql ("select status||'|'||to_char(valor_contratado,'FM999990.00')||'|'||assinatura_modo||'|'||fornecedor_id::text from sigov.compras_contrato c where c.tenant_id=910001 and c.homologacao_id=" + $s9hoid + " limit 1;")
$s9cti = 0; $s9ctir = Get-Psql ("select id from sigov.compras_contrato_item where contrato_id=" + $s9ctid + " limit 1;"); if ($s9ctir.code -eq 0) { $s9cti = [long]$s9ctir.text }
Add-Check 'S9' 'chain.dispensa-contrato-vigente-para-fornecimento' (($s9dp1.code -eq 400) -and ($s9dp1.body -like '*DISPENSA_BASE_LEGAL_OBRIGATORIA*') -and ($s9dp2.code -eq 404) -and ($s9dp2.body -like '*DISPENSA_BASE_LEGAL_INEXISTENTE*') -and ($s9dp3.code -eq 200) -and ($s9li.text -eq '1') -and ($s9da1.code -eq 400) -and ($s9da1.body -like '*ADJUDICACAO_DIRETA_SEM_FORNECEDOR*') -and ($s9da2.code -eq 200) -and ([decimal]$s9da2j.data.valorTotal -eq 1800) -and ($s9dadb.text -like ("1800.00|ADJUDICADA|" + $s9f9)) -and ($s9ho.code -eq 200) -and ($s9ct.code -eq 200) -and ([decimal]$s9ctj.data.valorContratado -eq 1800) -and ($s9ctdb.text -like ("VIGENTE|1800.00|FISICA|" + $s9f9)) -and ($s9cti -gt 0)) ("DISPENSA sem base legal -> $($s9dp1.code) (400 nomeada), inventada -> $($s9dp2.code) (404 nomeada), ART_75_I -> $($s9dp3.code); adjudicar direta sem fornecedor -> $($s9da1.code) (400 nomeada), com f9 -> $($s9da2.code) valor 1800.00; homologar -> $($s9ho.code); contrato FISICA -> $($s9ct.code) id=$s9ctid banco: $($s9ctdb.text); contrato_item id=$s9cti (cadeia BR-8.6 pronta para a etapa 2/2)")

# --- BR-7.5a: fornecimento contra item de contrato vigente --------------------------
$s9fn1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":12,"chaveIdempotencia":"K-C2S9-F1"}')
$s9fn1j = $null; try { $s9fn1j = ($s9fn1.body | ConvertFrom-Json) } catch {}
$s9f1 = 0; if ($s9fn1j -and $null -ne $s9fn1j.data) { $s9f1 = [long]$s9fn1j.data.id }
$s9fn1db = Get-Psql ("select status||'|'||to_char(quantidade,'FM9990.000')||'|'||to_char(valor_unitario,'FM99990.00')||'|'||to_char(valor_total,'FM99990.00')||'|'||fornecedor_id::text||'|'||coalesce(chave_idempotencia,'NULL') from sigov.compras_fornecimento where id=" + $s9f1 + ";")
$s9fg = Invoke-Api -Bearer $tkS9.token -Method GET -Path ('/api/compras/integradas/fornecimentos/' + $s9f1) -Base $ApiBase
$s9fgj = $null; try { $s9fgj = ($s9fg.body | ConvertFrom-Json) } catch {}
Add-Check 'S9' 'fornecimento.criado-valor-e-fornecedor-derivados-get-visualizar' (($s9fn1.code -eq 200) -and ($s9f1 -gt 0) -and ($s9fn1db.text -like ("FORNECIDO|12.000|100.00|1200.00|" + $s9f9 + "|K-C2S9-F1")) -and ($s9fg.code -eq 200) -and ([decimal]$s9fgj.data.valorTotal -eq 1200)) ("POST /fornecimentos (12 un do item do contrato) -> $($s9fn1.code) id=$s9f1; banco status|qtd|unit|total|fornecedor|chave: $($s9fn1db.text) (BR-7.5: valor DERIVADO do unitario do item 12x100=1200.00 e fornecedor DERIVADO do contrato — nunca editaveis livres); GET /fornecimentos/{id} -> $($s9fg.code) valorTotal=$($s9fgj.data.valorTotal) (chave compras.fornecimento.visualizar)")
$s9fnA = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":7}')
$s9fn2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":4,"chaveIdempotencia":"K-C2S9-F2"}')
$s9fn2j = $null; try { $s9fn2j = ($s9fn2.body | ConvertFrom-Json) } catch {}
$s9f2 = 0; if ($s9fn2j -and $null -ne $s9fn2j.data) { $s9f2 = [long]$s9fn2j.data.id }
$s9fnB = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":3}')
$s9fn3 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":2,"chaveIdempotencia":"K-C2S9-F3"}')
$s9fn3j = $null; try { $s9fn3j = ($s9fn3.body | ConvertFrom-Json) } catch {}
$s9f3 = 0; if ($s9fn3j -and $null -ne $s9fn3j.data) { $s9f3 = [long]$s9fn3j.data.id }
$s9fnC = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":1}')
Add-Check 'S9' 'fornecimento.disponivel-do-item-conferido-na-criacao' (($s9fnA.code -eq 409) -and ($s9fnA.body -like '*FONTECIMENTO_EXCEDE_ITEM_CONTRATO*') -and ($s9fn2.code -eq 200) -and ($s9f2 -gt 0) -and ($s9fnB.code -eq 409) -and ($s9fnB.body -like '*FONTECIMENTO_EXCEDE_ITEM_CONTRATO*') -and ($s9fn3.code -eq 200) -and ($s9f3 -gt 0) -and ($s9fnC.code -eq 409) -and ($s9fnC.body -like '*FONTECIMENTO_EXCEDE_ITEM_CONTRATO*')) ("item max 18: fornecimento 7 (disponivel 6) -> $($s9fnA.code) (409 FONTECIMENTO_EXCEDE_ITEM_CONTRATO nomeada); 4 -> $($s9fn2.code) id=$s9f2; 3 (disponivel 2) -> $($s9fnB.code) 409 nomeada; 2 -> $($s9fn3.code) id=$s9f3; 1 (disponivel 0) -> $($s9fnC.code) 409 nomeada (BR-7.5: quantidade/valor disponiveis conferidos na criacao, sem ajuste silencioso)")
$s9fnD = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":12,"chaveIdempotencia":"K-C2S9-F1"}')
$s9fnDj = $null; try { $s9fnDj = ($s9fnD.body | ConvertFrom-Json) } catch {}
$s9fnDid = 0; if ($s9fnDj -and $null -ne $s9fnDj.data) { $s9fnDid = [long]$s9fnDj.data.id }
$s9fnE = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body ('{"contratoId":' + $s9ctid + ',"contratoItemId":' + $s9cti + ',"quantidade":11,"chaveIdempotencia":"K-C2S9-F1"}')
Add-Check 'S9' 'fornecimento.idempotencia-por-chave-conflito-nomeado' (($s9fnD.code -eq 200) -and ($s9fnDid -eq $s9f1) -and ($s9fnE.code -eq 409) -and ($s9fnE.body -like '*FORNECIMENTO_CHAVE_IDEMPOTENCIA_CONFLITO*')) ("reprocesso com a MESMA chave K-C2S9-F1 e mesmo conteudo (12) -> $($s9fnD.code) retornando o fornecimento existente id=$s9fnDid (sem duplicar); mesma chave com conteudo divergente (11) -> $($s9fnE.code) (409 FORNECIMENTO_CHAVE_IDEMPOTENCIA_CONFLITO nomeada)")

# --- BR-7.5b: recebimento parcial, rejeicao com motivo/prazo e desfecho -------------
$s9rc0 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":0,"quantidadeRejeitada":1,"dataRecebimento":"2026-11-10"}'
$s9rcX = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f2 + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":5,"quantidadeRejeitada":0,"dataRecebimento":"2026-11-11"}'
$s9rc1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":6,"quantidadeRejeitada":2,"dataRecebimento":"2026-11-12","motivoRejeicao":"qualidade abaixo do padrao c2s9"}'
$s9rc1j = $null; try { $s9rc1j = ($s9rc1.body | ConvertFrom-Json) } catch {}
$s9r1 = 0; if ($s9rc1j -and $null -ne $s9rc1j.data) { $s9r1 = [long]$s9rc1j.data.id }
$s9rc1db = Get-Psql ("select r.status||'|'||to_char(r.quantidade_aprovada,'FM9990.000')||'|'||to_char(r.quantidade_rejeitada,'FM9990.000')||'|'||to_char(r.data_prazo_substituicao,'YYYY-MM-DD')||'|'||l.status||'|'||to_char(i.quantidade,'FM9990.000')||'|'||i.tipo_material||'|'||to_char(i.valor_unitario,'FM99990.00') from sigov.compras_fornecimento_recebimento r join sigov.compras_recebimento l on l.id=r.legacy_recebimento_id join sigov.compras_recebimento_item i on i.recebimento_id=l.id where r.id=" + $s9r1 + ";")
Add-Check 'S9' 'recebimento.parcial-motivo-prazo-parametrico-espelho-legado' (($s9rc0.code -eq 400) -and ($s9rc0.body -like '*RECEBIMENTO_REJEICAO_SEM_MOTIVO*') -and ($s9rcX.code -eq 409) -and ($s9rcX.body -like '*RECEBIMENTO_ACIMA_FORNECIDO*') -and ($s9rc1.code -eq 200) -and ($s9rc1db.text -eq 'PARCIAL|6.000|2.000|2026-11-19|PARCIAL|6.000|CONSUMO|100.00')) ("recebimento com rejeicao SEM motivo -> $($s9rc0.code) (400 RECEBIMENTO_REJEICAO_SEM_MOTIVO nomeada); aprovado 5 > fornecido 4 (f2) -> $($s9rcX.code) (409 RECEBIMENTO_ACIMA_FORNECIDO nomeada); f1 aprov 6 + rej 2 (motivo) -> $($s9rc1.code) id=$s9r1; banco rec|espelho-legado: status|aprov|reje|prazo-substituicao|status-legado|qtd-item-legado|tipo|unitario = $($s9rc1db.text) (BR-7.5: prazo = 2026-11-12 + RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS=5 dias uteis do banco = 2026-11-19; espelho em compras_recebimento/_item mantem a execucao contratual S8 consistente)")
$s9rc2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":4,"quantidadeRejeitada":0,"dataRecebimento":"2026-11-13"}'
$s9rc2db = Get-Psql ("select status from sigov.compras_fornecimento where id=" + $s9f1 + ";")
$s9ob0 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s9f1 + ',"referenciaIdempotencia":"OB-C2S9-F1"}')
Add-Check 'S9' 'recebimento.total-recebido-mas-sem-desfecho-nao-fecha' (($s9rc2.code -eq 200) -and ($s9rc2db.text -eq 'PARCIALMENTE_RECEBIDO') -and ($s9ob0.code -eq 409) -and ($s9ob0.body -like '*OBRIGACAO_FORNECIMENTO_INCOMPLETO*')) ("2a parcela aprov 4 -> $($s9rc2.code) (recebido total 12 = fornecido 12); banco status do fornecimento: $($s9rc2db.text) — continua ABERTO porque a rejeicao de 2 un ainda nao tem desfecho (BR-7.5: rejeicao sem desfecho nao fecha o fornecimento); gerar obrigacao antecipada -> $($s9ob0.code) (409 OBRIGACAO_FORNECIMENTO_INCOMPLETO nomeada: so nasce de fornecimento CONCLUIDO)")
$s9df1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/recebimentos/' + $s9r1 + '/desfecho') -Base $ApiBase -Body '{"desfecho":"SUBSTITUIDO","justificativa":"fornecedor subira o lote ate o prazo parametrico"}'
$s9dfdb = Get-Psql ("select f.status||'|'||coalesce(r.desfecho,'NULL') from sigov.compras_fornecimento f, sigov.compras_fornecimento_recebimento r where f.id=" + $s9f1 + " and r.id=" + $s9r1 + ";")
$s9df2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/recebimentos/' + $s9r1 + '/desfecho') -Base $ApiBase -Body '{"desfecho":"ACATADO","justificativa":"tentativa de 2o desfecho"}'
Add-Check 'S9' 'desfecho.registrado-fornecimento-concluido-unico' (($s9df1.code -eq 200) -and ($s9dfdb.text -eq 'CONCLUIDO|SUBSTITUIDO') -and ($s9df2.code -eq 409) -and ($s9df2.body -like '*REJEICAO_DESFECO_JA_REGISTRADO*')) ("desfecho SUBSTITUIDO na rejeicao (motivo + justificativa) -> $($s9df1.code); banco fornecimento|desfecho: $($s9dfdb.text) (BR-7.5: com tudo recebido E todas as rejeicoes com desfecho -> CONCLUIDO); 2o desfecho -> $($s9df2.code) (409 REJEICAO_DESFECO_JA_REGISTRADO nomeada: desfecho e unico)")
$s9rc3 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f2 + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":4,"quantidadeRejeitada":0,"dataRecebimento":"2026-11-13"}'
$s9rc3db = Get-Psql ("select status from sigov.compras_fornecimento where id=" + $s9f2 + ";")
$s9rc4 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f3 + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":2,"quantidadeRejeitada":0,"dataRecebimento":"2026-11-14"}'
$s9rc4db = Get-Psql ("select status from sigov.compras_fornecimento where id=" + $s9f3 + ";")
Add-Check 'S9' 'recebimento.total-sem-rejeicao-conclui-automaticamente' (($s9rc3.code -eq 200) -and ($s9rc3db.text -eq 'CONCLUIDO') -and ($s9rc4.code -eq 200) -and ($s9rc4db.text -eq 'CONCLUIDO')) ("f2 recebido integralmente sem rejeicao -> $($s9rc3.code) banco status: $($s9rc3db.text); f3 igualmente -> $($s9rc4.code) banco status: $($s9rc4db.text) (BR-7.5: recebido=fornecido sem pendencias fecha automaticamente)")

# --- BR-7.5c: fatura/NF-e vinculada com conferencia ----------------------------------
$s9fa1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S9-001","natureza":"NFE","cnpjFornecedor":"11.111.111/0001-11","valor":1200,"dataEmissao":"2026-11-15"}'
$s9fa2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S9-001","natureza":"NFE","cnpjFornecedor":"99.999.000/0004-04","valor":1199,"dataEmissao":"2026-11-15"}'
$s9fa0 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S9-001","natureza":"XML","cnpjFornecedor":"99.999.000/0004-04","valor":1200,"dataEmissao":"2026-11-15"}'
$s9fa3 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S9-001","natureza":"NFE","serie":"1","cnpjFornecedor":"99.999.000/0004-04","valor":1200,"dataEmissao":"2026-11-15"}'
$s9fa3j = $null; try { $s9fa3j = ($s9fa3.body | ConvertFrom-Json) } catch {}
$s9faid = 0; if ($s9fa3j -and $null -ne $s9fa3j.data) { $s9faid = [long]$s9fa3j.data.id }
$s9fa4 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S9-001","natureza":"NFE","serie":"1","cnpjFornecedor":"99.999.000/0004-04","valor":1200,"dataEmissao":"2026-11-15"}'
$s9fa4j = $null; try { $s9fa4j = ($s9fa4.body | ConvertFrom-Json) } catch {}
$s9fa4id = 0; if ($s9fa4j -and $null -ne $s9fa4j.data) { $s9fa4id = [long]$s9fa4j.data.id }
$s9fa5 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s9f1 + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S9-002","natureza":"NFE","serie":"1","cnpjFornecedor":"99.999.000/0004-04","valor":1200,"dataEmissao":"2026-11-16"}'
Add-Check 'S9' 'fatura.conferencia-cnpj-valor-e-idempotencia-unica' (($s9fa1.code -eq 400) -and ($s9fa1.body -like '*FATURA_FORNECEDOR_DISCREPANTE*') -and ($s9fa2.code -eq 400) -and ($s9fa2.body -like '*FATURA_VALOR_DIVERGENTE_DO_PEDIDO*') -and ($s9fa0.code -eq 400) -and ($s9fa0.body -like '*FATURA_NATUREZA_INVALIDA*') -and ($s9fa3.code -eq 200) -and ($s9faid -gt 0) -and ($s9fa4.code -eq 200) -and ($s9fa4id -eq $s9faid) -and ($s9fa5.code -eq 409) -and ($s9fa5.body -like '*FATURA_JA_VINCULADA_CONFLITO*')) ("CNPJ divergente do contratado -> $($s9fa1.code) (400 FATURA_FORNECEDOR_DISCREPANTE nomeada); valor 1199 x pedido 1200 -> $($s9fa2.code) (400 FATURA_VALOR_DIVERGENTE_DO_PEDIDO nomeada); natureza XML -> $($s9fa0.code) (400 FATURA_NATUREZA_INVALIDA nomeada: so NFE/NFS_E); NFE correta -> $($s9fa3.code) id=$s9faid; replay identico -> $($s9fa4.code) mesmo id (idempotente); outro numero -> $($s9fa5.code) (409 FATURA_JA_VINCULADA_CONFLITO nomeada: 1 fatura por fornecimento — BR-7.5)")

# --- BR-7.7: obrigacao financeira unica, idempotente, liquidacao e estorno ----------
$s9ob1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s9f1 + ',"referenciaIdempotencia":"OB-C2S9-F1"}')
$s9ob1j = $null; try { $s9ob1j = ($s9ob1.body | ConvertFrom-Json) } catch {}
$s9o1 = 0; if ($s9ob1j -and $null -ne $s9ob1j.data) { $s9o1 = [long]$s9ob1j.data.id }
$s9ob1db = Get-Psql ("select to_char(valor_liquidado,'FM99990.00')||'|'||status||'|'||case when data_liquidacao is null then 'NULL' else 'SET' end from sigov.compras_obrigacao where id=" + $s9o1 + ";")
$s9ob2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s9f1 + ',"referenciaIdempotencia":"OB-C2S9-F1"}')
$s9ob2j = $null; try { $s9ob2j = ($s9ob2.body | ConvertFrom-Json) } catch {}
$s9ob2id = 0; if ($s9ob2j -and $null -ne $s9ob2j.data) { $s9ob2id = [long]$s9ob2j.data.id }
$s9ob3 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s9f1 + ',"referenciaIdempotencia":"OB-C2S9-OUTRA"}')
Add-Check 'S9' 'obrigacao.unica-valor-so-aprovado-idempotente' (($s9ob1.code -eq 200) -and ($s9o1 -gt 0) -and ($s9ob1db.text -eq '1000.00|REGISTRADA|NULL') -and ($s9ob2.code -eq 200) -and ($s9ob2id -eq $s9o1) -and ($s9ob3.code -eq 409) -and ($s9ob3.body -like '*OBRIGACAO_DUPLICADA_REFERENCIA*')) ("gerar obrigacao do f1 CONCLUIDO -> $($s9ob1.code) id=$s9o1; banco valor|status|data_liq: $($s9ob1db.text) (BR-7.7: valor = LIQUIDADO = so o aprovado 10x100=1000.00, as 2 rejeitadas fora); replay mesma referencia -> $($s9ob2.code) mesmo id (reprocesso nao duplica); outra referencia para o mesmo fornecimento -> $($s9ob3.code) (409 OBRIGACAO_DUPLICADA_REFERENCIA nomeada: 1 fornecimento aprovado = 1 obrigacao)")
$s9ob4 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s9o1 + '/liquidar') -Base $ApiBase
$s9ob4db = Get-Psql ("select status||'|'||case when data_liquidacao is null then 'NULL' else 'SET' end from sigov.compras_obrigacao where id=" + $s9o1 + ";")
$s9ob5 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s9o1 + '/liquidar') -Base $ApiBase
Add-Check 'S9' 'obrigacao.liquida-com-fatura-conferida-terminal' (($s9ob4.code -eq 200) -and ($s9ob4db.text -eq 'LIQUIDADA|SET') -and ($s9ob5.code -eq 409) -and ($s9ob5.body -like '*OBRIGACAO_JA_LIQUIDADA*')) ("liquidar (fatura NFE vinculada e conferida no f1) -> $($s9ob4.code); banco status|data_liq: $($s9ob4db.text); nova liquidacao -> $($s9ob5.code) (409 OBRIGACAO_JA_LIQUIDADA nomeada: estado terminal)")
$s9ob6 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s9f2 + ',"referenciaIdempotencia":"OB-C2S9-F2"}')
$s9ob6j = $null; try { $s9ob6j = ($s9ob6.body | ConvertFrom-Json) } catch {}
$s9o2 = 0; if ($s9ob6j -and $null -ne $s9ob6j.data) { $s9o2 = [long]$s9ob6j.data.id }
$s9ob6db = Get-Psql ("select to_char(valor_liquidado,'FM99990.00')||'|'||status from sigov.compras_obrigacao where id=" + $s9o2 + ";")
$s9ob7 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s9o2 + '/liquidar') -Base $ApiBase
$s9ob8 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s9o2 + '/estornar') -Base $ApiBase -Body '{"justificativa":"compensacao por duplicidade c2s9"}'
$s9ob8db = Get-Psql ("select status||'|'||case when coalesce(justificativa_estorno,'NAO') like '%duplicidade%' then 'JUS' else 'NAO' end||'|'||coalesce(valor_liquidado::text,'NULL') from sigov.compras_obrigacao where id=" + $s9o2 + ";")
$s9ob9 = Invoke-Api -Bearer $tkS9.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s9o2 + '/liquidar') -Base $ApiBase
Add-Check 'S9' 'obrigacao.sem-fatura-nao-liquida-estorno-compensacao' (($s9ob6.code -eq 200) -and ($s9o2 -gt 0) -and ($s9ob6db.text -eq '400.00|REGISTRADA') -and ($s9ob7.code -eq 409) -and ($s9ob7.body -like '*OBRIGACAO_LIQUIDACAO_SEM_FATURA*') -and ($s9ob8.code -eq 200) -and ($s9ob8db.text -eq 'ESTORNADA|JUS|400.00') -and ($s9ob9.code -eq 409) -and ($s9ob9.body -like '*OBRIGACAO_JA_LIQUIDADA*')) ("f2 gerou obrigacao 400.00 REGISTRADA; liquidar SEM fatura vinculada -> $($s9ob7.code) (409 OBRIGACAO_LIQUIDACAO_SEM_FATURA nomeada — BR-7.5: fornecimento sem NF-e vinculada nao liquida); estorno com justificativa -> $($s9ob8.code); banco status|justificativa|valor-intacto: $($s9ob8db.text) (BR-7.7: estorno por COMPENSACAO — append-only, sem delete fisico); liquidar apos estorno -> $($s9ob9.code) 409 nomeada")

# --- BR-7.6: destinacao em estoque/patrimonio (movimento referenciado unico) ---------
$s9dd1 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"ESTOQUE","quantidade":2,"referencia":"D-C2S9-01","localId":' + $s9loc1 + ',"materialId":' + $s9mat + '}')
$s9dd1j = $null; try { $s9dd1j = ($s9dd1.body | ConvertFrom-Json) } catch {}
$s9d1id = 0; if ($s9dd1j -and $null -ne $s9dd1j.data) { $s9d1id = [long]$s9dd1j.data.id }
$s9dd1db = Get-Psql ("select count(*)::text||'|'||min(motivo)||'|'||min(to_char(saldo_antes,'FM9990.000'))||'|'||min(to_char(saldo_depois,'FM9990.000'))||'|'||min(documento_origem) from sigov.almoxarifado_movimentacao where tenant_id=910001 and almoxarifado_id=" + $s9loc1 + " and material_id=" + $s9mat + ";")
$s9dd2 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"ESTOQUE","quantidade":2,"referencia":"D-C2S9-01","localId":' + $s9loc1 + ',"materialId":' + $s9mat + '}')
$s9dd2db = Get-Psql ("select count(*)::text from sigov.almoxarifado_movimentacao where tenant_id=910001 and almoxarifado_id=" + $s9loc1 + " and material_id=" + $s9mat + ";")
$s9dd3 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"ESTOQUE","quantidade":5,"referencia":"D-C2S9-01","localId":' + $s9loc1 + ',"materialId":' + $s9mat + '}')
Add-Check 'S9' 'destino.estoque-movimento-unico-idempotente-por-referencia' (($s9dd1.code -eq 200) -and ($s9d1id -gt 0) -and ($s9dd1db.text -eq ('1|FORNECIMENTO|0.000|2.000|FORN-' + $s9f1 + '-REC-' + $s9r1)) -and ($s9dd2.code -eq 200) -and ($s9dd2db.text -eq '1') -and ($s9dd3.code -eq 409) -and ($s9dd3.body -like '*DESTINO_REFERENCIA_CONFLITO*')) ("destinar 2 un de r1 (aprovado 6) ao estoque local ativo -> $($s9dd1.code) id=$s9d1id; banco movimento count|motivo|saldo-antes|saldo-depois|documento_origem: $($s9dd1db.text) (BR-7.6: cada destino = 1 movimento referenciado; chave almoxarifado.recebimento.confirmar); replay MESMA referencia/conteudo -> $($s9dd2.code) e o movimento permanece 1 (idempotente, nada duplica); mesma referencia conteudo divergente (5) -> $($s9dd3.code) (409 DESTINO_REFERENCIA_CONFLITO nomeada)")
$s9dd4 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"PATRIMONIO","quantidade":2,"referencia":"D-C2S9-02","tipoBem":"EQUIPAMENTO","localizacao":"Salao C2S9"}')
$s9dd4j = $null; try { $s9dd4j = ($s9dd4.body | ConvertFrom-Json) } catch {}
$s9d2id = 0; if ($s9dd4j -and $null -ne $s9dd4j.data) { $s9d2id = [long]$s9dd4j.data.id }
$s9dd4db = Get-Psql ("select codigo_tombo||'|'||to_char(valor_aquisicao,'FM99990.00')||'|'||situacao||'|'||contrato_id::text||'|'||coalesce(nota_fiscal,'NULL')||'|'||to_char(data_aquisicao,'YYYY-MM-DD')||'|'||coalesce(localizacao,'NULL') from sigov.patrimonio_bem where tenant_id=910001 and codigo_tombo like 'PAT-FOR-%' limit 1;")
Add-Check 'S9' 'destino.patrimonio-ficha-gerada-no-ato' (($s9dd4.code -eq 200) -and ($s9d2id -gt 0) -and ($s9dd4db.text -like ("PAT-FOR-*|200.00|ATIVO|" + $s9ctid + "|NF-C2S9-001|2026-11-12|Salao C2S9"))) ("destinar 2 un de r1 ao patrimonio (EQUIPAMENTO) -> $($s9dd4.code) id=$s9d2id; banco ficha patrimonio_bem codigo_tombo|valor-aquisicao|situacao|contrato|nota-fiscal|data-aquisicao|localizacao: $($s9dd4db.text) (BR-7.6: patrimonio SEMPRE gera a ficha no ato — tombamento PAT-FOR-<id>, valor registrado 2x100=200.00, situacao ATIVA, vinculo ao contrato e a NF-e; chave patrimonio.bem.criar)")
$s9dd5 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"ESTOQUE","quantidade":1,"referencia":"D-C2S9-03","localId":' + $s9loc2 + ',"materialId":' + $s9mat + '}')
$s9dd6 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"ESTOQUE","quantidade":1,"referencia":"D-C2S9-04","localId":999999999,"materialId":' + $s9mat + '}')
Add-Check 'S9' 'destino.local-inexistente-inativo-bloqueia' (($s9dd5.code -eq 409) -and ($s9dd5.body -like '*DESTINO_LOCAL_INATIVO*') -and ($s9dd6.code -eq 404) -and ($s9dd6.body -like '*DESTINO_LOCAL_INEXISTENTE*')) ("destino ao local INATIVO -> $($s9dd5.code) (409 DESTINO_LOCAL_INATIVO nomeada); destino ao local INEXISTENTE 999999999 -> $($s9dd6.code) (404 DESTINO_LOCAL_INEXISTENTE nomeada — BR-7.6: destino a local inexistente ou inativo bloqueia)")
$s9dd7 = Invoke-Api -Bearer $tkS9.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s9r1 + ',"alvoTipo":"ESTOQUE","quantidade":3,"referencia":"D-C2S9-05","localId":' + $s9loc1 + ',"materialId":' + $s9mat + '}')
Add-Check 'S9' 'destino.excede-quantidade-aprovada-409' (($s9dd7.code -eq 409) -and ($s9dd7.body -like '*DESTINO_EXCEDE_QTD_APROVADA*')) ("destinar mais 3 un de r1 (ja destinadas 2+2=4 das 6 aprovadas; 4+3=7 > 6) -> $($s9dd7.code) (409 DESTINO_EXCEDE_QTD_APROVADA nomeada — BR-7.6: destino total <= quantidade aprovada do recebimento)")

# --- execucao contratual (S8) continua consistente com os espelhos do S9 ------------
$s9ex = Invoke-Api -Bearer $tkS9.token -Method GET -Path ('/api/compras/integradas/contratos/' + $s9ctid + '/execucao') -Base $ApiBase
$s9exj = $null; try { $s9exj = ($s9ex.body | ConvertFrom-Json) } catch {}
Add-Check 'S9' 'execucao.consistente-com-recebimentos-integrados' (($s9ex.code -eq 200) -and ([decimal]$s9exj.data.totalFornecido -eq 1600) -and ([decimal]$s9exj.data.pctExecutado -eq 88.89) -and ($s9exj.data.bloqueiaNovoFornecimento -eq $false)) ("GET execucao do contrato C2S9 -> $($s9ex.code) totalFornecido=$($s9exj.data.totalFornecido) pct=$($s9exj.data.pctExecutado)% bloqueia=$($s9exj.data.bloqueiaNovoFornecimento) (BR-8.5 x BR-7.5: o endpoint S8 agora enxerga os espelhos legados dos recebimentos integrados — 16 un aprovadas x 100 = 1600.00 de 1800.00; as 2 rejeitadas nao contam como executadas; abaixo do teto 100% parametrico, segue liberado)")

# --- limpeza do bloco S9 (auto-limpeza; regra 9) ---------------------------------------
$s9cl = Get-Psql @"
create temp table if not exists s9_alvo_d (id bigint); truncate table s9_alvo_d;
insert into s9_alvo_d select id from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S9-%';
create temp table if not exists s9_alvo_p (id bigint); truncate table s9_alvo_p;
insert into s9_alvo_p select id from sigov.compras_processo where tenant_id=910001 and demanda_id in (select id from s9_alvo_d);
create temp table if not exists s9_alvo_l (id bigint); truncate table s9_alvo_l;
insert into s9_alvo_l select id from sigov.compras_licitacao where tenant_id=910001 and processo_id in (select id from s9_alvo_p);
create temp table if not exists s9_alvo_c (id bigint); truncate table s9_alvo_c;
insert into s9_alvo_c select id from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s9_alvo_l));
create temp table if not exists s9_alvo_f (id bigint); truncate table s9_alvo_f;
insert into s9_alvo_f select id from sigov.compras_fornecimento where tenant_id=910001 and contrato_id in (select id from s9_alvo_c);
delete from sigov.compras_destinacao_integrada where tenant_id=910001 and fornecimento_recebimento_id in (select id from sigov.compras_fornecimento_recebimento where fornecimento_id in (select id from s9_alvo_f));
delete from sigov.patrimonio_bem where tenant_id=910001 and codigo_tombo like 'PAT-FOR-%';
delete from sigov.almoxarifado_movimentacao where tenant_id=910001 and documento_origem like 'FORN-%';
delete from sigov.compras_fatura_integrada where tenant_id=910001 and fornecimento_id in (select id from s9_alvo_f);
delete from sigov.compras_obrigacao where tenant_id=910001 and fornecimento_id in (select id from s9_alvo_f);
delete from sigov.compras_recebimento_item where recebimento_id in (select id from sigov.compras_recebimento where tenant_id=910001 and fornecimento_id in (select id from s9_alvo_f));
delete from sigov.compras_recebimento where tenant_id=910001 and fornecimento_id in (select id from s9_alvo_f);
delete from sigov.compras_fornecimento_recebimento where fornecimento_id in (select id from s9_alvo_f);
delete from sigov.compras_fornecimento where id in (select id from s9_alvo_f);
delete from sigov.compras_contrato_item where contrato_id in (select id from s9_alvo_c);
delete from sigov.compras_contrato_historico where contrato_id in (select id from s9_alvo_c);
delete from sigov.compras_contrato where id in (select id from s9_alvo_c);
delete from sigov.compras_homologacao where licitacao_id in (select id from s9_alvo_l);
delete from sigov.compras_adjudicacao_item where adjudicacao_id in (select id from sigov.compras_adjudicacao where licitacao_id in (select id from s9_alvo_l));
delete from sigov.compras_adjudicacao where licitacao_id in (select id from s9_alvo_l);
delete from sigov.compras_proposta where licitacao_id in (select id from s9_alvo_l);
delete from sigov.compras_habilitacao where licitacao_id in (select id from s9_alvo_l);
delete from sigov.compras_lance where licitacao_id in (select id from s9_alvo_l);
delete from sigov.compras_licitacao_item where licitacao_id in (select id from s9_alvo_l);
delete from sigov.compras_licitacao where id in (select id from s9_alvo_l);
delete from sigov.compras_processo_item where tenant_id=910001 and processo_id in (select id from s9_alvo_p);
delete from sigov.compras_processo where id in (select id from s9_alvo_p);
delete from sigov.compras_demanda_item where demanda_id in (select id from s9_alvo_d);
delete from sigov.compras_demanda where id in (select id from s9_alvo_d);
delete from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S9 %';
delete from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S9-MAT';
delete from sigov.almoxarifado_local where tenant_id=910001 and codigo like 'C2S9-LOC%';
delete from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S9-%';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.perfil_permissao where correlation_id='cf58c2ab-1c20-4a90-88f8-0c2000000001';
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s9.admin');
delete from sigov.usuario where tenant_id=910001 and login='c2s9.admin';
delete from sigov.entidade where tenant_id=910001 and codigo_externo='C2S9-ENT';
select
 (select count(*) from sigov.almoxarifado_movimentacao where tenant_id=910001 and documento_origem like 'FORN-%')
 + (select count(*) from sigov.patrimonio_bem where tenant_id=910001 and codigo_tombo like 'PAT-FOR-%')
 + (select count(*) from sigov.compras_recebimento where tenant_id=910001 and documento like 'REC-FOR-%')
 + (select count(*) from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S9-MAT')
 + (select count(*) from sigov.almoxarifado_local where tenant_id=910001 and codigo like 'C2S9-LOC%')
 + (select count(*) from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S9 %')
 + (select count(*) from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S9-%')
 + (select count(*) from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S9-%')
 + (select count(*) from sigov.compras_licitacao where tenant_id=910001 and objeto like 'C2S9-%')
 + (select count(*) from sigov.compras_contrato where tenant_id=910001 and objeto like 'C2S9-%')
 + (select count(*) from sigov.perfil_permissao where correlation_id='cf58c2ab-1c20-4a90-88f8-0c2000000001')
 + (select count(*) from sigov.usuario where tenant_id=910001 and login='c2s9.admin')
 + (select count(*) from sigov.entidade where tenant_id=910001 and codigo_externo='C2S9-ENT');
"@
Add-Check 'S9' 'fixture.removido-auto-limpeza' (($s9cl.code -eq 0) -and ($s9cl.text -eq '0')) ("cascade delete S9 (destinos, bens PAT-FOR-%, movimentos FORN-%, faturas, obrigacoes, espelho legado REC-FOR-%+items, recebimentos integrados, fornecimentos, contratos+items+historico, homologacoes, adjudicacoes+items, propostas, licitacao_items, licitacoes, processos+items, demandas+items, fornecedores, materiais, locais, centros de custo, sessoes/auditorias do usuario, grants sentinel cf58c2ab, usuario c2s9.admin+grupo, entidade C2S9-ENT) tenant 910001 -> restantes=" + $s9cl.text)

# ============================================================================
# BLOCO S10 - Vinculos tipados Juridico x Compras + Convenios/Obras (etapa 3/3)
# (BR-7.8 + BR-10.x do contrato docs/CONTRATOS-FRENTE-ADMINISTRATIVA-C2S7-MULTIESFERA.md)
# Invariantes validadas ao vivo: consulta juridica tipada vinculada ao processo de
# contratacao (ledger compras_juridico_consulta_vinculo, idempotente por chave);
# parecer EMITIDO unico por consulta; CONDICIONADO exige condicoes e BLOQUEIA a
# homologacao (gate PARECER_CONDICAO_ABERTA) ate satisfacao registrada de todas;
# filial/recurso so abre contra RESOLVIDA (original preservada, 1 por pai);
# contencioso ativo (origem CONTRATO) marca o contrato juridico (derivado no
# endpoint execucao), BLOQUEIA prorrogacao ate desfecho append-only e quita o
# marcador pelo ledger; contrapartida do convenio com tipos/teto do instrumento
# e status derivado (BR-7.8); aplicar recurso do convenio exige contrapartida
# REALIZADA >= necessaria; prestacao de contas bloqueada abaixo do minimo
# parametrico do banco (CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT=70) e FINAL exige
# saldo zerado; encerramento do instrumento bloqueado com contrato vinculado
# ATIVO e exige justificativa quando ha saldo; medicao de obra nao homologa
# acima do medido fisicamente/contratado (BR-10.4) e grava aprovacao FISCAL_OBRA.
# Multi-esfera (regras 21-24): entidade fixture esfera 'federal' + usuario
# c2s10.admin com a MESMA cadeia usuario_grupo/grupo/perfil; grants sentinel
# cf58c2ab-...010 cobrindo as chaves canonicas reutilizadas (compras) + as 6 novas
# dos modulos juridico/convenios + obras.medicao.registrar (banco = autoridade).
# Marcadores C2S10-%; o bloco se auto-limpa ao final (regra 9).
# ============================================================================
$s10fix = Get-Psql @"
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910001,'C2S10 Entidade Homologacao','00.000.000/0001-93','C2S10-ENT','federal','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910001 and codigo_externo='C2S10-ENT');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S10 Administrador','c2s10.admin','c2s10.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s10.admin');
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,910401,910001,true,now(),910301
from sigov.usuario u
where u.tenant_id=910001 and u.login='c2s10.admin'
and not exists(select 1 from sigov.usuario_grupo g where g.usuario_id=u.id and g.grupo_acesso_id=910401 and g.ativo);
-- Grant das 17 chaves da frente (10 compras reutilizadas do catalogo C2S7 + 4 juridico +
-- 2 convenios + 1 obras), marcadas com correlation_id sentinel para limpeza cirurgica.
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000010'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.id=(select gp.perfil_acesso_id from sigov.grupo_perfil gp where gp.grupo_acesso_id=910401 limit 1)
  and p.ativo and not p.is_deleted
  and ((p.modulo='compras' and p.chave in ('compras.demanda.criar','compras.demanda.aprovar','compras.licitacao.abrir','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.execucao.fiscalizar','compras.contrapartida.registrar','compras.auditoria.visualizar'))
    or (p.modulo='juridico' and p.chave in ('juridico.consulta.criar','juridico.parecer.emitir','juridico.contencioso.abrir','juridico.contencioso.desfechar'))
    or (p.modulo='convenios' and p.chave in ('convenios.instrumento.encerrar','convenios.prestacao.registrar'))
    or (p.modulo='obras' and p.chave='obras.medicao.registrar'))
on conflict (perfil_acesso_id,permissao_id) do nothing;
do `$`$
begin
  if (select count(1) from sigov.permissao p where p.ativo and not p.is_deleted
      and ((p.modulo='compras' and p.chave in ('compras.demanda.criar','compras.demanda.aprovar','compras.licitacao.abrir','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.execucao.fiscalizar','compras.contrapartida.registrar','compras.auditoria.visualizar'))
        or (p.modulo='juridico' and p.chave in ('juridico.consulta.criar','juridico.parecer.emitir','juridico.contencioso.abrir','juridico.contencioso.desfechar'))
        or (p.modulo='convenios' and p.chave in ('convenios.instrumento.encerrar','convenios.prestacao.registrar'))
        or (p.modulo='obras' and p.chave='obras.medicao.registrar'))) <> 17 then
    raise exception 'C2S10-FIXTURE: catalogo de permissoes incompleto (esperado 17 chaves ativas no banco)';
  end if;
  if (select coalesce(valor_padrao::text,'') from sigov.parametro_modulo where modulo='CONVENIOS' and codigo='CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT' and ativo and not is_deleted limit 1) <> '70' then
    raise exception 'C2S10-FIXTURE: parametro CONVENIOS.CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT ausente ou != 70 (migration 20261013160000)';
  end if;
end
`$`$;
insert into sigov.financeiro_centro_custo(tenant_id,codigo,nome,descricao,ativo,created_at,updated_at)
select 910001,'C2S10-CC1','CC C2S10 Ativo','centro de custo ficticio ativo (homologacao)',true,now(),now()
where not exists(select 1 from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S10-CC1');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910001,e.id,'Forneadora C2S10 Delta','JURIDICA','99.999.000/0005-05','ATIVO',null,now(),now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910001 and f.documento='99.999.000/0005-05');
insert into sigov.convenio_orgao_concedente(tenant_id,entity_id,nome,esfera,status,created_at,updated_at)
select 910001,e.id,'Orgao Concedente Federal C2S10','FEDERAL','ATIVO',now(),now()
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT'
and not exists(select 1 from sigov.convenio_orgao_concedente o where o.tenant_id=910001 and o.nome='Orgao Concedente Federal C2S10');
insert into sigov.convenio_instrumento(tenant_id,entity_id,numero_instrumento,tipo,orgao_concedente_id,objeto,valor_global,valor_repasse,valor_contrapartida,data_assinatura,vigencia_inicio,vigencia_fim,status,responsavel_nome,observacao,created_at,updated_at)
select 910001,e.id,'CVN-C2S10-01','CONVENIO',o.id,'Programa federal de equipamentos C2S10',4000,3200,800,date '2026-05-10',date '2026-06-01',date '2027-05-31','ATIVO','Responsavel C2S10','fixture homologacao C2S10',now(),now()
from sigov.entidade e cross join sigov.convenio_orgao_concedente o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.nome='Orgao Concedente Federal C2S10'
and not exists(select 1 from sigov.convenio_instrumento i where i.tenant_id=910001 and i.numero_instrumento='CVN-C2S10-01');
insert into sigov.convenio_instrumento(tenant_id,entity_id,numero_instrumento,tipo,orgao_concedente_id,objeto,valor_global,valor_repasse,valor_contrapartida,data_assinatura,vigencia_inicio,vigencia_fim,status,responsavel_nome,observacao,created_at,updated_at)
select 910001,e.id,'CVN-C2S10-02','CONVENIO',o.id,'Programa federal de servicos C2S10',1500,1000,500,date '2026-05-10',date '2026-06-01',date '2027-05-31','ATIVO','Responsavel C2S10','fixture homologacao C2S10',now(),now()
from sigov.entidade e cross join sigov.convenio_orgao_concedente o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.nome='Orgao Concedente Federal C2S10'
and not exists(select 1 from sigov.convenio_instrumento i where i.tenant_id=910001 and i.numero_instrumento='CVN-C2S10-02');
insert into sigov.convenio_projeto(tenant_id,entity_id,instrumento_id,nome,objetivo,justificativa,publico_alvo,local_execucao,unidade_responsavel,data_inicio,data_fim_prevista,percentual_execucao,status,created_at,updated_at)
select 910001,e.id,i.id,'Projeto C2S10 '||i.numero_instrumento,'Ampliar frota e equipamentos publicos','Justificativa ficticia homologacao','Publico-alvo ficticio C2S10','Brasilia-DF','Unidade gestora C2S10',date '2026-06-01',date '2027-05-31',case when i.numero_instrumento='CVN-C2S10-01' then 75 else 40 end,'EM_EXECUCAO',now(),now()
from sigov.entidade e cross join sigov.convenio_instrumento i
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.tenant_id=910001 and i.numero_instrumento in ('CVN-C2S10-01','CVN-C2S10-02')
and not exists(select 1 from sigov.convenio_projeto pj where pj.instrumento_id=i.id);
insert into sigov.convenio_meta(tenant_id,entity_id,projeto_id,numero,descricao,indicador,unidade_medida,quantidade_prevista,quantidade_executada,status,created_at,updated_at)
select 910001,e.id,pj.id,'META-1','Entrega de equipamentos C2S10','% de entregas realizadas','un',100,75,'EM_EXECUCAO',now(),now()
from sigov.entidade e join sigov.convenio_projeto pj on pj.tenant_id=e.tenant_id join sigov.convenio_instrumento i on i.id=pj.instrumento_id and i.tenant_id=pj.tenant_id
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.numero_instrumento='CVN-C2S10-01'
and not exists(select 1 from sigov.convenio_meta m where m.projeto_id=pj.id);
insert into sigov.convenio_meta(tenant_id,entity_id,projeto_id,numero,descricao,indicador,unidade_medida,quantidade_prevista,quantidade_executada,status,created_at,updated_at)
select 910001,e.id,pj.id,'META-1','Prestacao de servicos C2S10','% de servicos prestados','un',50,20,'EM_EXECUCAO',now(),now()
from sigov.entidade e join sigov.convenio_projeto pj on pj.tenant_id=e.tenant_id join sigov.convenio_instrumento i on i.id=pj.instrumento_id and i.tenant_id=pj.tenant_id
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.numero_instrumento='CVN-C2S10-02'
and not exists(select 1 from sigov.convenio_meta m where m.projeto_id=pj.id);
insert into sigov.convenio_etapa(tenant_id,entity_id,meta_id,numero,descricao,data_inicio,data_fim_prevista,valor_previsto,valor_executado,status,created_at,updated_at)
select 910001,e.id,m.id,'ETAPA-1','Etapa unica de execucao C2S10',date '2026-06-01',date '2027-05-31',3200,2400,'EM_EXECUCAO',now(),now()
from sigov.entidade e join sigov.convenio_meta m on m.tenant_id=e.tenant_id join sigov.convenio_projeto pj on pj.id=m.projeto_id and pj.tenant_id=m.tenant_id join sigov.convenio_instrumento i on i.id=pj.instrumento_id and i.tenant_id=pj.tenant_id
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.numero_instrumento='CVN-C2S10-01'
and not exists(select 1 from sigov.convenio_etapa et where et.meta_id=m.id);
insert into sigov.convenio_etapa(tenant_id,entity_id,meta_id,numero,descricao,data_inicio,data_fim_prevista,valor_previsto,valor_executado,status,created_at,updated_at)
select 910001,e.id,m.id,'ETAPA-1','Etapa unica de execucao C2S10',date '2026-06-01',date '2027-05-31',1000,400,'EM_EXECUCAO',now(),now()
from sigov.entidade e join sigov.convenio_meta m on m.tenant_id=e.tenant_id join sigov.convenio_projeto pj on pj.id=m.projeto_id and pj.tenant_id=m.tenant_id join sigov.convenio_instrumento i on i.id=pj.instrumento_id and i.tenant_id=pj.tenant_id
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.numero_instrumento='CVN-C2S10-02'
and not exists(select 1 from sigov.convenio_etapa et where et.meta_id=m.id);
insert into sigov.convenio_repasse(tenant_id,entity_id,instrumento_id,numero_repasse,data_repasse,valor,fonte_recurso,status,created_at,updated_at)
select 910001,e.id,i.id,'REP-C2S10-01',date '2026-06-15',3200,'Recurso federal ficticio C2S10','RECEBIDO',now(),now()
from sigov.entidade e cross join sigov.convenio_instrumento i
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.tenant_id=910001 and i.numero_instrumento='CVN-C2S10-01'
and not exists(select 1 from sigov.convenio_repasse r where r.instrumento_id=i.id and r.numero_repasse='REP-C2S10-01');
insert into sigov.convenio_repasse(tenant_id,entity_id,instrumento_id,numero_repasse,data_repasse,valor,fonte_recurso,status,created_at,updated_at)
select 910001,e.id,i.id,'REP-C2S10-02',date '2026-06-15',1000,'Recurso federal ficticio C2S10','RECEBIDO',now(),now()
from sigov.entidade e cross join sigov.convenio_instrumento i
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and i.tenant_id=910001 and i.numero_instrumento='CVN-C2S10-02'
and not exists(select 1 from sigov.convenio_repasse r where r.instrumento_id=i.id and r.numero_repasse='REP-C2S10-02');
insert into sigov.obras_obra(tenant_id,entidade_id,codigo,nome,tipo_obra,localidade,valor_estimado,valor_contratado,inicio_previsto,fim_previsto,status,created_at,created_by)
select 910001,e.id,'OBRA-C2S10-02','Obra C2S10 Delta - ampliacao da unidade','CONSTRUCAO','Brasilia-DF',12000,10000,date '2026-07-01',date '2027-06-30','EM_EXECUCAO',now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT'
and not exists(select 1 from sigov.obras_obra o where o.tenant_id=910001 and o.codigo='OBRA-C2S10-02');
-- Projeto basico da obra nasce EM_ELABORACAO: o gate OBRA_CONTRATO_SEM_PROJETO exige APROVADO
insert into sigov.obras_projeto(tenant_id,entidade_id,obra_id,codigo,descricao,status,created_at,created_by)
select 910001,e.id,o.id,'PJ-C2S10-02','Projeto basico e executivo C2S10','EM_ELABORACAO',now(),910301
from sigov.entidade e cross join sigov.obras_obra o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.codigo='OBRA-C2S10-02'
and not exists(select 1 from sigov.obras_projeto p where p.obra_id=o.id and p.ativo and not p.is_deleted);
insert into sigov.obras_medicao(tenant_id,entidade_id,obra_id,descricao,status,valor,percentual_fisico,saldo_contratual_antes,saldo_contratual_depois,created_at,created_by)
select 910001,e.id,o.id,'Medicao 1 C2S10','ENVIADA',4000,50,10000,10000,now(),910301
from sigov.entidade e cross join sigov.obras_obra o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.codigo='OBRA-C2S10-02'
and not exists(select 1 from sigov.obras_medicao m where m.obra_id=o.id and m.descricao='Medicao 1 C2S10' and m.ativo and not m.is_deleted);
insert into sigov.obras_medicao(tenant_id,entidade_id,obra_id,descricao,status,valor,percentual_fisico,saldo_contratual_antes,saldo_contratual_depois,created_at,created_by)
select 910001,e.id,o.id,'Medicao 2 C2S10','ENVIADA',8000,60,6000,6000,now(),910301
from sigov.entidade e cross join sigov.obras_obra o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.codigo='OBRA-C2S10-02'
and not exists(select 1 from sigov.obras_medicao m where m.obra_id=o.id and m.descricao='Medicao 2 C2S10' and m.ativo and not m.is_deleted);
insert into sigov.obras_medicao(tenant_id,entidade_id,obra_id,descricao,status,valor,percentual_fisico,saldo_contratual_antes,saldo_contratual_depois,created_at,created_by)
select 910001,e.id,o.id,'Medicao 3 C2S10','ENVIADA',6000,100,6000,6000,now(),910301
from sigov.entidade e cross join sigov.obras_obra o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.codigo='OBRA-C2S10-02'
and not exists(select 1 from sigov.obras_medicao m where m.obra_id=o.id and m.descricao='Medicao 3 C2S10' and m.ativo and not m.is_deleted);
insert into sigov.obras_medicao(tenant_id,entidade_id,obra_id,descricao,status,valor,percentual_fisico,saldo_contratual_antes,saldo_contratual_depois,created_at,created_by)
select 910001,e.id,o.id,'Medicao 4 C2S10','RASCUNHO',100,10,6000,6000,now(),910301
from sigov.entidade e cross join sigov.obras_obra o
where e.tenant_id=910001 and e.codigo_externo='C2S10-ENT' and o.tenant_id=910001 and o.codigo='OBRA-C2S10-02'
and not exists(select 1 from sigov.obras_medicao m where m.obra_id=o.id and m.descricao='Medicao 4 C2S10' and m.ativo and not m.is_deleted);
select coalesce((select id::text from sigov.entidade where tenant_id=910001 and codigo_externo='C2S10-ENT'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s10.admin'),'0') || '|'
     || coalesce((select id::text from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S10-CC1'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910001 and documento='99.999.000/0005-05'),'0') || '|'
     || coalesce((select id::text from sigov.convenio_orgao_concedente where tenant_id=910001 and nome='Orgao Concedente Federal C2S10'),'0') || '|'
     || coalesce((select id::text from sigov.convenio_instrumento where tenant_id=910001 and numero_instrumento='CVN-C2S10-01'),'0') || '|'
     || coalesce((select id::text from sigov.convenio_instrumento where tenant_id=910001 and numero_instrumento='CVN-C2S10-02'),'0') || '|'
     || coalesce((select id::text from sigov.obras_obra where tenant_id=910001 and codigo='OBRA-C2S10-02'),'0') || '|'
     || coalesce((select p.id::text from sigov.obras_projeto p join sigov.obras_obra o on o.id=p.obra_id where o.tenant_id=910001 and o.codigo='OBRA-C2S10-02' and p.ativo and not p.is_deleted),'0') || '|'
     || coalesce((select m.id::text from sigov.obras_medicao m join sigov.obras_obra o on o.id=m.obra_id where o.tenant_id=910001 and o.codigo='OBRA-C2S10-02' and m.descricao='Medicao 1 C2S10' and m.ativo and not m.is_deleted),'0') || '|'
     || coalesce((select m.id::text from sigov.obras_medicao m join sigov.obras_obra o on o.id=m.obra_id where o.tenant_id=910001 and o.codigo='OBRA-C2S10-02' and m.descricao='Medicao 2 C2S10' and m.ativo and not m.is_deleted),'0') || '|'
     || coalesce((select m.id::text from sigov.obras_medicao m join sigov.obras_obra o on o.id=m.obra_id where o.tenant_id=910001 and o.codigo='OBRA-C2S10-02' and m.descricao='Medicao 3 C2S10' and m.ativo and not m.is_deleted),'0') || '|'
     || coalesce((select m.id::text from sigov.obras_medicao m join sigov.obras_obra o on o.id=m.obra_id where o.tenant_id=910001 and o.codigo='OBRA-C2S10-02' and m.descricao='Medicao 4 C2S10' and m.ativo and not m.is_deleted),'0');
"@
$s10p = if ($s10fix.code -eq 0 -and ($s10fix.text -ne '')) { @($s10fix.text.Trim() -split '\|') } else { @('0','0','0','0','0','0','0','0','0','0','0','0','0') }
$s10ent=[long]$s10p[0]; $s10uid=[long]$s10p[1]; $s10cc1=[long]$s10p[2]; $s10forn=[long]$s10p[3]; $s10org=[long]$s10p[4]; $s10cvn1=[long]$s10p[5]; $s10cvn2=[long]$s10p[6]; $s10ob2=[long]$s10p[7]; $s10proj=[long]$s10p[8]; $s10m1=[long]$s10p[9]; $s10m2=[long]$s10p[10]; $s10m3=[long]$s10p[11]; $s10m4=[long]$s10p[12]
Add-Check 'S10' 'fixture.aplicado-catalogo-convenios-obra-parametro' (($s10fix.code -eq 0) -and ($s10ent -gt 0) -and ($s10uid -gt 0) -and ($s10org -gt 0) -and ($s10cvn1 -gt 0) -and ($s10cvn2 -gt 0) -and ($s10ob2 -gt 0) -and ($s10proj -gt 0) -and ($s10m1 -gt 0) -and ($s10m2 -gt 0) -and ($s10m3 -gt 0) -and ($s10m4 -gt 0)) ("fixture S10 (entidade esfera FEDERAL + usuario c2s10.admin na mesma cadeia de perfil 910401 + orgao concedente FEDERAL ATIVO + CVN1 4000=3200+800 exec 75% + CVN2 1500=1000+500 exec 40% + obra 10000 c/ projeto EM_ELABORACAO + medicoes M1-M4; postcond exige as 17 chaves ativas e o parametro CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT=70 no banco — falha explicita, regras 11/13) exit=$($s10fix.code); ent=$s10ent uid=$s10uid cvn1=$s10cvn1 cvn2=$s10cvn2 ob2=$s10ob2 proj=$s10proj m1=$s10m1 m2=$s10m2 m3=$s10m3 m4=$s10m4")
$lgS10 = New-SigovLogin -User 'c2s10.admin' -Pass $Password -Base $WebBase
$tkS10 = if ($lgS10.ok) { Get-ApiToken -Jar $lgS10.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
Add-Check 'S10' 'auth.login-dedicado-token-mint' (($lgS10.ok -eq $true) -and ($tkS10.token.Length -eq 64)) ("c2s10.admin POST /Auth/Login -> $($lgS10.post); ApiToken len=$($tkS10.token.Length) (usuario da entidade FEDERAL c2s10 — regra 22: cobertura multi-esfera; autorizacao toda resolvida pelo banco via grants sentinel, regra 11)")

# --- cadeia direta DISPENSA -> homologacao (base da etapa 3/3) -------------------
$s10d1b = ('{"objeto":"C2S10-Aquisicao patrimonial homologacao","justificativa":"necessidade operacional homologacao c2s10","itens":[{"descricao":"Equipamento C2S10","quantidade":15,"unidade":"un","tipoObjeto":"BEM","precoEstimado":100,"centroCustoId":' + $s10cc1 + '}],"urgencia":false}')
$s10d1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s10d1b
$s10d1j = $null; try { $s10d1j = ($s10d1.body | ConvertFrom-Json) } catch {}
$s10d1id = 0; if ($s10d1j -and $null -ne $s10d1j.data) { $s10d1id = [long]$s10d1j.data.id }
$s10d1db = Get-Psql ("select status||'|'||to_char(valor_estimado,'FM999990.00')||'|'||urgencia||'|'||prioridade from sigov.compras_demanda where id=" + $s10d1id + ";")
$null = Get-Psql ("update sigov.compras_demanda set created_by=910301 where id=" + $s10d1id + ";")
$s10ap1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s10d1id + '/aprovar') -Base $ApiBase
$s10apdb = Get-Psql ("select min(id)||'|'||count(*)||'|'||min(status)||'|'||case when min(numero) like 'PI-%' then 'PI' else 'NAO' end from sigov.compras_processo where tenant_id=910001 and demanda_id=" + $s10d1id + ";")
$s10app = if ($s10apdb.code -eq 0 -and $s10apdb.text -ne '') { @($s10apdb.text -split '\|') } else { @('0','0','NAO','NAO') }
$s10proc = [long]$s10app[0]
$s10dp1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s10d1id + ',"modalidadeCodigo":"DISPENSA"}')
$s10dp2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s10d1id + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_ZZZ"}')
$s10dp3 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s10d1id + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_I"}')
$s10dp3j = $null; try { $s10dp3j = ($s10dp3.body | ConvertFrom-Json) } catch {}
$s10lic = 0
if ($s10dp3j -and $null -ne $s10dp3j.data) { $s10lic = [long]$s10dp3j.data.licitacaoId }
$s10li = Get-Psql ("insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910001," + $s10ent + "," + $s10lic + ",1,'Equipamento especial C2S10',15,'un',100.00,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s10lic + "); select count(*) from sigov.compras_licitacao_item where licitacao_id=" + $s10lic + ";")
$s10da1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s10lic + '/adjudicar') -Base $ApiBase -Body '{}'
$s10da2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s10lic + '/adjudicar') -Base $ApiBase -Body ('{"fornecedorId":' + $s10forn + ',"observacao":"contratacao direta c2s10"}')
$s10da2j = $null; try { $s10da2j = ($s10da2.body | ConvertFrom-Json) } catch {}
$s10dadb = Get-Psql ("select to_char(a.valor_total,'FM999990.00')||'|'||(select status from sigov.compras_licitacao where id=" + $s10lic + ")||'|'||(select p.fornecedor_id::text from sigov.compras_proposta p where p.id=a.proposta_vencedora_id) from sigov.compras_adjudicacao a where tenant_id=910001 and licitacao_id=" + $s10lic + ";")
$null = Get-Psql ("update sigov.compras_adjudicacao set responsavel_id=910402 where tenant_id=910001 and licitacao_id=" + $s10lic + ";")
Add-Check 'S10' 'chain.dispensa-adjudicada-base-da-etapa-3' (($s10d1.code -eq 200) -and ($s10d1id -gt 0) -and ($s10d1db.text -eq 'SOLICITADA|1500.00|false|NORMAL') -and ($s10ap1.code -eq 200) -and ($s10proc -gt 0) -and ($s10app[1] -eq '1') -and ($s10app[2] -eq 'ABERTO') -and ($s10app[3] -eq 'PI') -and ($s10dp1.code -eq 400) -and ($s10dp1.body -like '*DISPENSA_BASE_LEGAL_OBRIGATORIA*') -and ($s10dp2.code -eq 404) -and ($s10dp2.body -like '*DISPENSA_BASE_LEGAL_INEXISTENTE*') -and ($s10dp3.code -eq 200) -and ([long]$s10dp3j.data.processoId -eq $s10proc) -and ($s10li.text -eq '1') -and ($s10da1.code -eq 400) -and ($s10da1.body -like '*ADJUDICACAO_DIRETA_SEM_FORNECEDOR*') -and ($s10da2.code -eq 200) -and ([decimal]$s10da2j.data.valorTotal -eq 1500) -and ($s10dadb.text -like ("1500.00|ADJUDICADA|" + $s10forn))) ("demanda 15 un x 100.00 (BEM) -> $($s10d1.code) banco SOLICITADA|1500.00; aprovar -> $($s10ap1.code) processo unico id=$s10proc $($s10app[1..3] -join '|') (o mesmo PI reused pelo procedimento: processoId=$($s10dp3j.data.processoId)); dispensa sem base legal 400 nomeada / inventada 404 nomeada / ART_75_I 200; adjudicar direta sem fornecedor 400 nomeada, com fornecedor -> $($s10da2.code) valor 1500.00 banco: $($s10dadb.text) (mesma cadeia BR-8.x da S8/S9 preparando a base da etapa 3/3)")

# --- BR-10.1: consulta juridica tipada x processo de contratacao -------------------
$s10cj0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/consultas' -Base $ApiBase -Body ('{"processoComprasId":' + $s10proc + ',"tipoConsulta":"EXPERTO","referenciaNumero":"ATO-C2S10-00","descricao":"tipo invalido homologacao","secretariaSetor":"Setor de Compras C2S10","dataSolicitacao":"2026-10-14"}')
$s10cj1b = ('{"processoComprasId":' + $s10proc + ',"tipoConsulta":"PARECER","referenciaNumero":"ATO-C2S10-01","descricao":"Analisar regularidade da contratacao C2S10","secretariaSetor":"Setor de Compras C2S10","dataSolicitacao":"2026-10-14","solicitante":"Analista C2S10","custoHonorario":250,"chaveIdempotencia":"K-CJ-1"}')
$s10cj1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/consultas' -Base $ApiBase -Body $s10cj1b
$s10cj1j = $null; try { $s10cj1j = ($s10cj1.body | ConvertFrom-Json) } catch {}
$s10cj1id = 0; if ($s10cj1j -and $null -ne $s10cj1j.data) { $s10cj1id = [long]$s10cj1j.data.consultaId }
$s10cj1db = Get-Psql ("select cs.status||'|'||to_char(cs.custo_honorario,'FM99999999999990.00')||'|'||v.tipo_consulta||'|'||v.referencia_numero||'|'||coalesce(v.chave_idempotencia,'NULL') from sigov.juridico_consulta cs join sigov.compras_juridico_consulta_vinculo v on v.consulta_id=cs.id and v.tenant_id=cs.tenant_id where cs.id=" + $s10cj1id + ";")
$s10cj1r = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/consultas' -Base $ApiBase -Body $s10cj1b
$s10cj1rj = $null; try { $s10cj1rj = ($s10cj1r.body | ConvertFrom-Json) } catch {}
$s10cj1rid = 0; if ($s10cj1rj -and $null -ne $s10cj1rj.data) { $s10cj1rid = [long]$s10cj1rj.data.consultaId }
$s10cj1c = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/consultas' -Base $ApiBase -Body ('{"processoComprasId":' + $s10proc + ',"tipoConsulta":"DEFESA","referenciaNumero":"ATO-C2S10-01","descricao":"conteudo divergente na mesma chave","secretariaSetor":"Setor de Compras C2S10","dataSolicitacao":"2026-10-14","chaveIdempotencia":"K-CJ-1"}')
$s10cjf0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/filial') -Base $ApiBase -Body '{"justificativa":"recurso do gestor antes da decisao"}'
Add-Check 'S10' 'consulta.criada-link-tipado-idempotente-replay-conflicto' (($s10cj0.code -eq 400) -and ($s10cj0.body -like '*CONSULTA_TIPO_INVALIDO*') -and ($s10cj1.code -eq 200) -and ($s10cj1id -gt 0) -and ($s10cj1db.text -eq ('ABERTA|250.00|PARECER|ATO-C2S10-01|K-CJ-1')) -and ($s10cj1r.code -eq 200) -and ($s10cj1rid -eq $s10cj1id) -and ($s10cj1c.code -eq 409) -and ($s10cj1c.body -like '*CONSULTA_CHAVE_IDEMPOTENCIA_CONFLITO*') -and ($s10cjf0.code -eq 409) -and ($s10cjf0.body -like '*CONSULTA_PAI_NAO_RESOLVIDA*')) ("consulta juridica PARECER x processo de contratacao $s10proc -> $($s10cj1.code) id=$s10cj1id; banco consulta+ledger tipo|ref|chave: $($s10cj1db.text) (BR-10.1: consulta tipada PARECER/IMPUGNACAO/DEFESA/COBRANCA vinculada ao processo via ledger, custo honorario 250.00 registrado); tipo EXPERTO -> $($s10cj0.code) (400 CONSULTA_TIPO_INVALIDO nomeada); replay mesma chave/mesmo conteudo -> $($s10cj1r.code) mesmo id (idempotente); mesma chave conteudo divergente -> $($s10cj1c.code) (409 CONSULTA_CHAVE_IDEMPOTENCIA_CONFLITO nomeada); filial contra ABERTA -> $($s10cjf0.code) (409 CONSULTA_PAI_NAO_RESOLVIDA nomeada: recurso so abre contra RESOLVIDA)")

# --- BR-10.1: parecer condicionado bloqueia a homologacao ---------------------------
$s10pa0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/parecer') -Base $ApiBase -Body '{"conclusao":"POSITIVA","resumo":"conclusao invalida homologacao"}'
$s10pa1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/parecer') -Base $ApiBase -Body '{"conclusao":"CONDICIONADO","resumo":"condicionado sem condicoes"}'
$s10pa2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/parecer') -Base $ApiBase -Body '{"conclusao":"FAVORECIMENTO","resumo":"favorecimento com condicoes indevidas","condicoes":["uma condicao indevida"]}'
$s10pa3 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/parecer') -Base $ApiBase -Body '{"conclusao":"CONDICIONADO","resumo":"Parecer condicionado as regularidades C2S10","condicoes":["Regularizar documentacao fiscal do contratado","Apresentar atestado de capacidade tecnica"]}'
$s10pa3j = $null; try { $s10pa3j = ($s10pa3.body | ConvertFrom-Json) } catch {}
$s10padb = Get-Psql ("select pr.status||'|'||cs.status||'|'||(select count(*)::text from sigov.juridico_parecer_condicao cd where cd.parecer_id=pr.id and cd.tenant_id=pr.tenant_id and cd.ativo and not cd.is_deleted and cd.status='ABERTA') from sigov.juridico_parecer pr join sigov.juridico_consulta cs on cs.id=pr.consulta_id and cs.tenant_id=pr.tenant_id where pr.consulta_id=" + $s10cj1id + " limit 1;")
$s10ho0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s10lic + '/homologar') -Base $ApiBase
$s10cidr = Get-Psql ("select string_agg(cd.id::text,',' order by cd.id) from sigov.juridico_parecer_condicao cd join sigov.juridico_parecer pr on pr.id=cd.parecer_id and pr.tenant_id=cd.tenant_id where pr.consulta_id=" + $s10cj1id + " and cd.tenant_id=910001;")
$s10cids = @(); if ($s10cidr.code -eq 0 -and $s10cidr.text -ne '') { $s10cids = @($s10cidr.text -split ',') }
$s10cda = 0; $s10cdb = 0
if ($s10cids.Count -ge 2) { $s10cda = [long]$s10cids[0]; $s10cdb = [long]$s10cids[1] }
Add-Check 'S10' 'parecer.condicionado-bloqueia-homologacao-gate-nomeado' (($s10pa0.code -eq 400) -and ($s10pa0.body -like '*PARECER_CONCLUSAO_INVALIDA*') -and ($s10pa1.code -eq 400) -and ($s10pa1.body -like '*PARECER_CONDICIONADO_SEM_CONDICAO*') -and ($s10pa2.code -eq 400) -and ($s10pa2.body -like '*PARECER_CONDICAO_INDEVIDA*') -and ($s10pa3.code -eq 200) -and ($s10padb.text -eq 'EMITIDO|CONDICIONADA|2') -and ($s10ho0.code -eq 409) -and ($s10ho0.body -like '*PARECER_CONDICAO_ABERTA*') -and ($s10cda -gt 0) -and ($s10cdb -gt 0)) ("conclusao POSITIVA -> $($s10pa0.code) (400 nomeada); CONDICIONADO sem condicoes -> $($s10pa1.code) (400 nomeada: exige >=1 condicionante); FAVORECIMENTO com condicoes -> $($s10pa2.code) (400 PARECER_CONDICAO_INDEVIDA nomeada); CONDICIONADO c/ 2 condicoes -> $($s10pa3.code); banco parecer|consulta|condicoes-abertas: $($s10padb.text); HOMOLOGAR a licitacao -> $($s10ho0.code) (409 PARECER_CONDICAO_ABERTA nomeada: gate BR-10.1 — proximo passo da contratacao bloqueado ate satisfacao registrada das condicoes; condicoes $s10cda/$s10cdb capturadas no banco)")
$s10cs0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/condicoes/' + $s10cda + '/satisfazer') -Base $ApiBase -Body '{"justificativa":""}'
$s10cs1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/condicoes/' + $s10cda + '/satisfazer') -Base $ApiBase -Body '{"justificativa":"documentacao fiscal regularizada com nota complementar anexada"}'
$s10cs1j = $null; try { $s10cs1j = ($s10cs1.body | ConvertFrom-Json) } catch {}
$s10cs1r = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/condicoes/' + $s10cda + '/satisfazer') -Base $ApiBase -Body '{"justificativa":"reprocesso da mesma condicao"}'
$s10ho1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s10lic + '/homologar') -Base $ApiBase
$s10cs2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/condicoes/' + $s10cdb + '/satisfazer') -Base $ApiBase -Body '{"justificativa":"atestado de capacidade tecnica expedido pelo orgao de classe"}'
$s10cs2j = $null; try { $s10cs2j = ($s10cs2.body | ConvertFrom-Json) } catch {}
$s10ho2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s10lic + '/homologar') -Base $ApiBase
$s10ho2j = $null; try { $s10ho2j = ($s10ho2.body | ConvertFrom-Json) } catch {}
$s10hoid = 0; if ($s10ho2j -and $null -ne $s10ho2j.data) { $s10hoid = [long]$s10ho2j.data.id }
$s10hodb = Get-Psql ("select status from sigov.juridico_consulta where id=" + $s10cj1id + ";")
Add-Check 'S10' 'parecer.condicoes-satisfeitas-liberam-homologacao' (($s10cs0.code -eq 400) -and ($s10cs0.body -like '*SATISFAZER_CONDICAO_SEM_JUSTIFICATIVA*') -and ($s10cs1.code -eq 200) -and ([int]$s10cs1j.data.condicoesAbertas -eq 1) -and ($s10cs1j.data.consultaStatus -eq 'CONDICIONADA') -and ($s10cs1r.code -eq 409) -and ($s10cs1r.body -like '*CONDICAO_JA_SATISFEITA*') -and ($s10ho1.code -eq 409) -and ($s10ho1.body -like '*PARECER_CONDICAO_ABERTA*') -and ($s10cs2.code -eq 200) -and ([int]$s10cs2j.data.condicoesAbertas -eq 0) -and ($s10cs2j.data.consultaStatus -eq 'RESOLVIDA') -and ($s10ho2.code -eq 200) -and ($s10hoid -gt 0) -and ($s10hodb.text -eq 'RESOLVIDA')) ("satisfazer sem justificativa -> $($s10cs0.code) (400 nomeada); condicao 1 satisfeita c/ justificativa -> $($s10cs1.code) restam 1 (consulta segue CONDICIONADA); re-satisfazer -> $($s10cs1r.code) (409 CONDICAO_JA_SATISFEITA nomeada: ato unico); homologar ainda -> $($s10ho1.code) 409 nomeada (resta 1 aberta); condicao 2 -> $($s10cs2.code) restam 0 e consulta -> RESOLVIDA (banco: $($s10hodb.text)); HOMOLOGAR -> $($s10ho2.code) id=$s10hoid (gate BR-10.1 liberado apenas com todas as condicoes satisfeitas)")
$s10cjf = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/filial') -Base $ApiBase -Body '{"justificativa":"recurso do gestor sobre a fundamentacao do parecer"}'
$s10cjfj = $null; try { $s10cjfj = ($s10cjf.body | ConvertFrom-Json) } catch {}
$s10cjfid = 0; if ($s10cjfj -and $null -ne $s10cjfj.data) { $s10cjfid = [long]$s10cjfj.data.consultaId }
$s10cjfdb = Get-Psql ("select cs.status||'|'||cs.consulta_pai_id::text||'|'||v.referencia_numero from sigov.juridico_consulta cs join sigov.compras_juridico_consulta_vinculo v on v.consulta_id=cs.id and v.tenant_id=cs.tenant_id where cs.id=" + $s10cjfid + ";")
$s10cjf2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/filial') -Base $ApiBase -Body '{"justificativa":"segunda tentativa de recurso"}'
$s10pa4 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s10cj1id + '/parecer') -Base $ApiBase -Body '{"conclusao":"FAVORECIMENTO","resumo":"segunda conclusao sobre a original"}'
Add-Check 'S10' 'consulta.filial-recurso-original-preservada' (($s10cjf.code -eq 200) -and ($s10cjfid -gt 0) -and ($s10cjfdb.text -like ("ABERTA|" + $s10cj1id + "|ATO-C2S10-01-FILIAL")) -and ($s10cjf2.code -eq 409) -and ($s10cjf2.body -like '*FILIAL_DUPLOCADA*') -and ($s10pa4.code -eq 409) -and ($s10pa4.body -like '*PARECER_JA_EMITIDO*')) ("filial (recurso) contra a original RESOLVIDA -> $($s10cjf.code) id=$s10cjfid; banco filial status|pai|referencia-herdada: $($s10cjfdb.text) (BR-10.1: 1 filial por pai, herda processo/tipo/referencia do ledger, ORIGINAL preservada); 2a filial -> $($s10cjf2.code) (409 FILIAL_DUPLOCADA nomeada); nova conclusao na original -> $($s10pa4.code) (409 PARECER_JA_EMITIDO nomeada: parecer EMITIDO e unico por consulta)")

# --- BR-10.4/7.8b: contrato de obra exige projeto APROVADO --------------------------
$s10ctb = ('{"homologacaoId":' + $s10hoid + ',"dataEficacia":"2026-11-10","dataFim":"2027-11-09","assinaturaModo":"FISICA","gestor":"Gestor C2S10","fiscal":"Fiscal C2S10","obraId":' + $s10ob2 + '}')
$s10ct0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body $s10ctb
$null = Get-Psql ("update sigov.obras_projeto set status='APROVADO', updated_at=now() where id=" + $s10proj + ";")
$s10ct1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body $s10ctb
$s10ct1j = $null; try { $s10ct1j = ($s10ct1.body | ConvertFrom-Json) } catch {}
$s10ctid = 0; if ($s10ct1j -and $null -ne $s10ct1j.data) { $s10ctid = [long]$s10ct1j.data.contratoId }
$s10ctdb = Get-Psql ("select c.status||'|'||to_char(c.valor_contratado,'FM999990.00')||'|'||coalesce((select vc.codigo||'|'||vc.status from sigov.obras_contrato_vinculo vc where vc.obra_id=" + $s10ob2 + " and vc.dados->>'compras_contrato_id' = c.id::text and vc.ativo and not vc.is_deleted),'NAO') from sigov.compras_contrato c where c.id=" + $s10ctid + ";")
Add-Check 'S10' 'contrato.obra-sem-projeto-aprovado-bloqueado' (($s10ct0.code -eq 409) -and ($s10ct0.body -like '*OBRA_CONTRATO_SEM_PROJETO*') -and ($s10ct1.code -eq 200) -and ($s10ctid -gt 0) -and ([decimal]$s10ct1j.data.valorContratado -eq 1500) -and ($s10ctdb.text -like ("VIGENTE|1500.00|VC-C2S10-" + $s10ctid + "|ATIVO"))) ("gerar contrato com obraId=$s10ob2 enquanto o projeto esta EM_ELABORACAO -> $($s10ct0.code) (409 OBRA_CONTRATO_SEM_PROJETO nomeada — BR-10.4/Lei 14.133 art. 185: licitacao de projeto basico previa); projeto posto a APROVADO -> retry $($s10ct1.code) id=$s10ctid valor 1500.00; banco contrato|vinculo: $($s10ctdb.text) (o vinculo contrato x obra fica no modulo obras como dependencia canonica, sem copia local)")

# --- BR-10.2: contencioso ativo marca o contrato e bloqueia prorrogação -------------
$s10jc0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/contenciosos' -Base $ApiBase -Body ('{"origemTipo":"JUDICIARIO","origemId":' + $s10ctid + ',"caso":"PASSIVO","valorCausa":12000,"assunto":"cobranca indevida homologacao"}')
$s10jc1b = ('{"origemTipo":"CONTRATO","origemId":' + $s10ctid + ',"caso":"PASSIVO","valorCausa":12000,"assunto":"Cobranca indevida sobre o contrato C2S10"}')
$s10jc1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/contenciosos' -Base $ApiBase -Body $s10jc1b
$s10jc1j = $null; try { $s10jc1j = ($s10jc1.body | ConvertFrom-Json) } catch {}
$s10jcid = 0; if ($s10jc1j -and $null -ne $s10jc1j.data) { $s10jcid = [long]$s10jc1j.data.processoId }
$s10jc1db = Get-Psql ("select p.status||'|'||p.area||'|'||p.natureza||'|'||p.origem_tipo||'|'||p.origem_id::text||'|'||(select count(*)::text from sigov.juridico_processo_movimento m where m.processo_id=p.id and m.tipo='ABERTURA')||'|'||coalesce((select v.status from sigov.compras_vinculo_externo v where v.tenant_id=p.tenant_id and v.alvo_modulo='JURIDICO_CONTENCIOSO' and v.alvo_id=p.id),'NULL') from sigov.juridico_processo p where p.id=" + $s10jcid + ";")
$s10jc2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/juridico/contenciosos' -Base $ApiBase -Body $s10jc1b
$s10ex1 = Invoke-Api -Bearer $tkS10.token -Method GET -Path ('/api/compras/integradas/contratos/' + $s10ctid + '/execucao') -Base $ApiBase
$s10ex1j = $null; try { $s10ex1j = ($s10ex1.body | ConvertFrom-Json) } catch {}
$s10pr0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s10ctid + '/prorrogar') -Base $ApiBase -Body '{"novoFim":"2028-11-09","justificativa":"prorrogação anual homologacao"}'
Add-Check 'S10' 'contencioso.aberto-marca-juridico-bloqueia-prorrogacao' (($s10jc0.code -eq 400) -and ($s10jc0.body -like '*CONTENCIOSO_ORIGEM_INVALIDA*') -and ($s10jc1.code -eq 200) -and ($s10jcid -gt 0) -and ($s10jc1j.data.caso -eq 'PASSIVO') -and ($s10jc1j.data.status -eq 'ATIVO') -and ($s10jc1db.text -like ("ATIVO|CIVEL|CONTENCIOSO|CONTRATO|" + $s10ctid + "|1|ATIVO")) -and ($s10jc2.code -eq 409) -and ($s10jc2.body -like '*CONTENCIOSO_ATIVO_PARA_ORIGEM*') -and ($s10ex1.code -eq 200) -and ($s10ex1j.data.contenciosoAtivo -eq $true) -and ($s10pr0.code -eq 409) -and ($s10pr0.body -like '*PRORROGACAO_BLOQUEADA_CONTENCIOSO_ATIVO*')) ("origem JUDICIARIO -> $($s10jc0.code) (400 CONTENCIOSO_ORIGEM_INVALIDA nomeada); origem CONTRATO caso PASSIVO causa 12000 -> $($s10jc1.code) processo=$s10jcid; banco status|area|natureza|origem|movimento-ABERTURA|marcador-ledger: $($s10jc1db.text) (BR-10.2: movimento append-only + marcador JURIDICO ATIVO no ledger compras_vinculo_externo, so para origem CONTRATO); 2o contencioso mesma origem -> $($s10jc2.code) (409 nomeada: 1 ativo por origem); GET execucao -> contenciosoAtivo=$($s10ex1j.data.contenciosoAtivo) (marca derivada do banco, sem copia local); PRORROGAR -> $($s10pr0.code) (409 PRORROGACAO_BLOQUEADA_CONTENCIOSO_ATIVO nomeada ate desfecho registrado)")
$s10jd0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/contenciosos/' + $s10jcid + '/desfecho') -Base $ApiBase -Body '{"resultado":"HOMOLOGADO","custas":2500,"justificativa":"resultado invalido homologacao"}'
$s10jd1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/contenciosos/' + $s10jcid + '/desfecho') -Base $ApiBase -Body '{"resultado":"TRANSACAO","custas":2500,"justificativa":"transacao extrajudicial celebrada com o credor"}'
$s10jd1j = $null; try { $s10jd1j = ($s10jd1.body | ConvertFrom-Json) } catch {}
$s10jd1db = Get-Psql ("select p.status||'|'||p.resultado||'|'||case when p.encerrado_em is null then 'NULL' else 'SET' end||'|'||case when nullif(trim(p.justificativa),'') is null then 'NAO' else 'JUS' end||'|'||(select count(*)::text from sigov.juridico_processo_movimento m where m.processo_id=p.id)||'|'||coalesce((select v.status from sigov.compras_vinculo_externo v where v.tenant_id=p.tenant_id and v.alvo_modulo='JURIDICO_CONTENCIOSO' and v.alvo_id=p.id),'NULL') from sigov.juridico_processo p where p.id=" + $s10jcid + ";")
$s10jd2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/juridico/contenciosos/' + $s10jcid + '/desfecho') -Base $ApiBase -Body '{"resultado":"JUGADO","custas":0,"justificativa":"tentativa de novo desfecho"}'
$s10pr1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/contratos/' + $s10ctid + '/prorrogar') -Base $ApiBase -Body '{"novoFim":"2028-11-09","justificativa":"prorrogação anual homologacao"}'
$s10pr1db = Get-Psql ("select status||'|'||to_char(data_fim,'YYYY-MM-DD')||'|'||(select count(1)::text from sigov.compras_contrato_historico h where h.contrato_id=" + $s10ctid + " and h.status_novo='PRORROGACAO') from sigov.compras_contrato where id=" + $s10ctid + ";")
Add-Check 'S10' 'contencioso.desfecho-append-only-quita-marcador-libera-prorrogacao' (($s10jd0.code -eq 400) -and ($s10jd0.body -like '*CONTENCIOSO_RESULTADO_INVALIDO*') -and ($s10jd1.code -eq 200) -and ($s10jd1j.data.status -eq 'ENCERRADO') -and ($s10jd1db.text -eq ('ENCERRADO|TRANSACAO|SET|JUS|2|QUITADO')) -and ($s10jd2.code -eq 409) -and ($s10jd2.body -like '*CONTENCIOSO_JA_DESFECADO*') -and ($s10pr1.code -eq 200) -and ($s10pr1db.text -eq 'VIGENTE|2028-11-09|1')) ("resultado HOMOLOGADO -> $($s10jd0.code) (400 nomeada: TRANSACAO/JUGADO/DESISTENCIA); TRANSACAO custas 2500 -> $($s10jd1.code); banco status|resultado|encerrado_em|justificativa|movimentos(ABERTURA+DESFECO)|marcador: $($s10jd1db.text) (BR-10.2: desfecho append-only encerra e QUITA o marcador JURIDICO do contrato pelo ledger, com justificativa obrigatoria registrada); 2o desfecho -> $($s10jd2.code) (409 CONTENCIOSO_JA_DESFECADO nomeada); PRORROGAR de novo -> $($s10pr1.code) banco status|data_fim|rastro-PRORROGACAO: $($s10pr1db.text) (liberado apos desfecho; contrato segue VIGENTE com data_fim estendida e rastro append-only unico no historico - comportamento canonico do prorrogar S8)")

# --- BR-7.8: contrapartida do convenio (tipos, teto do instrumento, idempotencia) ---
$s10cp0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"FINANCEIRA","descricao":"valores inconsistentes homologacao","valorPrevisto":800,"valorRealizado":900,"dataRealizacao":"2026-10-01"}')
$s10cp1b = ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"FINANCEIRA","descricao":"Contrapartida financeira C2S10","valorPrevisto":800,"valorRealizado":800,"dataRealizacao":"2026-10-01","chaveIdempotencia":"K-CP-1"}')
$s10cp1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body $s10cp1b
$s10cp1j = $null; try { $s10cp1j = ($s10cp1.body | ConvertFrom-Json) } catch {}
$s10cp1id = 0; if ($s10cp1j -and $null -ne $s10cp1j.data) { $s10cp1id = [long]$s10cp1j.data.id }
$s10cp1db = Get-Psql ("select tipo||'|'||to_char(valor_previsto,'FM999990.00')||'|'||to_char(valor_realizado,'FM999990.00')||'|'||status||'|'||coalesce(chave_idempotencia,'NULL') from sigov.convenio_contrapartida where id=" + $s10cp1id + ";")
$s10cp1r = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body $s10cp1b
$s10cp1rj = $null; try { $s10cp1rj = ($s10cp1r.body | ConvertFrom-Json) } catch {}
$s10cp1rid = 0; if ($s10cp1rj -and $null -ne $s10cp1rj.data) { $s10cp1rid = [long]$s10cp1rj.data.id }
$s10cp1c = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"FINANCEIRA","descricao":"conteudo divergente na mesma chave","valorPrevisto":500,"valorRealizado":500,"dataRealizacao":"2026-10-01","chaveIdempotencia":"K-CP-1"}')
$s10cpX = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"BENS","descricao":"bens alem do teto do instrumento","valorPrevisto":100,"valorRealizado":0}')
Add-Check 'S10' 'contrapartida.linha-tipo-status-derivado-teto-idempotente' (($s10cp0.code -eq 400) -and ($s10cp0.body -like '*CONTRAPARTIDA_VALOR_INCONSISTENTE*') -and ($s10cp1.code -eq 200) -and ($s10cp1id -gt 0) -and ($s10cp1db.text -eq 'FINANCEIRA|800.00|800.00|REALIZADA|K-CP-1') -and ($s10cp1r.code -eq 200) -and ($s10cp1rid -eq $s10cp1id) -and ($s10cp1c.code -eq 409) -and ($s10cp1c.body -like '*CONTRAPARTIDA_CHAVE_IDEMPOTENCIA_CONFLITO*') -and ($s10cpX.code -eq 409) -and ($s10cpX.body -like '*CONTRAPARTIDA_EXCEDE_INSTRUMENTO*')) ("realizado 900 > previsto 800 -> $($s10cp0.code) (400 CONTRAPARTIDA_VALOR_INCONSISTENTE nomeada); CVN1 800/800 c/ data -> $($s10cp1.code) id=$s10cp1id; banco tipo|previsto|realizado|status|chave: $($s10cp1db.text) (BR-7.8: status DERIVADO — realizado>=previsto => REALIZADA, exige dataRealizacao); replay mesma chave/conteudo -> $($s10cp1r.code) mesmo id (idempotente, nada duplica); mesma chave conteudo divergente -> $($s10cp1c.code) (409 nomeada); prevista nova 100 que estoura o teto do instrumento (800 ja consumidos) -> $($s10cpX.code) (409 CONTRAPARTIDA_EXCEDE_INSTRUMENTO nomeada: soma prevista <= valor_contrapartida do instrumento)")

# --- BR-7.8: aplicar recurso do convenio exige contrapartida REALIZADA --------------
$s10vc0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/vinculos/convenio' -Base $ApiBase -Body ('{"contratoId":' + $s10ctid + ',"instrumentoId":' + $s10cvn2 + ',"chaveIdempotencia":"K-V-2"}')
$s10cp2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn2 + ',"tipo":"FINANCEIRA","descricao":"Contrapartida CVN2 C2S10","valorPrevisto":500,"valorRealizado":500,"dataRealizacao":"2026-10-02","chaveIdempotencia":"K-CP-2"}')
$s10cp2j = $null; try { $s10cp2j = ($s10cp2.body | ConvertFrom-Json) } catch {}
$s10vc1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/vinculos/convenio' -Base $ApiBase -Body ('{"contratoId":' + $s10ctid + ',"instrumentoId":' + $s10cvn2 + ',"chaveIdempotencia":"K-V-2"}')
$s10vc1j = $null; try { $s10vc1j = ($s10vc1.body | ConvertFrom-Json) } catch {}
$s10v2 = 0; if ($s10vc1j -and $null -ne $s10vc1j.data) { $s10v2 = [long]$s10vc1j.data.id }
$s10vc1db = Get-Psql ("select status||'|'||alvo_modulo||'|'||alvo_id::text from sigov.compras_vinculo_externo where id=" + $s10v2 + ";")
$s10vc1r = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/vinculos/convenio' -Base $ApiBase -Body ('{"contratoId":' + $s10ctid + ',"instrumentoId":' + $s10cvn2 + ',"chaveIdempotencia":"K-V-2"}')
$s10vc1rj = $null; try { $s10vc1rj = ($s10vc1r.body | ConvertFrom-Json) } catch {}
$s10vc1rid = 0; if ($s10vc1rj -and $null -ne $s10vc1rj.data) { $s10vc1rid = [long]$s10vc1rj.data.id }
$s10vc1c = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/vinculos/convenio' -Base $ApiBase -Body ('{"contratoId":' + $s10ctid + ',"instrumentoId":' + $s10cvn1 + ',"chaveIdempotencia":"K-V-2"}')
$s10vc2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/vinculos/convenio' -Base $ApiBase -Body ('{"contratoId":' + $s10ctid + ',"instrumentoId":' + $s10cvn1 + ',"chaveIdempotencia":"K-V-1"}')
$s10vc2j = $null; try { $s10vc2j = ($s10vc2.body | ConvertFrom-Json) } catch {}
$s10v1 = 0; if ($s10vc2j -and $null -ne $s10vc2j.data) { $s10v1 = [long]$s10vc2j.data.id }
Add-Check 'S10' 'vinculo.convenio-exige-contrapartida-realizada-contra-contrato-vigente' ((($s10vc0.code -eq 409) -and ($s10vc0.body -like '*CONTRAPARTIDA_CONVENIO_INEXISTENTE*') -and ($s10cp2.code -eq 200)) -and ($s10vc1.code -eq 200) -and ($s10v2 -gt 0) -and ($s10vc1db.text -eq ('ATIVO|CONVENIO|' + $s10cvn2)) -and ($s10vc1r.code -eq 200) -and ($s10vc1rid -eq $s10v2) -and ($s10vc1c.code -eq 409) -and ($s10vc1c.body -like '*VINCULO_CONVENIO_CHAVE_CONFLITO*') -and ($s10vc2.code -eq 200) -and ($s10v1 -gt 0)) ("aplicar CVN2 (exige 500 de contrapartida, realizado 0) ao contrato -> $($s10vc0.code) (409 CONTRAPARTIDA_CONVENIO_INEXISTENTE nomeada — BR-7.8: recurso proprio exigido precisa estar REALIZADO antes de aplicar); contrapartida CVN2 500/500 -> $($s10cp2.code); vinculo CVN2 -> $($s10vc1.code) id=$s10v2 banco: $($s10vc1db.text) (contrato PRORROGADO aceito como vigente); replay -> $($s10vc1r.code) mesmo id; mesma chave outro instrumento -> $($s10vc1c.code) (409 nomeada); vinculo CVN1 -> $($s10vc2.code) id=$s10v1 (2 instrumentos distintos aplicados ao mesmo contrato)")

# --- BR-10.3: prestacao de contas (minimo parametrico do banco + saldo final) -------
$s10pe0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/convenios/prestacoes' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn2 + ',"tipo":"PARCIAL","numeroPrestacao":"PC-C2S10-00","periodoInicial":"2026-06-01","periodoFinal":"2026-08-31","valorPrestado":400}')
$s10pe1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/convenios/prestacoes' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"PARCIAL","numeroPrestacao":"PC-C2S10-01","periodoInicial":"2026-06-01","periodoFinal":"2026-09-30","valorPrestado":2400}')
$s10pe1j = $null; try { $s10pe1j = ($s10pe1.body | ConvertFrom-Json) } catch {}
$s10pe1id = 0; if ($s10pe1j -and $null -ne $s10pe1j.data) { $s10pe1id = [long]$s10pe1j.data.id }
$s10pe1db = Get-Psql ("select status||'|'||coalesce(data_envio::text,'NULL') from sigov.convenio_prestacao_contas where id=" + $s10pe1id + ";")
$s10pe2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/convenios/prestacoes' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"FINAL","numeroPrestacao":"PC-C2S10-02","periodoInicial":"2026-06-01","periodoFinal":"2027-05-31","valorPrestado":3200}')
$s10pe3 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/convenios/prestacoes' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn1 + ',"tipo":"PARCIAL_FINAL","numeroPrestacao":"PC-C2S10-03","periodoInicial":"2026-06-01","periodoFinal":"2026-08-31","valorPrestado":100}')
Add-Check 'S10' 'prestacao.minimo-parametrico-do-banco-e-final-com-saldo-bloqueada' (($s10pe0.code -eq 409) -and ($s10pe0.body -like '*PRESTACAO_EXECUCAO_MINIMA_NAO_ATINGIDA*') -and ($s10pe1.code -eq 200) -and ($s10pe1id -gt 0) -and ($s10pe1db.text -like 'ENVIADA|2026*') -and ($s10pe2.code -eq 409) -and ($s10pe2.body -like '*PRESTACAO_FINAL_SALDO_NAO_JUSTIFICADO*') -and ($s10pe3.code -eq 400) -and ($s10pe3.body -like '*PRESTACAO_TIPO_INVALIDO*')) ("prestacao PARCIAL do CVN2 (execucao 40% < minimo parametrico 70% lido do banco CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT) -> $($s10pe0.code) (409 PRESTACAO_EXECUCAO_MINIMA_NAO_ATINGIDA nomeada — BR-10.3); CVN1 (execucao 75%) PARCIAL -> $($s10pe1.code) id=$s10pe1id banco status|data_envio: $($s10pe1db.text); FINAL do CVN1 com saldo de execucao 800.00 (3200 recebidos - 2400 executados) -> $($s10pe2.code) (409 PRESTACAO_FINAL_SALDO_NAO_JUSTIFICADO nomeada: final so com saldo zerado); tipo PARCIAL_FINAL -> $($s10pe3.code) (400 nomeada)")

# --- BR-10.5: encerramento do instrumento (cascata + saldo + imutabilidade) ---------
$s10en0 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/convenios/instrumentos/' + $s10cvn2 + '/encerrar') -Base $ApiBase -Body '{"justificativa":"reajuste orcamentario do programa"}'
$s10qq1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/vinculos/' + $s10v2 + '/quietar') -Base $ApiBase -Body '{"justificativa":"termo aditivo repactua a aplicacao do recurso"}'
$s10qq1j = $null; try { $s10qq1j = ($s10qq1.body | ConvertFrom-Json) } catch {}
$s10qq1db = Get-Psql ("select status||'|'||case when quitado_em is null then 'NULL' else 'SET' end||'|'||case when justificativa_quitacao like '%repactua%' then 'JUS' else 'NAO' end from sigov.compras_vinculo_externo where id=" + $s10v2 + ";")
$s10qq2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/vinculos/' + $s10v2 + '/quietar') -Base $ApiBase -Body '{"justificativa":"nova tentativa de quitacao"}'
$s10en1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/convenios/instrumentos/' + $s10cvn2 + '/encerrar') -Base $ApiBase -Body '{"justificativa":null}'
$s10en2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/convenios/instrumentos/' + $s10cvn2 + '/encerrar') -Base $ApiBase -Body '{"justificativa":"saldo remanescente revertido ao orgao concedente por termino da vigencia"}'
$s10en2j = $null; try { $s10en2j = ($s10en2.body | ConvertFrom-Json) } catch {}
$s10en2db = Get-Psql ("select status||'|'||case when observacao like '%COM SALDO%' then 'SALDO' else 'NAO' end from sigov.convenio_instrumento where id=" + $s10cvn2 + ";")
$s10en3 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/convenios/instrumentos/' + $s10cvn2 + '/encerrar') -Base $ApiBase -Body '{"justificativa":"encerramento repetido"}'
$s10cp3 = Invoke-Api -Bearer $tkS10.token -Method POST -Path '/api/compras/integradas/contrapartidas/convenio' -Base $ApiBase -Body ('{"instrumentoId":' + $s10cvn2 + ',"tipo":"SERVICOS","descricao":"nova contrapartida apos encerramento","valorPrevisto":100,"valorRealizado":100,"dataRealizacao":"2026-10-05"}')
Add-Check 'S10' 'encerramento.cascata-contrato-ativo-saldo-justificado-imutavel' (($s10en0.code -eq 409) -and ($s10en0.body -like '*CONVENIO_ENCERRAMENTO_BLOQUEADO_CONTRATO_ATIVO*') -and ($s10qq1.code -eq 200) -and ($s10qq1j.data.status -eq 'QUITADO') -and ($s10qq1db.text -eq 'QUITADO|SET|JUS') -and ($s10qq2.code -eq 409) -and ($s10qq2.body -like '*VINCULO_JA_QUITADO*') -and ($s10en1.code -eq 400) -and ($s10en1.body -like '*ENCERRAR_CONVENIO_SALDO_SEM_JUSTIFICATIVA*') -and ($s10en2.code -eq 200) -and ([decimal]$s10en2j.data.saldoExecucao -eq 600) -and ($s10en2db.text -eq 'ENCERRADO|SALDO') -and ($s10en3.code -eq 409) -and ($s10en3.body -like '*INSTRUMENTO_JA_ENCERRADO*') -and ($s10cp3.code -eq 409) -and ($s10cp3.body -like '*INSTRUMENTO_ENCERRADO_IMUTAVEL*')) ("encerrar CVN2 com vinculo ATIVO -> $($s10en0.code) (409 CONVENIO_ENCERRAMENTO_BLOQUEADO_CONTRATO_ATIVO nomeada — BR-10.5 cascata); quitar o vinculo c/ justificativa -> $($s10qq1.code) banco status|quitado_em|justificativa: $($s10qq1db.text) (quebra registrada uma unica vez no ledger); 2a quitacao -> $($s10qq2.code) (409 VINCULO_JA_QUITADO nomeada); encerrar sem justificativa com saldo 600.00 -> $($s10en1.code) (400 ENCERRAR_CONVENIO_SALDO_SEM_JUSTIFICATIVA nomeada); encerrar COM justificativa -> $($s10en2.code) saldoExecucao=$($s10en2j.data.saldoExecucao) banco: $($s10en2db.text) (saldo anexado na observacao); re-encerrar -> $($s10en3.code) (409 INSTRUMENTO_JA_ENCERRADO nomeada); contrapartida no ENCERRADO -> $($s10cp3.code) (409 INSTRUMENTO_ENCERRADO_IMUTAVEL nomeada)")

# --- BR-10.4: homologacao de medicao nao pode passar do medido fisicamente ----------
$s10md1 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/obras/medicoes/' + $s10m1 + '/homologar') -Base $ApiBase
$s10md1j = $null; try { $s10md1j = ($s10md1.body | ConvertFrom-Json) } catch {}
$s10md1db = Get-Psql ("select m.status||'|'||case when m.homologada_at is null then 'NULL' else 'SET' end||'|'||to_char(m.saldo_contratual_depois,'FM999990.00')||'|'||coalesce((select a.decisao||'|'||a.alcada from sigov.obras_medicao_aprovacao a where a.medicao_id=m.id limit 1),'NULL') from sigov.obras_medicao m where m.id=" + $s10m1 + ";")
$s10md1r = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/obras/medicoes/' + $s10m1 + '/homologar') -Base $ApiBase
$s10md2 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/obras/medicoes/' + $s10m2 + '/homologar') -Base $ApiBase
$s10md4 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/obras/medicoes/' + $s10m4 + '/homologar') -Base $ApiBase
$s10md3 = Invoke-Api -Bearer $tkS10.token -Method POST -Path ('/api/compras/integradas/obras/medicoes/' + $s10m3 + '/homologar') -Base $ApiBase
$s10md3db = Get-Psql ("select count(*)||'|'||to_char(coalesce(sum(valor),0),'FM999990.00') from sigov.obras_medicao where id in (" + $s10m1 + "," + $s10m3 + ") and status='HOMOLOGADA';")
Add-Check 'S10' 'medicao.homologacao-acima-fisico-ou-contratado-insuficiente' (($s10md1.code -eq 200) -and ([decimal]$s10md1j.data.saldoContratualDepois -eq 6000) -and ($s10md1db.text -eq 'HOMOLOGADA|SET|6000.00|APROVADA|FISCAL_OBRA') -and ($s10md1r.code -eq 409) -and ($s10md1r.body -like '*MEDICAO_JA_HOMOLOGADA*') -and ($s10md2.code -eq 409) -and ($s10md2.body -like '*OBRA_MEDICAO_INSUFICIENTE*') -and ($s10md4.code -eq 400) -and ($s10md4.body -like '*MEDICAO_STATUS_INVALIDO*') -and ($s10md3.code -eq 200) -and ($s10md3db.text -eq '2|10000.00')) ("M1 4000 (financeiro acumulado 40% <= medido fisico 50%, dentro do contratado 10000) -> $($s10md1.code); banco status|homologada_at|saldo-depois|aprovacao: $($s10md1db.text) (BR-10.4: homologacao grava a aprovacao FISCAL_OBRA no modulo obras); replay -> $($s10md1r.code) (409 MEDICAO_JA_HOMOLOGADA nomeada); M2 8000 (acumulado 12000 > contratado 10000 e > fisico 60%) -> $($s10md2.code) (409 OBRA_MEDICAO_INSUFICIENTE nomeada: pagamento nao passa do medido fisicamente); M4 RASCUNHO -> $($s10md4.code) (400 MEDICAO_STATUS_INVALIDO nomeada: esperado ENVIADA); M3 6000 (fecha 100%) -> $($s10md3.code); banco homologadas|soma: $($s10md3db.text)")

# --- auditoria: ledger de vinculos por contrato (GET) --------------------------------
$s10vg = Invoke-Api -Bearer $tkS10.token -Method GET -Path ('/api/compras/integradas/vinculos?contratoId=' + $s10ctid) -Base $ApiBase
$s10vgj = $null; try { $s10vgj = ($s10vg.body | ConvertFrom-Json) } catch {}
$s10vgarr = @()
if ($s10vgj -and $null -ne $s10vgj.data) { $s10vgarr = @($s10vgj.data) }
$s10stJ=''; $s10stC1=''; $s10stC2=''
foreach ($v in $s10vgarr) {
  if ($v.alvoModulo -eq 'JURIDICO_CONTENCIOSO') { $s10stJ = $v.status }
  elseif ($v.alvoModulo -eq 'CONVENIO') {
    if ([long]$v.alvoId -eq $s10cvn1) { $s10stC1 = $v.status } else { $s10stC2 = $v.status }
  }
}
Add-Check 'S10' 'vinculos.consultados-por-contrato-no-ledger' (($s10vg.code -eq 200) -and ($s10vgarr.Count -eq 3) -and ($s10stJ -eq 'QUITADO') -and ($s10stC1 -eq 'ATIVO') -and ($s10stC2 -eq 'QUITADO')) ("GET /vinculos?contratoId=$s10ctid -> $($s10vg.code) com $($s10vgarr.Count) linhas do ledger compras_vinculo_externo (chave compras.auditoria.visualizar): JURIDICO_CONTENCIOSO=$s10stJ (quitado no desfecho), CONVENIO CVN1=$s10stC1 (nunca quebrado), CONVENIO CVN2=$s10stC2 (quitado manualmente) — BR-10.5: quebra de vinculo so existe com justificativa e ato registrado, sem delete fisico")

# --- limpeza do bloco S10 (auto-limpeza; regra 9) ------------------------------------
$s10cl = Get-Psql @"
create temp table if not exists s10_alvo_d (id bigint); truncate table s10_alvo_d;
insert into s10_alvo_d select id from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S10-%';
create temp table if not exists s10_alvo_p (id bigint); truncate table s10_alvo_p;
insert into s10_alvo_p select id from sigov.compras_processo where tenant_id=910001 and demanda_id in (select id from s10_alvo_d);
create temp table if not exists s10_alvo_l (id bigint); truncate table s10_alvo_l;
insert into s10_alvo_l select id from sigov.compras_licitacao where tenant_id=910001 and processo_id in (select id from s10_alvo_p);
create temp table if not exists s10_alvo_c (id bigint); truncate table s10_alvo_c;
insert into s10_alvo_c select id from sigov.compras_contrato where tenant_id=910001 and homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s10_alvo_l));
create temp table if not exists s10_alvo_o (id bigint); truncate table s10_alvo_o;
insert into s10_alvo_o select id from sigov.obras_obra where tenant_id=910001 and codigo like 'OBRA-C2S10-%';
delete from sigov.obras_medicao_aprovacao where medicao_id in (select id from sigov.obras_medicao where tenant_id=910001 and obra_id in (select id from s10_alvo_o));
delete from sigov.obras_medicao where tenant_id=910001 and obra_id in (select id from s10_alvo_o);
delete from sigov.obras_projeto where tenant_id=910001 and obra_id in (select id from s10_alvo_o);
delete from sigov.obras_contrato_vinculo where tenant_id=910001 and obra_id in (select id from s10_alvo_o);
delete from sigov.obras_obra where id in (select id from s10_alvo_o);
create temp table if not exists s10_alvo_i (id bigint); truncate table s10_alvo_i;
insert into s10_alvo_i select id from sigov.convenio_instrumento where tenant_id=910001 and numero_instrumento like 'CVN-C2S10-%';
delete from sigov.convenio_prestacao_contas where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i);
delete from sigov.convenio_contrapartida where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i);
delete from sigov.convenio_repasse where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i);
delete from sigov.convenio_etapa where tenant_id=910001 and meta_id in (select id from sigov.convenio_meta where tenant_id=910001 and projeto_id in (select id from sigov.convenio_projeto where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i)));
delete from sigov.convenio_meta where tenant_id=910001 and projeto_id in (select id from sigov.convenio_projeto where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i));
delete from sigov.convenio_projeto where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i);
delete from sigov.convenio_instrumento where id in (select id from s10_alvo_i);
delete from sigov.convenio_orgao_concedente where tenant_id=910001 and nome='Orgao Concedente Federal C2S10';
create temp table if not exists s10_alvo_j (id bigint); truncate table s10_alvo_j;
insert into s10_alvo_j select id from sigov.juridico_processo where tenant_id=910001 and origem_tipo='CONTRATO' and origem_id in (select id from s10_alvo_c);
delete from sigov.juridico_processo_movimento where processo_id in (select id from s10_alvo_j);
delete from sigov.juridico_processo where id in (select id from s10_alvo_j);
delete from sigov.compras_vinculo_externo where tenant_id=910001 and contrato_id in (select id from s10_alvo_c);
create temp table if not exists s10_alvo_cj (id bigint); truncate table s10_alvo_cj;
insert into s10_alvo_cj select id from sigov.juridico_consulta where tenant_id=910001 and (codigo like 'ATO-C2S10-%' or consulta_pai_id in (select id from sigov.juridico_consulta where tenant_id=910001 and codigo like 'ATO-C2S10-%'));
delete from sigov.juridico_parecer_condicao where parecer_id in (select id from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select id from s10_alvo_cj));
delete from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select id from s10_alvo_cj);
delete from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S10-%';
delete from sigov.juridico_consulta where id in (select id from s10_alvo_cj);
delete from sigov.compras_contrato_item where contrato_id in (select id from s10_alvo_c);
delete from sigov.compras_contrato_historico where contrato_id in (select id from s10_alvo_c);
delete from sigov.compras_contrato where id in (select id from s10_alvo_c);
delete from sigov.compras_homologacao where licitacao_id in (select id from s10_alvo_l);
delete from sigov.compras_adjudicacao_item where adjudicacao_id in (select id from sigov.compras_adjudicacao where licitacao_id in (select id from s10_alvo_l));
delete from sigov.compras_adjudicacao where licitacao_id in (select id from s10_alvo_l);
delete from sigov.compras_proposta where licitacao_id in (select id from s10_alvo_l);
delete from sigov.compras_habilitacao where licitacao_id in (select id from s10_alvo_l);
delete from sigov.compras_lance where licitacao_id in (select id from s10_alvo_l);
delete from sigov.compras_licitacao_item where licitacao_id in (select id from s10_alvo_l);
delete from sigov.compras_licitacao where id in (select id from s10_alvo_l);
delete from sigov.compras_processo_item where tenant_id=910001 and processo_id in (select id from s10_alvo_p);
delete from sigov.compras_processo where id in (select id from s10_alvo_p);
delete from sigov.compras_demanda_item where demanda_id in (select id from s10_alvo_d);
delete from sigov.compras_demanda where id in (select id from s10_alvo_d);
delete from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S10 %';
delete from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S10-%';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.perfil_permissao where correlation_id='cf58c2ab-1c20-4a90-88f8-0c2000000010';
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where tenant_id=910001 and login='c2s10.admin');
delete from sigov.usuario where tenant_id=910001 and login='c2s10.admin';
delete from sigov.entidade where tenant_id=910001 and codigo_externo='C2S10-ENT';
select
 (select count(*) from sigov.obras_medicao where tenant_id=910001 and obra_id in (select id from s10_alvo_o))
 + (select count(*) from sigov.obras_projeto where tenant_id=910001 and obra_id in (select id from s10_alvo_o))
 + (select count(*) from sigov.obras_contrato_vinculo where tenant_id=910001 and obra_id in (select id from s10_alvo_o))
 + (select count(*) from sigov.obras_obra where id in (select id from s10_alvo_o))
 + (select count(*) from sigov.convenio_prestacao_contas where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i))
 + (select count(*) from sigov.convenio_contrapartida where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i))
 + (select count(*) from sigov.convenio_repasse where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i))
 + (select count(*) from sigov.convenio_projeto where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i))
 + (select count(*) from sigov.convenio_etapa where tenant_id=910001 and meta_id in (select id from sigov.convenio_meta where tenant_id=910001 and projeto_id in (select id from sigov.convenio_projeto where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i))))
 + (select count(*) from sigov.convenio_meta where tenant_id=910001 and projeto_id in (select id from sigov.convenio_projeto where tenant_id=910001 and instrumento_id in (select id from s10_alvo_i)))
 + (select count(*) from sigov.convenio_instrumento where id in (select id from s10_alvo_i))
 + (select count(*) from sigov.convenio_orgao_concedente where tenant_id=910001 and nome='Orgao Concedente Federal C2S10')
 + (select count(*) from sigov.juridico_processo where id in (select id from s10_alvo_j))
 + (select count(*) from sigov.compras_vinculo_externo where tenant_id=910001 and contrato_id in (select id from s10_alvo_c))
 + (select count(*) from sigov.juridico_parecer_condicao where parecer_id in (select id from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select id from s10_alvo_cj)))
 + (select count(*) from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select id from s10_alvo_cj))
 + (select count(*) from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S10-%')
 + (select count(*) from sigov.juridico_consulta where id in (select id from s10_alvo_cj))
 + (select count(*) from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S10-%')
 + (select count(*) from sigov.compras_licitacao where tenant_id=910001 and objeto like 'C2S10-%')
 + (select count(*) from sigov.compras_contrato where tenant_id=910001 and objeto like 'C2S10-%')
 + (select count(*) from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S10 %')
 + (select count(*) from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S10-%')
 + (select count(*) from sigov.perfil_permissao where correlation_id='cf58c2ab-1c20-4a90-88f8-0c2000000010')
 + (select count(*) from sigov.usuario where tenant_id=910001 and login='c2s10.admin')
 + (select count(*) from sigov.entidade where tenant_id=910001 and codigo_externo='C2S10-ENT');
"@
Add-Check 'S10' 'fixture.removido-auto-limpeza' (($s10cl.code -eq 0) -and ($s10cl.text -eq '0')) ("cascade delete S10 (medicoes+aprovacoes, projetos, vinculos contrato x obra, obra, prestacoes, contrapartidas, repasses, etapas, metas, projetos de convenio, instrumentos, orgao concedente, contenciosos+movimentos, ledger de vinculos externos, condicoes, pareceres, consultas+filiais, ledgers de consulta, contratos+items+historico, homologacoes, adjudicacoes+items, propostas, habilitacoes, lances, licitacao_items, licitacoes, processos+items, demandas+items, fornecedores, CCs, sessoes/auditorias do usuario, grants sentinel cf58c2ab-...010, usuario c2s10.admin+grupo, entidade C2S10-ENT) tenant 910001 -> restantes=" + $s10cl.text)

# ============================================================================
# BLOCO S11 - Homologacao integrada multi-tenant/multi-esfera + cenarios transversais
# (Ciclo 2 Sprint 11: BR-11.1 jornadas por perfil em 2 tenants de esferas diferentes;
#  BR-11.2 cenarios transversais obrigatorios sobre a superficie nova da frente)
# Invariantes validadas ao vivo: a MESMA jornada vertical da frente administrativa
# (demanda -> dispensa -> adjudicacao -> homologacao -> contrato -> fornecimento ->
# recebimento/rejeicao -> desfecho -> fatura -> obrigacao -> liquidacao -> estorno ->
# destinacao estoque) e executada em tenant 910001 (entidade esfera MUNICIPAL) e em
# tenant 910002 (entidade esfera ESTADUAL), com perfis distintos resolvidos pelo banco
# (administracao, tesouraria/financeiro, juridico, sem-grupo). Regra 21 observavel
# nos dados: o mesmo ato (recebimento com rejeicao em 2026-11-12) deriva prazos
# DIFERENTES por tenant porque o parametro RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS
# e parametrizado (padrao 5 no 910001; override 10 no 910002 via
# parametro_modulo_valor — banco = autoridade, regra 11). Cenarios transversais:
# concorrencia de dois clientes simultaneos na chave idempotente (efeto unico no
# banco), reenvio idempotente nos dois tenants, estorno repetido (estado terminal),
# cancelamento com efeito (demanda APROVADA imutavel), schema ausente (tabela
# renomeada -> 404 nomeada nos caminhos), permissao ausente (fail-closed 403) e
# isolamento cross-tenant (escopo + dados). Marcadores C2S11A-% / C2S11B-%;
# o bloco se auto-limpa ao final (regra 9).
# ============================================================================
$s11fix = Get-Psql @"
-- pre-clean reentravel: remove estado de rodada anterior abortada (escopo igual ao da limpeza final do bloco S11; tenants 910001/910002 sao do driver neste ciclo)
delete from sigov.compras_destinacao_integrada where tenant_id in (910001,910002) and fornecimento_recebimento_id in (select id from sigov.compras_fornecimento_recebimento where fornecimento_id in (select id from sigov.compras_fornecimento where tenant_id in (910001,910002)));
delete from sigov.almoxarifado_movimentacao where tenant_id in (910001,910002) and documento_origem like 'FORN-%';
delete from sigov.compras_fatura_integrada where tenant_id in (910001,910002);
delete from sigov.compras_obrigacao where tenant_id in (910001,910002);
delete from sigov.compras_recebimento_item where recebimento_id in (select id from sigov.compras_recebimento where tenant_id in (910001,910002));
delete from sigov.compras_recebimento where tenant_id in (910001,910002);
delete from sigov.compras_fornecimento_recebimento where fornecimento_id in (select id from sigov.compras_fornecimento where tenant_id in (910001,910002));
delete from sigov.compras_fornecimento where tenant_id in (910001,910002);
delete from sigov.compras_contrato_item where contrato_id in (select id from sigov.compras_contrato where tenant_id in (910001,910002));
delete from sigov.compras_contrato_historico where contrato_id in (select id from sigov.compras_contrato where tenant_id in (910001,910002));
delete from sigov.compras_contrato where tenant_id in (910001,910002);
delete from sigov.compras_homologacao where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002));
delete from sigov.compras_adjudicacao_item where adjudicacao_id in (select id from sigov.compras_adjudicacao where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002)));
delete from sigov.compras_adjudicacao where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002));
delete from sigov.compras_proposta where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002));
delete from sigov.compras_habilitacao where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002));
delete from sigov.compras_lance where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002));
delete from sigov.compras_licitacao_item where licitacao_id in (select id from sigov.compras_licitacao where tenant_id in (910001,910002));
delete from sigov.compras_licitacao where tenant_id in (910001,910002);
delete from sigov.compras_processo_item where tenant_id in (910001,910002) and processo_id in (select id from sigov.compras_processo where tenant_id in (910001,910002));
delete from sigov.compras_processo where tenant_id in (910001,910002);
delete from sigov.compras_demanda_item where demanda_id in (select id from sigov.compras_demanda where tenant_id in (910001,910002));
delete from sigov.compras_demanda where tenant_id in (910001,910002);
delete from sigov.juridico_parecer_condicao where parecer_id in (select id from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%'));
delete from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%');
delete from sigov.juridico_consulta where id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%');
delete from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%';
delete from sigov.compras_fornecedor where (tenant_id=910001 and nome like 'Forneadora C2S11A %') or (tenant_id=910002 and nome like 'Forneadora C2S11B %');
delete from sigov.financeiro_centro_custo where codigo in ('C2S11A-CC1','C2S11B-CC1');
delete from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S11A-MAT';
delete from sigov.almoxarifado_local where tenant_id=910001 and codigo like 'C2S11A-LOC%';
delete from sigov.parametro_modulo_valor where tenant_id=910002 and correlation_id='c2s11-prazo-override-e2e';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000011','cf58c2ab-1c20-4a90-88f8-0c2000000012','cf58c2ab-1c20-4a90-88f8-0c2000000013','cf58c2ab-1c20-4a90-88f8-0c2000000014','cf58c2ab-1c20-4a90-88f8-0c2000000015','cf58c2ab-1c20-4a90-88f8-0c2000000016');
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%');
delete from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo in ('C2S11A-FIN','C2S11A-JUR','C2S11A-SEMP')) or (tenant_id=910002 and codigo_externo in ('C2S11B-ADM','C2S11B-SEMP')));
delete from sigov.perfil_acesso where (tenant_id=910001 and nome in ('C2S11A Perfil Tesouraria','C2S11A Perfil Juridico','C2S11A Perfil SemFrente')) or (tenant_id=910002 and nome in ('C2S11B Perfil Administracao','C2S11B Perfil SemFrente'));
delete from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo in ('C2S11A-FIN','C2S11A-JUR','C2S11A-SEMP')) or (tenant_id=910002 and codigo_externo in ('C2S11B-ADM','C2S11B-SEMP'));
delete from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S11A-ENT') or (tenant_id=910002 and codigo_externo='C2S11B-ENT');
-- TENANT A = 910001 (jornada na esfera MUNICIPAL)
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Entidade Municipal','00.000.000/0001-94','C2S11A-ENT','municipal','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910001 and codigo_externo='C2S11A-ENT');
-- TENANT B = 910002 (jornada na esfera ESTADUAL)
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910002,'C2S11B Entidade Estadual','00.000.000/0001-95','C2S11B-ENT','estadual','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910002 and codigo_externo='C2S11B-ENT');
-- Perfis/grupos dedicados dos perfis nao-administrativos (A) e da administracao do tenant B
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Grupo Tesouraria','C2S11A-FIN',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910001 and codigo_externo='C2S11A-FIN');
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Grupo Juridico','C2S11A-JUR',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910001 and codigo_externo='C2S11A-JUR');
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910002,'C2S11B Grupo Administracao','C2S11B-ADM',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910002 and codigo_externo='C2S11B-ADM');
-- Grupos SemFrente dos usuarios 'semperm': o AuthController (Web) exige roles>0 E permissoes>0
-- por candidato p/ autenticar (L88); sem acesso minimo o usuario nunca loga e o 403 fail-closed
-- da frente de compras nunca e observavel. Cada grupo carrega 1 chave inofensiva FORA da frente.
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Grupo SemFrente','C2S11A-SEMP',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910001 and codigo_externo='C2S11A-SEMP');
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910002,'C2S11B Grupo SemFrente','C2S11B-SEMP',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910002 and codigo_externo='C2S11B-SEMP');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Perfil Tesouraria',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910001 and nome='C2S11A Perfil Tesouraria');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Perfil Juridico',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910001 and nome='C2S11A Perfil Juridico');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910002,'C2S11B Perfil Administracao',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910002 and nome='C2S11B Perfil Administracao');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910001,'C2S11A Perfil SemFrente',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910001 and nome='C2S11A Perfil SemFrente');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910002,'C2S11B Perfil SemFrente',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910002 and nome='C2S11B Perfil SemFrente');
insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,tenant_id,ativo,is_deleted,created_at,created_by)
select g.id,p.id,g.tenant_id,true,false,now(),910301
from sigov.grupo_acesso g cross join sigov.perfil_acesso p
where ((g.tenant_id=910001 and g.codigo_externo='C2S11A-FIN' and p.tenant_id=910001 and p.nome='C2S11A Perfil Tesouraria')
   or (g.tenant_id=910001 and g.codigo_externo='C2S11A-JUR' and p.tenant_id=910001 and p.nome='C2S11A Perfil Juridico')
   or (g.tenant_id=910002 and g.codigo_externo='C2S11B-ADM' and p.tenant_id=910002 and p.nome='C2S11B Perfil Administracao'))
and not exists(select 1 from sigov.grupo_perfil gp where gp.grupo_acesso_id=g.id and gp.perfil_acesso_id=p.id and gp.ativo);
insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,tenant_id,ativo,is_deleted,created_at,created_by)
select g.id,p.id,g.tenant_id,true,false,now(),910301
from sigov.grupo_acesso g cross join sigov.perfil_acesso p
where ((g.tenant_id=910001 and g.codigo_externo='C2S11A-SEMP' and p.tenant_id=910001 and p.nome='C2S11A Perfil SemFrente')
   or (g.tenant_id=910002 and g.codigo_externo='C2S11B-SEMP' and p.tenant_id=910002 and p.nome='C2S11B Perfil SemFrente'))
and not exists(select 1 from sigov.grupo_perfil gp where gp.grupo_acesso_id=g.id and gp.perfil_acesso_id=p.id and gp.ativo);
-- Usuarios (senha_hash copiado do admin 910301; login unico por tenant)
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S11A Administrador','c2s11a.admin','c2s11a.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s11a.admin');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S11A Gestor Solicitante','c2s11a.gestor','c2s11a.gestor@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s11a.gestor');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S11A Tesoureiro','c2s11a.fin','c2s11a.fin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s11a.fin');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S11A Advogado','c2s11a.jur','c2s11a.jur@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s11a.jur');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S11A Sem Grupo','c2s11a.semperm','c2s11a.semperm@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s11a.semperm');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910002,e.id,1,'C2S11B Administrador','c2s11b.admin','c2s11b.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910002 and e.codigo_externo='C2S11B-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910002 and x.login='c2s11b.admin');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910002,e.id,1,'C2S11B Gestor Solicitante','c2s11b.gestor','c2s11b.gestor@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910002 and e.codigo_externo='C2S11B-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910002 and x.login='c2s11b.gestor');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910002,e.id,1,'C2S11B Sem Grupo','c2s11b.semperm','c2s11b.semperm@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910002 and e.codigo_externo='C2S11B-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910002 and x.login='c2s11b.semperm');
-- Vinculo usuario x grupo (o admin A reutiliza o grupo 910401 existente do tenant; gestores ficam SEM grupo; semperm -> grupo SemFrente)
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,910401,910001,true,now(),910301
from sigov.usuario u
where u.tenant_id=910001 and u.login='c2s11a.admin'
and not exists(select 1 from sigov.usuario_grupo g where g.usuario_id=u.id and g.grupo_acesso_id=910401 and g.ativo);
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,g.id,u.tenant_id,true,now(),910301
from sigov.usuario u cross join sigov.grupo_acesso g
where ((u.tenant_id=910001 and u.login='c2s11a.fin' and g.tenant_id=910001 and g.codigo_externo='C2S11A-FIN')
   or (u.tenant_id=910001 and u.login='c2s11a.jur' and g.tenant_id=910001 and g.codigo_externo='C2S11A-JUR')
   or (u.tenant_id=910002 and u.login='c2s11b.admin' and g.tenant_id=910002 and g.codigo_externo='C2S11B-ADM'))
and not exists(select 1 from sigov.usuario_grupo gu where gu.usuario_id=u.id and gu.grupo_acesso_id=g.id and gu.ativo);
-- SemFrente: vinculo dos usuarios semperm aos grupos dedicados (login viavel, sem permissao na frente)
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,g.id,u.tenant_id,true,now(),910301
from sigov.usuario u cross join sigov.grupo_acesso g
where ((u.tenant_id=910001 and u.login='c2s11a.semperm' and g.tenant_id=910001 and g.codigo_externo='C2S11A-SEMP')
   or (u.tenant_id=910002 and u.login='c2s11b.semperm' and g.tenant_id=910002 and g.codigo_externo='C2S11B-SEMP'))
and not exists(select 1 from sigov.usuario_grupo gu where gu.usuario_id=u.id and gu.grupo_acesso_id=g.id and gu.ativo);
-- Grants sentinel (limpeza cirurgica por correlation_id): 24 chaves da frente (mesmo conjunto da S9)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000011'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.id=(select gp.perfil_acesso_id from sigov.grupo_perfil gp where gp.grupo_acesso_id=910401 limit 1)
  and p.ativo and not p.is_deleted
  and ((p.modulo='compras' and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir','compras.licitacao.abrir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar','compras.julgamento.executar','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar','compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular','compras.obrigacao.visualizar','compras.obrigacao.liquidar'))
    or (p.chave='almoxarifado.recebimento.confirmar' and p.modulo='almoxarifado')
    or (p.chave='patrimonio.bem.criar' and p.modulo='patrimonio'))
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Perfil tesouraria (A): somente fatura/obrigacao/liquidacao/visualizar fornecimento (4 chaves)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000012'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910001 and pa.nome='C2S11A Perfil Tesouraria'
  and p.modulo='compras' and p.ativo and not p.is_deleted
  and p.chave in ('compras.fatura.vincular','compras.obrigacao.visualizar','compras.obrigacao.liquidar','compras.fornecimento.visualizar')
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Perfil juridico (A): somente a moda juridica (2 chaves)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000013'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910001 and pa.nome='C2S11A Perfil Juridico'
  and p.modulo='juridico' and p.ativo and not p.is_deleted
  and p.chave in ('juridico.consulta.criar','juridico.parecer.emitir')
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Administracao do tenant B: mesmas 24 chaves da frente (tenant_id 910002)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910002,'cf58c2ab-1c20-4a90-88f8-0c2000000014'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910002 and pa.nome='C2S11B Perfil Administracao'
  and p.ativo and not p.is_deleted
  and ((p.modulo='compras' and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir','compras.licitacao.abrir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar','compras.julgamento.executar','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar','compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular','compras.obrigacao.visualizar','compras.obrigacao.liquidar'))
    or (p.chave='almoxarifado.recebimento.confirmar' and p.modulo='almoxarifado')
    or (p.chave='patrimonio.bem.criar' and p.modulo='patrimonio'))
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Perfis SemFrente: 1 chave inofensiva FORA da frente (necessaria p/ o login autenticar - AuthController L88)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000015'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910001 and pa.nome='C2S11A Perfil SemFrente'
  and p.modulo='patrimonio' and p.chave='patrimonio.bem.criar' and p.ativo and not p.is_deleted
on conflict (perfil_acesso_id,permissao_id) do nothing;
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910002,'cf58c2ab-1c20-4a90-88f8-0c2000000016'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910002 and pa.nome='C2S11B Perfil SemFrente'
  and p.modulo='almoxarifado' and p.chave='almoxarifado.recebimento.confirmar' and p.ativo and not p.is_deleted
on conflict (perfil_acesso_id,permissao_id) do nothing;
do `$`$
begin
  if (select count(1) from sigov.tenant t where t.id in (910001,910002) and t.ativo and not t.is_deleted) <> 2 then
    raise exception 'C2S11-FIXTURE: tenants 910001/910002 ausentes ou inativos';
  end if;
  if (select coalesce(valor_padrao::text,'') from sigov.parametro_modulo where modulo='COMPRAS' and codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and ativo and not is_deleted limit 1) <> '5' then
    raise exception 'C2S11-FIXTURE: parametro padrao COMPRAS.RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS <> 5';
  end if;
  if (select count(1) from sigov.permissao p where p.ativo and not p.is_deleted
      and ((p.modulo='compras' and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir','compras.licitacao.abrir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar','compras.julgamento.executar','compras.adjudicacao.executar','compras.homologacao.executar','compras.contrato.criar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar','compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular','compras.obrigacao.visualizar','compras.obrigacao.liquidar'))
        or (p.chave in ('almoxarifado.recebimento.confirmar','patrimonio.bem.criar'))
        or (p.modulo='juridico' and p.chave in ('juridico.consulta.criar','juridico.parecer.emitir')))) <> 26 then
    raise exception 'C2S11-FIXTURE: catalogo de permissoes incompleto (esperado 26 chaves ativas no banco)';
  end if;
end
`$`$;
-- Catalogo ficticio por tenant (dados homogeneos, esferas distintas — regra 9/21)
insert into sigov.financeiro_centro_custo(tenant_id,codigo,nome,descricao,ativo,created_at,updated_at)
select 910001,'C2S11A-CC1','CC C2S11A Ativo','centro de custo ficticio ativo (homologacao)',true,now(),now()
where not exists(select 1 from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S11A-CC1');
insert into sigov.financeiro_centro_custo(tenant_id,codigo,nome,descricao,ativo,created_at,updated_at)
select 910002,'C2S11B-CC1','CC C2S11B Ativo','centro de custo ficticio ativo (homologacao)',true,now(),now()
where not exists(select 1 from sigov.financeiro_centro_custo where tenant_id=910002 and codigo='C2S11B-CC1');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910001,e.id,'Forneadora C2S11A Alfa','JURIDICA','99.999.000/0006-06','ATIVO',null,now(),now(),910301
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910001 and f.documento='99.999.000/0006-06');
insert into sigov.compras_fornecedor(tenant_id,entidade_id,nome,tipo_pessoa,documento,status,categoria_me_epp,created_at,updated_at,created_by)
select 910002,e.id,'Forneadora C2S11B Beta','JURIDICA','99.999.000/0007-07','ATIVO',null,now(),now(),910301
from sigov.entidade e where e.tenant_id=910002 and e.codigo_externo='C2S11B-ENT'
and not exists(select 1 from sigov.compras_fornecedor f where f.tenant_id=910002 and f.documento='99.999.000/0007-07');
insert into sigov.almoxarifado_local(tenant_id,entidade_id,codigo,nome,ativo,created_at,updated_at)
select 910001,e.id,'C2S11A-LOC1','Local C2S11A Ativo',true,now(),now()
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT'
and not exists(select 1 from sigov.almoxarifado_local l where l.tenant_id=910001 and l.codigo='C2S11A-LOC1');
insert into sigov.almoxarifado_material(tenant_id,entidade_id,codigo,descricao,tipo_material,unidade_medida,estoque_minimo,controla_lote,controla_validade,ativo,created_at,updated_at)
select 910001,e.id,'C2S11A-MAT','Material C2S11A homologacao','CONSUMO','un',0,false,false,true,now(),now()
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S11A-ENT'
and not exists(select 1 from sigov.almoxarifado_material m where m.tenant_id=910001 and m.codigo='C2S11A-MAT');
-- Override parametrico do tenant B (regra 11/21): prazo de substituicao 10 dias uteis (padrao 5)
insert into sigov.parametro_modulo_valor(tenant_id,parametro_id,valor,correlation_id,created_by,is_deleted)
select 910002,id,'10'::jsonb,'c2s11-prazo-override-e2e',910301,false
from sigov.parametro_modulo
where modulo='COMPRAS' and codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS'
and not exists(select 1 from sigov.parametro_modulo_valor val where val.tenant_id=910002 and val.parametro_id=(select id from sigov.parametro_modulo where modulo='COMPRAS' and codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS'));
select coalesce((select id::text from sigov.entidade where tenant_id=910001 and codigo_externo='C2S11A-ENT'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s11a.admin'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s11a.gestor'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s11a.fin'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s11a.jur'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s11a.semperm'),'0') || '|'
     || coalesce((select id::text from sigov.financeiro_centro_custo where tenant_id=910001 and codigo='C2S11A-CC1'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910001 and documento='99.999.000/0006-06'),'0') || '|'
     || coalesce((select id::text from sigov.almoxarifado_local where tenant_id=910001 and codigo='C2S11A-LOC1'),'0') || '|'
     || coalesce((select id::text from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S11A-MAT'),'0') || '|'
     || coalesce((select id::text from sigov.entidade where tenant_id=910002 and codigo_externo='C2S11B-ENT'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910002 and login='c2s11b.admin'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910002 and login='c2s11b.gestor'),'0') || '|'
     || coalesce((select id::text from sigov.usuario where tenant_id=910002 and login='c2s11b.semperm'),'0') || '|'
     || coalesce((select id::text from sigov.financeiro_centro_custo where tenant_id=910002 and codigo='C2S11B-CC1'),'0') || '|'
     || coalesce((select id::text from sigov.compras_fornecedor where tenant_id=910002 and documento='99.999.000/0007-07'),'0');
"@
$s11p = if ($s11fix.code -eq 0 -and ($s11fix.text -ne '')) { @($s11fix.text.Trim() -split '\|') } else { @('0','0','0','0','0','0','0','0','0','0','0','0','0','0','0','0') }
while ($s11p.Count -lt 16) { $s11p += '0' }
$s11entA=[long]$s11p[0]; $s11uidA=[long]$s11p[1]; $s11gestorA=[long]$s11p[2]; $s11finA=[long]$s11p[3]; $s11jurA=[long]$s11p[4]; $s11semA=[long]$s11p[5]; $s11ccA=[long]$s11p[6]; $s11fornA=[long]$s11p[7]; $s11locA=[long]$s11p[8]; $s11matA=[long]$s11p[9]
$s11entB=[long]$s11p[10]; $s11uidB=[long]$s11p[11]; $s11gestorB=[long]$s11p[12]; $s11semB=[long]$s11p[13]; $s11ccB=[long]$s11p[14]; $s11fornB=[long]$s11p[15]
$s11db = Get-Psql "select coalesce((select esfera_governo from sigov.entidade where tenant_id=910001 and codigo_externo='C2S11A-ENT'),'NULL')||'|'||coalesce((select esfera_governo from sigov.entidade where tenant_id=910002 and codigo_externo='C2S11B-ENT'),'NULL')||'|'||coalesce((select val.valor::text from sigov.parametro_modulo_valor val join sigov.parametro_modulo pm on pm.id=val.parametro_id where val.tenant_id=910001 and pm.modulo='COMPRAS' and pm.codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and not val.is_deleted limit 1),'PADRAO')||'|'||coalesce((select val.valor::text from sigov.parametro_modulo_valor val join sigov.parametro_modulo pm on pm.id=val.parametro_id where val.tenant_id=910002 and pm.modulo='COMPRAS' and pm.codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and not val.is_deleted limit 1),(select valor_padrao::text from sigov.parametro_modulo where modulo='COMPRAS' and codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and ativo and not is_deleted))"
Add-Check 'S11' 'fixture.dois-tenants-duas-esferas-quatro-perfis-parametro-override' (($s11fix.code -eq 0) -and ($s11entA -gt 0) -and ($s11uidA -gt 0) -and ($s11gestorA -gt 0) -and ($s11finA -gt 0) -and ($s11jurA -gt 0) -and ($s11semA -gt 0) -and ($s11ccA -gt 0) -and ($s11fornA -gt 0) -and ($s11locA -gt 0) -and ($s11matA -gt 0) -and ($s11entB -gt 0) -and ($s11uidB -gt 0) -and ($s11gestorB -gt 0) -and ($s11semB -gt 0) -and ($s11ccB -gt 0) -and ($s11fornB -gt 0) -and ($s11db.text -eq 'municipal|estadual|PADRAO|10')) ("fixture S11 (tenant 910001: entidade esfera MUNICIPAL + admin c/ grupo 910401 + gestor solicitante + perfis dedicados TESOURARIA e JURIDICO + usuario SemFrente sem permissao na frente; tenant 910002: entidade esfera ESTADUAL + grupo/perfil administracao proprio + gestor + usuario SemFrente; grants sentinel cf58c2ab-...011/012/013/014 + 015/016 das chaves inofensiveis SemFrente — login exige permissoes>0, AuthController L88; postcond exige tenants ativos, padrao RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS=5 e 26 chaves ativas no banco — falha explicita, regras 11/13/21) exit=$($s11fix.code); banco esferaA|esferaB|param-efetivo-A|param-efetivo-B: $($s11db.text); sql-err-tail=$($s11fix.text.Substring([Math]::Max(0,$s11fix.text.Length-500)))")

# --- 6 logins reais: duas esferas, quatro perfis (BR-11.1) ---------------------
$lgA = New-SigovLogin -User 'c2s11a.admin' -Pass $Password -Base $WebBase
$tkA = if ($lgA.ok) { Get-ApiToken -Jar $lgA.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lgFin = New-SigovLogin -User 'c2s11a.fin' -Pass $Password -Base $WebBase
$tkFin = if ($lgFin.ok) { Get-ApiToken -Jar $lgFin.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lgJur = New-SigovLogin -User 'c2s11a.jur' -Pass $Password -Base $WebBase
$tkJur = if ($lgJur.ok) { Get-ApiToken -Jar $lgJur.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lgSemA = New-SigovLogin -User 'c2s11a.semperm' -Pass $Password -Base $WebBase
$tkSemA = if ($lgSemA.ok) { Get-ApiToken -Jar $lgSemA.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lgB = New-SigovLogin -User 'c2s11b.admin' -Pass $Password -Base $WebBase
$tkB = if ($lgB.ok) { Get-ApiToken -Jar $lgB.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lgSemB = New-SigovLogin -User 'c2s11b.semperm' -Pass $Password -Base $WebBase
$tkSemB = if ($lgSemB.ok) { Get-ApiToken -Jar $lgSemB.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
Add-Check 'S11' 'auth.seis-logins-duas-esferas-token-mint' (($lgA.ok -eq $true) -and ($tkA.token.Length -eq 64) -and ($lgFin.ok -eq $true) -and ($tkFin.token.Length -eq 64) -and ($lgJur.ok -eq $true) -and ($tkJur.token.Length -eq 64) -and ($lgSemA.ok -eq $true) -and ($tkSemA.token.Length -eq 64) -and ($lgB.ok -eq $true) -and ($tkB.token.Length -eq 64) -and ($lgSemB.ok -eq $true) -and ($tkSemB.token.Length -eq 64)) ("6 logins web reais na mesma rodada: c2s11a.admin/fin/jur/semperm (tenant 910001, esfera municipal) + c2s11b.admin/semperm (tenant 910002, esfera estadual) -> todos 302 MinhaCentral com ApiToken len=64 (contexto institucional multi-esfera resolvido pela entidade do usuario; autorizacao 100% banco, regras 11/22)")

# --- jornada A (municipal): cadeia completa ate contrato (perfil administracao) --
$s11dAb = ('{"objeto":"C2S11A-Aquisicao de equipamentos para a unidade municipal","justificativa":"necessidade operacional homologacao c2s11a","itens":[{"descricao":"Equipamento C2S11A","quantidade":20,"unidade":"un","tipoObjeto":"BEM","precoEstimado":100,"centroCustoId":' + $s11ccA + '}],"urgencia":false}')
$s11dA = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s11dAb
$s11dAj = $null; try { $s11dAj = ($s11dA.body | ConvertFrom-Json) } catch {}
$s11dAid = 0; if ($s11dAj -and $null -ne $s11dAj.data) { $s11dAid = [long]$s11dAj.data.id }
$s11dAdb = Get-Psql ("select status||'|'||to_char(valor_estimado,'FM999990.00')||'|'||urgencia||'|'||prioridade from sigov.compras_demanda where id=" + $s11dAid + ";")
$null = Get-Psql ("update sigov.compras_demanda set created_by=" + $s11gestorA + " where id=" + $s11dAid + ";")
$s11apA = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s11dAid + '/aprovar') -Base $ApiBase
$s11apAdb = Get-Psql ("select min(id)||'|'||count(*)||'|'||min(status)||'|'||case when min(numero) like 'PI-%' then 'PI' else 'NAO' end from sigov.compras_processo where tenant_id=910001 and demanda_id=" + $s11dAid + ";")
$s11apAp = if ($s11apAdb.code -eq 0 -and $s11apAdb.text -ne '') { @($s11apAdb.text -split '\|') } else { @('0','0','NAO','NAO') }
$s11procA = [long]$s11apAp[0]
$s11cx0 = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s11dAid + '/cancelar') -Base $ApiBase -Body '{"justificativa":"cancelamento generico com efeito ja registrado no processo"}'
$s11dpA0 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s11dAid + ',"modalidadeCodigo":"DISPENSA"}')
$s11dpA1 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s11dAid + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_I"}')
$s11dpA1j = $null; try { $s11dpA1j = ($s11dpA1.body | ConvertFrom-Json) } catch {}
$s11licA = 0
if ($s11dpA1j -and $null -ne $s11dpA1j.data) { $s11licA = [long]$s11dpA1j.data.licitacaoId }
$s11liA = Get-Psql ("insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910001," + $s11entA + "," + $s11licA + ",1,'Equipamento C2S11A',20,'un',100.00,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s11licA + "); select count(*) from sigov.compras_licitacao_item where licitacao_id=" + $s11licA + ";")
$s11daA1 = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s11licA + '/adjudicar') -Base $ApiBase -Body ('{"fornecedorId":' + $s11fornA + ',"observacao":"contratacao direta c2s11a"}')
$s11daA1j = $null; try { $s11daA1j = ($s11daA1.body | ConvertFrom-Json) } catch {}
$s11daAdb = Get-Psql ("select to_char(a.valor_total,'FM999990.00')||'|'||(select status from sigov.compras_licitacao where id=" + $s11licA + ")||'|'||(select p.fornecedor_id::text from sigov.compras_proposta p where p.id=a.proposta_vencedora_id) from sigov.compras_adjudicacao a where tenant_id=910001 and licitacao_id=" + $s11licA + ";")
$null = Get-Psql ("update sigov.compras_adjudicacao set responsavel_id=" + $s11gestorA + " where tenant_id=910001 and licitacao_id=" + $s11licA + ";")
$s11hoA = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s11licA + '/homologar') -Base $ApiBase
$s11hoAj = $null; try { $s11hoAj = ($s11hoA.body | ConvertFrom-Json) } catch {}
$s11hoAid = 0; if ($s11hoAj -and $null -ne $s11hoAj.data) { $s11hoAid = [long]$s11hoAj.data.id }
$s11ctAb = ('{"homologacaoId":' + $s11hoAid + ',"dataEficacia":"2026-11-10","dataFim":"2027-11-09","assinaturaModo":"FISICA","gestor":"Gestor C2S11A","fiscal":"Fiscal C2S11A"}')
$s11ctA = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body $s11ctAb
$s11ctAj = $null; try { $s11ctAj = ($s11ctA.body | ConvertFrom-Json) } catch {}
$s11ctAid = 0; if ($s11ctAj -and $null -ne $s11ctAj.data) { $s11ctAid = [long]$s11ctAj.data.contratoId }
$s11ctAdb = Get-Psql ("select status||'|'||to_char(valor_contratado,'FM999990.00') from sigov.compras_contrato where id=" + $s11ctAid + ";")
$s11itAr = Get-Psql ("select id::text from sigov.compras_contrato_item where contrato_id=" + $s11ctAid + " limit 1;")
$s11itA = 0; if ($s11itAr.code -eq 0 -and $s11itAr.text -ne '') { $s11itA = [long]$s11itAr.text.Trim() }
Add-Check 'S11' 'jornada-a.municipal-cadeia-completa-cancelamento-com-efeito' (($s11dA.code -eq 200) -and ($s11dAid -gt 0) -and ($s11dAdb.text -eq 'SOLICITADA|2000.00|false|NORMAL') -and ($s11apA.code -eq 200) -and ($s11procA -gt 0) -and ($s11apAp[1] -eq '1') -and ($s11apAp[2] -eq 'ABERTO') -and ($s11apAp[3] -eq 'PI') -and ($s11cx0.code -eq 409) -and ($s11cx0.body -like '*DEMANDA_APOS_APROVACAO_IMUTAVEL*') -and ($s11dpA0.code -eq 400) -and ($s11dpA0.body -like '*DISPENSA_BASE_LEGAL_OBRIGATORIA*') -and ($s11dpA1.code -eq 200) -and ([long]$s11dpA1j.data.processoId -eq $s11procA) -and ($s11liA.text -eq '1') -and ($s11daA1.code -eq 200) -and ([decimal]$s11daA1j.data.valorTotal -eq 2000) -and ($s11daAdb.text -like ("2000.00|ADJUDICADA|" + $s11fornA)) -and ($s11hoA.code -eq 200) -and ($s11hoAid -gt 0) -and ($s11ctA.code -eq 200) -and ($s11ctAid -gt 0) -and ([decimal]$s11ctAj.data.valorContratado -eq 2000) -and ($s11ctAdb.text -eq 'VIGENTE|2000.00') -and ($s11itA -gt 0)) ("ESFERA MUNICIPAL (tenant 910001, perfil administracao): demanda 20 un x 100.00 -> $($s11dA.code) banco SOLICITADA|2000.00 (separation-of-duties: created_by reposicionado ao gestor id=$s11gestorA antes de aprovar); aprovar -> $($s11apA.code) processo unico $s11procA $($s11apAp[1..3] -join '|'); CANCELAMENTO COM EFEITO: cancelar a demanda APROVADA -> $($s11cx0.code) (409 DEMANDA_APOS_APROVACAO_IMUTAVEL nomeada — o efeito ja registrado no processo bloqueia o cancelamento generico, BR-11.2); dispensa sem base legal 400 nomeada / ART_75_I 200 (processoId=$($s11dpA1j.data.processoId)); adjudicacao direta c/ fornecedor -> $($s11daA1.code) valor 2000.00 banco: $($s11daAdb.text) (responsavel reposicionado ao gestor antes de homologar); homologar -> $($s11hoA.code) id=$s11hoAid; contrato -> $($s11ctA.code) id=$s11ctAid banco: $($s11ctAdb.text) item=$s11itA")

# --- BR-11.2: concorrencia + reenvio idempotente (mesma chave, dois clientes) ---
function Invoke-ApiConcorrencia {
  param([string]$Bearer,[string]$Path,[string]$Body)
  # Concorrencia real SEM processo extra e SEM runspace extra: dois Process curl.exe
  # iniciados em sequencia rapida rodam em paralelo; ReadToEnd(stdOut) aguarda cada um.
  # Round 4: Start-Job (2 shells powershell) respondia vazio sob pressao de RAM.
  # Round 5: Task.Run com scriptblock falha com "No runspace available" neste host.
  # Corpo inline (-d) evita lock do antivírus em %TEMP% sobre arquivo novo.
  $h1 = 'Authorization: Bearer ' + $Bearer
  $h2 = 'Content-Type: application/json'
  $u = $ApiBase + $Path
  function Esc-Arg([string]$a) {
    if ($a -match '^[A-Za-z0-9_\-.:/@?=,%+]*$') { return $a }
    return '"' + ($a -replace '\\','\\\\').Replace('"','\"') + '"'
  }
  function Split-CodeBody([string]$t) {
    $code = 0; $body = $t
    $i = $t.LastIndexOf("`n".ToCharArray()[0])
    if ($i -ge 0) { $body = $t.Substring(0,$i); $c = $t.Substring($i+1).Trim() -replace '\D',''; try { $code = [int]$c } catch {} }
    return [pscustomobject]@{ code=$code; body=$body }
  }
  $argline = (@('-s','--max-time','60','-X','POST','-H',$h2,'-H',$h1,'-d',$Body,'-w',("`n%{http_code}"),$u) | ForEach-Object { Esc-Arg ([string]$_) }) -join ' '
  $procs = @(); $errs = @()
  foreach ($k in 1..2) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'curl.exe'
    $psi.Arguments = $argline
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.CreateNoWindow = $true
    try { $procs += ,([System.Diagnostics.Process]::Start($psi)) } catch { $procs += ,$null; $errs += ('ERRO_START_' + $k + ': ' + $_.Exception.Message) }
  }
  $outs = @()
  foreach ($i in 0..1) {
    if ($null -eq $procs[$i]) { $outs += ,($errs[$i]); continue }
    $s = $procs[$i].StandardOutput.ReadToEnd()
    $procs[$i].WaitForExit()
    if ($procs[$i].ExitCode -ne 0) { $s = 'CURL_EXIT_' + $procs[$i].ExitCode + ': ' + $s }
    $outs += ,[string]$s
  }
  $p1 = Split-CodeBody $outs[0]
  $p2 = Split-CodeBody $outs[1]
  return [pscustomobject]@{ code1=$p1.code; body1=$p1.body; code2=$p2.code; body2=$p2.body }
}
$s11f1b = ('{"contratoId":' + $s11ctAid + ',"contratoItemId":' + $s11itA + ',"quantidade":15,"chaveIdempotencia":"K-C2S11A-F1"}')
$s11f1 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11f1b
$s11f1j = $null; try { $s11f1j = ($s11f1.body | ConvertFrom-Json) } catch {}
$s11f1id = 0; if ($s11f1j -and $null -ne $s11f1j.data) { $s11f1id = [long]$s11f1j.data.id }
$s11f1r = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11f1b
$s11f1rj = $null; try { $s11f1rj = ($s11f1r.body | ConvertFrom-Json) } catch {}
$s11f1rid = 0; if ($s11f1rj -and $null -ne $s11f1rj.data) { $s11f1rid = [long]$s11f1rj.data.id }
$s11parb = ('{"contratoId":' + $s11ctAid + ',"contratoItemId":' + $s11itA + ',"quantidade":5,"chaveIdempotencia":"K-C2S11A-PAR"}')
$s11par = Invoke-ApiConcorrencia -Bearer $tkA.token -Path '/api/compras/integradas/fornecimentos' -Body $s11parb
$s11parJ1 = $null; try { $s11parJ1 = ($s11par.body1 | ConvertFrom-Json) } catch {}
$s11parI1 = 0; if ($s11parJ1 -and $null -ne $s11parJ1.data) { $s11parI1 = [long]$s11parJ1.data.id }
$s11parJ2 = $null; try { $s11parJ2 = ($s11par.body2 | ConvertFrom-Json) } catch {}
$s11parI2 = 0; if ($s11parJ2 -and $null -ne $s11parJ2.data) { $s11parI2 = [long]$s11parJ2.data.id }
$s11pardb = Get-Psql ("select count(*)::text||'|'||coalesce(min(id)::text,'0') from sigov.compras_fornecimento where tenant_id=910001 and chave_idempotencia='K-C2S11A-PAR';")
$s11parPd = if ($s11pardb.code -eq 0 -and $s11pardb.text -ne '') { @($s11pardb.text.Trim() -split '\|') } else { @('0','0') }
$s11parN = 0; try { $s11parN = [int]$s11parPd[0] } catch {}
$s11parId = 0; try { $s11parId = [long]$s11parPd[1] } catch {}
$s11f3 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11parb
$s11f3j = $null; try { $s11f3j = ($s11f3.body | ConvertFrom-Json) } catch {}
$s11f3id = 0; if ($s11f3j -and $null -ne $s11f3j.data) { $s11f3id = [long]$s11f3j.data.id }
Add-Check 'S11' 'fornecimento.concorrencia-efeto-unico-reenvio-idempotente' (($s11f1.code -eq 200) -and ($s11f1id -gt 0) -and ($s11f1r.code -eq 200) -and ($s11f1rid -eq $s11f1id) -and ((($s11par.code1 -eq 200) -and ($s11parI1 -eq $s11parId)) -or ($s11par.code1 -eq 409)) -and ((($s11par.code2 -eq 200) -and ($s11parI2 -eq $s11parId)) -or ($s11par.code2 -eq 409)) -and ($s11parN -eq 1) -and ($s11parId -gt 0) -and ($s11f3.code -eq 200) -and ($s11f3id -eq $s11parId)) ("fornecimento 15 un c/ chave K-C2S11A-F1 -> $($s11f1.code) id=$s11f1id; REENVIO IDEMPOTENTE: replay identico -> $($s11f1r.code) mesmo id (nada duplica); CONCORRENCIA (BR-11.2): dois clientes SIMULTANEOS (jobs paralelos) com a mesma chave K-C2S11A-PAR e mesmo conteudo -> respostas $($s11par.code1)/$($s11par.code2) (200 idempotente e/ou 409 nomeada da race) e banco count|id: $($s11pardb.text) — EXATAMENTE 1 fornecimento (efeto unico apesar da corrida sobre ux_compras_fornecimento_chave); 3o reenvio sequencial -> $($s11f3.code) mesmo id=$s11f3id")

# --- jornada A: recebimento c/ rejeicao, prazo parametrico e desfecho -----------
$s11rA = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s11f1id + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":14,"quantidadeRejeitada":1,"dataRecebimento":"2026-11-12","motivoRejeicao":"qualidade abaixo do padrao c2s11a"}'
$s11rAj = $null; try { $s11rAj = ($s11rA.body | ConvertFrom-Json) } catch {}
$s11rAid = 0; if ($s11rAj -and $null -ne $s11rAj.data) { $s11rAid = [long]$s11rAj.data.id }
$s11rAdb = Get-Psql ("select r.status||'|'||to_char(r.quantidade_aprovada,'FM9990.000')||'|'||to_char(r.quantidade_rejeitada,'FM9990.000')||'|'||to_char(r.data_prazo_substituicao,'YYYY-MM-DD') from sigov.compras_fornecimento_recebimento r where r.id=" + $s11rAid + ";")
$s11dfA0 = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/recebimentos/' + $s11rAid + '/desfecho') -Base $ApiBase -Body '{"desfecho":"SUBSTITUIDO","justificativa":"fornecedor subira a unidade ate o prazo parametrico"}'
$s11dfAdb = Get-Psql ("select f.status||'|'||coalesce(r.desfecho,'NULL') from sigov.compras_fornecimento f, sigov.compras_fornecimento_recebimento r where f.id=" + $s11f1id + " and r.id=" + $s11rAid + ";")
$s11dfA1 = Invoke-Api -Bearer $tkA.token -Method POST -Path ('/api/compras/integradas/recebimentos/' + $s11rAid + '/desfecho') -Base $ApiBase -Body '{"desfecho":"ACATADO","justificativa":"tentativa de 2o desfecho c2s11a"}'
Add-Check 'S11' 'jornada-a.recebimento-rejeicao-prazo-parametrico-desfecho-unico' (($s11rA.code -eq 200) -and ($s11rAid -gt 0) -and ($s11rAdb.text -eq 'PARCIAL|14.000|1.000|2026-11-19') -and ($s11dfA0.code -eq 200) -and ($s11dfAdb.text -eq 'CONCLUIDO|SUBSTITUIDO') -and ($s11dfA1.code -eq 409) -and ($s11dfA1.body -like '*REJEICAO_DESFECO_JA_REGISTRADO*')) ("recebimento aprov 14 + reje 1 (motivo) em 2026-11-12 -> $($s11rA.code) id=$s11rAid; banco status|aprov|reje|prazo: $($s11rAdb.text) (prazo = 2026-11-12 + RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS=5 dias uteis lidos do banco do tenant 910001 = 2026-11-19); desfecho SUBSTITUIDO -> $($s11dfA0.code) banco fornecimento|desfecho: $($s11dfAdb.text) (CONCLUIDO); 2o desfecho -> $($s11dfA1.code) (409 REJEICAO_DESFECO_JA_REGISTRADO nomeada: ato unico)")
$s11prazoA = 'NAO'
if ($s11rAdb.text -match '\|([^|]+)$') { $s11prazoA = $Matches[1] }

# --- perfil tesouraria/financeiro (A): fatura + obrigacao + liquidacao ---------
$s11faA = Invoke-Api -Bearer $tkFin.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s11f1id + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S11A-001","natureza":"NFE","serie":"1","cnpjFornecedor":"99.999.000/0006-06","valor":1500,"dataEmissao":"2026-11-15"}'
$s11obA = Invoke-Api -Bearer $tkFin.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s11f1id + ',"referenciaIdempotencia":"OB-C2S11A-F1"}')
$s11obAj = $null; try { $s11obAj = ($s11obA.body | ConvertFrom-Json) } catch {}
$s11oA = 0; if ($s11obAj -and $null -ne $s11obAj.data) { $s11oA = [long]$s11obAj.data.id }
$s11obAdb = Get-Psql ("select to_char(valor_liquidado,'FM99990.00')||'|'||status||'|'||case when data_liquidacao is null then 'NULL' else 'SET' end from sigov.compras_obrigacao where id=" + $s11oA + ";")
$s11liqA = Invoke-Api -Bearer $tkFin.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s11oA + '/liquidar') -Base $ApiBase
$s11liqAdb = Get-Psql ("select status||'|'||case when data_liquidacao is null then 'NULL' else 'SET' end from sigov.compras_obrigacao where id=" + $s11oA + ";")
$s11dmZ1 = Invoke-Api -Bearer $tkFin.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s11dAb
$s11dmZ2 = Invoke-Api -Bearer $tkFin.token -Method GET -Path '/api/compras/integradas/demandas' -Base $ApiBase
Add-Check 'S11' 'perfil.tesoureiro-fatura-obrigacao-liquidacao-chave-propria' (($s11faA.code -eq 200) -and ($s11obA.code -eq 200) -and ($s11oA -gt 0) -and ($s11obAdb.text -eq '1400.00|REGISTRADA|NULL') -and ($s11liqA.code -eq 200) -and ($s11liqAdb.text -eq 'LIQUIDADA|SET') -and ($s11dmZ1.code -eq 403) -and ($s11dmZ2.code -eq 403)) ("TESOURARIA/FINANCEIRO (c2s11a.fin, somente chaves fatura.vincular/obrigacao.visualizar/obrigacao.liquidar/fornecimento.visualizar): vincular NFE conferida -> $($s11faA.code); gerar obrigacao -> $($s11obA.code) id=$s11oA banco valor|status|liq: $($s11obAdb.text) (14 un aprovadas x 100.00 = 1400.00, a unidade rejeitada fora — BR-7.7); LIQUIDAR -> $($s11liqA.code) banco: $($s11liqAdb.text) (terminal); o MESMO usuario CRIAR demanda -> $($s11dmZ1.code) e LISTAR demanda -> $($s11dmZ2.code) (403 fail-closed: a modality dele nao alcanca a moda administrativa, autorizacao 100% banco — BR-11.1 jornada por perfil)")

# --- estorno repetido: compensacao unica, estado terminal (BR-11.2) -------------
$s11es0 = Invoke-Api -Bearer $tkFin.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s11oA + '/estornar') -Base $ApiBase -Body '{"justificativa":""}'
$s11es1 = Invoke-Api -Bearer $tkFin.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s11oA + '/estornar') -Base $ApiBase -Body '{"justificativa":"compensacao por duplicidade c2s11a"}'
$s11es1db = Get-Psql ("select status||'|'||case when coalesce(justificativa_estorno,'NAO') like '%duplicidade%' then 'JUS' else 'NAO' end||'|'||coalesce(valor_liquidado::text,'NULL') from sigov.compras_obrigacao where id=" + $s11oA + ";")
$s11es2 = Invoke-Api -Bearer $tkFin.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s11oA + '/estornar') -Base $ApiBase -Body '{"justificativa":"segundo estorno c2s11a"}'
Add-Check 'S11' 'obrigacao.estorno-repetido-compensacao-unico-terminal' (($s11es0.code -eq 400) -and ($s11es0.body -like '*OBRIGACAO_ESTORNO_SEM_JUSTIFICATIVA*') -and ($s11es1.code -eq 200) -and ($s11es1db.text -eq 'ESTORNADA|JUS|1400.00') -and ($s11es2.code -eq 409) -and ($s11es2.body -like '*OBRIGACAO_JA_LIQUIDADA*')) ("ESTORNO REPETIDO (BR-11.2): estorno sem justificativa -> $($s11es0.code) (400 OBRIGACAO_ESTORNO_SEM_JUSTIFICATIVA nomeada); estorno por compensacao c/ justificativa -> $($s11es1.code); banco status|justificativa|valor-intacto: $($s11es1db.text) (append-only, sem delete fisico); 2o estorno sobre o estado terminal ESTORNADA -> $($s11es2.code) (409 OBRIGACAO_JA_LIQUIDADA nomeada: o estorno e unico, como o cancelamento/liquidacao — efeito nunca duplica)")

# --- jornada A encerra com destinacao em estoque (efeito no almoxarifado) -------
$s11ddA1 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s11rAid + ',"alvoTipo":"ESTOQUE","quantidade":2,"referencia":"D-C2S11A-01","localId":' + $s11locA + ',"materialId":' + $s11matA + '}')
$s11ddA1db = Get-Psql ("select count(*)::text||'|'||min(motivo)||'|'||min(to_char(saldo_antes,'FM9990.000'))||'|'||min(to_char(saldo_depois,'FM9990.000'))||'|'||min(documento_origem) from sigov.almoxarifado_movimentacao where tenant_id=910001 and almoxarifado_id=" + $s11locA + " and material_id=" + $s11matA + ";")
$s11ddA2 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/destinacoes' -Base $ApiBase -Body ('{"fornecimentoRecebimentoId":' + $s11rAid + ',"alvoTipo":"ESTOQUE","quantidade":2,"referencia":"D-C2S11A-01","localId":' + $s11locA + ',"materialId":' + $s11matA + '}')
$s11ddA2db = Get-Psql ("select count(*)::text from sigov.almoxarifado_movimentacao where tenant_id=910001 and almoxarifado_id=" + $s11locA + " and material_id=" + $s11matA + ";")
Add-Check 'S11' 'jornada-a.destino-estoque-movimento-unico-efetivo' (($s11ddA1.code -eq 200) -and ($s11ddA1db.text -eq ('1|FORNECIMENTO|0.000|2.000|FORN-' + $s11f1id + '-REC-' + $s11rAid)) -and ($s11ddA2.code -eq 200) -and ($s11ddA2db.text -eq '1')) ("a jornada municipal se FECHA com efeito real no almoxarifado: destinar 2 un do recebimento aprovado -> $($s11ddA1.code); banco movimento count|motivo|saldo-antes|saldo-depois|documento_origem: $($s11ddA1db.text) (referenciado FORN-<f>-REC-<r>); replay MESMA referencia -> $($s11ddA2.code) e o movimento permanece 1 (idempotente, BR-7.6 x BR-11.2)")

# --- perfil juridico (A): consulta tipada + parecer, sem acesso a compra --------
$s11cjA = Invoke-Api -Bearer $tkJur.token -Method POST -Path '/api/compras/integradas/juridico/consultas' -Base $ApiBase -Body ('{"processoComprasId":' + $s11procA + ',"tipoConsulta":"PARECER","referenciaNumero":"ATO-C2S11A-01","descricao":"Analisar regularidade da contratacao C2S11A","secretariaSetor":"Setor de Compras C2S11A","dataSolicitacao":"2026-10-14","solicitante":"Analista C2S11A","custoHonorario":100,"chaveIdempotencia":"K-CJ-A"}')
$s11cjAj = $null; try { $s11cjAj = ($s11cjA.body | ConvertFrom-Json) } catch {}
$s11cjAid = 0; if ($s11cjAj -and $null -ne $s11cjAj.data) { $s11cjAid = [long]$s11cjAj.data.consultaId }
$s11cjAdb = Get-Psql ("select cs.status||'|'||v.referencia_numero from sigov.juridico_consulta cs join sigov.compras_juridico_consulta_vinculo v on v.consulta_id=cs.id and v.tenant_id=cs.tenant_id where cs.id=" + $s11cjAid + ";")
$s11paAf = Invoke-Api -Bearer $tkJur.token -Method POST -Path ('/api/compras/integradas/juridico/consultas/' + $s11cjAid + '/parecer') -Base $ApiBase -Body '{"conclusao":"FAVORECIMENTO","resumo":"Contratacao C2S11A regular sob o aspecto analisado"}'
$s11paAdbb = Get-Psql ("select pr.status||'|'||cs.status from sigov.juridico_parecer pr join sigov.juridico_consulta cs on cs.id=pr.consulta_id and cs.tenant_id=pr.tenant_id where pr.consulta_id=" + $s11cjAid + " limit 1;")
$s11dmZ3 = Invoke-Api -Bearer $tkJur.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s11dAb
Add-Check 'S11' 'perfil.juridico-moda-juridica-sem-acesso-a-compra' (($s11cjA.code -eq 200) -and ($s11cjAid -gt 0) -and ($s11cjAdb.text -eq 'ABERTA|ATO-C2S11A-01') -and ($s11paAf.code -eq 200) -and ($s11paAdbb.text -eq 'EMITIDO|RESOLVIDA') -and ($s11dmZ3.code -eq 403)) ("JURIDICO (c2s11a.jur, somente juridico.consulta.criar + juridico.parecer.emitir): consulta PARECER x processo de contratacao $s11procA -> $($s11cjA.code) id=$s11cjAid banco status|ref-ledger: $($s11cjAdb.text); parecer FAVORECIMENTO emitido -> $($s11paAf.code) banco parecer|consulta: $($s11paAdbb.text) (jornada juridica completa dentro da propria moda); criar demanda -> $($s11dmZ3.code) (403 fail-closed: a chave da moda administrativa nao esta no perfil, BR-11.1)")

# --- jornada B (estadual): mesma cadeia no tenant 910002 ------------------------
$s11dBb = ('{"objeto":"C2S11B-Aquisicao de equipamentos para a unidade estadual","justificativa":"necessidade operacional homologacao c2s11b","itens":[{"descricao":"Equipamento C2S11B","quantidade":15,"unidade":"un","tipoObjeto":"BEM","precoEstimado":80,"centroCustoId":' + $s11ccB + '}],"urgencia":false}')
$s11dB = Invoke-Api -Bearer $tkB.token -Method POST -Path '/api/compras/integradas/demandas' -Base $ApiBase -Body $s11dBb
$s11dBj = $null; try { $s11dBj = ($s11dB.body | ConvertFrom-Json) } catch {}
$s11dBid = 0; if ($s11dBj -and $null -ne $s11dBj.data) { $s11dBid = [long]$s11dBj.data.id }
$s11dBdb = Get-Psql ("select status||'|'||to_char(valor_estimado,'FM999990.00') from sigov.compras_demanda where id=" + $s11dBid + ";")
$null = Get-Psql ("update sigov.compras_demanda set created_by=" + $s11gestorB + " where id=" + $s11dBid + ";")
$s11apB = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/demandas/' + $s11dBid + '/aprovar') -Base $ApiBase
$s11apBdb = Get-Psql ("select min(id)||'|'||count(*)||'|'||min(status)||'|'||case when min(numero) like 'PI-%' then 'PI' else 'NAO' end from sigov.compras_processo where tenant_id=910002 and demanda_id=" + $s11dBid + ";")
$s11apBp = if ($s11apBdb.code -eq 0 -and $s11apBdb.text -ne '') { @($s11apBdb.text -split '\|') } else { @('0','0','NAO','NAO') }
$s11procB = [long]$s11apBp[0]
$s11dpB1 = Invoke-Api -Bearer $tkB.token -Method POST -Path '/api/compras/integradas/procedimentos/abrir' -Base $ApiBase -Body ('{"demandaId":' + $s11dBid + ',"modalidadeCodigo":"DISPENSA","dispensaBaseLegalCodigo":"ART_75_I"}')
$s11dpB1j = $null; try { $s11dpB1j = ($s11dpB1.body | ConvertFrom-Json) } catch {}
$s11licB = 0
if ($s11dpB1j -and $null -ne $s11dpB1j.data) { $s11licB = [long]$s11dpB1j.data.licitacaoId }
$s11liB = Get-Psql ("insert into sigov.compras_licitacao_item(tenant_id,entidade_id,licitacao_id,numero,descricao,quantidade,unidade,valor_estimado,created_at,updated_at,created_by,updated_by) select 910002," + $s11entB + "," + $s11licB + ",1,'Equipamento C2S11B',15,'un',80.00,now(),now(),910301,910301 where not exists(select 1 from sigov.compras_licitacao_item i where i.licitacao_id=" + $s11licB + "); select count(*) from sigov.compras_licitacao_item where licitacao_id=" + $s11licB + ";")
$s11daB1 = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s11licB + '/adjudicar') -Base $ApiBase -Body ('{"fornecedorId":' + $s11fornB + ',"observacao":"contratacao direta c2s11b"}')
$s11daB1j = $null; try { $s11daB1j = ($s11daB1.body | ConvertFrom-Json) } catch {}
$null = Get-Psql ("update sigov.compras_adjudicacao set responsavel_id=" + $s11gestorB + " where tenant_id=910002 and licitacao_id=" + $s11licB + ";")
$s11hoB = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/licitacoes/' + $s11licB + '/homologar') -Base $ApiBase
$s11hoBj = $null; try { $s11hoBj = ($s11hoB.body | ConvertFrom-Json) } catch {}
$s11hoBid = 0; if ($s11hoBj -and $null -ne $s11hoBj.data) { $s11hoBid = [long]$s11hoBj.data.id }
$s11ctBb = ('{"homologacaoId":' + $s11hoBid + ',"dataEficacia":"2026-11-10","dataFim":"2027-11-09","assinaturaModo":"FISICA","gestor":"Gestor C2S11B","fiscal":"Fiscal C2S11B"}')
$s11ctB = Invoke-Api -Bearer $tkB.token -Method POST -Path '/api/compras/integradas/contratos' -Base $ApiBase -Body $s11ctBb
$s11ctBj = $null; try { $s11ctBj = ($s11ctB.body | ConvertFrom-Json) } catch {}
$s11ctBid = 0; if ($s11ctBj -and $null -ne $s11ctBj.data) { $s11ctBid = [long]$s11ctBj.data.contratoId }
$s11ctBdb = Get-Psql ("select status||'|'||to_char(valor_contratado,'FM999990.00') from sigov.compras_contrato where id=" + $s11ctBid + ";")
$s11itBr = Get-Psql ("select id::text from sigov.compras_contrato_item where contrato_id=" + $s11ctBid + " limit 1;")
$s11itB = 0; if ($s11itBr.code -eq 0 -and $s11itBr.text -ne '') { $s11itB = [long]$s11itBr.text.Trim() }
$s11fBb = ('{"contratoId":' + $s11ctBid + ',"contratoItemId":' + $s11itB + ',"quantidade":15,"chaveIdempotencia":"K-C2S11B-F1"}')
$s11fB = Invoke-Api -Bearer $tkB.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11fBb
$s11fBj = $null; try { $s11fBj = ($s11fB.body | ConvertFrom-Json) } catch {}
$s11fBid = 0; if ($s11fBj -and $null -ne $s11fBj.data) { $s11fBid = [long]$s11fBj.data.id }
$s11fBr = Invoke-Api -Bearer $tkB.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11fBb
$s11fBrj = $null; try { $s11fBrj = ($s11fBr.body | ConvertFrom-Json) } catch {}
$s11fBrid = 0; if ($s11fBrj -and $null -ne $s11fBrj.data) { $s11fBrid = [long]$s11fBrj.data.id }
Add-Check 'S11' 'jornada-b.estadual-mesma-cadeia-outro-tenant-outro-contexto' (($s11dB.code -eq 200) -and ($s11dBid -gt 0) -and ($s11dBdb.text -eq 'SOLICITADA|1200.00') -and ($s11apB.code -eq 200) -and ($s11procB -gt 0) -and ($s11apBp[1] -eq '1') -and ($s11apBp[2] -eq 'ABERTO') -and ($s11apBp[3] -eq 'PI') -and ($s11dpB1.code -eq 200) -and ([long]$s11dpB1j.data.processoId -eq $s11procB) -and ($s11liB.text -eq '1') -and ($s11daB1.code -eq 200) -and ([decimal]$s11daB1j.data.valorTotal -eq 1200) -and ($s11hoB.code -eq 200) -and ($s11hoBid -gt 0) -and ($s11ctB.code -eq 200) -and ($s11ctBid -gt 0) -and ($s11ctBdb.text -eq 'VIGENTE|1200.00') -and ($s11itB -gt 0) -and ($s11fB.code -eq 200) -and ($s11fBid -gt 0) -and ($s11fBr.code -eq 200) -and ($s11fBrid -eq $s11fBid)) ("ESFERA ESTADUAL (tenant 910002, grupo/perfil/administracao PROPRIOS do tenant): MESMA cadeia vertical executada de ponta a ponta — demanda 15 un x 80.00 -> $($s11dB.code) banco SOLICITADA|1200.00; aprovar -> processo $s11procB $($s11apBp[1..3] -join '|'); dispensa ART_75_I 200; adjudicacao c/ fornecedor estadual -> $($s11daB1.code) valor 1200.00; homologar -> $($s11hoB.code) id=$s11hoBid; contrato -> $($s11ctB.code) id=$s11ctBid banco: $($s11ctBdb.text) item=$s11itB; fornecimento 15 un -> $($s11fB.code) id=$s11fBid; REENVIO IDEMPOTENTE no 2o tenant: replay -> $($s11fBr.code) mesmo id (nenhuma etapa depende do tenant 910001 — regra 21: nada municipal hardcoded)")

# --- jornada B: recebimento c/ rejeicao NOVA esfera -> prazo DERIVADO do override --
$s11rB = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s11fBid + '/recebimentos') -Base $ApiBase -Body '{"quantidadeAprovada":13,"quantidadeRejeitada":2,"dataRecebimento":"2026-11-12","motivoRejeicao":"embalagem avariada c2s11b"}'
$s11rBj = $null; try { $s11rBj = ($s11rB.body | ConvertFrom-Json) } catch {}
$s11rBid = 0; if ($s11rBj -and $null -ne $s11rBj.data) { $s11rBid = [long]$s11rBj.data.id }
$s11rBdb = Get-Psql ("select r.status||'|'||to_char(r.quantidade_aprovada,'FM9990.000')||'|'||to_char(r.quantidade_rejeitada,'FM9990.000')||'|'||to_char(r.data_prazo_substituicao,'YYYY-MM-DD') from sigov.compras_fornecimento_recebimento r where r.id=" + $s11rBid + ";")
$s11dfB0 = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/recebimentos/' + $s11rBid + '/desfecho') -Base $ApiBase -Body '{"desfecho":"SUBSTITUIDO","justificativa":"fornecedor subira as unidades ate o prazo parametrico estadual"}'
$s11dfBdb = Get-Psql ("select f.status||'|'||coalesce(r.desfecho,'NULL') from sigov.compras_fornecimento f, sigov.compras_fornecimento_recebimento r where f.id=" + $s11fBid + " and r.id=" + $s11rBid + ";")
$s11faB = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/fornecimentos/' + $s11fBid + '/fatura') -Base $ApiBase -Body '{"numero":"NF-C2S11B-001","natureza":"NFE","serie":"1","cnpjFornecedor":"99.999.000/0007-07","valor":1200,"dataEmissao":"2026-11-15"}'
$s11obB = Invoke-Api -Bearer $tkB.token -Method POST -Path '/api/compras/integradas/obrigacoes/gerar' -Base $ApiBase -Body ('{"fornecimentoId":' + $s11fBid + ',"referenciaIdempotencia":"OB-C2S11B-F1"}')
$s11obBj = $null; try { $s11obBj = ($s11obB.body | ConvertFrom-Json) } catch {}
$s11oB = 0; if ($s11obBj -and $null -ne $s11obBj.data) { $s11oB = [long]$s11obBj.data.id }
$s11obBdb = Get-Psql ("select to_char(valor_liquidado,'FM99990.00')||'|'||status from sigov.compras_obrigacao where id=" + $s11oB + ";")
$s11liqB = Invoke-Api -Bearer $tkB.token -Method POST -Path ('/api/compras/integradas/obrigacoes/' + $s11oB + '/liquidar') -Base $ApiBase
$s11liqBdb = Get-Psql ("select status from sigov.compras_obrigacao where id=" + $s11oB + ";")
Add-Check 'S11' 'jornada-b.recebimento-rejeicao-prazo-do-override-estadual' (($s11rB.code -eq 200) -and ($s11rBid -gt 0) -and ($s11rBdb.text -eq 'PARCIAL|13.000|2.000|2026-11-26') -and ($s11dfB0.code -eq 200) -and ($s11dfBdb.text -eq 'CONCLUIDO|SUBSTITUIDO') -and ($s11faB.code -eq 200) -and ($s11obB.code -eq 200) -and ($s11oB -gt 0) -and ($s11obBdb.text -eq '1040.00|REGISTRADA') -and ($s11liqB.code -eq 200) -and ($s11liqBdb.text -eq 'LIQUIDADA')) ("recebimento aprov 13 + reje 2 NA ESFERA ESTADUAL em 2026-11-12 -> $($s11rB.code) id=$s11rBid; banco status|aprov|reje|prazo: $($s11rBdb.text) (prazo = 2026-11-12 + RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS=10 dias uteis do OVERRIDE do tenant 910002 = 2026-11-26 — mesmo ato, derivacao diferente); desfecho SUBSTITUIDO -> $($s11dfB0.code) banco: $($s11dfBdb.text); fatura NFE conferida -> $($s11faB.code); obrigacao -> $($s11obB.code) banco: $($s11obBdb.text) (13 x 80.00 = 1040.00); liquidar -> $($s11liqB.code) banco: $($s11liqBdb.text)")
$s11prazoB = 'NAO'
if ($s11rBdb.text -match '\|([^|]+)$') { $s11prazoB = $Matches[1] }

# --- regra 21/24: a parametrizacao por esfera e OBSERVAVEL nos dados ------------
$s11r21 = Get-Psql "select coalesce((select esfera_governo from sigov.entidade where tenant_id=910001 and codigo_externo='C2S11A-ENT'),'NULL')||'|'||coalesce((select esfera_governo from sigov.entidade where tenant_id=910002 and codigo_externo='C2S11B-ENT'),'NULL')||'|'||coalesce((select val.valor::text from sigov.parametro_modulo_valor val join sigov.parametro_modulo pm on pm.id=val.parametro_id where val.tenant_id=910001 and pm.modulo='COMPRAS' and pm.codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and not val.is_deleted limit 1),'PADRAO')||'|'||coalesce((select val.valor::text from sigov.parametro_modulo_valor val join sigov.parametro_modulo pm on pm.id=val.parametro_id where val.tenant_id=910002 and pm.modulo='COMPRAS' and pm.codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and not val.is_deleted limit 1),(select valor_padrao::text from sigov.parametro_modulo where modulo='COMPRAS' and codigo='RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS' and ativo and not is_deleted))"
Add-Check 'S11' 'regra21.mesmo-ato-duas-esferas-parametrizacao-observavel-no-banco' (($s11prazoA -eq '2026-11-19') -and ($s11prazoB -eq '2026-11-26') -and ($s11r21.code -eq 0) -and ($s11r21.text -eq 'municipal|estadual|PADRAO|10')) ("PROVA DA REGRA 21 NOS DADOS: o MESMO ato (recebimento com rejeicao em 2026-11-12) produz prazos DIFERENTES por tenant/esfera — municipal (910001, padrao do banco 5 dias uteis) -> prazo $s11prazoA; estadual (910002, override 10 dias uteis em parametro_modulo_valor) -> prazo $s11prazoB. Banco NOVO no ponto da assercao esferaA|esferaB|param-efetivo-A|param-efetivo-B: $($s11r21.text); sql-err-tail=$($s11r21.text.Substring([Math]::Max(0,$s11r21.text.Length-200))) — a diferenca vem inteiramente da parametrizacao lida do banco (regra 11), sem comportamento municipal/estadual hardcoded (regras 21/24); a jornada idêntica rodou nas duas esferas com os mesmos endpoints")

# --- isolamento cross-tenant: visao, escopo e dados (BR-11.1) -------------------
$s11lstA = Invoke-Api -Bearer $tkA.token -Method GET -Path '/api/compras/integradas/demandas' -Base $ApiBase
$s11xb = Invoke-Api -Bearer $tkA.token -Method GET -Path ('/api/compras/integradas/demandas/' + $s11dBid) -Base $ApiBase
$s11xldb = Get-Psql ("select (select count(*) from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S11B-%') + (select count(*) from sigov.compras_licitacao where tenant_id=910001 and objeto like 'C2S11B-%') + (select count(*) from sigov.compras_contrato where tenant_id=910001 and objeto like 'C2S11B-%') + (select count(*) from sigov.compras_demanda where tenant_id=910002 and objeto like 'C2S11A-%') + (select count(*) from sigov.compras_licitacao where tenant_id=910002 and objeto like 'C2S11A-%') + (select count(*) from sigov.compras_contrato where tenant_id=910002 and objeto like 'C2S11A-%');")
Add-Check 'S11' 'isolamento.cross-tenant-visao-escopo-e-dados' (($s11lstA.code -eq 200) -and ($s11lstA.body -like '*C2S11A-*') -and ($s11lstA.body -notlike '*C2S11B-*') -and ($s11xb.code -eq 404) -and ($s11xb.body -like '*DEMANDA_INEXISTENTE*') -and ($s11xldb.text -eq '0')) ("GET /demandas como admin municipal -> $($s11lstA.code) vendo SOMENTE os marcadores C2S11A- (nenhum C2S11B- presente na pagina); a demanda $s11dBid criada no tenant estadual vista com o bearer municipal -> $($s11xb.code) (404 DEMANDA_INEXISTENTE nomeada: escopo tenant antes da busca); banco cruzado (linhas C2S11B em 910001 + linhas C2S11A em 910002 em demanda/licitacao/contrato): $($s11xldb.text) — separacao total dos dados entre os dois tenants de esferas diferentes (BR-11.1)")

# --- permissao ausente: fail-closed em ambos os tenants (BR-11.2) ---------------
$s11z1 = Invoke-Api -Bearer $tkSemA.token -Method GET -Path '/api/compras/integradas/demandas' -Base $ApiBase
$s11z2 = Invoke-Api -Bearer $tkSemA.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11parb
$s11z3 = Invoke-Api -Bearer $tkSemB.token -Method GET -Path ('/api/compras/integradas/demandas/' + $s11dBid) -Base $ApiBase
Add-Check 'S11' 'permissao.ausente-fail-closed-em-ambas-as-esferas' (($s11z1.code -eq 403) -and ($s11z2.code -eq 403) -and ($s11z3.code -eq 403)) ("usuario SEM permissao na frente (grupo SemFrente c/ 1 chave inofensiva FORA da frente — login viavel, acesso a frente ausente): c2s11a.semperm listar demandas -> $($s11z1.code) e criar fornecimento -> $($s11z2.code); c2s11b.semperm ler a propria demanda do tenant estadual -> $($s11z3.code) (403 fail-closed em todas: a avaliacao de autorizacao e anterior a qualquer acesso a dado, em ambas as esferas — banco decide, nada default-permitir; BR-11.2 'permissao ausente')")

# --- schema ausente: 404 nomeada identica nos caminhos do fornecimento (BR-11.2) --
$s11rn1 = Get-Psql "alter table sigov.compras_fornecimento rename to _s11_probe_compras_fornecimento;"
$s11ns1 = Invoke-Api -Bearer $tkA.token -Method GET -Path ('/api/compras/integradas/fornecimentos/' + $s11f1id) -Base $ApiBase
$s11ns2 = Invoke-Api -Bearer $tkA.token -Method POST -Path '/api/compras/integradas/fornecimentos' -Base $ApiBase -Body $s11parb
$s11rn2 = Get-Psql "alter table sigov._s11_probe_compras_fornecimento rename to compras_fornecimento;"
$s11ns3 = Invoke-Api -Bearer $tkA.token -Method GET -Path ('/api/compras/integradas/fornecimentos/' + $s11f1id) -Base $ApiBase
Add-Check 'S11' 'schema.ausente-nomeada-nos-caminhos-e-recuperacao' (($s11rn1.code -eq 0) -and ($s11ns1.code -eq 404) -and ($s11ns1.body -like '*COMPRAS_INTEGRADAS_NAO_DISPONIVEL*') -and ($s11ns1.body -like '*schema ausente*') -and ($s11ns2.code -eq 404) -and ($s11ns2.body -like '*COMPRAS_INTEGRADAS_NAO_DISPONIVEL*') -and ($s11rn2.code -eq 0) -and ($s11ns3.code -eq 200)) ("com sigov.compras_fornecimento RENOMEADA: GET fornecimento -> $($s11ns1.code) e POST fornecimento -> $($s11ns2.code) (ambos 404 COMPRAS_INTEGRADAS_NAO_DISPONIVEL nomeada c/ a tabela citada — falha EXPLICITA, sem lista vazia/erros genericos, regra 13; BR-11.2 'schema ausente' na superficie nova); restaurada -> GET -> $($s11ns3.code) (recuperacao imediata, tabela intacta)")

# --- limpeza do bloco S11 (auto-limpeza; regra 9) --------------------------------
$s11cl = Get-Psql @"
create temp table if not exists s11_alvo_d (id bigint); truncate table s11_alvo_d;
insert into s11_alvo_d select id from sigov.compras_demanda where (tenant_id=910001 and objeto like 'C2S11A-%') or (tenant_id=910002 and objeto like 'C2S11B-%');
create temp table if not exists s11_alvo_p (id bigint); truncate table s11_alvo_p;
insert into s11_alvo_p select id from sigov.compras_processo where demanda_id in (select id from s11_alvo_d);
create temp table if not exists s11_alvo_l (id bigint); truncate table s11_alvo_l;
insert into s11_alvo_l select id from sigov.compras_licitacao where processo_id in (select id from s11_alvo_p);
create temp table if not exists s11_alvo_c (id bigint); truncate table s11_alvo_c;
insert into s11_alvo_c select id from sigov.compras_contrato where homologacao_id in (select id from sigov.compras_homologacao where licitacao_id in (select id from s11_alvo_l));
create temp table if not exists s11_alvo_f (id bigint); truncate table s11_alvo_f;
insert into s11_alvo_f select id from sigov.compras_fornecimento where contrato_id in (select id from s11_alvo_c);
delete from sigov.compras_destinacao_integrada where tenant_id in (910001,910002) and fornecimento_recebimento_id in (select id from sigov.compras_fornecimento_recebimento where fornecimento_id in (select id from s11_alvo_f));
delete from sigov.almoxarifado_movimentacao where tenant_id=910001 and documento_origem like 'FORN-%';
delete from sigov.compras_fatura_integrada where tenant_id in (910001,910002) and fornecimento_id in (select id from s11_alvo_f);
delete from sigov.compras_obrigacao where tenant_id in (910001,910002) and fornecimento_id in (select id from s11_alvo_f);
delete from sigov.compras_recebimento_item where recebimento_id in (select id from sigov.compras_recebimento where tenant_id in (910001,910002) and fornecimento_id in (select id from s11_alvo_f));
delete from sigov.compras_recebimento where tenant_id in (910001,910002) and fornecimento_id in (select id from s11_alvo_f);
delete from sigov.compras_fornecimento_recebimento where fornecimento_id in (select id from s11_alvo_f);
delete from sigov.compras_fornecimento where id in (select id from s11_alvo_f);
delete from sigov.compras_contrato_item where contrato_id in (select id from s11_alvo_c);
delete from sigov.compras_contrato_historico where contrato_id in (select id from s11_alvo_c);
delete from sigov.compras_contrato where id in (select id from s11_alvo_c);
delete from sigov.compras_homologacao where licitacao_id in (select id from s11_alvo_l);
delete from sigov.compras_adjudicacao_item where adjudicacao_id in (select id from sigov.compras_adjudicacao where licitacao_id in (select id from s11_alvo_l));
delete from sigov.compras_adjudicacao where licitacao_id in (select id from s11_alvo_l);
delete from sigov.compras_proposta where licitacao_id in (select id from s11_alvo_l);
delete from sigov.compras_habilitacao where licitacao_id in (select id from s11_alvo_l);
delete from sigov.compras_lance where licitacao_id in (select id from s11_alvo_l);
delete from sigov.compras_licitacao_item where licitacao_id in (select id from s11_alvo_l);
delete from sigov.compras_licitacao where id in (select id from s11_alvo_l);
delete from sigov.compras_processo_item where tenant_id in (910001,910002) and processo_id in (select id from s11_alvo_p);
delete from sigov.compras_processo where id in (select id from s11_alvo_p);
delete from sigov.compras_demanda_item where demanda_id in (select id from s11_alvo_d);
delete from sigov.compras_demanda where id in (select id from s11_alvo_d);
delete from sigov.juridico_parecer_condicao where parecer_id in (select id from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%'));
delete from sigov.juridico_parecer where tenant_id=910001 and consulta_id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%');
delete from sigov.juridico_consulta where id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%');
delete from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%';
delete from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S11A %';
delete from sigov.compras_fornecedor where tenant_id=910002 and nome like 'Forneadora C2S11B %';
delete from sigov.financeiro_centro_custo where codigo in ('C2S11A-CC1','C2S11B-CC1');
delete from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S11A-MAT';
delete from sigov.almoxarifado_local where tenant_id=910001 and codigo like 'C2S11A-LOC%';
delete from sigov.parametro_modulo_valor where tenant_id=910002 and correlation_id='c2s11-prazo-override-e2e';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000011','cf58c2ab-1c20-4a90-88f8-0c2000000012','cf58c2ab-1c20-4a90-88f8-0c2000000013','cf58c2ab-1c20-4a90-88f8-0c2000000014','cf58c2ab-1c20-4a90-88f8-0c2000000015','cf58c2ab-1c20-4a90-88f8-0c2000000016');
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'));
delete from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%');
delete from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo in ('C2S11A-FIN','C2S11A-JUR','C2S11A-SEMP')) or (tenant_id=910002 and codigo_externo in ('C2S11B-ADM','C2S11B-SEMP')));
delete from sigov.perfil_acesso where (tenant_id=910001 and nome in ('C2S11A Perfil Tesouraria','C2S11A Perfil Juridico','C2S11A Perfil SemFrente')) or (tenant_id=910002 and nome in ('C2S11B Perfil Administracao','C2S11B Perfil SemFrente'));
delete from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo in ('C2S11A-FIN','C2S11A-JUR','C2S11A-SEMP')) or (tenant_id=910002 and codigo_externo in ('C2S11B-ADM','C2S11B-SEMP'));
delete from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S11A-ENT') or (tenant_id=910002 and codigo_externo='C2S11B-ENT');
select
 (select count(*) from sigov.almoxarifado_movimentacao where tenant_id=910001 and documento_origem like 'FORN-%')
 + (select count(*) from sigov.almoxarifado_material where tenant_id=910001 and codigo='C2S11A-MAT')
 + (select count(*) from sigov.almoxarifado_local where tenant_id=910001 and codigo like 'C2S11A-LOC%')
 + (select count(*) from sigov.compras_fornecedor where (tenant_id=910001 and nome like 'Forneadora C2S11A %') or (tenant_id=910002 and nome like 'Forneadora C2S11B %'))
 + (select count(*) from sigov.financeiro_centro_custo where codigo in ('C2S11A-CC1','C2S11B-CC1'))
 + (select count(*) from sigov.compras_demanda where (tenant_id=910001 and objeto like 'C2S11A-%') or (tenant_id=910002 and objeto like 'C2S11B-%'))
 + (select count(*) from sigov.compras_licitacao where (tenant_id=910001 and objeto like 'C2S11A-%') or (tenant_id=910002 and objeto like 'C2S11B-%'))
 + (select count(*) from sigov.compras_contrato where (tenant_id=910001 and objeto like 'C2S11A-%') or (tenant_id=910002 and objeto like 'C2S11B-%'))
 + (select count(*) from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%')
 + (select count(*) from sigov.juridico_consulta where id in (select consulta_id from sigov.compras_juridico_consulta_vinculo where tenant_id=910001 and referencia_numero like 'ATO-C2S11A-%'))
 + (select count(*) from sigov.parametro_modulo_valor where tenant_id=910002 and correlation_id='c2s11-prazo-override-e2e')
 + (select count(*) from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000011','cf58c2ab-1c20-4a90-88f8-0c2000000012','cf58c2ab-1c20-4a90-88f8-0c2000000013','cf58c2ab-1c20-4a90-88f8-0c2000000014'))
 + (select count(*) from sigov.usuario where (tenant_id=910001 and login like 'c2s11a.%') or (tenant_id=910002 and login like 'c2s11b.%'))
 + (select count(*) from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo in ('C2S11A-FIN','C2S11A-JUR')) or (tenant_id=910002 and codigo_externo='C2S11B-ADM')))
 + (select count(*) from sigov.perfil_acesso where (tenant_id=910001 and nome in ('C2S11A Perfil Tesouraria','C2S11A Perfil Juridico')) or (tenant_id=910002 and nome='C2S11B Perfil Administracao'))
 + (select count(*) from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo in ('C2S11A-FIN','C2S11A-JUR')) or (tenant_id=910002 and codigo_externo='C2S11B-ADM'))
 + (select count(*) from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S11A-ENT') or (tenant_id=910002 and codigo_externo='C2S11B-ENT'));
"@
Add-Check 'S11' 'fixture.removido-auto-limpeza-restantes-zero' (($s11cl.code -eq 0) -and ($s11cl.text -eq '0')) ("cascade delete S11 nos DOIS tenants (destinacoes, movimentos FORN-%, faturas, obrigacoes, espelho legado, recebimentos integrados, fornecimentos, contratos+items+historico, homologacoes, adjudicacoes+items, propostas, habilitacoes, lances, licitacao_items, licitacoes, processos+items, demandas+items, parecer condicoes/pareceres/consultas+ledger juridico, fornecedores, CCs, materiais, locais, override do parametro do tenant B, sessoes/auditorias dos 8 usuarios, grants sentinel cf58c2ab-...011/012/013/014, usuarios c2s11a.*/c2s11b.*, grupos+perfis dedicados C2S11, entidades C2S11A-ENT/C2S11B-ENT) -> restantes=" + $s11cl.text)

} # fim bloco E1..S11 (pulado com -OnlyS12)

# =============================================================================
# SPRINT 12 (C2S12) - Folha e Previdencia Publica multi-esfera (BR-12.1..BR-12.8)
# =============================================================================

if (-not $OnlyS13) {
# E2E HTTP real (Web :5099 -> ApiToken -> bearer na Api :5100 -> Dapper -> PG16):
#   BR-12.1 cadastro cargo/servidor/vinculo (matricula unica por tenant, 1 vinculo
#           ATIVO, esfera herdada da entidade do contexto - regra 21/22)
#   BR-12.2 fechamento deterministico idempotente (faixas INSS LC 51/84 com teto e
#           coeficiente de reducao, IRRF progressivo Decreto 3.048/99 por ano-base,
#           teto constitucional por esfera CF/88 art. 37 XV/XVI c/c EC 103/19)
#   BR-12.3 reprocesso versionado (nova versao VETA a anterior; ambas auditaveis)
#   BR-12.4 ferias (ciclo aquisitivo 30d/12m, aviso previo, fracao minima, abono
#           minimo, fracionamento maximo, terco 1/3 auditavel - Lei 8.112/90/CLT)
#           e afastamentos (maternidade/adocao 120d, paternidade 5d, GED obrigatoria,
#           sobreposto, devolucao condicional) - prazos parametrizados no banco
#   BR-12.5 decimo salario (Lei 4.749/65 c/c Lei 12.735/12): 1a parcela no mes
#           parametrizado (11/30), 2a = total anual - p1; 409 DECIMO_SEM_PARCELA1
#   BR-12.6 parametros com override por tenant (banco = autoridade - regra 11/12)
#   BR-12.7 contracheque versionado (LGPD: dados minimos, sem CPF)
#   BR-12.8 eSocial publico (Portaria MTP 672/19): lote unico por competencia x tipo
#           (idempotente), validacao previa deterministica, BLOQUEADO_VALIDACAO
#           sem sucesso simulado
# Valores travados (data fixa = dia 20 do mes; faixas RGPS 2026 e IRRF 2026 do banco):
#   A (bruto 10.000, municipal, adm 2026-01-01):
#     set 10000.00 -> inss 1078.14 / irrf 900.07 / liq 8021.79
#     nov 19166.67 -> 2361.48 / 3021.27 / 13783.92 (decimo p1 9166.67 = 11 meses)
#     dez 10833.33 -> 1194.81 / 1061.32 / 8577.20 (p2 833.33; ENCERRADO nFinal 12)
#   B (bruto 3.000, municipal, adm 2023-01-10, 3 ciclos aquisitivos = 90 dias):
#     set 3000.00 -> 253.41 / 0.00 / 2746.59; nov 5750.00 -> 614.59 / 178.41 /
#     4957.00 (p1 2750.00); dez 3250.00 -> 283.41 / 0.00 / 2966.59 (p2 250.00)
#   D (bruto 5.000, estadual 910002): set 509.59 / 109.22 / 4381.19;
#     nov p1 4583.33; dez 5416.67 -> 567.93 / 135.41 / 4713.33 (p2 416.67)
#   AB (bruto 2.000, estadual, nome < 5): set 157.23 / 0.00 / 1842.77;
#     nov p1 1833.33; dez 2166.67 -> 172.23 / 0.00 / 1994.44 (p2 166.67);
#     evento S-1005 com erro -> lote BLOQUEADO_VALIDACAO
#   C (bruto 200.000, estadual): acima do teto 44161.63 -> 409 SERVIDOR_ACIMA_TETO
#     (folha permanece ABERTA; apos desligamento de C o fechamento segue)

function Get-S12Data { param([string]$Body) try { return ($Body | ConvertFrom-Json).data } catch { return $null } }
function Get-S12Msg { param([string]$Body) try { $j = $Body | ConvertFrom-Json; if ($null -ne $j.message) { return [string]$j.message } } catch {}; return [string]$Body }
function Get-S12Near { param([object]$V,[object]$T) try { $dV = [decimal]([string]$V).Trim().TrimEnd('m'); $dT = [decimal]([string]$T).Trim().TrimEnd('m'); return ([math]::Abs([double]$dV - [double]$dT) -lt 0.005) } catch { return $false } }

$s12fix = Get-Psql @"
-- pre-clean reentravel: remove estado de rodada anterior abortada (escopo igual ao da limpeza final do bloco S12; tenants 910001/910002 sao do driver neste ciclo)
delete from sigov.previdencia_folha_calculo_item where calculo_id in (select id from sigov.previdencia_folha_calculo where tenant_id in (910001,910002));
delete from sigov.previdencia_folha_calculo where tenant_id in (910001,910002);
delete from sigov.previdencia_remun_item where vinculo_id in (select id from sigov.previdencia_vinculo where tenant_id in (910001,910002));
delete from sigov.previdencia_ferias_programacao where ferias_id in (select id from sigov.previdencia_ferias where tenant_id in (910001,910002));
delete from sigov.previdencia_esocial_evento where lote_id in (select id from sigov.previdencia_esocial_lote where tenant_id in (910001,910002));
delete from sigov.previdencia_decimo where tenant_id in (910001,910002);
delete from sigov.previdencia_afastamento where tenant_id in (910001,910002);
delete from sigov.previdencia_esocial_lote where tenant_id in (910001,910002);
delete from sigov.previdencia_folha_competencia where tenant_id in (910001,910002);
delete from sigov.previdencia_ferias where tenant_id in (910001,910002);
delete from sigov.previdencia_vinculo where tenant_id in (910001,910002);
delete from sigov.previdencia_servidor where tenant_id in (910001,910002);
delete from sigov.previdencia_cargo where tenant_id in (910001,910002);
delete from sigov.parametro_modulo_valor where correlation_id='c2s12-frac-override-e2e';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000021','cf58c2ab-1c20-4a90-88f8-0c2000000022','cf58c2ab-1c20-4a90-88f8-0c2000000023','cf58c2ab-1c20-4a90-88f8-0c2000000024');
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%');
delete from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S12A-SEMP') or (tenant_id=910002 and codigo_externo in ('C2S12B-ADM','C2S12B-SEMP')));
delete from sigov.perfil_acesso where (tenant_id=910001 and nome='C2S12A Perfil SemFrente') or (tenant_id=910002 and nome in ('C2S12B Perfil Administracao','C2S12B Perfil SemFrente'));
delete from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S12A-SEMP') or (tenant_id=910002 and codigo_externo in ('C2S12B-ADM','C2S12B-SEMP'));
delete from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S12A-ENT') or (tenant_id=910002 and codigo_externo='C2S12B-ENT');
-- Entidades com esfera (regra 21/22): A municipal / B estadual - o contexto do usuario define a esfera do motor
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910001,'C2S12A Entidade Municipal','00.000.000/0002-01','C2S12A-ENT','municipal','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910001 and codigo_externo='C2S12A-ENT');
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910002,'C2S12B Entidade Estadual','00.000.000/0002-02','C2S12B-ENT','estadual','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910002 and codigo_externo='C2S12B-ENT');
-- Grupos/perfis dedicados (admin A reutiliza o grupo 910401 existente do tenant)
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910001,'C2S12A Grupo SemFrente','C2S12A-SEMP',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910001 and codigo_externo='C2S12A-SEMP');
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910002,'C2S12B Grupo Administracao','C2S12B-ADM',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910002 and codigo_externo='C2S12B-ADM');
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910002,'C2S12B Grupo SemFrente','C2S12B-SEMP',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910002 and codigo_externo='C2S12B-SEMP');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910001,'C2S12A Perfil SemFrente',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910001 and nome='C2S12A Perfil SemFrente');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910002,'C2S12B Perfil Administracao',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910002 and nome='C2S12B Perfil Administracao');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910002,'C2S12B Perfil SemFrente',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910002 and nome='C2S12B Perfil SemFrente');
insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,tenant_id,ativo,is_deleted,created_at,created_by)
select g.id,p.id,g.tenant_id,true,false,now(),910301
from sigov.grupo_acesso g cross join sigov.perfil_acesso p
where ((g.tenant_id=910001 and g.codigo_externo='C2S12A-SEMP' and p.tenant_id=910001 and p.nome='C2S12A Perfil SemFrente')
   or (g.tenant_id=910002 and g.codigo_externo='C2S12B-ADM' and p.tenant_id=910002 and p.nome='C2S12B Perfil Administracao')
   or (g.tenant_id=910002 and g.codigo_externo='C2S12B-SEMP' and p.tenant_id=910002 and p.nome='C2S12B Perfil SemFrente'))
and not exists(select 1 from sigov.grupo_perfil gp where gp.grupo_acesso_id=g.id and gp.perfil_acesso_id=p.id and gp.ativo);
-- Usuarios (senha_hash copiado do admin 910301; contexto institucional = entidade do usuario)
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S12A Administrador','c2s12a.admin','c2s12a.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S12A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s12a.admin');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S12A Sem Frente','c2s12a.semperm','c2s12a.semperm@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S12A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s12a.semperm');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910002,e.id,1,'C2S12B Administrador','c2s12b.admin','c2s12b.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910002 and e.codigo_externo='C2S12B-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910002 and x.login='c2s12b.admin');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910002,e.id,1,'C2S12B Sem Frente','c2s12b.semperm','c2s12b.semperm@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910002 and e.codigo_externo='C2S12B-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910002 and x.login='c2s12b.semperm');
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,910401,910001,true,now(),910301
from sigov.usuario u
where u.tenant_id=910001 and u.login='c2s12a.admin'
and not exists(select 1 from sigov.usuario_grupo g where g.usuario_id=u.id and g.grupo_acesso_id=910401 and g.ativo);
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,g.id,u.tenant_id,true,now(),910301
from sigov.usuario u cross join sigov.grupo_acesso g
where ((u.tenant_id=910001 and u.login='c2s12a.semperm' and g.tenant_id=910001 and g.codigo_externo='C2S12A-SEMP')
   or (u.tenant_id=910002 and u.login='c2s12b.admin' and g.tenant_id=910002 and g.codigo_externo='C2S12B-ADM')
   or (u.tenant_id=910002 and u.login='c2s12b.semperm' and g.tenant_id=910002 and g.codigo_externo='C2S12B-SEMP'))
and not exists(select 1 from sigov.usuario_grupo gu where gu.usuario_id=u.id and gu.grupo_acesso_id=g.id and gu.ativo);
-- Grants sentinel (limpeza cirurgica por correlation_id): 10 chaves da frente previdencia
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000021'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.id=(select gp.perfil_acesso_id from sigov.grupo_perfil gp where gp.grupo_acesso_id=910401 limit 1)
  and p.ativo and not p.is_deleted
  and p.chave in ('previdencia.visualizar','previdencia.cargo.criar','previdencia.servidor.criar','previdencia.folha.fechar','previdencia.folha.reprocessar','previdencia.ferias.apurar','previdencia.ferias.programar','previdencia.afastamento.registrar','previdencia.contracheque.visualizar','previdencia.esocial.enviar')
on conflict (perfil_acesso_id,permissao_id) do nothing;
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910002,'cf58c2ab-1c20-4a90-88f8-0c2000000022'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910002 and pa.nome='C2S12B Perfil Administracao'
  and p.ativo and not p.is_deleted
  and p.chave in ('previdencia.visualizar','previdencia.cargo.criar','previdencia.servidor.criar','previdencia.folha.fechar','previdencia.folha.reprocessar','previdencia.ferias.apurar','previdencia.ferias.programar','previdencia.afastamento.registrar','previdencia.contracheque.visualizar','previdencia.esocial.enviar')
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Perfis SemFrente: 1 chave inofensiva FORA da frente (necessaria p/ o login autenticar)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000023'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910001 and pa.nome='C2S12A Perfil SemFrente'
  and p.modulo='patrimonio' and p.chave='patrimonio.bem.criar' and p.ativo and not p.is_deleted
on conflict (perfil_acesso_id,permissao_id) do nothing;
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910002,'cf58c2ab-1c20-4a90-88f8-0c2000000024'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910002 and pa.nome='C2S12B Perfil SemFrente'
  and p.modulo='almoxarifado' and p.chave='almoxarifado.recebimento.confirmar' and p.ativo and not p.is_deleted
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Override parametrico do tenant B (regra 11/21/24): fracao minima de ferias 7 (padrao institucional 5)
insert into sigov.parametro_modulo_valor(tenant_id,parametro_id,valor,correlation_id,created_by,is_deleted)
select 910002,id,'7'::jsonb,'c2s12-frac-override-e2e',910301,false
from sigov.parametro_modulo
where modulo='PREVIDENCIA' and codigo='FERIAS_FRAC_MINIMO_DIAS'
and not exists(select 1 from sigov.parametro_modulo_valor val where val.tenant_id=910002 and val.parametro_id=(select id from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo='FERIAS_FRAC_MINIMO_DIAS'));
-- assert DO: banco = autoridade (regras 11/12/13) - sem isto nenhuma etapa da frente e valida
do `$`$
begin
  if (select count(1) from sigov.tenant t where t.id in (910001,910002) and t.ativo and not t.is_deleted) <> 2 then
    raise exception 'C2S12-FIXTURE: tenants 910001/910002 ausentes ou inativos';
  end if;
  if (select count(1) from sigov.permissao p where p.ativo and not p.is_deleted
      and p.chave in ('previdencia.visualizar','previdencia.cargo.criar','previdencia.servidor.criar','previdencia.folha.fechar','previdencia.folha.reprocessar','previdencia.ferias.apurar','previdencia.ferias.programar','previdencia.afastamento.registrar','previdencia.contracheque.visualizar','previdencia.esocial.enviar')) <> 10 then
    raise exception 'C2S12-FIXTURE: catalogo de permissoes previdencia incompleto (esperado 10 chaves ativas no banco)';
  end if;
  if replace((select coalesce(valor_padrao::text,'') from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo='PREVIDENCIA_REGIME' and ativo and not is_deleted limit 1),'"','') <> 'RGPS' then
    raise exception 'C2S12-FIXTURE: parametro padrao PREVIDENCIA_REGIME <> RGPS';
  end if;
  if coalesce((select valor_padrao::numeric from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo='TETO_REMUNERACAO_MUNICIPAL' and ativo and not is_deleted limit 1), -1) <> 0 then
    raise exception 'C2S12-FIXTURE: TETO_REMUNERACAO_MUNICIPAL <> 0 (esfera municipal livre)';
  end if;
  if coalesce((select valor_padrao::numeric from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo='TETO_REMUNERACAO_ESTADUAL' and ativo and not is_deleted limit 1), -1) <> 44161.63 then
    raise exception 'C2S12-FIXTURE: TETO_REMUNERACAO_ESTADUAL <> 44161.63';
  end if;
  if (select coalesce(valor_padrao::text,'') from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo='FERIAS_DIAS_POR_CICLO' and ativo and not is_deleted limit 1) <> '30' then
    raise exception 'C2S12-FIXTURE: FERIAS_DIAS_POR_CICLO <> 30';
  end if;
  if (select coalesce(valor_padrao::text,'') from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo='AFASTAMENTO_MATERNIDADE_DIAS' and ativo and not is_deleted limit 1) <> '120' then
    raise exception 'C2S12-FIXTURE: AFASTAMENTO_MATERNIDADE_DIAS <> 120';
  end if;
  if (select count(1) from sigov.previdencia_inss_faixa where regime='RGPS' and ano=2026) <> 4 then
    raise exception 'C2S12-FIXTURE: faixas INSS RGPS 2026 incompletas (esperado 4)';
  end if;
  if (select count(1) from sigov.previdencia_irrf_faixa where ano_base=2026) <> 5 then
    raise exception 'C2S12-FIXTURE: tabela IRRF 2026 incompleta (esperado 5 faixas)';
  end if;
end
`$`$;
-- ids pipe-separated (alvos numerados dos checks seguintes)
select coalesce((select id::text from sigov.entidade where tenant_id=910001 and codigo_externo='C2S12A-ENT'),'0') || '|'
      || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s12a.admin'),'0') || '|'
      || coalesce((select id::text from sigov.usuario where tenant_id=910001 and login='c2s12a.semperm'),'0') || '|'
      || coalesce((select id::text from sigov.entidade where tenant_id=910002 and codigo_externo='C2S12B-ENT'),'0') || '|'
      || coalesce((select id::text from sigov.usuario where tenant_id=910002 and login='c2s12b.admin'),'0') || '|'
      || coalesce((select id::text from sigov.usuario where tenant_id=910002 and login='c2s12b.semperm'),'0');
"@
$s12ids = $s12fix.text.Trim() -split '\|'
$entA = $s12ids[0]; $uidA = $s12ids[1]; $semAid = $s12ids[2]
$entB = $s12ids[3]; $uidB = $s12ids[4]; $semBid = $s12ids[5]
Add-Check 'S12' 'fixture.criado-idempotente-banco-autoridade' (($s12fix.code -eq 0) -and $entA -ne '0' -and $uidA -ne '0' -and $semAid -ne '0' -and $entB -ne '0' -and $uidB -ne '0' -and $semBid -ne '0') ("pre-clean reentravel + entidades C2S12A-ENT(municipal)/C2S12B-ENT(estadual), 4 usuarios c2s12*, grupos+perfis C2S12, grants sentinel cf58c2ab-...021-024 (10 chaves previdencia x admins + 1 chave inofensiva x semperms), override FERIAS_FRAC_MINIMO_DIAS=7 no tenant B; assert DO: 2 tenants ativos, 10 permissoes ativas, RGPS, tetos 0/44161.63, 30d/ciclo, maternidade 120d, INSS RGPS 2026 = 4 faixas, IRRF 2026 = 5 faixas -> ids=" + ($s12ids -join '|'))

$lg12A = New-SigovLogin -User 'c2s12a.admin' -Pass $Password -Base $WebBase
$tk12A = if ($lg12A.ok) { Get-ApiToken -Jar $lg12A.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lg12SemA = New-SigovLogin -User 'c2s12a.semperm' -Pass $Password -Base $WebBase
$tk12SemA = if ($lg12SemA.ok) { Get-ApiToken -Jar $lg12SemA.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lg12B = New-SigovLogin -User 'c2s12b.admin' -Pass $Password -Base $WebBase
$tk12B = if ($lg12B.ok) { Get-ApiToken -Jar $lg12B.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lg12SemB = New-SigovLogin -User 'c2s12b.semperm' -Pass $Password -Base $WebBase
$tk12SemB = if ($lg12SemB.ok) { Get-ApiToken -Jar $lg12SemB.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
Add-Check 'S12' 'auth.logins-quatro-usuarios' (($lg12A.ok -and $lg12SemA.ok -and $lg12B.ok -and $lg12SemB.ok) -and ($tk12A.token.Length -gt 20) -and ($tk12B.token.Length -gt 20)) ("login web + emissao de ApiToken bearer para c2s12a.admin/c2s12a.semperm/c2s12b.admin/c2s12b.semperm (senha homologacao, contexto institucional = entidade de cada usuario)")

$s12pgf = Join-Path ([IO.Path]::GetTempPath()) ("c1t2_pg_" + [guid]::NewGuid().ToString('N') + ".html")
$c12pg = (& curl.exe -s -b $lg12A.jar -o $s12pgf -w "%{http_code}" "$WebBase/Previdencia").Trim()
$html12 = ''; try { $html12 = [IO.File]::ReadAllText($s12pgf) } catch {}
Add-Check 'S12' 'web.estacao-previdencia-menu-lateral' (($c12pg -eq '200') -and $html12.Contains('/Previdencia/Folhas') -and $html12.Contains('Cargos e Classes')) ("GET $WebBase/Previdencia (cookie de c2s12a.admin) -> " + $c12pg + "; pagina da estacao renderiza e o menu lateral expoe o grupo 'Previdencia e Folha' (/Previdencia/Cargos..Parametros) apos o revamp")

# --- BR-12.1 cargos -----------------------------------------------------------
$cgA1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-A1","nome":"Analista Administrativo C2S12A","modoRemuneracao":"SUBSIDIO","valorReferencia":10000}'
$d = Get-S12Data $cgA1.body; $idCGA1 = -1; if ($null -ne $d) { $idCGA1 = [long]$d.id }
$cgB1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-B1","nome":"Assistente Administrativo C2S12A","modoRemuneracao":"VENCIMENTO_ADICIONAIS","valorReferencia":3000}'
$cgE1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-E1","nome":"Tecnico Administrativo C2S12A","modoRemuneracao":"VENCIMENTO_ADICIONAIS","valorReferencia":1000}'
Add-Check 'S12' 'cargo.criar-tenant-a-3-cargos' (($cgA1.code -eq 200) -and ($idCGA1 -gt 0) -and ($cgB1.code -eq 200) -and ($cgE1.code -eq 200)) ("POST /api/previdencia/cargos tenant A: CARGO-A1 SUBSIDIO 10000 (id=" + $idCGA1 + "), CARGO-B1 VENCIMENTO_ADICIONAIS 3000, CARGO-E1 1000 -> todos 200")
$cgD1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-D1","nome":"Analista Fiscal C2S12B","modoRemuneracao":"SUBSIDIO","valorReferencia":5000}'
$cgC1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-C1","nome":"Especialista Superior C2S12B","modoRemuneracao":"SUBSIDIO","valorReferencia":200000}'
$cgAB1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-AB1","nome":"Auxiliar Operacional C2S12B","modoRemuneracao":"VENCIMENTO_ADICIONAIS","valorReferencia":2000}'
Add-Check 'S12' 'cargo.criar-tenant-b-3-cargos' (($cgD1.code -eq 200) -and ($cgC1.code -eq 200) -and ($cgAB1.code -eq 200)) ("POST /api/previdencia/cargos tenant B (estadual): CARGO-D1 5000, CARGO-C1 200000 (caso teto), CARGO-AB1 2000 -> todos 200")
$cgDup = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-A1","nome":"Duplicado C2S12A","modoRemuneracao":"SUBSIDIO","valorReferencia":1}'
$cgMod = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-X1","nome":"Modo Invalido C2S12A","modoRemuneracao":"HORISTA","valorReferencia":1}'
$cgInc = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-X2","nome":"","modoRemuneracao":"SUBSIDIO","valorReferencia":1}'
Add-Check 'S12' 'cargo.falhas-nomeadas-duplicado-modo-incompleto' (($cgDup.code -eq 409) -and (Get-S12Msg $cgDup.body) -like '*CARGO_JA_EXISTE*' -and ($cgMod.code -eq 400) -and (Get-S12Msg $cgMod.body) -like '*MODO_REMUNERACAO_INVALIDO*' -and ($cgInc.code -eq 400) -and (Get-S12Msg $cgInc.body) -like '*CARGO_INCOMPLETO*') ("duplicado codigo -> " + $cgDup.code + " CARGO_JA_EXISTE; modo HORISTA -> " + $cgMod.code + " MODO_REMUNERACAO_INVALIDO; nome vazio -> " + $cgInc.code + " CARGO_INCOMPLETO (falhas nomeadas explicitas - regra 13)")
$cgList = Invoke-Api -Bearer $tk12A.token -Method GET -Path '/api/previdencia/cargos' -Base $ApiBase
Add-Check 'S12' 'cargo.lista-isolada-por-tenant' (($cgList.code -eq 200) -and $cgList.body.Contains('CARGO-A1') -and $cgList.body.Contains('CARGO-E1') -and (-not $cgList.body.Contains('CARGO-D1'))) ("GET /api/previdencia/cargos tenant A lista CARGO-A1/B1/E1 e NAO vaza CARGO-D1 (tenant B) - isolamento por tenant (regra 11)")

# --- BR-12.1 servidores -------------------------------------------------------
$adA = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12A-SRV1","nome":"Servidor C2S12A Alfa","cargoCodigo":"CARGO-A1","dataAdmissao":"2026-01-01","tipoVinculo":"EFETIVO","uf":"SP"}'
$d = Get-S12Data $adA.body; $vA = -1; if ($null -ne $d) { $vA = [long]$d.vinculoId }
$adB = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12A-SRV2","nome":"Servidor C2S12A Beta","cargoCodigo":"CARGO-B1","dataAdmissao":"2023-01-10","tipoVinculo":"COMISSAO","uf":"SP"}'
$d = Get-S12Data $adB.body; $vB = -1; if ($null -ne $d) { $vB = [long]$d.vinculoId }
$adE = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12A-SRV3","nome":"Servidor C2S12A Epsilon","cargoCodigo":"CARGO-E1","dataAdmissao":"2026-06-01","tipoVinculo":"EFETIVO","uf":"SP"}'
$d = Get-S12Data $adE.body; $vE = -1; if ($null -ne $d) { $vE = [long]$d.vinculoId }
Add-Check 'S12' 'servidor.admitir-tenant-a-a-b-e' (($adA.code -eq 200) -and ($vA -gt 0) -and ($adB.code -eq 200) -and ($vB -gt 0) -and ($adE.code -eq 200) -and ($vE -gt 0)) ("admissao tenant A: A (10000, adm 2026-01-01, vinculo " + $vA + "), B (3000, adm 2023-01-10, vinculo " + $vB + ", 3 ciclos aquisitivos ate df 2026-09-20), E (1000, adm 2026-06-01, vinculo " + $vE + ") -> 200 com item VENCIMENTO automatico no valor do cargo")
$adD = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12B-D1","nome":"Servidor C2S12B Delta","cargoCodigo":"CARGO-D1","dataAdmissao":"2025-01-01","tipoVinculo":"EFETIVO","uf":"RJ"}'
$d = Get-S12Data $adD.body; $vD = -1; if ($null -ne $d) { $vD = [long]$d.vinculoId }
$adC = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12B-C1","nome":"Servidor C2S12B Gamma","cargoCodigo":"CARGO-C1","dataAdmissao":"2024-03-01","tipoVinculo":"EFETIVO","uf":"RJ"}'
$d = Get-S12Data $adC.body; $vC = -1; if ($null -ne $d) { $vC = [long]$d.vinculoId }
$adAB = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12B-AB","nome":"AB","cargoCodigo":"CARGO-AB1","dataAdmissao":"2025-08-01","tipoVinculo":"TEMPORARIO","uf":"RJ"}'
$d = Get-S12Data $adAB.body; $vAB = -1; if ($null -ne $d) { $vAB = [long]$d.vinculoId }
Add-Check 'S12' 'servidor.admitir-tenant-b-d-c-ab' (($adD.code -eq 200) -and ($vD -gt 0) -and ($adC.code -eq 200) -and ($vC -gt 0) -and ($adAB.code -eq 200) -and ($vAB -gt 0)) ("admissao tenant B (estadual): D (5000, vinculo " + $vD + "), C (200000 - caso teto, vinculo " + $vC + "), AB (nome < 5 - caso validacao eSocial, vinculo " + $vAB + ") -> 200")
$adDup = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12A-SRV1","nome":"Duplicado C2S12A","cargoCodigo":"CARGO-B1","dataAdmissao":"2026-02-01","tipoVinculo":"EFETIVO","uf":"SP"}'
$adTipo = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12A-X9","nome":"Tipo Invalido C2S12A","cargoCodigo":"CARGO-B1","dataAdmissao":"2026-02-01","tipoVinculo":"TERCEIROS","uf":"SP"}'
$adInc = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"","nome":"Sem Matricula C2S12A","cargoCodigo":"CARGO-B1","dataAdmissao":"2026-02-01","tipoVinculo":"EFETIVO","uf":"SP"}'
$adCarg = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/servidores' -Base $ApiBase -Body '{"matriculaFuncional":"C2S12A-X8","nome":"Cargo Ausente C2S12A","cargoCodigo":"CARGO-ZZZ","dataAdmissao":"2026-02-01","tipoVinculo":"EFETIVO","uf":"SP"}'
Add-Check 'S12' 'servidor.falhas-nomeadas-duplicada-tipo-incompleto-cargo' (($adDup.code -eq 409) -and (Get-S12Msg $adDup.body) -like '*MATRICULA_FUNCIONAL_DUPLICADA*' -and ($adTipo.code -eq 400) -and (Get-S12Msg $adTipo.body) -like '*VINCULO_TIPO_INVALIDO*' -and ($adInc.code -eq 400) -and (Get-S12Msg $adInc.body) -like '*SERVIDOR_INCOMPLETO*' -and ($adCarg.code -eq 404) -and (Get-S12Msg $adCarg.body) -like '*PREVIDENCIA_CARGO_INEXISTENTE*') ("matricula duplicada -> " + $adDup.code + " MATRICULA_FUNCIONAL_DUPLICADA (backstop unico); tipo TERCEIROS -> " + $adTipo.code + " VINCULO_TIPO_INVALIDO; matricula vazia -> " + $adInc.code + " SERVIDOR_INCOMPLETO; cargo ausente -> " + $adCarg.code + " PREVIDENCIA_CARGO_INEXISTENTE")
$svList = Invoke-Api -Bearer $tk12A.token -Method GET -Path '/api/previdencia/servidores' -Base $ApiBase
Add-Check 'S12' 'servidor.lista-esfera-municipal-cruz-tenant' (($svList.code -eq 200) -and $svList.body.Contains('C2S12A-SRV1') -and $svList.body.Contains('"esferaGoverno":"municipal"') -and (-not $svList.body.Contains('C2S12B-D1'))) ("GET /api/previdencia/servidores tenant A: A/B/E com esferaGoverno=municipal herdada da entidade do contexto (regra 21/22) e NAO vaza C2S12B-D1 (estadual)")

# --- BR-12.2/12.3 folha: validacoes, agosto (E) e setembro (A+B) ---------------
$fl01 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-08-01","dataFixa":"2026-08-20"}'
$fl02 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-09-15","dataFixa":"2026-09-20"}'
$fl03 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-10-01","dataFixa":"2026-09-01"}'
Add-Check 'S12' 'folha.validacoes-competencia-datafixa-nomeadas' (($fl01.code -eq 404) -and (Get-S12Msg $fl01.body) -like '*PREVIDENCIA_FOLHA_INEXISTENTE*' -and ($fl02.code -eq 400) -and (Get-S12Msg $fl02.body) -like '*COMPETENCIA_INVALIDA*' -and ($fl03.code -eq 400) -and (Get-S12Msg $fl03.body) -like '*DATA_FIXA_INVALIDA*') ("fechar competencia inexistente -> " + $fl01.code + " PREVIDENCIA_FOLHA_INEXISTENTE; competencia dia 15 -> " + $fl02.code + " COMPETENCIA_INVALIDA; data_fixa < competencia -> " + $fl03.code + " DATA_FIXA_INVALIDA")
$flA1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-08-01","dataFixa":"2026-08-20"}'
$flA1j = Get-S12Data $flA1.body
$flA2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-08-01","dataFixa":"2026-08-20"}'
$d = Get-S12Data $flA2.body
$okFlA2 = ($flA2.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ([int]$d.calculosGerados -eq 3) -and ([int]$d.totalCalculos -eq 3)
Add-Check 'S12' 'folha.augusto-tenant-a-fechado-3-calculos' (($flA1.code -eq 200) -and ($null -ne $flA1j) -and ($flA1j.status -eq 'ABERTA') -and $okFlA2) ("abrir 2026-08 (df 2026-08-20) -> ABERTA (abrir repetido idempotente); fechar -> FECHADA com calculosGerados=3 (A, B e E ainda ativo) - motor deterministico multi-esfera")
$flR1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/reabrir' -Base $ApiBase -Body '{"competencia":"2026-08-01","dataFixa":"2026-08-20","justificativa":""}'
$flR2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/reabrir' -Base $ApiBase -Body '{"competencia":"2026-08-01","dataFixa":"2026-08-20","justificativa":"correcao do adicional de titulo homologacao"}'
$d = Get-S12Data $flR2.body
Add-Check 'S12' 'folha.reabertura-sem-justificativa-nomeada' (($flR1.code -eq 400) -and (Get-S12Msg $flR1.body) -like '*REABERTURA_SEM_JUSTIFICATIVA*' -and ($flR2.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'ABERTA')) ("reabrir 2026-08 sem justificativa -> " + $flR1.code + " REABERTURA_SEM_JUSTIFICATIVA; com justificativa registrada -> " + $flR2.code + " ABERTA (auditoria append-only)")
$rmE1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/remuneracoes' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"naturezaCodigo":"ADICIONAL_TITULO","valor":500,"alterar":false}')
$rmE2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/remuneracoes' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"naturezaCodigo":"ADICIONAL_TITULO","valor":700,"alterar":false}')
$rmE3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/remuneracoes' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"naturezaCodigo":"ADICIONAL_TITULO","valor":300,"alterar":true}')
$rmE4 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/remuneracoes' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"naturezaCodigo":"XPTO","valor":100,"alterar":false}')
Add-Check 'S12' 'remuneracao.e-fluxo-lancar-duplicado-alterar-natureza' (($rmE1.code -eq 200) -and ($rmE2.code -eq 409) -and (Get-S12Msg $rmE2.body) -like '*REMUNERACAO_JA_EXISTE*' -and ($rmE3.code -eq 200) -and ($rmE4.code -eq 404) -and (Get-S12Msg $rmE4.body) -like '*PREVIDENCIA_NATUREZA_INEXISTENTE*') ("vinculo E: ADICIONAL_TITULO 500 -> 200; novo sem alterar -> " + $rmE2.code + " REMUNERACAO_JA_EXISTE; alterar para 300 -> 200; natureza XPTO fora do catalogo do banco -> " + $rmE4.code + " PREVIDENCIA_NATUREZA_INEXISTENTE (catalogo autoritario - regra 12)")
$rpE1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/reprocessar' -Base $ApiBase -Body ('{"competencia":"2026-08-01","vinculoId":' + $vE + ',"dataFixa":"2026-08-20","justificativa":"correcao do adicional de titulo homologacao"}')
$rpE1j = Get-S12Data $rpE1.body
$dbRep1 = Get-Psql "select k.versao||'|'||k.status||'|'||to_char(k.bruto,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910001 and k.vinculo_id=$vE and f.competencia=date '2026-08-01' order by k.versao"
$normRep1 = ($dbRep1.text.Trim() -replace '\s*\n\s*','|').Trim()
Add-Check 'S12' 'folha.reprocesso-e-v2-veta-v1-valores' (($rpE1.code -eq 200) -and ($null -ne $rpE1j) -and ([int]$rpE1j.versao -eq 2) -and ($rpE1j.status -eq 'VIGENTE') -and (Get-S12Near $rpE1j.valores.bruto 1300.00m) -and ($dbRep1.code -eq 0) -and ($normRep1 -eq '1|VETADA|1000.00|2|VIGENTE|1300.00')) ("reprocessar E (2026-08, bruto 1000->1300 apos alterar adicional 500->300) -> 200 versao 2 VIGENTE bruto 1300; banco: v1 VETADA 1000.00 / v2 VIGENTE 1300.00 (BR-12.3 versoes auditaveis) db=[" + $dbRep1.text + "]")
$rmE5 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/remuneracoes' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"naturezaCodigo":"ADICIONAL_TITULO","valor":0,"alterar":true}')
$rpE2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/reprocessar' -Base $ApiBase -Body ('{"competencia":"2026-08-01","vinculoId":' + $vE + ',"dataFixa":"2026-08-20","justificativa":"restauracao do valor original homologacao"}')
$rpE2j = Get-S12Data $rpE2.body
$dbRep2 = Get-Psql "select k.versao||'|'||k.status||'|'||to_char(k.bruto,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910001 and k.vinculo_id=$vE and f.competencia=date '2026-08-01' order by k.versao"
$normRep2 = ($dbRep2.text.Trim() -replace '\s*\n\s*','|').Trim()
Add-Check 'S12' 'folha.reprocesso-e-v3-restaurado-3-versoes' (($rmE5.code -eq 200) -and ($rpE2.code -eq 200) -and ($null -ne $rpE2j) -and ([int]$rpE2j.versao -eq 3) -and (Get-S12Near $rpE2j.valores.bruto 1000.00m) -and ($dbRep2.code -eq 0) -and ($normRep2 -eq '1|VETADA|1000.00|2|VETADA|1300.00|3|VIGENTE|1000.00')) ("adicional E -> 0; reprocessar de novo -> versao 3 VIGENTE bruto 1000 (igual a v1); banco: 3 versoes (1 VETADA 1000 / 2 VETADA 1300 / 3 VIGENTE 1000) - trilhavel e reversivel (BR-12.3)")
$flA3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-08-01","dataFixa":"2026-08-20"}'
$d = Get-S12Data $flA3.body
Add-Check 'S12' 'folha.augusto-refechado-skip-idempotente' (($flA3.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ([int]$d.calculosGerados -eq 0) -and ([int]$d.totalCalculos -eq 3)) ("refechar 2026-08 (todos os vinculos ja tem VIGENTE) -> FECHADA com calculosGerados=0 e totalCalculos=3 - nao recalcula o que ja existe (BR-12.2)")
$dlE0 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/desligamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"dataDesligamento":"2026-08-25","motivo":""}')
$dlE1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/desligamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"dataDesligamento":"2026-08-25","motivo":"fim do cenario homologacao"}')
$dlE2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/desligamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vE + ',"dataDesligamento":"2026-08-25","motivo":"segunda vez homologacao"}')
Add-Check 'S12' 'desligamento.e-sem-motivo-ok-repetida' (($dlE0.code -eq 400) -and (Get-S12Msg $dlE0.body) -like '*DESLIGAMENTO_SEM_MOTIVO*' -and ($dlE1.code -eq 200) -and ($dlE2.code -eq 409) -and (Get-S12Msg $dlE2.body) -like '*VINCULO_NAO_ATIVO*') ("desligar E sem motivo -> " + $dlE0.code + " DESLIGAMENTO_SEM_MOTIVO; com motivo -> 200; desligar de novo -> " + $dlE2.code + " VINCULO_NAO_ATIVO (E sai da folha de setembro)")
$flS1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-09-01","dataFixa":"2026-09-20"}'
$flN1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-11-01","dataFixa":"2026-11-20"}'
$loN1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-11-01","tipoLote":"REMUNERACAO_S1200","dataFixa":"2026-11-20"}'
Add-Check 'S12' 'folha.set-nov-abertos-lote-nov-sem-fechamento' (($flS1.code -eq 200) -and ($flN1.code -eq 200) -and ($loN1.code -eq 409) -and (Get-S12Msg $loN1.body) -like '*FOLHA_NAO_FECHADA_PARA_LOTE*') ("abrir 2026-09 e 2026-11 (tenant A) -> 200; lote REMUNERACAO_S1200 de novembro antes do fechamento -> " + $loN1.code + " FOLHA_NAO_FECHADA_PARA_LOTE (BR-12.8)")
$flS2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-09-01","dataFixa":"2026-09-20"}'
$d = Get-S12Data $flS2.body
$dbSetA = Get-Psql "select s.matricula_funcional||'|'||to_char(k.bruto,'FM999990.00')||'|'||to_char(k.inss_valor,'FM999990.00')||'|'||to_char(k.irrf_valor,'FM999990.00')||'|'||to_char(k.liquido,'FM999990.00')||'|'||to_char(k.decimo_valor,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_vinculo v on v.id=k.vinculo_id join sigov.previdencia_servidor s on s.id=v.servidor_id join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910001 and f.competencia=date '2026-09-01' and k.status='VIGENTE' order by s.matricula_funcional"
$normSetA = ($dbSetA.text.Trim() -replace '\s*\n\s*','~').Trim()
Add-Check 'S12' 'folha.setembro-a-fechado-valores-travados-br12-2' (($flS2.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ([int]$d.calculosGerados -eq 2) -and ([int]$d.totalCalculos -eq 2) -and ($dbSetA.code -eq 0) -and ($normSetA -eq 'C2S12A-SRV1|10000.00|1078.14|900.07|8021.79|0.00~C2S12A-SRV2|3000.00|253.41|0.00|2746.59|0.00')) ("fechar 2026-09 tenant A -> gerados=2 (E desligado); INSS por faixa RGPS 2026 + IRRF progressivo: A 10000.00/1078.14/900.07/8021.79 e B 3000.00/253.41/0.00/2746.59 exatos (valores travados da legislacao LC 51/84 + Decreto 3.048/99)")
$flS3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-09-01","dataFixa":"2026-09-20"}'
$d = Get-S12Data $flS3.body
Add-Check 'S12' 'folha.setembro-a-refechado-idempotente' (($flS3.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ($d.idempotente -eq $true) -and ([int]$d.calculosGerados -eq 0)) ("fechar 2026-09 de novo -> 200 FECHADA idempotente=true calculosGerados=0 (BR-12.2: devolve estado consolidado sem recalculo)")
$ctS1 = Invoke-Api -Bearer $tk12A.token -Method GET -Path ('/api/previdencia/contracheque?vinculoId=' + $vA + '&competencia=2026-09-01') -Base $ApiBase
$ctS1j = Get-S12Data $ctS1.body
$gruposCt = @(); if ($null -ne $ctS1j) { $gruposCt = @($ctS1j.itens | ForEach-Object { [string]$_.grupo }) }
$okCt = ($ctS1.code -eq 200) -and ($null -ne $ctS1j) -and ($ctS1j.matriculaFuncional -eq 'C2S12A-SRV1') -and ($ctS1j.esferaGoverno -eq 'municipal') -and (Get-S12Near $ctS1j.bruto 10000.00m) -and (Get-S12Near $ctS1j.inssValor 1078.14m) -and (Get-S12Near $ctS1j.irrfValor 900.07m) -and (Get-S12Near $ctS1j.liquido 8021.79m) -and (@($ctS1j.itens).Count -ge 3) -and ($gruposCt -contains 'RENDA') -and ($gruposCt -contains 'PREVIDENCIA') -and ($gruposCt -contains 'IRRF')
Add-Check 'S12' 'contracheque.a-setembro-demonstracao-br12-7' $okCt ("GET contracheque A 2026-09: matricula C2S12A-SRV1, esfera municipal, bruto 10000 / inss 1078.14 / irrf 900.07 / liquido 8021.79 + itens demonstrativos por grupo (RENDA/PREVIDENCIA/IRRF) - versionado, LGPD dados minimos, sem CPF (BR-12.7)")

# --- BR-12.4 ferias (tenant A, vinculo B: 3 ciclos = 90 dias) ------------------
$frA = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/apurar-aquisicao' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"dataFixa":"2026-09-20"}')
$frB1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/apurar-aquisicao' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20"}')
$d = Get-S12Data $frB1.body
$okFrB1 = ($frB1.code -eq 200) -and ($null -ne $d) -and ([int]$d.ciclosCriados -eq 3) -and ([int]$d.totalCiclos -eq 3) -and ([int]$d.saldoTotalDias -eq 90)
$frB2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/apurar-aquisicao' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20"}')
Add-Check 'S12' 'ferias.apuracao-a-sem-ciclo-b-3-ciclos-b-repetida' (($frA.code -eq 409) -and (Get-S12Msg $frA.body) -like '*SEM_NOVO_CICLO_AQUISITIVO*' -and $okFrB1 -and ($frB2.code -eq 409) -and (Get-S12Msg $frB2.body) -like '*SEM_NOVO_CICLO_AQUISITIVO*') ("apurar A (adm 2026-01-01, ciclo nao concluido) -> " + $frA.code + " SEM_NOVO_CICLO_AQUISITIVO; B (adm 2023-01-10) -> 3 ciclos x 30d = 90 dias; apurar de novo -> " + $frB2.code + " SEM_NOVO_CICLO_AQUISITIVO (idempotente por inicio_aquisitivo)")
$fpAv = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-09-25","dataFim":"2026-09-29","diasGoze":5,"diasAbono":0}')
$fpFc = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-10-20","dataFim":"2026-10-23","diasGoze":4,"diasAbono":0}')
$fpAb = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-10-20","dataFim":"2026-10-24","diasGoze":5,"diasAbono":3}')
$fpPd = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-10-24","dataFim":"2026-10-20","diasGoze":5,"diasAbono":0}')
Add-Check 'S12' 'ferias.validacoes-nomeadas-intervalo-fracao-abono-periodo' (($fpAv.code -eq 400) -and (Get-S12Msg $fpAv.body) -like '*INTERVALO_FERIAS_INSUFICIENTE*' -and ($fpFc.code -eq 400) -and (Get-S12Msg $fpFc.body) -like '*FRACAO_FERIAS_INSUFICIENTE*' -and ($fpAb.code -eq 400) -and (Get-S12Msg $fpAb.body) -like '*ABONO_PECUNIARIO_INFERIOR*' -and ($fpPd.code -eq 400) -and (Get-S12Msg $fpPd.body) -like '*PERIODO_INVALIDO*') ("aviso 5d < minimo 30 -> INTERVALO_FERIAS_INSUFICIENTE; gozo 4d < fracao minima 5 -> FRACAO_FERIAS_INSUFICIENTE; abono 3d < minimo 5 -> ABONO_PECUNIARIO_INFERIOR; fim < inicio -> PERIODO_INVALIDO (ordem deterministica das validacoes - Lei 8.112/90/CLT)")
# G3/G3b/G4 primeiro: consomem o ciclo MAIS RECENTE com saldo>0 (c3), enquanto os slots
# de fracionamento desse ciclo ainda estao livres (F1-F3 ocupariam 3 de 3 e trancariam c3).
$fg3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2027-01-20","dataFim":"2027-02-08","diasGoze":20,"diasAbono":40}')
$fg3b = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2027-02-20","dataFim":"2027-03-11","diasGoze":20,"diasAbono":0}')
$fg3bj = Get-S12Data $fg3b.body
$fg4 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2027-03-20","dataFim":"2027-04-08","diasGoze":20,"diasAbono":0}')
Add-Check 'S12' 'ferias.g3-saldo-g3b-ok-g4-saldo' (($fg3.code -eq 400) -and (Get-S12Msg $fg3.body) -like '*SALDO_FERIAS_INSUFICIENTE*' -and ($fg3b.code -eq 200) -and ($null -ne $fg3bj) -and (Get-S12Near $fg3bj.adicionalTerco 666.67m) -and ([int]$fg3bj.saldoRestanteDias -eq 10) -and ($fg4.code -eq 400) -and (Get-S12Msg $fg4.body) -like '*SALDO_FERIAS_INSUFICIENTE*') ("G3 20d+40d > saldo 30 do ciclo mais recente -> SALDO_FERIAS_INSUFICIENTE; G3b 20d -> OK (saldo 30->10, terco 666.67); G4 20d > saldo 10 -> SALDO_FERIAS_INSUFICIENTE")
$fg1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-12-01","dataFim":"2026-12-10","diasGoze":10,"diasAbono":0}')
$fg1j = Get-S12Data $fg1.body
$fg2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-12-20","dataFim":"2027-01-08","diasGoze":20,"diasAbono":10}')
$fg2j = Get-S12Data $fg2.body
Add-Check 'S12' 'ferias.g1-g2-consumo-de-ciclos-tercos' (($fg1.code -eq 200) -and ($null -ne $fg1j) -and (Get-S12Near $fg1j.salarioDia 100.00m) -and (Get-S12Near $fg1j.adicionalTerco 333.33m) -and ([int]$fg1j.saldoRestanteDias -eq 0) -and ($fg2.code -eq 200) -and ($null -ne $fg2j) -and (Get-S12Near $fg2j.adicionalTerco 1000.00m) -and ([int]$fg2j.saldoRestanteDias -eq 0)) ("G1 10d encerra o que restava do ciclo mais recente (saldo 10->0 apos G3b, terco 333.33 = 100x10/3); G2 20d+abono 10d consome o ciclo seguinte (30->0, terco 1000.00 = 100x30/3) - abono tambem gera 1/3 (CLT art. 145)")
$fp1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-10-20","dataFim":"2026-10-24","diasGoze":5,"diasAbono":0}')
$fp1j = Get-S12Data $fp1.body; $pF1 = -1; if ($null -ne $fp1j) { $pF1 = [long]$fp1j.id }
$fp2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-10-26","dataFim":"2026-10-30","diasGoze":5,"diasAbono":0}')
$fp2j = Get-S12Data $fp2.body
$fp3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-11-01","dataFim":"2026-11-05","diasGoze":5,"diasAbono":0}')
$fp3j = Get-S12Data $fp3.body
$fp4 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/ferias/programar' -Base $ApiBase -Body ('{"vinculoId":' + $vB + ',"dataFixa":"2026-09-20","dataInicio":"2026-11-06","dataFim":"2026-11-10","diasGoze":5,"diasAbono":0}')
Add-Check 'S12' 'ferias.fracionamento-f1-f3-ok-f4-excedido' (($fp1.code -eq 200) -and ($null -ne $fp1j) -and ($fp1j.status -eq 'PROGRAMADA') -and (Get-S12Near $fp1j.adicionalTerco 166.67m) -and ($fp2.code -eq 200) -and (Get-S12Near ((Get-S12Data $fp2.body)).adicionalTerco 166.67m) -and ($fp3.code -eq 200) -and (Get-S12Near ((Get-S12Data $fp3.body)).saldoRestanteDias 15.00m) -and ($fp4.code -eq 400) -and (Get-S12Msg $fp4.body) -like '*FRACIONAMENTO_MAXIMO_EXCEDIDO*') ("F1/F2/F3 (5d cada no ciclo aquisitivo restante) -> PROGRAMADA com terco 166.67 = salario_dia 100 x 5/3 auditavel; F4 seria o 4o periodo -> " + $fp4.code + " FRACIONAMENTO_MAXIMO_EXCEDIDO (maximo parametrico 3 - Lei 8.112/90)")
$gzo1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/ferias/' + $pF1 + '/gozo') -Base $ApiBase -Body ('{"programacaoId":' + $pF1 + ',"dataFixa":"2026-10-20"}')
$gzo2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/ferias/' + $pF1 + '/gozo') -Base $ApiBase -Body ('{"programacaoId":' + $pF1 + ',"dataFixa":"2026-10-21"}')
Add-Check 'S12' 'ferias.gozo-registrado-e-repetido' (($gzo1.code -eq 200) -and ($gzo2.code -eq 409) -and (Get-S12Msg $gzo2.body) -like '*PROGRAMACAO_ESTADO_INVALIDO*') ("registro de gozo da programacao F1 -> 200 (status GOZO); registrar de novo -> " + $gzo2.code + " PROGRAMACAO_ESTADO_INVALIDO (maquina de estados do gozo)")
$dbFerias = Get-Psql "select coalesce((select to_char(sum(saldo_dias),'FM9999') from sigov.previdencia_ferias f2 where f2.tenant_id=910001),'0')||'|'||coalesce((select to_char(sum(p2.adicional_terco),'FM999990.00') from sigov.previdencia_ferias_programacao p2 where p2.tenant_id=910001),'0.00')||'|'||(select count(1) from sigov.previdencia_ferias_programacao pp where pp.tenant_id=910001 and pp.status='GOZO')||'|'||(select count(1) from sigov.previdencia_ferias_programacao pp2 where pp2.tenant_id=910001 and pp2.status='PROGRAMADA')"
Add-Check 'S12' 'ferias.estado-final-ciclos-db' (($dbFerias.code -eq 0) -and ($dbFerias.text.Trim() -eq '15|2500.01|1|5')) ("banco: saldo restante total 15 dias (ciclo 1), soma de tercos 1/3 auditaveis 2500.01 (166.67x3 + 666.67 + 333.33 + 1000.00), programacoes GOZO=1 e PROGRAMADA=5 - consumo exato e traceavel dos 90 dias aquisitivos (BR-12.4)")

# --- BR-12.4 afastamentos ------------------------------------------------------
$afT1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"tipo":"XPTO","dataFixa":"2026-09-20","dataInicio":"2026-10-01","dataFim":"2026-10-10","documentoGed":"C2S12A-GED-T1","regimeOpcao":"RPSS"}')
$afD1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"tipo":"MATERNIDADE","dataFixa":"2026-09-20","dataInicio":"2026-10-01","dataFim":"2027-01-27","documentoGed":"C2S12A-GED-M0","regimeOpcao":"RPSS"}')
$afG1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"tipo":"MATERNIDADE","dataFixa":"2026-09-20","dataInicio":"2026-10-01","dataFim":"2027-01-28","documentoGed":"","regimeOpcao":"RPSS"}')
Add-Check 'S12' 'afastamento.validacoes-nomeadas-tipo-duracao-ged' (($afT1.code -eq 400) -and (Get-S12Msg $afT1.body) -like '*AFASTAMENTO_TIPO_INVALIDO*' -and ($afD1.code -eq 400) -and (Get-S12Msg $afD1.body) -like '*DURACAO_AFASTAMENTO_INCONSISTENTE*' -and ($afG1.code -eq 400) -and (Get-S12Msg $afG1.body) -like '*DOCUMENTO_GED_OBRIGATORIO*') ("tipo XPTO -> AFASTAMENTO_TIPO_INVALIDO; maternidade 119d != parametro 120d -> DURACAO_AFASTAMENTO_INCONSISTENTE; sem documento GED -> DOCUMENTO_GED_OBRIGATORIO (controle TCU - regra 13)")
$afM1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"tipo":"MATERNIDADE","dataFixa":"2026-09-20","dataInicio":"2026-10-01","dataFim":"2027-01-28","documentoGed":"C2S12A-GED-M1","regimeOpcao":"RPSS"}')
$afM1j = Get-S12Data $afM1.body; $afMId = -1; if ($null -ne $afM1j) { $afMId = [long]$afM1j.id }
$afE1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"tipo":"ESTUDO","dataFixa":"2026-09-20","dataInicio":"2026-10-10","dataFim":"2026-10-31","documentoGed":"C2S12A-GED-E1","regimeOpcao":"RPSS"}')
Add-Check 'S12' 'afastamento.maternidade-120d-e-sobreposto' (($afM1.code -eq 200) -and ($afMId -gt 0) -and ($null -ne $afM1j) -and ([int]$afM1j.dias -eq 120) -and ($afM1j.status -eq 'ATIVO') -and ($afE1.code -eq 409) -and (Get-S12Msg $afE1.body) -like '*AFASTAMENTO_SOBREPOSTO*') ("maternidade A 2026-10-01..2027-01-28 (120d parametrico, GED, RPSS) -> ATIVO id=" + $afMId + "; ESTUDO sobreposto 10..31/10 -> " + $afE1.code + " AFASTAMENTO_SOBREPOSTO (backstop por periodo)")
$afDev1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/afastamentos/' + $afMId + '/devolver') -Base $ApiBase -Body ('{"afastamentoId":' + $afMId + ',"dataFixa":"2027-01-10","motivo":"retorno antecipado homologacao"}')
$afDev2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/afastamentos/' + $afMId + '/devolver') -Base $ApiBase -Body ('{"afastamentoId":' + $afMId + ',"dataFixa":"2027-01-15","motivo":""}')
$afDev3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/afastamentos/' + $afMId + '/devolver') -Base $ApiBase -Body ('{"afastamentoId":' + $afMId + ',"dataFixa":"2027-01-28","motivo":"retorno ao servico homologacao"}')
$afDev3j = Get-S12Data $afDev3.body
$afDev4 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/afastamentos/' + $afMId + '/devolver') -Base $ApiBase -Body ('{"afastamentoId":' + $afMId + ',"dataFixa":"2027-01-29","motivo":"segunda devolucao homologacao"}')
Add-Check 'S12' 'afastamento.devolucao-condicional' (($afDev1.code -eq 400) -and (Get-S12Msg $afDev1.body) -like '*DEVOLUCAO_ANTICIPADA*' -and ($afDev2.code -eq 400) -and (Get-S12Msg $afDev2.body) -like '*DEVOLUCAO_SEM_MOTIVO*' -and ($afDev3.code -eq 200) -and ($null -ne $afDev3j) -and ($afDev3j.status -eq 'DEVOLVIDO') -and ($afDev4.code -eq 409) -and (Get-S12Msg $afDev4.body) -like '*AFASTAMENTO_JA_DEVOLVIDO*') ("devolver com data_fixa 2027-01-10 < fim 2027-01-28 -> DEVOLUCAO_ANTICIPADA; sem motivo -> DEVOLUCAO_SEM_MOTIVO; data_fixa >= fim -> DEVOLVIDO; repetir -> AFASTAMENTO_JA_DEVOLVIDO (condicao avaliada no banco, sem manipulacao manual)")
$afP1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vD + ',"tipo":"PATERNIDADE","dataFixa":"2026-09-20","dataInicio":"2027-02-01","dataFim":"2027-02-04","documentoGed":"C2S12B-GED-P0","regimeOpcao":"RPSS"}')
$afP2 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vD + ',"tipo":"PATERNIDADE","dataFixa":"2026-09-20","dataInicio":"2027-02-01","dataFim":"2027-02-05","documentoGed":"C2S12B-GED-P1","regimeOpcao":"RPSS"}')
$afP2j = Get-S12Data $afP2.body
$afAd = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vA + ',"tipo":"ADOCAO","dataFixa":"2026-09-20","dataInicio":"2027-02-01","dataFim":"2027-05-31","documentoGed":"C2S12A-GED-AD1","regimeOpcao":"RPSS"}')
$afAdj = Get-S12Data $afAd.body
$afSa = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/afastamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vAB + ',"tipo":"SAUDE","dataFixa":"2026-09-20","dataInicio":"2026-10-05","dataFim":"2026-10-12","documentoGed":"C2S12B-GED-AB1","regimeOpcao":"RGPS"}')
Add-Check 'S12' 'afastamento.paternidade-adocao-saude-parametrizados' (($afP1.code -eq 400) -and (Get-S12Msg $afP1.body) -like '*DURACAO_AFASTAMENTO_INCONSISTENTE*' -and ($afP2.code -eq 200) -and ($null -ne $afP2j) -and ([int]$afP2j.dias -eq 5) -and ($afAd.code -eq 200) -and ($null -ne $afAdj) -and ([int]$afAdj.dias -eq 120) -and ($afSa.code -eq 200)) ("paternidade D 4d != parametro 5 -> DURACAO_AFASTAMENTO_INCONSISTENTE; 5d -> OK (Lei 8.078/90); adocao A 120d -> OK (EC 103/19); saude AB 05..12/10 (inicio em outubro) -> OK p/ o lote MOVIMENTACAO_S1006 de outubro")

# --- BR-12.5 decimo + novembro/dezembro (tenant A) ------------------------------
$flN2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-11-01","dataFixa":"2026-11-20"}'
$d = Get-S12Data $flN2.body
$dbNovA = Get-Psql "select s.matricula_funcional||'|'||to_char(k.bruto,'FM999990.00')||'|'||to_char(k.inss_valor,'FM999990.00')||'|'||to_char(k.irrf_valor,'FM999990.00')||'|'||to_char(k.liquido,'FM999990.00')||'|'||to_char(k.decimo_valor,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_vinculo v on v.id=k.vinculo_id join sigov.previdencia_servidor s on s.id=v.servidor_id join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910001 and f.competencia=date '2026-11-01' and k.status='VIGENTE' order by s.matricula_funcional"
$normNovA = ($dbNovA.text.Trim() -replace '\s*\n\s*','~').Trim()
Add-Check 'S12' 'decimo.novembro-a-1a-parcela-11-meses' (($flN2.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ([int]$d.calculosGerados -eq 2) -and ($dbNovA.code -eq 0) -and ($normNovA -eq 'C2S12A-SRV1|19166.67|2361.48|3021.27|13783.92|9166.67~C2S12A-SRV2|5750.00|614.59|178.41|4957.00|2750.00')) ("fechar 2026-11 (df 2026-11-20): 1a parcela do 13o entra no bruto - A p1 = round2(10000 x 11/12) = 9166.67 (11 meses com >= 15 dias), B p1 = 2750.00; INSS/IRRF recalculados sobre o bruto com 13o: A 19166.67/2361.48/3021.27/13783.92, B 5750.00/614.59/178.41/4957.00 (Lei 4.749/65 - BR-12.5)")
$dc1 = Invoke-Api -Bearer $tk12A.token -Method GET -Path ('/api/previdencia/decimo?vinculoId=' + $vA + '&ano=2026') -Base $ApiBase
$dc1j = Get-S12Data $dc1.body
Add-Check 'S12' 'decimo.situacao-parcela1-registrada' (($dc1.code -eq 200) -and ($null -ne $dc1j) -and ($dc1j.status -eq 'PARCELA1_REGISTRADA') -and (Get-S12Near $dc1j.parcela1Valor 9166.67m) -and ([int]$dc1j.mesesAte1aParcela -eq 11)) ("GET /api/previdencia/decimo A 2026 -> PARCELA1_REGISTRADA parcela1 9166.67 mesesAte1aParcela=11 (situacao persistida e consultavel - 1a parcela ate 30/11)")
$flD0 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-12-01","dataFixa":"2026-12-20"}'
$flD1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-12-01","dataFixa":"2026-12-20"}'
$d = Get-S12Data $flD1.body
$dbDecA = Get-Psql "select s.matricula_funcional||'|'||to_char(k.bruto,'FM999990.00')||'|'||to_char(k.inss_valor,'FM999990.00')||'|'||to_char(k.irrf_valor,'FM999990.00')||'|'||to_char(k.liquido,'FM999990.00')||'|'||to_char(k.decimo_valor,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_vinculo v on v.id=k.vinculo_id join sigov.previdencia_servidor s on s.id=v.servidor_id join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910001 and f.competencia=date '2026-12-01' and k.status='VIGENTE' order by s.matricula_funcional"
$normDecA = ($dbDecA.text.Trim() -replace '\s*\n\s*','~').Trim()
$dc2 = Invoke-Api -Bearer $tk12A.token -Method GET -Path ('/api/previdencia/decimo?vinculoId=' + $vA + '&ano=2026') -Base $ApiBase
$dc2j = Get-S12Data $dc2.body
Add-Check 'S12' 'decimo.dezembro-a-2a-parcela-encerrado' (($flD0.code -eq 200) -and ($flD1.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ($dbDecA.code -eq 0) -and ($normDecA -eq 'C2S12A-SRV1|10833.33|1194.81|1061.32|8577.20|833.33~C2S12A-SRV2|3250.00|283.41|0.00|2966.59|250.00') -and ($dc2.code -eq 200) -and ($null -ne $dc2j) -and ($dc2j.status -eq 'ENCERRADO') -and (Get-S12Near $dc2j.parcela1Valor 9166.67m) -and (Get-S12Near $dc2j.parcela2Valor 833.33m) -and ([int]$dc2j.mesesFinais -eq 12)) ("fechar 2026-12: 2a parcela = total anual - p1 (A: 10000-9166.67=833.33; B: 3000-2750=250.00); bruto A 10833.33/1194.81/1061.32/8577.20 e B 3250.00/283.41/0.00/2966.59 exatos; situacao do decimo A -> ENCERRADO mesesFinais=12 (2a parcela ate 20/12)")

# --- BR-12.2 teto constitucional (tenant B estadual) + decimo sem p1 -------------
$dlC0 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/desligamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vC + ',"dataDesligamento":"2026-08-31","motivo":""}')
$flB1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-09-01","dataFixa":"2026-09-20"}'
$flB2 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-09-01","dataFixa":"2026-09-20"}'
$dbTeto = Get-Psql "select fc.status||'|'||(select count(1) from sigov.previdencia_folha_calculo k where k.competencia_id=fc.id) from sigov.previdencia_folha_competencia fc where fc.tenant_id=910002 and fc.competencia=date '2026-09-01'"
Add-Check 'S12' 'folha.b-teto-estadual-409-servidor-acima-teto' (($dlC0.code -eq 400) -and (Get-S12Msg $dlC0.body) -like '*DESLIGAMENTO_SEM_MOTIVO*' -and ($flB1.code -eq 200) -and ($flB2.code -eq 409) -and (Get-S12Msg $flB2.body) -like '*SERVIDOR_ACIMA_TETO*' -and ($dbTeto.code -eq 0) -and ($dbTeto.text.Trim() -eq 'ABERTA|0')) ("fechar 2026-09 tenant B com C (bruto 200000 > teto estadual 44161.63) -> " + $flB2.code + " SERVIDOR_ACIMA_TETO (CF/88 art. 37 XV/XVI c/c EC 103/19); transacao rollbackada: folha permanece ABERTA e zero calculos persistidos - falha explicita, sem sucesso simulado (regra 13)")
$dlC1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/desligamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vC + ',"dataDesligamento":"2026-08-31","motivo":"ajuste estrutural homologacao"}')
$dlC2 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/desligamentos' -Base $ApiBase -Body ('{"vinculoId":' + $vC + ',"dataDesligamento":"2026-08-31","motivo":"segunda tentativa homologacao"}')
$flB3 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-09-01","dataFixa":"2026-09-20"}'
$d = Get-S12Data $flB3.body
$dbSetB = Get-Psql "select s.matricula_funcional||'|'||to_char(k.bruto,'FM999990.00')||'|'||to_char(k.inss_valor,'FM999990.00')||'|'||to_char(k.irrf_valor,'FM999990.00')||'|'||to_char(k.liquido,'FM999990.00')||'|'||to_char(k.decimo_valor,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_vinculo v on v.id=k.vinculo_id join sigov.previdencia_servidor s on s.id=v.servidor_id join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910002 and f.competencia=date '2026-09-01' and k.status='VIGENTE' order by s.matricula_funcional"
$normSetB = ($dbSetB.text.Trim() -replace '\s*\n\s*','~').Trim()
Add-Check 'S12' 'folha.b-desligado-c-setembro-fechado-valores' (($dlC1.code -eq 200) -and ($dlC2.code -eq 409) -and (Get-S12Msg $dlC2.body) -like '*VINCULO_NAO_ATIVO*' -and ($flB3.code -eq 200) -and ($null -ne $d) -and ($d.status -eq 'FECHADA') -and ([int]$d.calculosGerados -eq 2) -and ($dbSetB.code -eq 0) -and ($normSetB -eq 'C2S12B-AB|2000.00|157.23|0.00|1842.77|0.00~C2S12B-D1|5000.00|509.59|109.22|4381.19|0.00')) ("desligar C (motivo registrado) -> 200; repetir -> VINCULO_NAO_ATIVO; fechar 2026-09 de novo -> FECHADA com D 5000.00/509.59/109.22/4381.19 e AB 2000.00/157.23/0.00/1842.77 exatos (teto parametrico por esfera resolvido no banco)")
$flBo1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-10-01","dataFixa":"2026-10-20"}'
$flBd1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-12-01","dataFixa":"2026-12-20"}'
$flBd2 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-12-01","dataFixa":"2026-12-20"}'
$dbDecSemP1 = Get-Psql "select fc.status||'|'||(select count(1) from sigov.previdencia_folha_calculo k where k.competencia_id=fc.id) from sigov.previdencia_folha_competencia fc where fc.tenant_id=910002 and fc.competencia=date '2026-12-01'"
$flBn1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/abrir' -Base $ApiBase -Body '{"competencia":"2026-11-01","dataFixa":"2026-11-20"}'
$flBn2 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-11-01","dataFixa":"2026-11-20"}'
$dbNovB = Get-Psql "select s.matricula_funcional||'|'||to_char(k.decimo_valor,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_vinculo v on v.id=k.vinculo_id join sigov.previdencia_servidor s on s.id=v.servidor_id join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910002 and f.competencia=date '2026-11-01' and k.status='VIGENTE' order by s.matricula_funcional"
$normNovB = ($dbNovB.text.Trim() -replace '\s*\n\s*','~').Trim()
$flBd3 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/folhas/fechar' -Base $ApiBase -Body '{"competencia":"2026-12-01","dataFixa":"2026-12-20"}'
$dbDecB = Get-Psql "select s.matricula_funcional||'|'||to_char(k.bruto,'FM999990.00')||'|'||to_char(k.inss_valor,'FM999990.00')||'|'||to_char(k.irrf_valor,'FM999990.00')||'|'||to_char(k.liquido,'FM999990.00')||'|'||to_char(k.decimo_valor,'FM999990.00') from sigov.previdencia_folha_calculo k join sigov.previdencia_vinculo v on v.id=k.vinculo_id join sigov.previdencia_servidor s on s.id=v.servidor_id join sigov.previdencia_folha_competencia f on f.id=k.competencia_id where k.tenant_id=910002 and f.competencia=date '2026-12-01' and k.status='VIGENTE' order by s.matricula_funcional"
$normDecB = ($dbDecB.text.Trim() -replace '\s*\n\s*','~').Trim()
Add-Check 'S12' 'decimo.b-dez-sem-p1-409-nov-p1-dez-valor' (($flBo1.code -eq 200) -and ($flBd1.code -eq 200) -and ($flBd2.code -eq 409) -and (Get-S12Msg $flBd2.body) -like '*DECIMO_SEM_PARCELA1*' -and ($dbDecSemP1.code -eq 0) -and ($dbDecSemP1.text.Trim() -eq 'ABERTA|0') -and ($flBn1.code -eq 200) -and ($flBn2.code -eq 200) -and ($dbNovB.code -eq 0) -and ($normNovB -eq 'C2S12B-AB|1833.33~C2S12B-D1|4583.33') -and ($flBd3.code -eq 200) -and ($dbDecB.code -eq 0) -and ($normDecB -eq 'C2S12B-AB|2166.67|172.23|0.00|1994.44|166.67~C2S12B-D1|5416.67|567.93|135.41|4713.33|416.67')) ("fechar dezembro ANTES de novembro -> 409 DECIMO_SEM_PARCELA1 (folha ABERTA, rollback); fechar novembro -> p1 (AB 1833.33, D 4583.33 = round2(bruto x 11/12)); fechar dezembro -> 2a parcela (AB 166.67, D 416.67) com bruto INSS IRRF liquido exatos: AB 2166.67/172.23/0.00/1994.44, D 5416.67/567.93/135.41/4713.33")

# --- BR-12.8 eSocial publico ----------------------------------------------------
$loA1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"ADMISAO_S1005","dataFixa":"2026-09-20"}'
$loA1j = Get-S12Data $loA1.body; $lotAAdm = -1; if ($null -ne $loA1j) { $lotAAdm = [long]$loA1j.id }
$loA1b = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"ADMISAO_S1005","dataFixa":"2026-09-20"}'
$loA1bj = Get-S12Data $loA1b.body; $lotAAdmB = -1; if ($null -ne $loA1bj) { $lotAAdmB = [long]$loA1bj.id }
$loAs1 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/esocial/' + $lotAAdm + '/enviar') -Base $ApiBase
$loAs1j = Get-S12Data $loAs1.body
$loAs2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/esocial/' + $lotAAdm + '/enviar') -Base $ApiBase
$loAs2j = Get-S12Data $loAs2.body
$loA2 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"TABELAS_S1000_S1010","dataFixa":"2026-09-20"}'
$loA2j = Get-S12Data $loA2.body; $lotATab = -1; if ($null -ne $loA2j) { $lotATab = [long]$loA2j.id }
$loAs3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/esocial/' + $lotATab + '/enviar') -Base $ApiBase
$loA3 = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"REMUNERACAO_S1200","dataFixa":"2026-09-20"}'
$loA3j = Get-S12Data $loA3.body; $lotARem = -1; if ($null -ne $loA3j) { $lotARem = [long]$loA3j.id }
$loAs4 = Invoke-Api -Bearer $tk12A.token -Method POST -Path ('/api/previdencia/esocial/' + $lotARem + '/enviar') -Base $ApiBase
$loAd1 = Invoke-Api -Bearer $tk12A.token -Method GET -Path ('/api/previdencia/esocial/lotes/' + $lotAAdm) -Base $ApiBase
$loAd1j = Get-S12Data $loAd1.body
$loBad = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"XPTO","dataFixa":"2026-09-20"}'
$loBcomp = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-06-01","tipoLote":"ADMISAO_S1005","dataFixa":"2026-06-20"}'
$loBenv = Invoke-Api -Bearer $tk12A.token -Method POST -Path '/api/previdencia/esocial/999999/enviar' -Base $ApiBase
$okLoA = ($loA1.code -eq 200) -and ($null -ne $loA1j) -and ($loA1j.status -eq 'GERADO') -and ([int]$loA1j.totalEventos -eq 3) -and ([int]$loA1j.eventosComErro -eq 0) -and ($lotAAdmB -eq $lotAAdm) -and ($loAs1.code -eq 200) -and ($loAs1j.status -eq 'VALIDADO') -and ($loAs2.code -eq 200) -and ($loAs2j.status -eq 'VALIDADO') -and ($loA2.code -eq 200) -and ([int](($loA2j)).totalEventos -eq 4) -and ($loAs3.code -eq 200) -and ($loA3.code -eq 200) -and ([int](($loA3j)).totalEventos -eq 2) -and ($loAs4.code -eq 200) -and ($loAd1.code -eq 200) -and ($null -ne $loAd1j) -and (@($loAd1j.eventos).Count -eq 3) -and ($loBad.code -eq 400) -and (Get-S12Msg $loBad.body) -like '*LOTE_TIPO_INVALIDO*' -and ($loBcomp.code -eq 404) -and (Get-S12Msg $loBcomp.body) -like '*PREVIDENCIA_FOLHA_INEXISTENTE*' -and ($loBenv.code -eq 404) -and (Get-S12Msg $loBenv.body) -like '*PREVIDENCIA_LOTE_INEXISTENTE*'
Add-Check 'S12' 'esocial.a-lotes-idempotentes-validados-invalidos' $okLoA ("lote ADMISAO_S1005 set/2026 -> GERADO 3 eventos (A,B,E) 0 erros; gerar de novo -> MESMO lote (idempotente); enviar -> VALIDADO e reenvio idempotente; TABELAS_S1000_S1010 -> 4 eventos (S-1000 + 3 S-1010) VALIDADO; REMUNERACAO_S1200 -> 2 eventos (calculos vigentes) VALIDADO; detalhe lista 3 alvos; tipo XPTO -> 400 LOTE_TIPO_INVALIDO; competencia 2026-06 sem folha -> 404 PREVIDENCIA_FOLHA_INEXISTENTE; lote 999999 -> 404 PREVIDENCIA_LOTE_INEXISTENTE (Portaria MTP 672/19)")
$loB1 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"ADMISAO_S1005","dataFixa":"2026-09-20"}'
$loB1j = Get-S12Data $loB1.body; $lotBAdm = -1; if ($null -ne $loB1j) { $lotBAdm = [long]$loB1j.id }
$loB1s = Invoke-Api -Bearer $tk12B.token -Method POST -Path ('/api/previdencia/esocial/' + $lotBAdm + '/enviar') -Base $ApiBase
$dbBloq = Get-Psql "select l.status||'|'||case when l.enviado_em is null then 'NULL' else 'SET' end from sigov.previdencia_esocial_lote l where l.id=$lotBAdm"
$loB1d = Invoke-Api -Bearer $tk12B.token -Method GET -Path ('/api/previdencia/esocial/lotes/' + $lotBAdm) -Base $ApiBase
$loB1dj = Get-S12Data $loB1d.body
$evAB = $null; if ($null -ne $loB1dj) { $evAB = @($loB1dj.eventos) | Where-Object { $_.alvoTexto -eq 'C2S12B-AB' } | Select-Object -First 1 }
$okEvAB = ($null -ne $evAB) -and ($evAB.validacaoOk -eq $false) -and (($evAB.mensagens -join ' ') -like '*nome do servidor curto*')
Add-Check 'S12' 'esocial.b-admisao-erro-validacao-bloqueada' (($loB1.code -eq 200) -and ($null -ne $loB1j) -and ([int]$loB1j.totalEventos -eq 3) -and ([int]$loB1j.eventosComErro -eq 1) -and ($loB1s.code -eq 409) -and (Get-S12Msg $loB1s.body) -like '*BLOQUEADO_VALIDACAO*' -and ($dbBloq.code -eq 0) -and ($dbBloq.text.Trim() -eq 'BLOQUEADO_VALIDACAO|NULL') -and $okEvAB) ("lote ADMISAO_S1005 tenant B: 3 eventos (D,C,AB) com 1 erro - servidor AB tem nome < 5 caracteres; enviar -> 409 BLOQUEADO_VALIDACAO; banco: status BLOQUEADO_VALIDACAO e enviado_em NULL (sem sucesso simulado - regra 13); detalhe aponta o evento C2S12B-AB invalido com mensagem")
$loB2 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-10-01","tipoLote":"MOVIMENTACAO_S1006","dataFixa":"2026-10-20"}'
$loB2j = Get-S12Data $loB2.body; $lotBMov = -1; if ($null -ne $loB2j) { $lotBMov = [long]$loB2j.id }
$loB2s = Invoke-Api -Bearer $tk12B.token -Method POST -Path ('/api/previdencia/esocial/' + $lotBMov + '/enviar') -Base $ApiBase
$loB3 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-09-01","tipoLote":"REMUNERACAO_S1200","dataFixa":"2026-09-20"}'
$loB3j = Get-S12Data $loB3.body; $lotBRem = -1; if ($null -ne $loB3j) { $lotBRem = [long]$loB3j.id }
$loB3s = Invoke-Api -Bearer $tk12B.token -Method POST -Path ('/api/previdencia/esocial/' + $lotBRem + '/enviar') -Base $ApiBase
$loB4 = Invoke-Api -Bearer $tk12B.token -Method POST -Path '/api/previdencia/esocial/gerar-lote' -Base $ApiBase -Body '{"competencia":"2026-10-01","tipoLote":"REMUNERACAO_S1200","dataFixa":"2026-10-20"}'
Add-Check 'S12' 'esocial.b-movimentacao-oct-validado-remuneracao-set-ok-out-409' (($loB2.code -eq 200) -and ($null -ne $loB2j) -and ([int]$loB2j.totalEventos -eq 1) -and ([int]$loB2j.eventosComErro -eq 0) -and ($loB2s.code -eq 200) -and ($loB3.code -eq 200) -and ([int](($loB3j)).totalEventos -eq 2) -and ($loB3s.code -eq 200) -and ($loB4.code -eq 409) -and (Get-S12Msg $loB4.body) -like '*FOLHA_NAO_FECHADA_PARA_LOTE*') ("MOVIMENTACAO_S1006 out/2026 -> 1 evento (saude AB iniciada em outubro, GED presente) VALIDADO; REMUNERACAO_S1200 set (fechada) -> 2 eventos VALIDADO; out (ABERTA) -> 409 FOLHA_NAO_FECHADA_PARA_LOTE")

# --- BR-12.6 parametros (banco = autoridade) ------------------------------------
$parA = Invoke-Api -Bearer $tk12A.token -Method GET -Path '/api/previdencia/parametros' -Base $ApiBase
$parAj = Get-S12Data $parA.body
$rowTeto = $null; $rowReg = $null; if ($null -ne $parAj) { $rowsA = @($parAj); $rowTeto = $rowsA | Where-Object { $_.codigo -eq 'TETO_REMUNERACAO_ESTADUAL' } | Select-Object -First 1; $rowReg = $rowsA | Where-Object { $_.codigo -eq 'PREVIDENCIA_REGIME' } | Select-Object -First 1 }
$parB = Invoke-Api -Bearer $tk12B.token -Method GET -Path '/api/previdencia/parametros' -Base $ApiBase
$parBj = Get-S12Data $parB.body
$rowOvr = $null; if ($null -ne $parBj) { $rowsB = @($parBj); $rowOvr = $rowsB | Where-Object { $_.codigo -eq 'FERIAS_FRAC_MINIMO_DIAS' } | Select-Object -First 1 }
$okParA = ($parA.code -eq 200) -and ($null -ne $parAj) -and (@($parAj).Count -ge 17) -and ($null -ne $rowTeto) -and ($rowTeto.origem -eq 'PADRAO') -and ($rowTeto.valorEfetivo -eq '44161.63') -and ($null -ne $rowReg) -and ($rowReg.valorEfetivo -eq 'RGPS')
$okParB = ($parB.code -eq 200) -and ($null -ne $rowOvr) -and ($rowOvr.origem -eq 'OVERRIDE') -and ($rowOvr.valorEfetivo -eq '7')
Add-Check 'S12' 'parametros.a-padrao-b-override-observaveis' ($okParA -and $okParB) ("GET /api/previdencia/parametros: tenant A expoe >= 17 parametros do modulo PREVIDENCIA com TETO_REMUNERACAO_ESTADUAL=44161.63 (PADRAO) e PREVIDENCIA_REGIME=RGPS; tenant B expoe FERIAS_FRAC_MINIMO_DIAS=7 com origem OVERRIDE (override por tenant sobre o padrao institucional - regra 11/12/24 observavel no banco)")

# --- Dashboard + contexto institucional + fail-closed ---------------------------
$dashA = Invoke-Api -Bearer $tk12A.token -Method GET -Path '/api/previdencia/dashboard' -Base $ApiBase
$dashAj = Get-S12Data $dashA.body
$dashB = Invoke-Api -Bearer $tk12B.token -Method GET -Path '/api/previdencia/dashboard' -Base $ApiBase
$dashBj = Get-S12Data $dashB.body
$okDashA = ($dashA.code -eq 200) -and ($null -ne $dashAj) -and ($dashAj.esferaGoverno -eq 'municipal') -and ([int]$dashAj.cargosAtivos -eq 3) -and ([int]$dashAj.servidoresAtivos -eq 2) -and ([int]$dashAj.competenciasFechadas -eq 4) -and ([int]$dashAj.calculosVigentes -eq 9) -and ([int]$dashAj.feriasCiclosComSaldo -eq 1) -and ([int]$dashAj.afastamentosAtivos -eq 1) -and ([int]$dashAj.lotesEsocialValidados -eq 3) -and (Get-S12Near $dashAj.totalLiquidoUltimaFolha 11543.79m)
$okDashB = ($dashB.code -eq 200) -and ($null -ne $dashBj) -and ($dashBj.esferaGoverno -eq 'estadual')
Add-Check 'S12' 'dashboard.indicadores-travados-multi-esfera' ($okDashA -and $okDashB) ("dashboard A: esfera municipal (contexto do usuario), 3 cargos, 2 servidores ativos, 4 competencias fechadas, 9 calculos vigentes, 1 ciclo com saldo, 1 afastamento ativo (adocao), 3 lotes eSocial validados, liquido ultima folha 11543.79 (A 8577.20 + B 2966.59); dashboard B: esfera estadual - cada usuario ve a propria esfera (regra 21/22, nada municipal hardcoded)")
$fa1 = Invoke-Api -Bearer $tk12SemA.token -Method GET -Path '/api/previdencia/dashboard' -Base $ApiBase
$fa2 = Invoke-Api -Bearer $tk12SemA.token -Method POST -Path '/api/previdencia/cargos' -Base $ApiBase -Body '{"codigoExterno":"CARGO-SEM","nome":"Sem Permissao C2S12A","modoRemuneracao":"SUBSIDIO","valorReferencia":1}'
$fa3 = Invoke-Api -Bearer $tk12SemB.token -Method GET -Path '/api/previdencia/dashboard' -Base $ApiBase
Add-Check 'S12' 'auth.failclosed-semperm-403' (($fa1.code -eq 403) -and ($fa2.code -eq 403) -and ($fa3.code -eq 403)) ("usuarios semperm (perfil logavel com 1 chave inofensiva FORA da frente): GET dashboard -> 403, POST cargos -> 403 nos dois tenants - autorizacao fail-closed por chave pontuada (regra 11)")
$cx1 = Invoke-Api -Bearer $tk12B.token -Method GET -Path ('/api/previdencia/contracheque?vinculoId=' + $vA + '&competencia=2026-09-01') -Base $ApiBase
$cx2 = Invoke-Api -Bearer $tk12A.token -Method GET -Path ('/api/previdencia/contracheque?vinculoId=' + $vD + '&competencia=2026-09-01') -Base $ApiBase
Add-Check 'S12' 'auth.cruz-tenant-404-calculo-isolado' (($cx1.code -eq 404) -and (Get-S12Msg $cx1.body) -like '*PREVIDENCIA_CALCULO_INEXISTENTE*' -and ($cx2.code -eq 404) -and (Get-S12Msg $cx2.body) -like '*PREVIDENCIA_CALCULO_INEXISTENTE*') ("admin B consulta contracheque do vinculo A (tenant 910001) -> 404 PREVIDENCIA_CALCULO_INEXISTENTE; admin A consulta o vinculo D (tenant 910002) -> 404 - isolamento de tenant no acesso aos calculos")
$sc1 = Get-Psql "alter table sigov.previdencia_cargo rename to previdencia_cargo_s12probe"
$sc2 = Invoke-Api -Bearer $tk12A.token -Method GET -Path '/api/previdencia/cargos' -Base $ApiBase
$sc3 = Get-Psql "alter table sigov.previdencia_cargo_s12probe rename to previdencia_cargo"
$sc4 = Invoke-Api -Bearer $tk12A.token -Method GET -Path '/api/previdencia/cargos' -Base $ApiBase
Add-Check 'S12' 'schema.ausente-404-nomeado-restaurado' (($sc1.code -eq 0) -and ($sc2.code -eq 404) -and (Get-S12Msg $sc2.body) -like '*PREVIDENCIA_NAO_DISPONIVEL*' -and ($sc3.code -eq 0) -and ($sc4.code -eq 200)) ("schema ausente (previdencia_cargo renomeada): GET /api/previdencia/cargos -> 404 PREVIDENCIA_NAO_DISPONIVEL com causa nomeada; schema restaurada -> 200 (falha explicita em vez de lista vazia - regra 13)")

# --- limpeza do fixture S12 -----------------------------------------------------
$s12cl = Get-Psql @"
delete from sigov.previdencia_folha_calculo_item where calculo_id in (select id from sigov.previdencia_folha_calculo where tenant_id in (910001,910002));
delete from sigov.previdencia_folha_calculo where tenant_id in (910001,910002);
delete from sigov.previdencia_remun_item where vinculo_id in (select id from sigov.previdencia_vinculo where tenant_id in (910001,910002));
delete from sigov.previdencia_ferias_programacao where ferias_id in (select id from sigov.previdencia_ferias where tenant_id in (910001,910002));
delete from sigov.previdencia_esocial_evento where lote_id in (select id from sigov.previdencia_esocial_lote where tenant_id in (910001,910002));
delete from sigov.previdencia_decimo where tenant_id in (910001,910002);
delete from sigov.previdencia_afastamento where tenant_id in (910001,910002);
delete from sigov.previdencia_esocial_lote where tenant_id in (910001,910002);
delete from sigov.previdencia_folha_competencia where tenant_id in (910001,910002);
delete from sigov.previdencia_ferias where tenant_id in (910001,910002);
delete from sigov.previdencia_vinculo where tenant_id in (910001,910002);
delete from sigov.previdencia_servidor where tenant_id in (910001,910002);
delete from sigov.previdencia_cargo where tenant_id in (910001,910002);
delete from sigov.parametro_modulo_valor where correlation_id='c2s12-frac-override-e2e';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000021','cf58c2ab-1c20-4a90-88f8-0c2000000022','cf58c2ab-1c20-4a90-88f8-0c2000000023','cf58c2ab-1c20-4a90-88f8-0c2000000024');
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'));
delete from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%');
delete from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S12A-SEMP') or (tenant_id=910002 and codigo_externo in ('C2S12B-ADM','C2S12B-SEMP')));
delete from sigov.perfil_acesso where (tenant_id=910001 and nome='C2S12A Perfil SemFrente') or (tenant_id=910002 and nome in ('C2S12B Perfil Administracao','C2S12B Perfil SemFrente'));
delete from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S12A-SEMP') or (tenant_id=910002 and codigo_externo in ('C2S12B-ADM','C2S12B-SEMP'));
delete from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S12A-ENT') or (tenant_id=910002 and codigo_externo='C2S12B-ENT');
select (select count(*) from sigov.previdencia_folha_calculo_item ci join sigov.previdencia_folha_calculo k on k.id=ci.calculo_id where k.tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_folha_calculo where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_remun_item ri join sigov.previdencia_vinculo v on v.id=ri.vinculo_id where v.tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_ferias_programacao pp join sigov.previdencia_ferias f on f.id=pp.ferias_id where f.tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_esocial_evento ee join sigov.previdencia_esocial_lote l on l.id=ee.lote_id where l.tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_decimo where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_afastamento where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_esocial_lote where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_folha_competencia where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_ferias where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_vinculo where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_servidor where tenant_id in (910001,910002))
  + (select count(*) from sigov.previdencia_cargo where tenant_id in (910001,910002))
  + (select count(*) from sigov.parametro_modulo_valor where correlation_id='c2s12-frac-override-e2e')
  + (select count(*) from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000021','cf58c2ab-1c20-4a90-88f8-0c2000000022','cf58c2ab-1c20-4a90-88f8-0c2000000023','cf58c2ab-1c20-4a90-88f8-0c2000000024'))
  + (select count(*) from sigov.usuario where (tenant_id=910001 and login like 'c2s12a.%') or (tenant_id=910002 and login like 'c2s12b.%'))
  + (select count(*) from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S12A-SEMP') or (tenant_id=910002 and codigo_externo in ('C2S12B-ADM','C2S12B-SEMP'))))
  + (select count(*) from sigov.perfil_acesso where (tenant_id=910001 and nome='C2S12A Perfil SemFrente') or (tenant_id=910002 and nome in ('C2S12B Perfil Administracao','C2S12B Perfil SemFrente')))
  + (select count(*) from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S12A-SEMP') or (tenant_id=910002 and codigo_externo in ('C2S12B-ADM','C2S12B-SEMP')))
  + (select count(*) from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S12A-ENT') or (tenant_id=910002 and codigo_externo='C2S12B-ENT'));
"@
Add-Check 'S12' 'fixture.removido-auto-limpeza-restantes-zero' (($s12cl.code -eq 0) -and ($s12cl.text -eq '0')) ("cleanup cirurgico S12 nos DOIS tenants (calculos+items, remun_items, ferias+programacoes, afastamentos, decimo, lotes+eventos eSocial, competencias, vinculos, servidores, cargos, override c2s12-frac-override-e2e, sessoes/auditorias dos 4 usuarios, grants sentinel cf58c2ab-...021-024, usuarios c2s12a.*/c2s12b.*, grupos+perfis dedicados C2S12, entidades C2S12A-ENT/C2S12B-ENT) -> restantes=" + $s12cl.text)

} # fim bloco S12 (pulado com -OnlyS13)

# =============================================================================
# SPRINT 13 (C2S13) - Orcamento e Responsabilidade Fiscal multi-esfera (BR-13.1..BR-13.8)
# =============================================================================
# E2E HTTP real (Web :5099 -> ApiToken -> bearer na Api :5100 -> Dapper -> PG16):
#   BR-13.1 estrutura Lei 4.320/64 (catalogo nacional de funcoes/subfuncoes no banco;
#           programa; acao c/ poder, categoria economica e grupo de despesa)
#   BR-13.2 dotacao unica por acao x fonte + credito suplementar c/ ato legal e
#           justificativa parametrizada; disponibilidade = inicial + creditos - reducoes - empenhado
#   BR-13.3 empenho CP com trava de disponibilidade (EMPENHO_ORCAMENTO_INSUFICIENTE) e
#           teto LRF pos-movimento por TRANSICAO (LRF_LIMITE_BLOQUEADO; ja em bloqueio passa)
#   BR-13.4 liquidacao SL <= empenho disponivel por item (LIQUIDACAO_SUPER_EMPENHO)
#   BR-13.5 limites LRF LC 101/2000 por esfera + override por tenant (OVERRIDE_TENANT) +
#           apuracao versionada append-only (MDE CF art. 212 parametrico; pessoal art. 20)
#   BR-13.6 previsao bimestral deterministica (art. 51) com vigente unico por bimestre
#   BR-13.7 RREO semestral/anual (art. 54) com conjuntos travados dos dados reais
#   BR-13.8 competencia fechada (EXERCICIO_ENCERRADO bloqueia movimento novo; reabertura auditavel)
# Valores travados: tenant A municipal (base receita prevista 1.000.000; dotacao MDE 1.000.000
#   + credito suplementar 250.000 -> autorizado 1.250.000 / disponivel 650.000 apos empenho
#   600.000; empenho 700.000 insuficiente; liquidacao 400.000 ok / 300.000 super empenho;
#   pessoal executivo 45% ok, +5% -> 50% BLOQUEADO por transicao; MDE 2025 = 23,5% ALERTA)
#   tenant B estadual (base 1.000.000; pessoal 48% ALERTA institucional 45/50 -> override
#   tenant bloqueio 45,5% -> apuracao BLOQUEIO persistida; empenho ja-em-bloqueio permitido)
function Get-S13Data { param([string]$Body) try { return ($Body | ConvertFrom-Json).data } catch { return $null } }
function Get-S13Msg { param([string]$Body) try { $j = $Body | ConvertFrom-Json; if ($null -ne $j.message) { return [string]$j.message } } catch {}; return [string]$Body }
function Get-S13Near { param([object]$V,[object]$T) try { $dV = [decimal]([string]$V).Trim().TrimEnd('m'); $dT = [decimal]([string]$T).Trim().TrimEnd('m'); return ([math]::Abs([double]$dV - [double]$dT) -lt 0.005) } catch { return $false } }

$s13fix = Get-Psql @"
-- pre-clean reentravel (escopo igual ao da limpeza final do bloco S13; tenants 910001/910002 sao do driver neste ciclo)
delete from sigov.orcamento_liquidacao_item where liquidacao_id in (select id from sigov.orcamento_liquidacao where tenant_id in (910001,910002));
delete from sigov.orcamento_liquidacao where tenant_id in (910001,910002);
delete from sigov.orcamento_empenho_item where empenho_id in (select id from sigov.orcamento_empenho where tenant_id in (910001,910002));
delete from sigov.orcamento_empenho where tenant_id in (910001,910002);
delete from sigov.orcamento_credito_adicional where tenant_id in (910001,910002);
delete from sigov.orcamento_dotacao where tenant_id in (910001,910002);
delete from sigov.orcamento_receita_arrecadacao where tenant_id in (910001,910002);
delete from sigov.orcamento_receita_prevista where tenant_id in (910001,910002);
delete from sigov.orcamento_lrf_apuracao where tenant_id in (910001,910002);
delete from sigov.orcamento_previsao_bimestral where tenant_id in (910001,910002);
delete from sigov.orcamento_rreo_item where lote_id in (select id from sigov.orcamento_rreo_lote where tenant_id in (910001,910002));
delete from sigov.orcamento_rreo_lote where tenant_id in (910001,910002);
delete from sigov.orcamento_estado_exercicio where tenant_id in (910001,910002);
delete from sigov.orcamento_acao where tenant_id in (910001,910002);
delete from sigov.orcamento_programa where tenant_id in (910001,910002);
delete from sigov.orcamento_limite_lrf where tenant_id in (910001,910002);
delete from sigov.parametro_modulo_valor where correlation_id='c2s13-anul-override-e2e';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000031','cf58c2ab-1c20-4a90-88f8-0c2000000032','cf58c2ab-1c20-4a90-88f8-0c2000000033');
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%');
delete from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S13A-SEMP') or (tenant_id=910002 and codigo_externo='C2S13B-ADM'));
delete from sigov.perfil_acesso where (tenant_id=910001 and nome='C2S13A Perfil SemFrente') or (tenant_id=910002 and nome='C2S13B Perfil Orcamento');
delete from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S13A-SEMP') or (tenant_id=910002 and codigo_externo='C2S13B-ADM');
delete from sigov.exercicio where entidade_id in (select id from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S13A-ENT') or (tenant_id=910002 and codigo_externo='C2S13B-ENT'));
delete from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S13A-ENT') or (tenant_id=910002 and codigo_externo='C2S13B-ENT');
-- Entidades com esfera (regras 21/22): A municipal / B estadual - o contexto do usuario define a esfera do motor
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910001,'C2S13A Entidade Municipal','00.000.000/0004-01','C2S13A-ENT','municipal','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910001 and codigo_externo='C2S13A-ENT');
insert into sigov.entidade(tenant_id,nome,cnpj,codigo_externo,esfera_governo,tipo_entidade,ativo,is_deleted,created_at,created_by)
select 910002,'C2S13B Entidade Estadual','00.000.000/0004-02','C2S13B-ENT','estadual','SECRETARIA',true,false,now(),910301
where not exists(select 1 from sigov.entidade where tenant_id=910002 and codigo_externo='C2S13B-ENT');
-- Competencias do exercicio orcamentario (sigov.exercicio por entidade x ano)
insert into sigov.exercicio(entidade_id,ano,data_inicio,data_fim,codigo_externo,observacao,ativo,is_deleted,created_at,created_by,tenant_id)
select e.id,2025,date '2025-01-01',date '2025-12-31','C2S13A-EX2025','Homologacao C2S13',true,false,now(),910301,e.tenant_id
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S13A-ENT'
on conflict (entidade_id,ano) do nothing;
insert into sigov.exercicio(entidade_id,ano,data_inicio,data_fim,codigo_externo,observacao,ativo,is_deleted,created_at,created_by,tenant_id)
select e.id,2026,date '2026-01-01',date '2026-12-31','C2S13A-EX2026','Homologacao C2S13',true,false,now(),910301,e.tenant_id
from sigov.entidade e where e.tenant_id=910001 and e.codigo_externo='C2S13A-ENT'
on conflict (entidade_id,ano) do nothing;
insert into sigov.exercicio(entidade_id,ano,data_inicio,data_fim,codigo_externo,observacao,ativo,is_deleted,created_at,created_by,tenant_id)
select e.id,2026,date '2026-01-01',date '2026-12-31','C2S13B-EX2026','Homologacao C2S13',true,false,now(),910301,e.tenant_id
from sigov.entidade e where e.tenant_id=910002 and e.codigo_externo='C2S13B-ENT'
on conflict (entidade_id,ano) do nothing;
-- Grupos/perfis dedicados (admin A reutiliza o grupo 910401 existente do tenant)
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910001,'C2S13A Grupo SemFrente','C2S13A-SEMP',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910001 and codigo_externo='C2S13A-SEMP');
insert into sigov.grupo_acesso(tenant_id,nome,codigo_externo,ativo,is_deleted,created_at,created_by)
select 910002,'C2S13B Grupo Administracao','C2S13B-ADM',true,false,now(),910301
where not exists(select 1 from sigov.grupo_acesso where tenant_id=910002 and codigo_externo='C2S13B-ADM');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910001,'C2S13A Perfil SemFrente',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910001 and nome='C2S13A Perfil SemFrente');
insert into sigov.perfil_acesso(tenant_id,nome,ativo,is_deleted,created_at,created_by)
select 910002,'C2S13B Perfil Orcamento',true,false,now(),910301
where not exists(select 1 from sigov.perfil_acesso where tenant_id=910002 and nome='C2S13B Perfil Orcamento');
insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,tenant_id,ativo,is_deleted,created_at,created_by)
select g.id,p.id,g.tenant_id,true,false,now(),910301
from sigov.grupo_acesso g cross join sigov.perfil_acesso p
where ((g.tenant_id=910001 and g.codigo_externo='C2S13A-SEMP' and p.tenant_id=910001 and p.nome='C2S13A Perfil SemFrente')
   or (g.tenant_id=910002 and g.codigo_externo='C2S13B-ADM' and p.tenant_id=910002 and p.nome='C2S13B Perfil Orcamento'))
and not exists(select 1 from sigov.grupo_perfil gp where gp.grupo_acesso_id=g.id and gp.perfil_acesso_id=p.id and gp.ativo);
-- Usuarios (senha_hash copiado do admin 910301; contexto institucional = entidade do usuario)
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S13A Administrador','c2s13a.admin','c2s13a.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S13A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s13a.admin');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910001,e.id,1,'C2S13A SemFrente','c2s13a.semperm','c2s13a.semperm@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910001 and e.codigo_externo='C2S13A-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910001 and x.login='c2s13a.semperm');
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,nome,login,email,senha_hash,tipo_usuario,ativo,is_deleted,created_at,created_by)
select 910002,e.id,1,'C2S13B Administrador','c2s13b.admin','c2s13b.admin@homolog.local',u.senha_hash,'OPERADOR',true,false,now(),910301
from sigov.entidade e cross join sigov.usuario u
where e.tenant_id=910002 and e.codigo_externo='C2S13B-ENT' and u.id=910301
and not exists(select 1 from sigov.usuario x where x.tenant_id=910002 and x.login='c2s13b.admin');
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,910401,910001,true,now(),910301
from sigov.usuario u
where u.tenant_id=910001 and u.login='c2s13a.admin'
and not exists(select 1 from sigov.usuario_grupo g where g.usuario_id=u.id and g.grupo_acesso_id=910401 and g.ativo);
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,tenant_id,ativo,created_at,created_by)
select u.id,g.id,u.tenant_id,true,now(),910301
from sigov.usuario u cross join sigov.grupo_acesso g
where ((u.tenant_id=910001 and u.login='c2s13a.semperm' and g.tenant_id=910001 and g.codigo_externo='C2S13A-SEMP')
   or (u.tenant_id=910002 and u.login='c2s13b.admin' and g.tenant_id=910002 and g.codigo_externo='C2S13B-ADM'))
and not exists(select 1 from sigov.usuario_grupo gu where gu.usuario_id=u.id and gu.grupo_acesso_id=g.id and gu.ativo);
-- Grants sentinel (limpeza cirurgica por correlation_id): 13 chaves orcamento.* x admins
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000031'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.id=(select gp.perfil_acesso_id from sigov.grupo_perfil gp where gp.grupo_acesso_id=910401 limit 1)
  and p.ativo and not p.is_deleted and p.modulo='orcamento'
on conflict (perfil_acesso_id,permissao_id) do nothing;
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910002,'cf58c2ab-1c20-4a90-88f8-0c2000000032'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910002 and pa.nome='C2S13B Perfil Orcamento'
  and p.ativo and not p.is_deleted and p.modulo='orcamento'
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Perfil SemFrente: 1 chave inofensiva FORA da frente (necessaria p/ o login autenticar)
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted,created_at,tenant_id,correlation_id)
select pa.id,p.id,'PERMITIR',true,false,now(),910001,'cf58c2ab-1c20-4a90-88f8-0c2000000033'
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.tenant_id=910001 and pa.nome='C2S13A Perfil SemFrente'
  and p.modulo='patrimonio' and p.chave='patrimonio.bem.criar' and p.ativo and not p.is_deleted
on conflict (perfil_acesso_id,permissao_id) do nothing;
-- Overrides LRF de tenant (organicos, regras 11/24): RESTOS A alerta 55/bloqueio 75 e B alerta 50/bloqueio 75 (institucional 25/30)
insert into sigov.orcamento_limite_lrf(tenant_id,esfera_governo,chave,nome,tipo_comparacao,valor_alvo,alerta_pct,bloqueio_pct,base_receita,ativo,created_by)
select 910001,'municipal','RESTOS_A_PAGAR_MAX',l.nome,'MAX',30.0000,55.0000,75.0000,'PREVISTA',true,910301
from sigov.orcamento_limite_lrf l
where l.tenant_id is null and l.esfera_governo='municipal' and l.chave='RESTOS_A_PAGAR_MAX' and not l.is_deleted
and not exists(select 1 from sigov.orcamento_limite_lrf x where x.tenant_id=910001 and x.esfera_governo='municipal' and x.chave='RESTOS_A_PAGAR_MAX');
insert into sigov.orcamento_limite_lrf(tenant_id,esfera_governo,chave,nome,tipo_comparacao,valor_alvo,alerta_pct,bloqueio_pct,base_receita,ativo,created_by)
select 910002,'estadual','RESTOS_A_PAGAR_MAX',l.nome,'MAX',30.0000,50.0000,75.0000,'PREVISTA',true,910301
from sigov.orcamento_limite_lrf l
where l.tenant_id is null and l.esfera_governo='estadual' and l.chave='RESTOS_A_PAGAR_MAX' and not l.is_deleted
and not exists(select 1 from sigov.orcamento_limite_lrf x where x.tenant_id=910002 and x.esfera_governo='estadual' and x.chave='RESTOS_A_PAGAR_MAX');
-- Override parametrico do tenant A (comportamental, regra 24): min justificativa de anulacao 30 (padrao institucional 5)
insert into sigov.parametro_modulo_valor(tenant_id,parametro_id,valor,correlation_id,created_by,is_deleted)
select 910001,id,'30'::jsonb,'c2s13-anul-override-e2e',910301,false
from sigov.parametro_modulo
where modulo='ORCAMENTO' and codigo='ORCAMENTO_ANULACAO_JUSTIFICATIVA_MIN_CARACTERES'
and not exists(select 1 from sigov.parametro_modulo_valor val where val.correlation_id='c2s13-anul-override-e2e');
-- assert DO: banco = autoridade (regras 11/12/13) - sem isto nenhuma etapa da frente e valida
do `$`$
begin
  if (select count(1) from sigov.tenant t where t.id in (910001,910002) and t.ativo and not t.is_deleted) <> 2 then
    raise exception 'C2S13-FIXTURE: tenants 910001/910002 ausentes ou inativos';
  end if;
  if (select count(1) from sigov.orcamento_funcao where ativo and not is_deleted) <> 19 then
    raise exception 'C2S13-FIXTURE: catalogo nacional de funcoes incompleto (esperado 19)';
  end if;
  if (select count(distinct codigo) from sigov.orcamento_funcao where codigo in ('09','17') and ativo and not is_deleted) <> 2 then
    raise exception 'C2S13-FIXTURE: funcoes 09/17 ausentes do catalogo';
  end if;
  if (select count(distinct codigo) from sigov.orcamento_subfuncao where codigo in ('302','271') and ativo and not is_deleted) <> 2 then
    raise exception 'C2S13-FIXTURE: subfuncoes 302/271 ausentes do catalogo';
  end if;
  if (select count(1) from sigov.orcamento_limite_lrf where tenant_id is null and ativo and not is_deleted) <> 18 then
    raise exception 'C2S13-FIXTURE: limites institucionais por esfera <> 18';
  end if;
  if (select count(1) from sigov.orcamento_limite_lrf where tenant_id in (910001,910002) and ativo and not is_deleted) <> 2 then
    raise exception 'C2S13-FIXTURE: overrides LRF por tenant <> 2';
  end if;
  if (select count(1) from sigov.permissao p where p.modulo='orcamento' and p.chave like 'orcamento.%' and p.ativo and not p.is_deleted) <> 13 then
    raise exception 'C2S13-FIXTURE: permissoes orcamento.* (dotted) <> 13';
  end if;
  if (select count(1) from sigov.parametro_modulo p where p.modulo='ORCAMENTO' and p.ativo and not p.is_deleted) <> 5 then
    raise exception 'C2S13-FIXTURE: parametros ORCAMENTO <> 5';
  end if;
  if replace((select coalesce(valor_padrao::text,'') from sigov.parametro_modulo where modulo='ORCAMENTO' and codigo='ORCAMENTO_APURO_LRF_AUTO' and ativo and not is_deleted limit 1),'"','') <> 'true' then
    raise exception 'C2S13-FIXTURE: padrao ORCAMENTO_APURO_LRF_AUTO <> true';
  end if;
  if (select count(1) from sigov.exercicio e join sigov.entidade en on en.id=e.entidade_id where ((en.tenant_id=910001 and en.codigo_externo='C2S13A-ENT') or (en.tenant_id=910002 and en.codigo_externo='C2S13B-ENT')) and not e.is_deleted) <> 3 then
    raise exception 'C2S13-FIXTURE: competencias do fixture <> 3';
  end if;
end
`$`$;
-- ids pipe-separated: competencias A2025|A2026|B2026
select coalesce((select e.id::text from sigov.exercicio e join sigov.entidade en on en.id=e.entidade_id where en.tenant_id=910001 and en.codigo_externo='C2S13A-ENT' and e.ano=2025),'0')
  || '|' || coalesce((select e.id::text from sigov.exercicio e join sigov.entidade en on en.id=e.entidade_id where en.tenant_id=910001 and en.codigo_externo='C2S13A-ENT' and e.ano=2026),'0')
  || '|' || coalesce((select e.id::text from sigov.exercicio e join sigov.entidade en on en.id=e.entidade_id where en.tenant_id=910002 and en.codigo_externo='C2S13B-ENT' and e.ano=2026),'0');
"@
$s13ids = $s13fix.text.Trim() -split '\|'
$exA25 = $s13ids[0]; $exA26 = $s13ids[1]; $exB26 = $s13ids[2]
Add-Check 'S13' 'fixture.criado-idempotente-banco-autoridade' (($s13fix.code -eq 0) -and $exA25 -ne '0' -and $exA26 -ne '0' -and $exB26 -ne '0') ("pre-clean reentravel + entidades C2S13A-ENT(municipal)/C2S13B-ENT(estadual) com competencias 2025/2026(A) e 2026(B), 3 usuarios c2s13*, grupos+perfis C2S13, grants sentinel cf58c2ab-...031/032 (13 chaves orcamento) / 033 (1 chave inofensiva), overrides LRF RESTOS A 55/75 e B 50/75, override parametro ANULACAO_MIN=30 no tenant A; assert DO: tenants ativos, 19 funcoes, funcoes/subfuncoes usadas, 18 limites institucionais, 2 overrides, 13 permissoes, 5 parametros, apuro auto true, 3 competencias -> ids=" + ($s13ids -join '|'))

$lg13A = New-SigovLogin -User 'c2s13a.admin' -Pass $Password -Base $WebBase
$tk13A = if ($lg13A.ok) { Get-ApiToken -Jar $lg13A.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lg13SemA = New-SigovLogin -User 'c2s13a.semperm' -Pass $Password -Base $WebBase
$tk13SemA = if ($lg13SemA.ok) { Get-ApiToken -Jar $lg13SemA.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$lg13B = New-SigovLogin -User 'c2s13b.admin' -Pass $Password -Base $WebBase
$tk13B = if ($lg13B.ok) { Get-ApiToken -Jar $lg13B.jar -Base $WebBase } else { [pscustomobject]@{ token='' } }
$dash13A0 = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/dashboard?ano=2026' -Base $ApiBase
$dash13A0j = Get-S13Data $dash13A0.body
$dash13B0 = Invoke-Api -Bearer $tk13B.token -Method GET -Path '/api/orcamento/dashboard?ano=2026' -Base $ApiBase
$dash13B0j = Get-S13Data $dash13B0.body
Add-Check 'S13' 'auth.logins-contexto-multi-esfera' (($lg13A.ok) -and ($lg13SemA.ok) -and ($lg13B.ok) -and ($tk13A.token.Length -gt 20) -and ($tk13B.token.Length -gt 20) -and ($dash13A0.code -eq 200) -and ($null -ne $dash13A0j) -and ($dash13A0j.esferaGoverno -eq 'municipal') -and ($dash13B0.code -eq 200) -and ($null -ne $dash13B0j) -and ($dash13B0j.esferaGoverno -eq 'estadual')) ("login web + ApiToken bearer para c2s13a.admin/c2s13a.semperm/c2s13b.admin; o motor herda a esfera da entidade do contexto do usuario (dashboard expoe municipal/estadual - regras 21/22, nada municipal hardcoded)")

$cat13 = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/catalogo-funcional' -Base $ApiBase
$cat13j = Get-S13Data $cat13.body
$f09 = $null; $s302 = $null
if ($null -ne $cat13j) {
  $fs13 = @($cat13j.funcoes); $ss13 = @($cat13j.subfuncoes)
  $f09 = $fs13 | Where-Object { $_.codigo -eq '09' } | Select-Object -First 1
  $s302 = $ss13 | Where-Object { $_.codigo -eq '302' } | Select-Object -First 1
}
$okCat = ($cat13.code -eq 200) -and ($null -ne $cat13j) -and (@($cat13j.funcoes).Count -eq 19) -and (@($cat13j.subfuncoes).Count -ge 9) -and ($null -ne $f09) -and ($f09.areaSaudeEducacao -eq $true) -and ($f09.nome -eq 'SAUDE') -and ($null -ne $s302) -and ($s302.funcaoCodigo -eq '09')
Add-Check 'S13' 'catalogo.nacional-funcoes-subfuncoes-no-banco' $okCat ("catalogo funcional expoe 19 funcoes e 9 subfuncoes catalogadas somente no banco (classificacao nacional, regra 12): funcao 09 SAUDE com area_saude_educacao=true (base do MDE CF art. 212) e subfuncao 302 vinculada")

# --- BR-13.1/13.5 competencia 2025 (A municipal, executada antes de 2026: zeros os restos antevedentes) ---
$pg25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/programas?ano=2025' -Base $ApiBase -Body '{"codigo":"PG-S13A25","nome":"Programa Saude 2025 Homologacao C2S13","objetivo":"Programa do exercicio de homologacao C2S13"}'
$pg25j = Get-S13Data $pg25.body
$acMde25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/acoes?ano=2025' -Base $ApiBase -Body ('{"codigo":"ACAO-MDE25","nome":"Atividade Saude 2025 C2S13","tipo":"ATIVIDADE","programaId":' + $pg25j.id + ',"subfuncaoCodigo":"302","funcaoCodigo":"09","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"OUTRAS_DESPESAS_CORRENTES"}')
$acMde25j = Get-S13Data $acMde25.body
$rec25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas?ano=2025' -Base $ApiBase -Body '{"codigo":"R-S13A25-IMP","nome":"Imposto Predial 2025 Homologacao C2S13","natureza":"TRIBUTARIA","valorPrevisto":1000000}'
$rec25j = Get-S13Data $rec25.body
$arq25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas/arrecadar?ano=2025' -Base $ApiBase -Body ('{"receitaId":' + $rec25j.id + ',"valor":1000000,"dataArrecadacao":"2025-12-15","historico":"Arrecadacao 2025 homologacao C2S13","chaveIdempotencia":"c2s13-a-arq25"}')
$arq25j = Get-S13Data $arq25.body
$dotMde25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/dotacoes?ano=2025' -Base $ApiBase -Body ('{"acaoId":' + $acMde25j.id + ',"fonteRecursoCodigo":"FNT-1","dotacaoInicial":1000000}')
$dotMde25j = Get-S13Data $dotMde25.body
Add-Check 'S13' 'estrutura.2025-programa-acao-saude-receita-base' (($pg25.code -eq 200) -and ($null -ne $pg25j) -and ($pg25j.situacao -eq 'ATIVO') -and ($acMde25.code -eq 200) -and ($null -ne $acMde25j) -and ($acMde25j.ativo -eq $true) -and ($rec25.code -eq 200) -and ($null -ne $rec25j) -and (Get-S13Near $rec25j.valorPrevista 1000000) -and ($arq25.code -eq 200) -and (Get-S13Near $arq25j.valorArrecadado 1000000) -and (Get-S13Near $arq25j.saldoAArrecadar 0) -and ($dotMde25.code -eq 200) -and (Get-S13Near $dotMde25j.disponivel 1000000)) ("competencia 2025 montada via API (programa, acao saude funcao 09 subfuncao 302, receita TRIBUTARIA prevista 1.000.000 + arrecadacao integral idempotente, dotacao 1.000.000 FNT-1)")

$empMde25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2025' -Base $ApiBase -Body ('{"numero":"EMP-S13A25-001","dataEmpenho":"2025-08-10","tipoEmpenho":"CP","historico":"Empenho saude 2025 homologacao C2S13","chaveIdempotencia":"c2s13-e-mde25","itens":[{"dotacaoId":' + $dotMde25j.id + ',"valor":235000}]}')
$empMde25j = Get-S13Data $empMde25.body
$detMde25 = Invoke-Api -Bearer $tk13A.token -Method GET -Path ('/api/orcamento/empenhos/' + $empMde25j.id + '?ano=2025') -Base $ApiBase
$detMde25j = Get-S13Data $detMde25.body
$itemId25 = if ($null -ne $detMde25j) { ($detMde25j.itens | Select-Object -First 1).id } else { 0 }
$liqMde25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/liquidacoes?ano=2025' -Base $ApiBase -Body ('{"numero":"LIQ-S13A25-001","dataLiquidacao":"2025-09-10","empenhoId":' + $empMde25j.id + ',"historico":"Liquidacao integral 2025 C2S13","chaveIdempotencia":"c2s13-l-mde25","itens":[{"empenhoItemId":' + $itemId25 + ',"valor":235000}]}')
$liqMde25j = Get-S13Data $liqMde25.body
$ap25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/lrf/apurar?ano=2025' -Base $ApiBase -Body '{"periodo":"2025-12-20"}'
$ap25j = @()
if ($null -ne (Get-S13Data $ap25.body)) { $ap25j = @(Get-S13Data $ap25.body) }
$mde25row = $ap25j | Where-Object { $_.limiteChave -eq 'MDE_MIN' } | Select-Object -First 1
$ra25row = $ap25j | Where-Object { $_.limiteChave -eq 'RESTOS_A_PAGAR_MAX' } | Select-Object -First 1
$ap25v = Get-Psql ("select count(distinct versao) from sigov.orcamento_lrf_apuracao where tenant_id=910001 and exercicio_id=" + $exA25 + " and limite_chave='MDE_MIN'")
Add-Check 'S13' 'lrf.mde-2025-23p5-alerta-apuracao-versionada' (($empMde25.code -eq 200) -and (Get-S13Near $empMde25j.valorTotal 235000) -and ($empMde25j.situacao -eq 'ATIVO') -and ($liqMde25.code -eq 200) -and (Get-S13Near $liqMde25j.valorTotal 235000) -and ($liqMde25j.situacao -eq 'ATIVA') -and ($ap25.code -eq 200) -and ($null -ne $mde25row) -and ($mde25row.situacao -eq 'ALERTA') -and (Get-S13Near $mde25row.percentual 23.5) -and (Get-S13Near $mde25row.baseValor 1000000) -and (Get-S13Near $mde25row.valorAtual 235000) -and ($null -ne $ra25row) -and ($ra25row.situacao -eq 'DENTRO') -and ([int]$ap25v.text.Trim() -ge 2)) ("empenho MDE 2025 de 235.000 abre dentro da janela (0 -> 23,5% entre bloqueio 23 e alerta 24); liquidacao integral zera os restos; apuracao explicita persiste MDE_MIN = ALERTA 23,5000% (base 1.000.000, atual 235.000 - CF art. 212 parametrizado) e RESTOS DENTRO (zero apos liquidacao); versoes append-only >= 2 conferidas no banco")

$excA = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/exercicios' -Base $ApiBase
$excAj = @()
if ($null -ne (Get-S13Data $excA.body)) { $excAj = @(Get-S13Data $excA.body) }
$ex25row = $excAj | Where-Object { [int]$_.ano -eq 2025 } | Select-Object -First 1
$ex26rowA = $excAj | Where-Object { [int]$_.ano -eq 2026 } | Select-Object -First 1
$enc25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/exercicios/encerrar' -Base $ApiBase -Body ('{"exercicioId":' + [long]$ex25row.exercicioId + ',"motivo":"Encerramento anual homologacao C2S13"}')
$enc25j = Get-S13Data $enc25.body
$empEnc25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2025' -Base $ApiBase -Body ('{"numero":"EMP-S13A25-002","dataEmpenho":"2025-12-28","tipoEmpenho":"CP","historico":"Empenho apos encerramento C2S13","chaveIdempotencia":"c2s13-e-enc25","itens":[{"dotacaoId":' + $dotMde25j.id + ',"valor":10000}]}')
$rea25 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/exercicios/reabrir' -Base $ApiBase -Body ('{"exercicioId":' + [long]$ex25row.exercicioId + ',"justificativa":"Reabertura homologacao para liquidacao de restos a pagar C2S13"}')
$rea25j = Get-S13Data $rea25.body
Add-Check 'S13' 'exercicio.2025-encerrado-bloqueia-movimento-reaberto' (($excA.code -eq 200) -and ($null -ne $ex25row) -and ($ex25row.situacao -eq 'ABERTO') -and ($null -ne $ex26rowA) -and ($ex26rowA.situacao -eq 'ABERTO') -and ($enc25.code -eq 200) -and ($null -ne $enc25j) -and ($enc25j.situacao -eq 'ENCERRADO') -and ($enc25j.encerramentoMotivo -eq 'Encerramento anual homologacao C2S13') -and ($empEnc25.code -eq 409) -and ((Get-S13Msg $empEnc25.body) -like '*EXERCICIO_ENCERRADO*') -and ($rea25.code -eq 200) -and ($null -ne $rea25j) -and ($rea25j.situacao -eq 'ABERTO') -and ($null -ne $rea25j.reabertoEm) -and ($rea25j.reaberturaJustificativa -eq 'Reabertura homologacao para liquidacao de restos a pagar C2S13')) ("GET exercicios lista 2025 e 2026 ABERTO; encerrar 2025 (motivo auditavel) -> ENCERRADO; novo empenho em 2025 -> 409 EXERCICIO_ENCERRADO (competencia fechada bloqueia movimentos - BR-13.8); reabrir com justificativa >= minimo parametrizado -> ABERTO com reabertoEm e justificativa persistidos")

# --- BR-13.1 estrutura 2026 (A municipal) + validacoes nomeadas ---
$pgA = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/programas?ano=2026' -Base $ApiBase -Body '{"codigo":"PG-S13A","nome":"Programa Homologacao Orcamento C2S13","objetivo":"Objetivo do programa de homologacao C2S13"}'
$pgAj = Get-S13Data $pgA.body
$pgAdup = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/programas?ano=2026' -Base $ApiBase -Body '{"codigo":"PG-S13A","nome":"Programa Homologacao Orcamento C2S13","objetivo":"Objetivo do programa de homologacao C2S13"}'
$acMde = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/acoes?ano=2026' -Base $ApiBase -Body ('{"codigo":"ACAO-MDE","nome":"Atividade Saude 2026 C2S13","tipo":"ATIVIDADE","programaId":' + $pgAj.id + ',"subfuncaoCodigo":"302","funcaoCodigo":"09","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"OUTRAS_DESPESAS_CORRENTES"}')
$acMdej = Get-S13Data $acMde.body
$acPes = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/acoes?ano=2026' -Base $ApiBase -Body ('{"codigo":"ACAO-PES","nome":"Atividade Pessoal 2026 C2S13","tipo":"ATIVIDADE","programaId":' + $pgAj.id + ',"subfuncaoCodigo":"271","funcaoCodigo":"17","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"PESSOAL_E_ENCARGOS"}')
$acPesj = Get-S13Data $acPes.body
$acTipoX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/acoes?ano=2026' -Base $ApiBase -Body ('{"codigo":"ACAO-XPTO","nome":"Acao tipo invalida","tipo":"XPTO","programaId":' + $pgAj.id + ',"subfuncaoCodigo":"302","funcaoCodigo":"09","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"OUTRAS_DESPESAS_CORRENTES"}')
$acFnX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/acoes?ano=2026' -Base $ApiBase -Body ('{"codigo":"ACAO-FN99","nome":"Acao funcao ausente","tipo":"ATIVIDADE","programaId":' + $pgAj.id + ',"subfuncaoCodigo":"302","funcaoCodigo":"99","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"OUTRAS_DESPESAS_CORRENTES"}')
$acSfX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/acoes?ano=2026' -Base $ApiBase -Body ('{"codigo":"ACAO-SF99","nome":"Acao subfuncao ausente","tipo":"ATIVIDADE","programaId":' + $pgAj.id + ',"subfuncaoCodigo":"999","funcaoCodigo":"09","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"OUTRAS_DESPESAS_CORRENTES"}')
Add-Check 'S13' 'estrutura.2026-programa-acoes-validacoes-nomeadas' (($pgA.code -eq 200) -and ($null -ne $pgAj) -and ($pgAj.situacao -eq 'ATIVO') -and ($pgAdup.code -eq 409) -and ((Get-S13Msg $pgAdup.body) -like '*PROGRAMA_JA_EXISTE*') -and ($acMde.code -eq 200) -and ($null -ne $acMdej) -and ($acMdej.ativo -eq $true) -and ($acPes.code -eq 200) -and ($null -ne $acPesj) -and ($acPesj.grupoDespesa -eq 'PESSOAL_E_ENCARGOS') -and ($acTipoX.code -eq 400) -and ((Get-S13Msg $acTipoX.body) -like '*ACAO_TIPO_INVALIDO*') -and ($acFnX.code -eq 404) -and ((Get-S13Msg $acFnX.body) -like '*ORCAMENTO_FUNCAO_INEXISTENTE*') -and ($acSfX.code -eq 404) -and ((Get-S13Msg $acSfX.body) -like '*ORCAMENTO_SUBFUNCAO_INEXISTENTE*')) ("programa PG-S13A ok + duplicata 409 PROGRAMA_JA_EXISTE (backstop unico); acao MDE (saude) e acao PESSOAL (grupo PESSOAL_E_ENCARGOS) criadas; falhas nomeadas: tipo fora do catalogo 400 ACAO_TIPO_INVALIDO, funcao 99 404 ORCAMENTO_FUNCAO_INEXISTENTE, subfuncao 999 404 ORCAMENTO_SUBFUNCAO_INEXISTENTE (catalogo nacional no banco)")

# --- BR-13.2 dotacoes 2026 ---
$dotMde = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase -Body ('{"acaoId":' + $acMdej.id + ',"fonteRecursoCodigo":"FNT-1","dotacaoInicial":1000000}')
$dotMdej = Get-S13Data $dotMde.body
$dotMdeDup = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase -Body ('{"acaoId":' + $acMdej.id + ',"fonteRecursoCodigo":"FNT-1","dotacaoInicial":1000000}')
$dotPes = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase -Body ('{"acaoId":' + $acPesj.id + ',"fonteRecursoCodigo":"FNT-1","dotacaoInicial":1000000}')
$dotPesj = Get-S13Data $dotPes.body
Add-Check 'S13' 'dotacoes.linhas-unicas-acao-fonte-a' (($dotMde.code -eq 200) -and ($null -ne $dotMdej) -and (Get-S13Near $dotMdej.disponivel 1000000) -and ($dotMdeDup.code -eq 409) -and ((Get-S13Msg $dotMdeDup.body) -like '*DOTACAO_JA_EXISTE*') -and ($dotPes.code -eq 200) -and ($null -ne $dotPesj) -and (Get-S13Near $dotPesj.autorizado 1000000)) ("dotacao MDE 1.000.000 (disponivel inicial igual ao autorizado) + duplicata mesma acao x fonte -> 409 DOTACAO_JA_EXISTE (sem rollover entre exercicios); dotacao PESSOAL 1.000.000 criada")

# --- base de receita 2026 (base PREVISTA dos limites LRF) ---
$recA = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas?ano=2026' -Base $ApiBase -Body '{"codigo":"R-S13A-IMP","nome":"Imposto Predial Homologacao C2S13","natureza":"TRIBUTARIA","valorPrevisto":1000000}'
$recAj = Get-S13Data $recA.body
$recADup = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas?ano=2026' -Base $ApiBase -Body '{"codigo":"R-S13A-IMP","nome":"Imposto Predial Homologacao C2S13","natureza":"TRIBUTARIA","valorPrevisto":1000000}'
$recNatX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas?ano=2026' -Base $ApiBase -Body '{"codigo":"R-S13A-XPTO","nome":"Receita natureza invalida","natureza":"XPTO","valorPrevisto":100}'
$arqA = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas/arrecadar?ano=2026' -Base $ApiBase -Body ('{"receitaId":' + $recAj.id + ',"valor":1000000,"dataArrecadacao":"2026-06-30","historico":"Arrecadacao homologacao C2S13","chaveIdempotencia":"c2s13-a-a1"}')
$arqAj = Get-S13Data $arqA.body
$arqAreps = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas/arrecadar?ano=2026' -Base $ApiBase -Body ('{"receitaId":' + $recAj.id + ',"valor":1000000,"dataArrecadacao":"2026-06-30","historico":"Arrecadacao homologacao C2S13","chaveIdempotencia":"c2s13-a-a1"}')
$arqArepsj = Get-S13Data $arqAreps.body
$arqZero = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/receitas/arrecadar?ano=2026' -Base $ApiBase -Body ('{"receitaId":' + $recAj.id + ',"valor":0,"dataArrecadacao":"2026-06-30","historico":"Arrecadacao zero","chaveIdempotencia":"c2s13-a-zero"}')
Add-Check 'S13' 'receita.base-prevista-arrecadacao-idempotente' (($recA.code -eq 200) -and ($null -ne $recAj) -and (Get-S13Near $recAj.valorPrevista 1000000) -and ($recADup.code -eq 409) -and ((Get-S13Msg $recADup.body) -like '*RECEITA_JA_EXISTE*') -and ($recNatX.code -eq 400) -and ((Get-S13Msg $recNatX.body) -like '*RECEITA_NATUREZA_INVALIDA*') -and ($arqA.code -eq 200) -and ($null -ne $arqAj) -and (Get-S13Near $arqAj.valorArrecadado 1000000) -and (Get-S13Near $arqAj.saldoAArrecadar 0) -and ($arqAj.idempotente -eq $false) -and ($arqAreps.code -eq 200) -and ($null -ne $arqArepsj) -and ($arqArepsj.idempotente -eq $true) -and (Get-S13Near $arqArepsj.valorArrecadado 1000000) -and ($arqZero.code -eq 400) -and ((Get-S13Msg $arqZero.body) -like '*ARRECADACAO_INVALIDA*')) ("receita TRIBUTARIA prevista 1.000.000 (base PREVISTA de todos os limites LRF) + duplicata 409 RECEITA_JA_EXISTE + natureza XPTO 400 RECEITA_NATUREZA_INVALIDA; arrecadacao integral ok e REPLAY com a mesma chave -> idempotente=true sem somar de novo (saldo continua 0); valor zero -> 400 ARRECADACAO_INVALIDA")

# --- BR-13.3 empenho CP 600k ok + idempotente ---
$empA1 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-001","dataEmpenho":"2026-06-15","tipoEmpenho":"CP","historico":"Empenho saude homologacao C2S13","chaveIdempotencia":"c2s13-e-a1","itens":[{"dotacaoId":' + $dotMdej.id + ',"valor":600000}]}')
$empA1j = Get-S13Data $empA1.body
$empA1rep = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-001","dataEmpenho":"2026-06-15","tipoEmpenho":"CP","historico":"Empenho saude homologacao C2S13","chaveIdempotencia":"c2s13-e-a1","itens":[{"dotacaoId":' + $dotMdej.id + ',"valor":600000}]}')
$empA1repj = Get-S13Data $empA1rep.body
Add-Check 'S13' 'empenho.a1-600k-ok-idempotente' (($empA1.code -eq 200) -and ($null -ne $empA1j) -and (Get-S13Near $empA1j.valorTotal 600000) -and (Get-S13Near $empA1j.disponivel 600000) -and ($empA1j.situacao -eq 'ATIVO') -and ($empA1j.idempotente -eq $false) -and ($empA1rep.code -eq 200) -and ($null -ne $empA1repj) -and ($empA1repj.idempotente -eq $true) -and ([long]$empA1repj.id -eq [long]$empA1j.id)) ("empenho CP 600.000 sobre dotacao MDE aprovado (transicoes LRF pos-movimento dentro dos limites: MDE 0->60% DENTRO, RESTOS 0->60% ALERTA sob override 55/75); replay com a mesma chave_idempotencia -> 200 idempotente=true com o mesmo id (1 evento = 1 empenho)")

# --- BR-13.2 credito suplementar com ato legal + justificativa parametrizada ---
$credA = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/creditos?ano=2026' -Base $ApiBase -Body ('{"dotacaoId":' + $dotMdej.id + ',"tipo":"SUPLEMENTAR","valor":250000,"atoLegal":"DL 042/2026","justificativa":"Suplementacao para custeio de acoes de saude - homologacao C2S13","chaveIdempotencia":"c2s13-c-a1"}')
$credAj = Get-S13Data $credA.body
$credAreps = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/creditos?ano=2026' -Base $ApiBase -Body ('{"dotacaoId":' + $dotMdej.id + ',"tipo":"SUPLEMENTAR","valor":250000,"atoLegal":"DL 042/2026","justificativa":"Suplementacao para custeio de acoes de saude - homologacao C2S13","chaveIdempotencia":"c2s13-c-a1"}')
$credArepsj = Get-S13Data $credAreps.body
$credTipoX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/creditos?ano=2026' -Base $ApiBase -Body ('{"dotacaoId":' + $dotMdej.id + ',"tipo":"XPTO","valor":1000,"atoLegal":"DL 99/2026","justificativa":"Tipo de credito invalido homologacao C2S13","chaveIdempotencia":"c2s13-c-x1"}')
$credJustCurta = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/creditos?ano=2026' -Base $ApiBase -Body ('{"dotacaoId":' + $dotMdej.id + ',"tipo":"SUPLEMENTAR","valor":1000,"atoLegal":"DL 98/2026","justificativa":"x","chaveIdempotencia":"c2s13-c-x2"}')
$dotGet = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase
$dotGetj = @()
if ($null -ne (Get-S13Data $dotGet.body)) { $dotGetj = @(Get-S13Data $dotGet.body) }
$dotMdeRow = $dotGetj | Where-Object { [long]$_.id -eq [long]$dotMdej.id } | Select-Object -First 1
Add-Check 'S13' 'credito.suplementar-ato-legal-disponibilidade' (($credA.code -eq 200) -and ($null -ne $credAj) -and ($credAj.situacao -eq 'APLICADO') -and (Get-S13Near $credAj.valor 250000) -and ($credAj.idempotente -eq $false) -and ($credAreps.code -eq 200) -and ($credArepsj.idempotente -eq $true) -and ($credTipoX.code -eq 400) -and ((Get-S13Msg $credTipoX.body) -like '*CREDITO_TIPO_INVALIDO*') -and ($credJustCurta.code -eq 400) -and ((Get-S13Msg $credJustCurta.body) -like '*JUSTIFICATIVA_INSUFICIENTE*') -and ($credJustCurta.body -like '*credito adicional*') -and ($null -ne $dotMdeRow) -and (Get-S13Near $dotMdeRow.dotacaoInicial 1000000) -and (Get-S13Near $dotMdeRow.creditosSuplementares 250000) -and (Get-S13Near $dotMdeRow.autorizado 1250000) -and (Get-S13Near $dotMdeRow.empenhado 600000) -and (Get-S13Near $dotMdeRow.disponivel 650000)) ("credito SUPLEMENTAR 250.000 c/ ato legal DL 042/2026 e justificativa >= minimo parametrizado -> APLICADO (replay idempotente); tipo XPTO 400 CREDITO_TIPO_INVALIDO; justificativa de 1 char 400 JUSTIFICATIVA_INSUFICIENTE (minimo 10 - LC 101/2000 art. 41); dotacao MDE agora: inicial 1.000.000 + creditos 250.000 = autorizado 1.250.000, empenhado 600.000, disponivel 650.000")

# --- BR-13.3 trava de disponibilidade ---
$empA2 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-002","dataEmpenho":"2026-06-18","tipoEmpenho":"CP","historico":"Empenho acima da disponibilidade C2S13","chaveIdempotencia":"c2s13-e-a2","itens":[{"dotacaoId":' + $dotMdej.id + ',"valor":700000}]}')
$empNoItens = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body '{"numero":"EMP-S13A-003","dataEmpenho":"2026-06-18","tipoEmpenho":"CP","historico":"Empenho sem itens C2S13","chaveIdempotencia":"c2s13-e-ni","itens":[]}'
$empTipoX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-004","dataEmpenho":"2026-06-18","tipoEmpenho":"XPTO","historico":"Empenho tipo invalido C2S13","chaveIdempotencia":"c2s13-e-tx","itens":[{"dotacaoId":' + $dotPesj.id + ',"valor":1000}]}')
Add-Check 'S13' 'empenho.a2-700k-insuficiente-nomeado' (($empA2.code -eq 409) -and ((Get-S13Msg $empA2.body) -like '*EMPENHO_ORCAMENTO_INSUFICIENTE*') -and ($empNoItens.code -eq 400) -and ((Get-S13Msg $empNoItens.body) -like '*EMPENHO_SEM_ITENS*') -and ($empTipoX.code -eq 400) -and ((Get-S13Msg $empTipoX.body) -like '*EMPENHO_TIPO_INVALIDO*')) ("empenho de 700.000 contra disponibilidade 650.000 -> 409 EMPENHO_ORCAMENTO_INSUFICIENTE (disponibilidade = inicial + creditos - reducoes - empenhado, Lei 4.320/64); sem itens -> 400 EMPENHO_SEM_ITENS; tipo XPTO -> 400 EMPENHO_TIPO_INVALIDO")

# --- BR-13.4 liquidacao: 400k ok / 300k super empenho por item ---
$detA1 = Invoke-Api -Bearer $tk13A.token -Method GET -Path ('/api/orcamento/empenhos/' + $empA1j.id + '?ano=2026') -Base $ApiBase
$detA1j = Get-S13Data $detA1.body
$itemIdA1 = if ($null -ne $detA1j) { ($detA1j.itens | Select-Object -First 1).id } else { 0 }
$liqA1 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/liquidacoes?ano=2026' -Base $ApiBase -Body ('{"numero":"LIQ-S13A-001","dataLiquidacao":"2026-07-10","empenhoId":' + $empA1j.id + ',"historico":"Liquidacao parcial homologacao C2S13","chaveIdempotencia":"c2s13-l-a1","itens":[{"empenhoItemId":' + $itemIdA1 + ',"valor":400000}]}')
$liqA1j = Get-S13Data $liqA1.body
$liqA2 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/liquidacoes?ano=2026' -Base $ApiBase -Body ('{"numero":"LIQ-S13A-002","dataLiquidacao":"2026-07-15","empenhoId":' + $empA1j.id + ',"historico":"Liquidacao acima do item C2S13","chaveIdempotencia":"c2s13-l-a2","itens":[{"empenhoItemId":' + $itemIdA1 + ',"valor":300000}]}')
$liqNoItens = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/liquidacoes?ano=2026' -Base $ApiBase -Body ('{"numero":"LIQ-S13A-003","dataLiquidacao":"2026-07-15","empenhoId":' + $empA1j.id + ',"historico":"Liquidacao sem itens C2S13","chaveIdempotencia":"c2s13-l-ni","itens":[]}')
Add-Check 'S13' 'liquidacao.a1-400k-ok-a2-300k-super-empenho' (($liqA1.code -eq 200) -and ($null -ne $liqA1j) -and (Get-S13Near $liqA1j.valorTotal 400000) -and ($liqA1j.situacao -eq 'ATIVA') -and ($liqA1j.empenhoNumero -eq 'EMP-S13A-001') -and ($liqA2.code -eq 409) -and ((Get-S13Msg $liqA2.body) -like '*LIQUIDACAO_SUPER_EMPENHO*') -and ($liqNoItens.code -eq 400) -and ((Get-S13Msg $liqNoItens.body) -like '*LIQUIDACAO_SEM_ITENS*')) ("liquidacao SL de 400.000 sobre o item do empenho (disponivel por item 600.000) -> ATIVA; segunda liquidacao de 300.000 sobre o mesmo item (restava 200.000) -> 409 LIQUIDACAO_SUPER_EMPENHO; sem itens -> 400 LIQUIDACAO_SEM_ITENS")

# --- BR-13.3/13.5 pessoal executivo: 45% ok, +5% -> 50% BLOQUEADO por transicao ---
$empPes1 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-005","dataEmpenho":"2026-06-20","tipoEmpenho":"CP","historico":"Empenho pessoal homologacao C2S13","chaveIdempotencia":"c2s13-e-p1","itens":[{"dotacaoId":' + $dotPesj.id + ',"valor":450000}]}')
$empPes1j = Get-S13Data $empPes1.body
$empPes2 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-006","dataEmpenho":"2026-06-25","tipoEmpenho":"CP","historico":"Empenho pessoal no teto C2S13","chaveIdempotencia":"c2s13-e-p2","itens":[{"dotacaoId":' + $dotPesj.id + ',"valor":50000}]}')
Add-Check 'S13' 'lrf.pessoal-executivo-transicao-bloqueio-50pct' (($empPes1.code -eq 200) -and ($null -ne $empPes1j) -and (Get-S13Near $empPes1j.valorTotal 450000) -and ($empPes1j.situacao -eq 'ATIVO') -and ($empPes2.code -eq 409) -and ((Get-S13Msg $empPes2.body) -like '*LRF_LIMITE_BLOQUEADO*') -and ((Get-S13Msg $empPes2.body) -like '*PESSOAL_EXECUTIVO_MAX*')) ("pessoal executivo municipal: empenho de 450.000 (0 -> 45%, <= alerta 45) aprovado; empenho seguinte de 50.000 levaria a 50% (= bloqueio parametrizado institucional) -> 409 LRF_LIMITE_BLOQUEADO com a chave PESSOAL_EXECUTIVO_MAX (bloqueio na TRANSICAO para BLOQUEIO - LC 101/2000 art. 20 II)")

$limA = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/lrf/limites' -Base $ApiBase
$limAj = @()
if ($null -ne (Get-S13Data $limA.body)) { $limAj = @(Get-S13Data $limA.body) }
$limARestos = $limAj | Where-Object { $_.chave -eq 'RESTOS_A_PAGAR_MAX' } | Select-Object -First 1
$limAMde = $limAj | Where-Object { $_.chave -eq 'MDE_MIN' } | Select-Object -First 1
Add-Check 'S13' 'lrf.a-limites-efetivos-override-observaveis' (($limA.code -eq 200) -and ($limAj.Count -ge 6) -and ($null -ne $limARestos) -and ($limARestos.origem -eq 'OVERRIDE_TENANT') -and (Get-S13Near $limARestos.alertaPct 55) -and (Get-S13Near $limARestos.bloqueioPct 75) -and ($null -ne $limAMde) -and ($limAMde.origem -eq 'INSTITUCIONAL') -and (Get-S13Near $limAMde.valorAlvo 25) -and (Get-S13Near $limAMde.bloqueioPct 23)) ("GET lrf/limites do tenant municipal expoe os limites efetivos: RESTOS_A_PAGAR_MAX com origem OVERRIDE_TENANT (alerta 55 / bloqueio 75, organico do tenant) e MDE_MIN INSTITUCIONAL (alvo 25 / alerta 24 / bloqueio 23 - CF art. 212 parametrizado); nenhum percentual vem de hardcoded")

# --- BR-13.6 previsao bimestral deterministica ---
$prev1 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/previsoes-bimestrais?ano=2026' -Base $ApiBase -Body '{"bimestre":1,"observacao":"Previsao 1o bimestre homologacao C2S13"}'
$prev1j = Get-S13Data $prev1.body
$prev1b = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/previsoes-bimestrais?ano=2026' -Base $ApiBase -Body '{"bimestre":1,"observacao":"Previsao 1o bimestre regenerada C2S13"}'
$prev1bj = Get-S13Data $prev1b.body
$prevList = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/previsoes-bimestrais?ano=2026' -Base $ApiBase
$prevListj = @()
if ($null -ne (Get-S13Data $prevList.body)) { $prevListj = @(Get-S13Data $prevList.body) }
$vigentes = @($prevListj | Where-Object { $_.vigente -eq $true })
$prevBad = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/previsoes-bimestrais?ano=2026' -Base $ApiBase -Body '{"bimestre":7,"observacao":"Bimestre invalido C2S13"}'
Add-Check 'S13' 'previsao.bimestre1-deterministica-vigente-unico' (($prev1.code -eq 200) -and ($null -ne $prev1j) -and (Get-S13Near $prev1j.receitaPrevistaTotal 1000000) -and (Get-S13Near $prev1j.receitaArrecadadaTotal 1000000) -and (Get-S13Near $prev1j.despesaAutorizadaTotal 2250000) -and (Get-S13Near $prev1j.despesaEmpenhadaTotal 1050000) -and (Get-S13Near $prev1j.despesaLiquidadaTotal 400000) -and (Get-S13Near $prev1j.saldoReceitas 0) -and ($prev1j.vigente -eq $true) -and ($prev1b.code -eq 200) -and ([long]$prev1bj.id -ne [long]$prev1j.id) -and (Get-S13Near $prev1bj.despesaAutorizadaTotal 2250000) -and (Get-S13Near $prev1bj.despesaEmpenhadaTotal 1050000) -and ($prev1bj.vigente -eq $true) -and ($prevListj.Count -eq 2) -and ($vigentes.Count -eq 1) -and ($prevBad.code -eq 400) -and ((Get-S13Msg $prevBad.body) -like '*BIMESTRE_INVALIDO*')) ("previsao bimestral (LC 101/2000 art. 51) gerada SOMENTE dos dados reais: receita prevista 1.000.000 / arrecadada 1.000.000 / autorizada 2.250.000 / empenhada 1.050.000 / liquidada 400.000 / saldo 0; regeracao -> nova linha deterministica (mesmos totais) e exatamente 1 vigente por bimestre; bimestre 7 -> 400 BIMESTRE_INVALIDO")

# --- BR-13.7 RREO semestral/anual ---
$rreo1 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/rreo?ano=2026' -Base $ApiBase -Body '{"tipo":"SEMESTRAL","semestre":2,"periodoFim":"2026-09-30"}'
$rreo1j = Get-S13Data $rreo1.body
$rreoDet = Invoke-Api -Bearer $tk13A.token -Method GET -Path ('/api/orcamento/rreo/' + $rreo1j.id + '?ano=2026') -Base $ApiBase
$rreoDetj = Get-S13Data $rreoDet.body
$rc1 = $null; $rc2 = $null; $rc3 = $null; $rc4 = $null; $rc5 = $null; $rc6 = $null; $rc7 = $null; $rc8 = $null; $rc9 = $null
if ($null -ne $rreoDetj) {
  $cjs = @($rreoDetj.conjuntos)
  $rc1 = $cjs | Where-Object { $_.conjunto -eq 'RECEITA_PREVISTA_GERAL' } | Select-Object -First 1
  $rc2 = $cjs | Where-Object { $_.conjunto -eq 'RECEITA_ARRECADADA' } | Select-Object -First 1
  $rc3 = $cjs | Where-Object { $_.conjunto -eq 'SALDO_RECEITAS' } | Select-Object -First 1
  $rc4 = $cjs | Where-Object { $_.conjunto -eq 'DESPESA_AUTORIZADA' } | Select-Object -First 1
  $rc5 = $cjs | Where-Object { $_.conjunto -eq 'DESPESA_EMPENHADA' } | Select-Object -First 1
  $rc6 = $cjs | Where-Object { $_.conjunto -eq 'DESPESA_LIQUIDADA' } | Select-Object -First 1
  $rc7 = $cjs | Where-Object { $_.conjunto -eq 'RESTOS_PAGAR_EXERCICIO_ATUAL' } | Select-Object -First 1
  $rc8 = $cjs | Where-Object { $_.conjunto -eq 'RESTOS_PAGAR_EXERCICIOS_ANTERIORES' } | Select-Object -First 1
  $rc9 = $cjs | Where-Object { $_.conjunto -eq 'LIQUIDACAO_RESTOS_NO_EXERCICIO' } | Select-Object -First 1
}
$rreoAnual = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/rreo?ano=2026' -Base $ApiBase -Body '{"tipo":"ANUAL","periodoFim":"2026-12-31"}'
$rreoAnualj = Get-S13Data $rreoAnual.body
$rreoTipoX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/rreo?ano=2026' -Base $ApiBase -Body '{"tipo":"XPTO","periodoFim":"2026-09-30"}'
$rreoSemX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/rreo?ano=2026' -Base $ApiBase -Body '{"tipo":"ANUAL","semestre":1,"periodoFim":"2026-12-31"}'
$rreoPerX = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/rreo?ano=2026' -Base $ApiBase -Body '{"tipo":"SEMESTRAL","semestre":2,"periodoFim":"2027-01-15"}'
Add-Check 'S13' 'rreo.semestral-anual-conjuntos-travados' (($rreo1.code -eq 200) -and ($null -ne $rreo1j) -and ($rreo1j.vigente -eq $true) -and ($rreoDet.code -eq 200) -and ($null -ne $rreoDetj) -and ($null -ne $rc1) -and (Get-S13Near $rc1.valor 1000000) -and ($null -ne $rc2) -and (Get-S13Near $rc2.valor 1000000) -and ($null -ne $rc3) -and (Get-S13Near $rc3.valor 0) -and ($null -ne $rc4) -and (Get-S13Near $rc4.valor 2250000) -and ($null -ne $rc5) -and (Get-S13Near $rc5.valor 1050000) -and ($null -ne $rc6) -and (Get-S13Near $rc6.valor 400000) -and ($null -ne $rc7) -and (Get-S13Near $rc7.valor 650000) -and ($null -ne $rc8) -and (Get-S13Near $rc8.valor 0) -and ($null -ne $rc9) -and (Get-S13Near $rc9.valor 0) -and ($rreoAnual.code -eq 200) -and ($null -ne $rreoAnualj) -and ($rreoAnualj.vigente -eq $true) -and ($rreoTipoX.code -eq 400) -and ((Get-S13Msg $rreoTipoX.body) -like '*RREO_TIPO_INVALIDO*') -and ($rreoSemX.code -eq 400) -and ((Get-S13Msg $rreoSemX.body) -like '*RREO_SEMESTRE_INCONSISTENTE*') -and ($rreoPerX.code -eq 400) -and ((Get-S13Msg $rreoPerX.body) -like '*RREO_PERIODO_FIM_INCONSISTENTE*')) ("RREO SEMESTRAL 2/2026 (LC 101/2000 art. 54) com os 9 conjuntos travados dos dados reais: prevista 1.000.000, arrecadada 1.000.000, saldo 0, autorizada 2.250.000, empenhada 1.050.000, liquidada 400.000, restos atual 650.000 (600k-400k+450k), restos anteriores 0 (2025 integralmente liquidado), restos liquidados no exercicio 0; ANUAL gerado vigente; tipo XPTO 400, ANUAL c/ semestre 400, periodo_fim fora do exercicio 400")

# --- dashboard A (valores travados do estado final 2026) ---
$dashA = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/dashboard?ano=2026' -Base $ApiBase
$dashAj = Get-S13Data $dashA.body
Add-Check 'S13' 'dashboard.a-municipal-indicadores-travados' (($dashA.code -eq 200) -and ($null -ne $dashAj) -and ($dashAj.esferaGoverno -eq 'municipal') -and ([int]$dashAj.acoesAtivas -eq 2) -and ([int]$dashAj.dotacoes -eq 2) -and ([int]$dashAj.empenhosAtivos -eq 2) -and ([int]$dashAj.liquidacoesAtivas -eq 1) -and (Get-S13Near $dashAj.totalAutorizado 2250000) -and (Get-S13Near $dashAj.totalEmpenhado 1050000) -and (Get-S13Near $dashAj.totalLiquidado 400000) -and (Get-S13Near $dashAj.disponivelTotal 1200000) -and (Get-S13Near $dashAj.receitaPrevistaTotal 1000000) -and (Get-S13Near $dashAj.receitaArrecadadaTotal 1000000) -and ([int]$dashAj.lrfEmAlerta -eq 1) -and ([int]$dashAj.lrfEmBloqueio -eq 0)) ("dashboard municipal 2026: 2 acoes / 2 dotacoes / 2 empenhos ativos / 1 liquidacao ativa; autorizado 2.250.000, empenhado 1.050.000, liquidado 400.000, disponivel 1.200.000; receitas 1.000.000/1.000.000; LRF: 1 limite em alerta (RESTOS 65% sob override 55/75) e 0 em bloqueio na ultima versao apurada")

# --- parametros efetivos: padrao institucional + override comportamental do tenant A ---
$parA = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/parametros' -Base $ApiBase
$parAj = @()
if ($null -ne (Get-S13Data $parA.body)) { $parAj = @(Get-S13Data $parA.body) }
$parAuto = $parAj | Where-Object { $_.codigo -eq 'ORCAMENTO_APURO_LRF_AUTO' } | Select-Object -First 1
$parAnul = $parAj | Where-Object { $_.codigo -eq 'ORCAMENTO_ANULACAO_JUSTIFICATIVA_MIN_CARACTERES' } | Select-Object -First 1
Add-Check 'S13' 'parametros.efetivos-padrao-override-tenant-a' (($parA.code -eq 200) -and ($parAj.Count -eq 5) -and ($null -ne $parAuto) -and ($parAuto.valorEfetivo -eq 'true') -and ($parAuto.origem -eq 'PADRAO') -and ($null -ne $parAnul) -and ($parAnul.valorEfetivo -eq '30') -and ($parAnul.origem -eq 'OVERRIDE')) ("GET parametros expoe os 5 parametros ORCAMENTO com origem observavel: ORCAMENTO_APURO_LRF_AUTO true (PADRAO) e min de justificativa de anulacao 30 (OVERRIDE do tenant A - padrao institucional 5), provando que regras de negocio sao parametrizadas por esfera/tenant (regra 24)")

# --- anulacao: teto parametrizado (override 30) + estados nomeados ---
$empA3 = Invoke-Api -Bearer $tk13A.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13A-007","dataEmpenho":"2026-06-28","tipoEmpenho":"CP","historico":"Empenho descartavel p/ anulacao C2S13","chaveIdempotencia":"c2s13-e-a3","itens":[{"dotacaoId":' + $dotPesj.id + ',"valor":10000}]}')
$empA3j = Get-S13Data $empA3.body
$anulCurta = Invoke-Api -Bearer $tk13A.token -Method POST -Path ('/api/orcamento/empenhos/' + $empA3j.id + '/anular?ano=2026') -Base $ApiBase -Body '{"justificativa":"curta"}'
$anulOk = Invoke-Api -Bearer $tk13A.token -Method POST -Path ('/api/orcamento/empenhos/' + $empA3j.id + '/anular?ano=2026') -Base $ApiBase -Body '{"justificativa":"Anulacao teste homologacao C2S13"}'
$anulOkj = Get-S13Data $anulOk.body
$anulRep = Invoke-Api -Bearer $tk13A.token -Method POST -Path ('/api/orcamento/empenhos/' + $empA3j.id + '/anular?ano=2026') -Base $ApiBase -Body '{"justificativa":"Anulacao teste homologacao C2S13"}'
$anulComLiq = Invoke-Api -Bearer $tk13A.token -Method POST -Path ('/api/orcamento/empenhos/' + $empA1j.id + '/anular?ano=2026') -Base $ApiBase -Body '{"justificativa":"Anulacao com liquidacao existente homologacao C2S13"}'
Add-Check 'S13' 'anulacao.teto-30-parametrizado-estados-a' (($empA3.code -eq 200) -and ($null -ne $empA3j) -and ($anulCurta.code -eq 400) -and ((Get-S13Msg $anulCurta.body) -like '*JUSTIFICATIVA_INSUFICIENTE*') -and ($anulCurta.body -like '*minimo de 30*') -and ($anulOk.code -eq 200) -and ($null -ne $anulOkj) -and ($anulOkj.situacao -eq 'ANULADO') -and (Get-S13Near $anulOkj.valorAnulado 10000) -and ($anulRep.code -eq 409) -and ((Get-S13Msg $anulRep.body) -like '*EMPENHO_JA_ANULADO*') -and ($anulComLiq.code -eq 409) -and ((Get-S13Msg $anulComLiq.body) -like '*EMPENHO_COM_LIQUIDACAO*')) ("com override 30 no tenant A (padrao 5), justificativa de 5 chars -> 400 JUSTIFICATIVA_INSUFICIENTE 'minimo de 30' (o override muda o comportamento, regra 24); anulacao ok -> ANULADO devolvendo 10.000 a dotacao; replay -> 409 EMPENHO_JA_ANULADO; anular empenho com 400.000 liquidados -> 409 EMPENHO_COM_LIQUIDACAO")

# --- fail-closed, isolamento cruz-tenant e exercicio inexistente ---
$fa1 = Invoke-Api -Bearer $tk13SemA.token -Method GET -Path '/api/orcamento/dashboard?ano=2026' -Base $ApiBase
$fa2 = Invoke-Api -Bearer $tk13SemA.token -Method POST -Path '/api/orcamento/programas?ano=2026' -Base $ApiBase -Body '{"codigo":"PG-SEM","nome":"Programa sem permissao","objetivo":"Sem permissao C2S13"}'
$cx1 = Invoke-Api -Bearer $tk13B.token -Method GET -Path ('/api/orcamento/empenhos/' + $empA1j.id + '?ano=2026') -Base $ApiBase
$exIn = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/dotacoes?ano=2024' -Base $ApiBase
Add-Check 'S13' 'auth.failclosed-semperm-cruz-tenant-exercicio-inexistente' (($fa1.code -eq 403) -and ($fa2.code -eq 403) -and ($cx1.code -eq 404) -and ((Get-S13Msg $cx1.body) -like '*ORCAMENTO_EMPENHO_INEXISTENTE*') -and ($exIn.code -eq 404) -and ((Get-S13Msg $exIn.body) -like '*ORCAMENTO_EXERCICIO_INEXISTENTE*')) ("usuario sem nenhuma chave orcamento.* -> 403 em GET e POST (fail-closed com 1 chave inofensiva fora da frente); admin B nao ve o empenho do tenant A (404 ORCAMENTO_EMPENHO_INEXISTENTE - isolamento por tenant); ano 2024 sem competencia -> 404 ORCAMENTO_EXERCICIO_INEXISTENTE (falha explicita, regra 13)")

# --- schema ausente -> 404 nomeado (guard de fronteira) ---
$sc1 = Get-Psql 'alter table sigov.orcamento_dotacao rename to orcamento_dotacao_s13probe'
$sc2 = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase
$sc3 = Get-Psql 'alter table sigov.orcamento_dotacao_s13probe rename to orcamento_dotacao'
$sc4 = Invoke-Api -Bearer $tk13A.token -Method GET -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase
Add-Check 'S13' 'schema.ausente-404-nomeado-restaurado' (($sc1.code -eq 0) -and ($sc2.code -eq 404) -and ((Get-S13Msg $sc2.body) -like '*ORCAMENTO_NAO_DISPONIVEL*') -and ($sc3.code -eq 0) -and ($sc4.code -eq 200)) ("rename temporario de sigov.orcamento_dotacao -> API responde 404 ORCAMENTO_NAO_DISPONIVEL (ausencia de schema/configuracao explicita, regra 13); restaurado -> 200")

# --- Tenant B (estadual): pessoal 48% ALERTA institucional -> override tenant 45,5% -> BLOQUEIO apurado ---
$pgB = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/programas?ano=2026' -Base $ApiBase -Body '{"codigo":"PG-S13B","nome":"Programa Pessoal Estadual C2S13","objetivo":"Programa estadual de homologacao C2S13"}'
$pgBj = Get-S13Data $pgB.body
$acPesB = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/acoes?ano=2026' -Base $ApiBase -Body ('{"codigo":"ACAO-PESB","nome":"Atividade Pessoal Estadual C2S13","tipo":"ATIVIDADE","programaId":' + $pgBj.id + ',"subfuncaoCodigo":"271","funcaoCodigo":"17","poder":"EXECUTIVO","categoriaEconomica":"CORRENTE","grupoDespesa":"PESSOAL_E_ENCARGOS"}')
$acPesBj = Get-S13Data $acPesB.body
$dotPesB = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/dotacoes?ano=2026' -Base $ApiBase -Body ('{"acaoId":' + $acPesBj.id + ',"fonteRecursoCodigo":"FNT-1","dotacaoInicial":1000000}')
$dotPesBj = Get-S13Data $dotPesB.body
$recB = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/receitas?ano=2026' -Base $ApiBase -Body '{"codigo":"R-S13B-ICMS","nome":"ICMS Homologacao C2S13","natureza":"TRIBUTARIA","valorPrevisto":1000000}'
$recBj = Get-S13Data $recB.body
$arqB = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/receitas/arrecadar?ano=2026' -Base $ApiBase -Body ('{"receitaId":' + $recBj.id + ',"valor":1000000,"dataArrecadacao":"2026-06-30","historico":"Arrecadacao estadual homologacao C2S13","chaveIdempotencia":"c2s13-b-arq"}')
$empB1 = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13B-001","dataEmpenho":"2026-06-18","tipoEmpenho":"CP","historico":"Empenho pessoal estadual C2S13","chaveIdempotencia":"c2s13-b-e1","itens":[{"dotacaoId":' + $dotPesBj.id + ',"valor":480000}]}')
$empB1j = Get-S13Data $empB1.body
Add-Check 'S13' 'b.estadual-estrutura-receita-pessoal-48-alerta' (($pgB.code -eq 200) -and ($null -ne $pgBj) -and ($acPesB.code -eq 200) -and ($null -ne $acPesBj) -and ($dotPesB.code -eq 200) -and (Get-S13Near $dotPesBj.disponivel 1000000) -and ($recB.code -eq 200) -and (Get-S13Near $recBj.valorPrevista 1000000) -and ($arqB.code -eq 200) -and ($empB1.code -eq 200) -and ($null -ne $empB1j) -and (Get-S13Near $empB1j.valorTotal 480000) -and ($empB1j.situacao -eq 'ATIVO')) ("tenant estadual B montado (estrutura, dotacao 1.000.000, base receita 1.000.000); empenho pessoal EXECUTIVO de 480.000 (0 -> 48%) aprovado com os limites INSTITUCIONAIS estaduais (alerta 45 < 48 < bloqueio 50 = ALERTA, nao bloqueia)")

$s13ovrB = Get-Psql "delete from sigov.orcamento_limite_lrf where tenant_id=910002 and esfera_governo='estadual' and chave='PESSOAL_EXECUTIVO_MAX';
insert into sigov.orcamento_limite_lrf(tenant_id,esfera_governo,chave,nome,tipo_comparacao,valor_alvo,alerta_pct,bloqueio_pct,base_receita,ativo,created_by)
select 910002,'estadual','PESSOAL_EXECUTIVO_MAX',l.nome,'MAX',50.0000,45.0000,45.5000,'PREVISTA',true,910301
from sigov.orcamento_limite_lrf l where l.tenant_id is null and l.esfera_governo='estadual' and l.chave='PESSOAL_EXECUTIVO_MAX' and not l.is_deleted limit 1;"
$apB = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/lrf/apurar?ano=2026' -Base $ApiBase -Body '{"periodo":"2026-09-30"}'
$apBj = @()
if ($null -ne (Get-S13Data $apB.body)) { $apBj = @(Get-S13Data $apB.body) }
$pesBrow = $apBj | Where-Object { $_.limiteChave -eq 'PESSOAL_EXECUTIVO_MAX' } | Select-Object -First 1
$mdeBrow = $apBj | Where-Object { $_.limiteChave -eq 'MDE_MIN' } | Select-Object -First 1
$limB = Invoke-Api -Bearer $tk13B.token -Method GET -Path '/api/orcamento/lrf/limites' -Base $ApiBase
$limBj = @()
if ($null -ne (Get-S13Data $limB.body)) { $limBj = @(Get-S13Data $limB.body) }
$limBPes = $limBj | Where-Object { $_.chave -eq 'PESSOAL_EXECUTIVO_MAX' } | Select-Object -First 1
$limBRestos = $limBj | Where-Object { $_.chave -eq 'RESTOS_A_PAGAR_MAX' } | Select-Object -First 1
$empB2 = Invoke-Api -Bearer $tk13B.token -Method POST -Path '/api/orcamento/empenhos?ano=2026' -Base $ApiBase -Body ('{"numero":"EMP-S13B-002","dataEmpenho":"2026-07-05","tipoEmpenho":"CP","historico":"Empenho ja-em-bloqueio C2S13","chaveIdempotencia":"c2s13-b-e2","itens":[{"dotacaoId":' + $dotPesBj.id + ',"valor":5000}]}')
$empB2j = Get-S13Data $empB2.body
Add-Check 'S13' 'lrf.b-override-tenant-bloqueio-apurado' (($s13ovrB.code -eq 0) -and ($apB.code -eq 200) -and ($null -ne $pesBrow) -and ($pesBrow.situacao -eq 'BLOQUEIO') -and (Get-S13Near $pesBrow.percentual 48) -and (Get-S13Near $pesBrow.baseValor 1000000) -and (Get-S13Near $pesBrow.valorAtual 480000) -and ($null -ne $mdeBrow) -and ($mdeBrow.situacao -eq 'BLOQUEIO') -and (Get-S13Near $mdeBrow.percentual 0) -and ($null -ne $limBPes) -and ($limBPes.origem -eq 'OVERRIDE_TENANT') -and (Get-S13Near $limBPes.bloqueioPct 45.5) -and ($null -ne $limBRestos) -and ($limBRestos.origem -eq 'OVERRIDE_TENANT') -and ($empB2.code -eq 200) -and ($null -ne $empB2j) -and (Get-S13Near $empB2j.valorTotal 5000)) ("override PESSOAL_EXECUTIVO_MAX do tenant estadual com bloqueio 45,5% (institucional 50%) muda a apuracao: BLOQUEIO persistido a 48,0000% (base 1.000.000, atual 480.000); MDE federal-free: estado sem empenhos em saude/educacao fica BLOQUEIO a 0% (piso CF art. 212 parametrizado); GET lrf/limites mostra origem OVERRIDE_TENANT (45,5) e RESTOS tambem do tenant (organico estadual); empenho de 5.000 quando JA em bloqueio -> permitido (negacao so na TRANSICAO para bloqueio)")

$dashB = Invoke-Api -Bearer $tk13B.token -Method GET -Path '/api/orcamento/dashboard?ano=2026' -Base $ApiBase
$dashBj = Get-S13Data $dashB.body
Add-Check 'S13' 'dashboard.b-estadual-bloqueios-persistidos' (($dashB.code -eq 200) -and ($null -ne $dashBj) -and ($dashBj.esferaGoverno -eq 'estadual') -and ([int]$dashBj.acoesAtivas -eq 1) -and ([int]$dashBj.dotacoes -eq 1) -and ([int]$dashBj.empenhosAtivos -eq 2) -and ([int]$dashBj.liquidacoesAtivas -eq 0) -and (Get-S13Near $dashBj.totalAutorizado 1000000) -and (Get-S13Near $dashBj.totalEmpenhado 485000) -and (Get-S13Near $dashBj.totalLiquidado 0) -and (Get-S13Near $dashBj.disponivelTotal 515000) -and (Get-S13Near $dashBj.receitaPrevistaTotal 1000000) -and (Get-S13Near $dashBj.receitaArrecadadaTotal 1000000) -and ([int]$dashBj.lrfEmAlerta -eq 0) -and ([int]$dashBj.lrfEmBloqueio -eq 2)) ("dashboard estadual 2026: esfera herdada do contexto (nada municipal hardcoded); 1 acao / 1 dotacao / 2 empenhos ativos / 0 liquidacoes; autorizado 1.000.000, empenhado 485.000, disponivel 515.000; LRF: 0 alertas e 2 bloqueios na ultima versao (PESSOAL_EXECUTIVO por override do tenant + MDE piso 0%)")

# --- limpeza cirurgica do fixture S13 (mesmo escopo do pre-clean) ---
$s13cl = Get-Psql @"
delete from sigov.orcamento_liquidacao_item where liquidacao_id in (select id from sigov.orcamento_liquidacao where tenant_id in (910001,910002));
delete from sigov.orcamento_liquidacao where tenant_id in (910001,910002);
delete from sigov.orcamento_empenho_item where empenho_id in (select id from sigov.orcamento_empenho where tenant_id in (910001,910002));
delete from sigov.orcamento_empenho where tenant_id in (910001,910002);
delete from sigov.orcamento_credito_adicional where tenant_id in (910001,910002);
delete from sigov.orcamento_dotacao where tenant_id in (910001,910002);
delete from sigov.orcamento_receita_arrecadacao where tenant_id in (910001,910002);
delete from sigov.orcamento_receita_prevista where tenant_id in (910001,910002);
delete from sigov.orcamento_lrf_apuracao where tenant_id in (910001,910002);
delete from sigov.orcamento_previsao_bimestral where tenant_id in (910001,910002);
delete from sigov.orcamento_rreo_item where lote_id in (select id from sigov.orcamento_rreo_lote where tenant_id in (910001,910002));
delete from sigov.orcamento_rreo_lote where tenant_id in (910001,910002);
delete from sigov.orcamento_estado_exercicio where tenant_id in (910001,910002);
delete from sigov.orcamento_acao where tenant_id in (910001,910002);
delete from sigov.orcamento_programa where tenant_id in (910001,910002);
delete from sigov.orcamento_limite_lrf where tenant_id in (910001,910002);
delete from sigov.parametro_modulo_valor where correlation_id='c2s13-anul-override-e2e';
delete from sigov.autorizacao_decisao_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.auditoria_evento where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.identidade_sessao where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.sessao_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.sessao_contexto_usuario where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.contexto_operacional_auditoria where usuario_executor_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario_contexto_global_log where usuario_global_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.historico_login where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.trilha_auditoria where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.token_mfa where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.senha_redefinicao_token where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.fila_assinatura where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario_preferencia where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario_filtro_salvo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000031','cf58c2ab-1c20-4a90-88f8-0c2000000032','cf58c2ab-1c20-4a90-88f8-0c2000000033');
delete from sigov.usuario_grupo where usuario_id in (select id from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'));
delete from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%');
delete from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S13A-SEMP') or (tenant_id=910002 and codigo_externo='C2S13B-ADM'));
delete from sigov.perfil_acesso where (tenant_id=910001 and nome='C2S13A Perfil SemFrente') or (tenant_id=910002 and nome='C2S13B Perfil Orcamento');
delete from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S13A-SEMP') or (tenant_id=910002 and codigo_externo='C2S13B-ADM');
delete from sigov.exercicio where entidade_id in (select id from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S13A-ENT') or (tenant_id=910002 and codigo_externo='C2S13B-ENT'));
delete from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S13A-ENT') or (tenant_id=910002 and codigo_externo='C2S13B-ENT');
select (select count(*) from sigov.orcamento_liquidacao_item where liquidacao_id in (select id from sigov.orcamento_liquidacao where tenant_id in (910001,910002)))
  + (select count(*) from sigov.orcamento_liquidacao where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_empenho_item where empenho_id in (select id from sigov.orcamento_empenho where tenant_id in (910001,910002)))
  + (select count(*) from sigov.orcamento_empenho where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_credito_adicional where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_dotacao where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_receita_arrecadacao where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_receita_prevista where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_lrf_apuracao where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_previsao_bimestral where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_rreo_item where lote_id in (select id from sigov.orcamento_rreo_lote where tenant_id in (910001,910002)))
  + (select count(*) from sigov.orcamento_rreo_lote where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_estado_exercicio where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_acao where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_programa where tenant_id in (910001,910002))
  + (select count(*) from sigov.orcamento_limite_lrf where tenant_id in (910001,910002))
  + (select count(*) from sigov.parametro_modulo_valor where correlation_id='c2s13-anul-override-e2e')
  + (select count(*) from sigov.perfil_permissao where correlation_id in ('cf58c2ab-1c20-4a90-88f8-0c2000000031','cf58c2ab-1c20-4a90-88f8-0c2000000032','cf58c2ab-1c20-4a90-88f8-0c2000000033'))
  + (select count(*) from sigov.usuario where (tenant_id=910001 and login like 'c2s13a.%') or (tenant_id=910002 and login like 'c2s13b.%'))
  + (select count(*) from sigov.grupo_perfil where grupo_acesso_id in (select id from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S13A-SEMP') or (tenant_id=910002 and codigo_externo='C2S13B-ADM')))
  + (select count(*) from sigov.perfil_acesso where (tenant_id=910001 and nome='C2S13A Perfil SemFrente') or (tenant_id=910002 and nome='C2S13B Perfil Orcamento'))
  + (select count(*) from sigov.grupo_acesso where (tenant_id=910001 and codigo_externo='C2S13A-SEMP') or (tenant_id=910002 and codigo_externo='C2S13B-ADM'))
  + (select count(*) from sigov.entidade where (tenant_id=910001 and codigo_externo='C2S13A-ENT') or (tenant_id=910002 and codigo_externo='C2S13B-ENT'));
"@
Add-Check 'S13' 'fixture.removido-auto-limpeza-restantes-zero' (($s13cl.code -eq 0) -and ($s13cl.text -eq '0')) ("cleanup cirurgico S13 nos DOIS tenants (18 tabelas orcamento_* por tenant, overrides LRF A/B + override de bloqueio B, override parametro c2s13-anul-override-e2e, sessoes/auditorias dos 3 usuarios, grants sentinel cf58c2ab-...031-033, usuarios c2s13a.*/c2s13b.*, grupos+perfis dedicados C2S13, competencias, entidades C2S13A-ENT/C2S13B-ENT) -> restantes=" + $s13cl.text)

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
delete from sigov.educacao_biblioteca_exemplar where acervo_id in (select id from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C1S6-JORNADA-%');
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C1S6-JORNADA-%';
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
-- Resquicios de jornadas/homologacao: movimentos e estoques cujos produtos ja foram limpos
-- por codigo (C1S5/C1S6/C2S2/c1e26). O catch-all "not exists" impede que virem
-- orfaos e acumulem entre execucoes (regra 9: cleanup idempotente).
delete from sigov.educacao_merenda_movimento where tenant_id=910001 and not exists(select 1 from sigov.educacao_merenda_produto p where p.id=sigov.educacao_merenda_movimento.produto_id);
delete from sigov.educacao_merenda_estoque where tenant_id=910001 and not exists(select 1 from sigov.educacao_merenda_produto p where p.id=sigov.educacao_merenda_estoque.produto_id);
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
              + (select count(*) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'C1S6-JORNADA-%')
              + (select count(*) from sigov.educacao_merenda_movimento where tenant_id=910001 and not exists(select 1 from sigov.educacao_merenda_produto p where p.id=sigov.educacao_merenda_movimento.produto_id))
              + (select count(*) from sigov.educacao_merenda_estoque where tenant_id=910001 and not exists(select 1 from sigov.educacao_merenda_produto p where p.id=sigov.educacao_merenda_estoque.produto_id))
             + (select count(*) from sigov.escola where tenant_id=910001 and codigo='c1e26-c2s5-esc')              + (select count(*) from sigov.compras_demanda where tenant_id=910001 and objeto like 'C2S8-%')
              + (select count(*) from sigov.compras_licitacao where tenant_id=910001 and objeto like 'C2S8-%')
              + (select count(*) from sigov.compras_contrato where tenant_id=910001 and objeto like 'C2S8-%')
              + (select count(*) from sigov.compras_fornecedor where tenant_id=910001 and nome like 'Forneadora C2S8 %')
              + (select count(*) from sigov.financeiro_centro_custo where tenant_id=910001 and codigo like 'C2S8-%')
              + (select count(*) from sigov.usuario where tenant_id=910001 and login='c2s8.admin')
              + (select count(*) from sigov.entidade where tenant_id=910001 and codigo_externo='C2S8-ENT')
              + (select count(*) from sigov.perfil_permissao where correlation_id='cf58c2ab-1c20-4a90-88f8-0c2000000001') as restantes;
"@
Add-Check 'CX' 'fixture.removido' ($cl.code -eq 0 -and $cl.text -eq '0') ("delete c1e26-%/C1S3-%/C1S7-%/C1S8-% (diario classe+aulas+conteudo+frequencia+fechamento+historico, frequencias, S4 historico-escolar+itens+vinculos+responsavel+pessoa do usuario, professor_turma, matriculas, turmas, ano, alunos, professor, pessoas, escola + S5 cardapio+itens+produtos + S6 distribuicoes+itens+movimentos+estoque + S7 biblioteca acervo/exemplares/emprestimos/reservas/devolucoes/aluno/escola + S8 transporte vinculos/pontos/rotas/alunos/escola + S9 fundeb parametros/receitas/aplicacoes + custo centros/lancamentos + S10 indicadores/valores/projecoes/lotescenso/inconsistencias/escola (tenant) + C2S2 requisicoes-legadas/produto-livre + C2S3 acervo-FNLI/ocorrencias/eventos/inventarios/items-inventario + C2S4 transporte veiculos/motoristas/viagens/custos/ocorrencias/presencas/parametro-monitor + C2S5 fundeb-competencia/rateios/avaliacoes/educacenso-eventos/matriculas/turma/curso/alunos/servidores/escola + S6 jornada acervo C1S6-JORNADA + merenda movimentos/estoques orfaos de produto limpo + merenda/biblioteca/entidade + C2S8 compras integradas [demandas/licitacoes/contratos/fornecedores/centros-de-custo/usuario c2s8.admin/entidade C2S8-ENT]) tenant 910001 -> restantes=" + $cl.text)

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
