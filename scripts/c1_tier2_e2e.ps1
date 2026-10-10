# =============================================================================
# SIGOV PLUS · Ciclo 1 (Educacao Avancada) · TIER2 — E2E HTTP real sobre p1_dev
# =============================================================================
# Prova por HTTP REAL (Web :5099 -> ApiToken -> bearer na Api :5100 -> Dapper ->
# PG16) as correcoes comprovadas do Ciclo 1 Sprint 1 em educacao_biblioteca_acervo
# com fixture de 120 linhas no tenant 910001 (marcador codigo 'c1e26-%'):
#   E1  Dashboard: totais sobre o conjunto COMPLETO (Dapper ReadFirstAsync<IDict>)
#   E2  Exportar CSV: todas as linhas (sem teto 100), BOM, escape e protecao formula
#   E3  Schema ausente: PagedResult.Diagnostic explicito em vez de lista vazia
#   E4  AlterarStatus: auditoria append-only com de/para/motivo
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
  $body = if (Test-Path $of) { (Get-Content $of -Raw) } else { '' }
  return [pscustomobject]@{ code=[int]$code; body="$body"; outFile="$of" }
}
function Get-Psql {
  param([string]$Sql)
  $hf = Join-Path ([IO.Path]::GetTempPath()) 'c1t2_psql.sql'
  Set-Content -LiteralPath $hf -Value $Sql -Encoding utf8
  docker cp "$hf" "sigov-postgres:/tmp/c1t2_psql.sql" | Out-Null
  $r = docker exec sigov-postgres psql -U postgres -d p1_dev -Atq -v ON_ERROR_STOP=1 -f /tmp/c1t2_psql.sql 2>&1 | Out-String
  return @{ code = $LASTEXITCODE; text = $r.Trim() }
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

# --- limpeza do fixture --------------------------------------------------------
$cl = Get-Psql @"
delete from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c1e26-%';
delete from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%';
select (select count(*) from sigov.educacao_merenda_requisicao where tenant_id=910001 and codigo like 'c1e26-%')
     + (select count(*) from sigov.educacao_biblioteca_acervo where tenant_id=910001 and codigo like 'c1e26-%') as restantes;
"@
Add-Check 'CX' 'fixture.removido' ($cl.code -eq 0 -and $cl.text -eq '0') ("delete c1e26-% (merenda_requisicao + biblioteca_acervo) tenant 910001 -> restantes=" + $cl.text)

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
