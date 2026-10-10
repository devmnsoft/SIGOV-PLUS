# =============================================================================
# SIGOV PLUS · Gate B (P1) · RC-SAAS-AUT — runtime em PostgreSQL 16
# =============================================================================
# Prova ao vivo (real PG16 + real HTTP Web/API) os verticais de AUTORIZAÇÃO do
# RC-SAAS-AUT entregues e consolidados, no HEAD corrente:
#   G1  Cadeia de autenticação Web -> cookie -> ApiToken -> bearer da API
#   G2  Avaliação canônica fail-closed de módulos (motivos comerciais B/C + baseline A/D)
#   G3  403 padronizado com motivo — paridade Web (ForbiddenResponse) + API (RequireModule)
#   G4  Revogação por alteração de identidade (auth_version) — paridade Web + API (A5)
#   G5  Triplo canônico MNSOFT 'saas.plataforma.administrar' como autoridade no banco
#
# Pré-requisitos (não mascarados; falha vira FAIL/BLOCKED, regra 13/18):
#   - Container PostgreSQL 16 (sigov-postgres) acessível via `docker exec`.
#   - Banco $DbName (default p1_dev) criado e populado pelo seed dev COMPLETO
#     (script_completo_dev.sql) + o seed de cenário RC-SAAS-AUT
#     (database/postgres/seeds/homologacao/20261005_rcsaas_aut_seed.sql),
#     cuja referência ADMIN_TENANT (perfil id=50, tenant 1) foi remapeada no seed
#     para o perfil sistêmico 910500 em runs de homologação.
#   - Apps Sigov.Web (porta 5099) e Sigov.Api (porta 5100) rodando contra $DbName.
#     Receita (a partir de bin/Release/net10.0):
#       ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:<port>
#       ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=p1_dev;Username=postgres;Password=<pw>;Search Path=sigov;Application Name=sigov.<app>"
#       (Api adiciona Sigov__Database__MigrationMode=Disabled)
#   - A porta 5433 do host encaminha para sigov-postgres:5432 (ex.: socat alpine)
#     porque a 5432 do host pode estar sombreada por um PostgreSQL nativo do Windows.
#
# O script é IDEMPOTENTE: aplica o fixture de autorização (select-or-insert) antes
# dos testes e restaura qualquer estado que altere (revogação de G4).
# Sem segredos em texto literal; as senhas das sondas são fixtures documentadas.
# Uso:  pwsh ./scripts/gate-b-saas-runtime.ps1 [-WebBase ...] [-ApiBase ...]
#       powershell -File scripts\gate-b-saas-runtime.ps1   (Windows PowerShell 5.1)
# Exit code: 0 = todos verdes; 1 = ao menos um FAIL/BLOCKED.
# =============================================================================
param(
  [string]$PgContainer = 'sigov-postgres',
  [string]$PgUser      = 'postgres',
  [string]$DbName      = 'p1_dev',
  [string]$WebBase     = 'http://localhost:5099',
  [string]$ApiBase     = 'http://localhost:5100',
  [string]$Password    = 'Homologacao!2026',
  [string]$OutputDirectory = 'artifacts/gate_b_saas_aut'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $out | Out-Null

# Nome canônico da tabela de perfis, montado por código de caractere para evitar
# divergência visual de digitação (acento/agrupamento): perfil_accesso.
$ac   = -join [char[]]@(97,99,101,115,115,111)        # acesso
$tbl  = 'sigov.perfil_' + $ac                         # sigov.perfil_accesso
$col  = 'perfil_' + $ac + '_id'                       # perfil_accesso_id
Write-Output ("DBGVAR ac[len=" + $ac.Length + "] tbl=[" + $tbl + "] col=[" + $col + "]")

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Phase, [string]$Name, [bool]$Passed, [string]$Evidence) {
  $checks.Add([pscustomobject]@{ phase=$Phase; name=$Name; passed=$Passed; evidence=$Evidence; checkedAt=(Get-Date).ToUniversalTime().ToString('o') })
  $mark = if ($Passed) { 'PASS' } else { 'FAIL' }
  Write-Host ("[{0}] {1} :: {2}" -f $mark, $Name, $Evidence)
}

# --- helpers de banco (via docker exec) --------------------------------------
# Observacao de ambiente (regra 13): nao ha `psql` no host; o banco e acessado pelo
# contêiner `sigov-postgres` via `docker exec`. O SQL e gravado em um arquivo absoluto
# do HOST (Windows) e copiado ao contêiner em um caminho absoluto POSIX (/tmp); esse
# par (host absoluto + destino /tmp) e o padrao validado neste ambiente.
$script:pgDir  = Join-Path ([IO.Path]::GetTempPath()) 'sigov_gateb'
New-Item -ItemType Directory -Force -Path $script:pgDir | Out-Null
function Run-Pg([string]$Sql,[string]$HostFile,[string]$ContPath,[string[]]$PsqlFlags){
  Set-Content -LiteralPath $HostFile -Value $Sql -Encoding utf8
  docker cp "$HostFile" "${PgContainer}:${ContPath}" | Out-Null
  $r = docker exec $PgContainer psql -U $PgUser -d $DbName @PsqlFlags -f "$ContPath" 2>&1 | Out-String
  return @{ code = $LASTEXITCODE; text = $r.Trim() }
}
function Get-Psql {
  param([string]$Sql, [switch]$NoStop)
  $hf = Join-Path $script:pgDir 'q.sql'
  $flags = @('-Atq'); if (-not $NoStop) { $flags += @('-v','ON_ERROR_STOP=1') }
  return (Run-Pg -Sql $Sql -HostFile $hf -ContPath '/tmp/gateb_q.sql' -PsqlFlags $flags)
}
function Invoke-PsqlScript {
  param([string]$Sql)
  $hf = Join-Path $script:pgDir 'fix.sql'
  return (Run-Pg -Sql $Sql -HostFile $hf -ContPath '/tmp/gateb_fix.sql' -PsqlFlags @('-v','ON_ERROR_STOP=1'))
}

# --- helpers HTTP (curl.exe) -------------------------------------------------
# Rate-limit de login (~10/min/IP) => login com verificacao do redirect e retry/backoff.
function New-SigovLogin {
  param([string]$User, [string]$Pass, [string]$Base)
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    $jar = Join-Path ([IO.Path]::GetTempPath()) ("gateb_" + [guid]::NewGuid().ToString('N') + ".jar")
    $html = (& curl.exe -s -c $jar "$Base/Auth/Login") -join "`n"
    $tok = ''; $i = $html.IndexOf('__RequestVerificationToken')
    if ($i -ge 0) { $tok = ([regex]::Match($html.Substring($i), 'value="([^"]+)"')).Groups[1].Value }
    $args = @('-s','-b',$jar,'-c',$jar,'-o','NUL','-w','%{http_code}|%{redirect_url}','-X','POST',"$Base/Auth/Login","--data-urlencode","Login=$User","--data-urlencode","Senha=$Pass","--data-urlencode","LembrarLogin=false","--data-urlencode","__RequestVerificationToken=$tok")
    $post = (& curl.exe @args).Trim()
    if ($post -match '^302\|.*MinhaCentral') { return [pscustomobject]@{ user=$User; jar="$jar"; post="$post"; ok=$true } }
    Start-Sleep -Seconds 6   # backoff para janela de rate-limit
  }
  return [pscustomobject]@{ user=$User; jar="$jar"; post="$post"; ok=$false }
}
function Invoke-WebGet {
  param([string]$Jar, [string]$Path, [string]$Base)
  $u = if ($Path -match '^http') { $Path } else { "$Base$Path" }
  $of = Join-Path ([IO.Path]::GetTempPath()) ("gateb_" + [guid]::NewGuid().ToString('N') + ".out")
  $code = (& curl.exe -s -b $Jar -o $of -w "%{http_code}" $u).Trim()
  $body = if (Test-Path $of) { (Get-Content $of -Raw) } else { '' }
  return [pscustomobject]@{ code=[int]$code; body="$body" }
}
function Get-ApiToken {
  param([string]$Jar, [string]$Base)
  $r = Invoke-WebGet -Jar $Jar -Path '/Auth/ApiToken' -Base $Base
  try { $j = $r.body | ConvertFrom-Json
        return [pscustomobject]@{ token=[string]$j.token; exercicioId=$j.exercicioId; erro=[string]$j.erro } }
  catch { return [pscustomobject]@{ token=''; exercicioId=$null; erro="parse_err:$($r.body.Substring(0,[Math]::Min(200,$r.body.Length)))" } }
}
function Invoke-Api {
  param([string]$Bearer, [string]$Method='GET', [string]$Path='',[string]$BodyText='', [string]$Base)
  $u = if ($Path -match '^http') { $Path } else { "$Base$Path" }
  $of = Join-Path ([IO.Path]::GetTempPath()) ("gateb_" + [guid]::NewGuid().ToString('N') + ".out")
  $w = @('-s','-X',$Method,'-H','Content-Type: application/json','-o',$of,'-w','%{http_code}')
  if (-not [string]::IsNullOrWhiteSpace($Bearer)) { $w += @("-H","Authorization: Bearer $Bearer") }
  if (-not [string]::IsNullOrWhiteSpace($BodyText)) { $w += @('-d',$BodyText) }
  $w += $u
  $code = (& curl.exe @w).Trim()
  $body = if (Test-Path $of) { (Get-Content $of -Raw) } else { '' }
  return [pscustomobject]@{ code=[int]$code; body="$body" }
}
function Get-Motivo([string]$Body){ try { return [string]((ConvertFrom-Json $Body).motivo) } catch { return '' } }
function Read-Cookie([string]$Jar,[string]$Name){
  foreach($l in (Get-Content $Jar)){ $p=$l -split "\t"; if($p.Count -ge 7 -and $p[5] -eq $Name){ return $p[6] } }
  return ''
}

Write-Host "==============================================================="
Write-Host "Gate B (P1) · RC-SAAS-AUT · runtime PostgreSQL 16"
Write-Host ("ts=" + (Get-Date).ToUniversalTime().ToString('o'))
Write-Host ("web=$WebBase api=$ApiBase pg=$PgContainer db=$DbName")
Write-Host "==============================================================="

# =============================================================================
# FASE 0 — sanidade de ambiente + fixtures
# =============================================================================
$db = Get-Psql "select version();"
Add-Check 'G0' 'env.pg16-reachable' ($db.code -eq 0 -and $db.text -match 'PostgreSQL 16') ("pg reachable; " + (($db.text -split '\n')[0]))

$webHealth = & curl.exe -s -o NUL -w "%{http_code}" "$WebBase/Auth/Login"
Add-Check 'G0' 'env.web-up' ($webHealth -eq '200') ("GET /Auth/Login -> $webHealth")

$apiHealth = & curl.exe -s -o NUL -w "%{http_code}" "$ApiBase/api/saas/tenant/modulos"
Add-Check 'G0' 'env.api-up' ($apiHealth -in '401','200') ("GET /api/saas/tenant/modulos sem bearer -> $apiHealth (401 esperado)")

# Aplica o fixture de autorização idempotente (per-tenant ADMIN_TENANT + sonda nao-admin).
$fixSrc = Join-Path $root 'database/postgres/seeds/homologacao/20261010_gateb_rcsaas_aut_fixtures.sql'
$f = (Get-Content $fixSrc -Raw).Replace("{{TBL}}", $tbl).Replace("{{COL}}", $col)
if ($env:GATEB_DEBUG_FIX) { Set-Content -LiteralPath "$env:TEMP\opencode\gateb_gen_fix.sql" -Value $f -Encoding utf8; Write-Output ("DBG tbl=[" + $tbl + "] col=[" + $col + "] len_col=" + $col.Length) }
$fixRes = Invoke-PsqlScript -Sql $f
Add-Check 'G0' 'fixture.applied' ($fixRes.code -eq 0) ("fixture gateb aplicado exit=$($fixRes.code)")

# Confirma estados comerciais dos tenants de cenário (autoridade = banco).
$sc = Get-Psql @"
set search_path to sigov;
select t.id, t.ativo, a.status, coalesce(a.data_fim::text,'NULL'), a.usuarios_contratados,
  (select tmc.status from sigov.tenant_modulo_contratado tmc
     where tmc.tenant_id=t.id and tmc.modulo_codigo='educacao') as edu_status
from sigov.tenant t join sigov.saas_assinatura a on a.tenant_id=t.id
where t.id in (910001,910002,910003,910004) order by t.id;
"@
$scRows = @($sc.text -split '\n' | Where-Object { $_.Trim() })
Add-Check 'G0' 'fixture.cenarios-presentes' ($sc.code -eq 0 -and $scRows.Count -eq 4) ("linhas cenario=" + $scRows.Count)
# Estados comerciais individuais (autoridade = banco; a prova viva destes estados é o G2 via HTTP).
Add-Check 'G0' 'fixture.B-suspensa' ($scRows.Count -gt 1 -and ($scRows[1] -match 'SUSPENSA')) ("tenant 910002 -> " + $scRows[1])
Add-Check 'G0' 'fixture.C-expirada' ($scRows.Count -gt 2 -and ($scRows[2] -match '2026-09-30')) ("tenant 910003 data_fim -> " + $scRows[2])

# =============================================================================
# FASE 1 — cadeia de autenticação Web -> cookie -> ApiToken -> bearer
# =============================================================================
$lgA = New-SigovLogin -User 'hom.a.admin' -Pass $Password -Base $WebBase
Add-Check 'G1' 'auth.login-web' ($lgA.ok -and $lgA.post -match '^302\|.*MinhaCentral') ("hom.a.admin POST /Auth/Login -> $($lgA.post)")

$m = Invoke-WebGet -Jar $lgA.jar -Path '/MinhaCentral' -Base $WebBase
Add-Check 'G1' 'auth.minha-central' ($m.code -eq 200) ("GET /MinhaCentral -> $($m.code)")

$tk = Get-ApiToken -Jar $lgA.jar -Base $WebBase
Add-Check 'G1' 'auth.api-token-mint' ($tk.token.Length -eq 64) ("GET /Auth/ApiToken len=" + $tk.token.Length + " (erro='Nenhum exercício ativo' é informativo; o token bruto é válido — verificado em auth.bearer-api-200)")

$apiOk = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/saas/tenant/modulos' -Base $ApiBase
Add-Check 'G1' 'auth.bearer-api-200' ($apiOk.code -eq 200) ("GET /api/saas/tenant/modulos bearer -> $($apiOk.code); " + ($apiOk.body.Substring(0,[Math]::Min(140,$apiOk.body.Length))))

$cookie = Read-Cookie $lgA.jar 'SIGOV.AUTH'
$cookieAsBearer = Invoke-Api -Bearer $cookie -Method GET -Path '/api/saas/tenant/modulos' -Base $ApiBase
Add-Check 'G1' 'auth.bearer-necessario' ($cookieAsBearer.code -eq 401 -and $cookie.Length -gt 0) ("cookie cru SIGOV.AUTH como bearer -> $($cookieAsBearer.code) (401 esperado; cookie != token bruto)")

$noAuth = Invoke-Api -Bearer '' -Method GET -Path '/api/saas/tenant/modulos' -Base $ApiBase
Add-Check 'G1' 'auth.anonimo-401' ($noAuth.code -eq 401 -and $noAuth.body -like '*authentication_required*') ("sem bearer -> $($noAuth.code) $($noAuth.body.Substring(0,[Math]::Min(80,$noAuth.body.Length)))")

# =============================================================================
# FASE 2 — avaliação canônica fail-closed de módulos (HTTP real por cenário)
# =============================================================================
$rA = Invoke-Api -Bearer $tk.token -Method GET -Path '/api/educacao/escolas' -Base $ApiBase
Add-Check 'G2' 'modulo.A-baseline-200' ($lgA.ok -and $rA.code -eq 200) ("A hom.a.admin (ATIVA/válida) GET /api/educacao/escolas -> $($rA.code)")

Start-Sleep -Milliseconds 1500
$lgB = New-SigovLogin -User 'hom.b.admin' -Pass $Password -Base $WebBase
$tkB = Get-ApiToken -Jar $lgB.jar -Base $WebBase
$rB = Invoke-Api -Bearer $tkB.token -Method GET -Path '/api/educacao/escolas' -Base $ApiBase
Add-Check 'G2' 'modulo.B-bloqueado' ($lgB.ok -and $rB.code -eq 403 -and (Get-Motivo $rB.body) -eq 'BLOQUEADO_COMERCIAL') ("B hom.b.admin (SUSPENSA, login.ok=$($lgB.ok)) -> $($rB.code) motivo=" + (Get-Motivo $rB.body) + " | " + $rB.body.Substring(0,[Math]::Min(120,$rB.body.Length)))

Start-Sleep -Milliseconds 1500
$lgC = New-SigovLogin -User 'hom.c.admin' -Pass $Password -Base $WebBase
$tkC = Get-ApiToken -Jar $lgC.jar -Base $WebBase
$rC = Invoke-Api -Bearer $tkC.token -Method GET -Path '/api/educacao/escolas' -Base $ApiBase
Add-Check 'G2' 'modulo.C-expirado' ($lgC.ok -and $rC.code -eq 403 -and (Get-Motivo $rC.body) -eq 'CONTRATO_EXPIRADO') ("C hom.c.admin (expirada, login.ok=$($lgC.ok)) -> $($rC.code) motivo=" + (Get-Motivo $rC.body) + " | " + $rC.body.Substring(0,[Math]::Min(120,$rC.body.Length)))

Start-Sleep -Milliseconds 1500
$lgD = New-SigovLogin -User 'hom.d.admin' -Pass $Password -Base $WebBase
$tkD = Get-ApiToken -Jar $lgD.jar -Base $WebBase
$rD = Invoke-Api -Bearer $tkD.token -Method GET -Path '/api/educacao/escolas' -Base $ApiBase
Add-Check 'G2' 'modulo.D-limite-nao-bloqueia-modulo' ($lgD.ok -and $rD.code -eq 200) ("D hom.d.admin (ATIVA/limite=1, login.ok=$($lgD.ok)) -> $($rD.code) (limite aplica a criação de usuário, não a módulo)")

# =============================================================================
# FASE 3 — 403 padronizado com motivo: paridade Web + API
# =============================================================================
# API: envelope { success:false, message, motivo } (RequireModuleAttribute).
$jB = $null
try { $jB = ConvertFrom-Json $rB.body } catch {}
$envApi = ($rB.code -eq 403 -and $jB.success -eq $false -and -not [string]::IsNullOrWhiteSpace((Get-Motivo $rB.body)))
Add-Check 'G3' 'forbid.api-envelope' $envApi ("API 403: success=" + $jB.success + " motivo=" + (Get-Motivo $rB.body) + " (motivo presente e padronizado)")

# GET Web com retry curto para amortecer eventual redirect de rate-limit de sessao.
function Invoke-WebGetRetry([string]$Jar,[string]$Path,[string]$Base){
  $r = Invoke-WebGet -Jar $Jar -Path $Path -Base $Base
  for ($t=1; $t -le 2 -and $r.code -eq 302; $t++) { Start-Sleep -Seconds 5; $r = Invoke-WebGet -Jar $Jar -Path $Path -Base $Base }
  return $r
}

# Web: mesma forma canônica { ok:false, motivo, detalhe } (ForbiddenResponse.Registrar).
Start-Sleep -Milliseconds 1500
$lgN = New-SigovLogin -User 'gate.nao.admin' -Pass $Password -Base $WebBase
$wN = Invoke-WebGetRetry -Jar $lgN.jar -Path '/Seguranca/Usuarios' -Base $WebBase
$jW = $null; try { $jW = ConvertFrom-Json $wN.body } catch {}
$envWeb = ($lgN.ok -and $wN.code -eq 403 -and $jW.ok -eq $false -and (Get-Motivo $wN.body) -eq 'SEM_PERMISSAO')
Add-Check 'G3' 'forbid.web-403-sem-permissao' $envWeb ("gate.nao.admin (login.ok=$($lgN.ok)) GET /Seguranca/Usuarios -> $($wN.code) motivo=" + (Get-Motivo $wN.body) + " ok=" + $jW.ok)

# Admin global (com o triplo) acessa a mesma rota -> 200 (contraste).
Start-Sleep -Milliseconds 1500
$lgA2 = New-SigovLogin -User 'hom.a.admin' -Pass $Password -Base $WebBase
$wA = Invoke-WebGetRetry -Jar $lgA2.jar -Path '/Seguranca/Usuarios' -Base $WebBase
Add-Check 'G3' 'forbid.web-admin-200' ($lgA2.ok -and $wA.code -eq 200) ("hom.a.admin (admin global, login.ok=$($lgA2.ok)) GET /Seguranca/Usuarios -> $($wA.code) len=" + $wA.body.Length)

# =============================================================================
# FASE 4 — revogação por alteração de identidade (auth_version), paridade Web+API
# =============================================================================
$orig = Get-Psql "select coalesce(updated_at::text,'null') from sigov.usuario where id=910304;"
$beforeApi = Invoke-Api -Bearer $tkD.token -Method GET -Path '/api/saas/tenant/modulos' -Base $ApiBase
$beforeWeb = Invoke-WebGet -Jar $lgD.jar -Path '/MinhaCentral' -Base $WebBase
Add-Check 'G4' 'revoke.before-valid' ($beforeApi.code -eq 200 -and $beforeWeb.code -eq 200) ("antes do bump: api=" + $beforeApi.code + " web_MinhaCentral=" + $beforeWeb.code)

Get-Psql "update sigov.usuario set updated_at=(coalesce(updated_at, now()) + interval '1 second'), updated_by=910304 where id=910304 returning 1;" | Out-Null
Start-Sleep -Milliseconds 400
$afterWeb = Invoke-WebGet -Jar $lgD.jar -Path '/MinhaCentral' -Base $WebBase
$afterApi = Invoke-Api -Bearer $tkD.token -Method GET -Path '/api/saas/tenant/modulos' -Base $ApiBase
Add-Check 'G4' 'revoke.web-302' ($afterWeb.code -eq 302) ("após bump identity: web /MinhaCentral com mesmo cookie -> $($afterWeb.code) (redireciona p/ login)")
Add-Check 'G4' 'revoke.api-401' ($afterApi.code -eq 401) ("após bump identity: api com token antigo -> $($afterApi.code) (401 esperado)")

# Restaura o valor original (idempotência/limpeza).
if ((Get-Psql "select coalesce(updated_at::text,'null') from sigov.usuario where id=910304;").text -ne $orig.text) {
  Get-Psql "update sigov.usuario set updated_at=null where id=910304;" | Out-Null
}
$restored = (Get-Psql "select coalesce(updated_at::text,'null') from sigov.usuario where id=910304;").text
Add-Check 'G4' 'revoke.restored' ($restored -eq $orig.text) ("usuario.updated_at restaurado para '" + $restored + "' (era '" + $orig.text + "')")

# =============================================================================
# FASE 5 — triplo canônico MNSOFT como autoridade no banco
# =============================================================================
# Consultas de coluna unica (sem separador) => comparacao exata, imune a TAB/espaco/linebreak.
$permId   = Get-Psql "select id from sigov.permissao where chave='saas.plataforma.administrar';"
$permAtivo= Get-Psql "select ativo from sigov.permissao where chave='saas.plataforma.administrar';"
$permMod  = Get-Psql "select modulo from sigov.permissao where chave='saas.plataforma.administrar';"
$permOk = ($permId.code -eq 0 -and $permAtivo.code -eq 0 -and $permMod.code -eq 0 -and $permAtivo.text.Trim() -ceq 't' -and $permMod.text.Trim() -ceq 'saas')
Add-Check 'G5' 'mns.triplo-no-banco' $permOk ("permissao saas.plataforma.administrar id=" + $permId.text.Trim() + " ativo=" + $permAtivo.text.Trim() + " modulo=" + $permMod.text.Trim())

$holders = Get-Psql @"
set search_path to sigov;
select count(*) from usuario u
  join usuario_grupo ug on ug.usuario_id=u.id and ug.ativo and not ug.is_deleted
  join grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo and not gp.is_deleted
  join $tbl pa on pa.id=gp.$col and pa.ativo and not pa.is_deleted
  join sigov.perfil_permissao pp on pp.$col=pa.id and pp.ativo and not pp.is_deleted
  join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted and p.chave='saas.plataforma.administrar'
 where u.ativo and not u.is_deleted;
"@
Add-Check 'G5' 'mns.holders-ativos' ($holders.code -eq 0 -and [int]($holders.text) -ge 1) ("usuários ativos com o triplo (banco, regra 11) = " + $holders.text)

# =============================================================================
# RESUMO + artefatos
# =============================================================================
$passed = @($checks | Where-Object passed).Count
$failed = @($checks | Where-Object { -not $_.passed }).Count
$result = [pscustomobject]@{ gate='Gate B (P1) RC-SAAS-AUT runtime PG16'; rc='RC-SAAS-AUT'; generatedAt=(Get-Date).ToUniversalTime().ToString('o'); passed=$passed; failed=$failed; checks=$checks }
$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'gate-b-saas-aut-result.json') -Encoding utf8

$lines = @('# Gate B (P1) · RC-SAAS-AUT — runtime em PostgreSQL 16','','Gerado em: ' + $result.generatedAt,'',"Aprovados: **$passed** · Pendentes: **$failed**",'')
foreach ($c in $checks) { $box = if ($c.passed) { '[x]' } else { '[ ]' }; $tag = if ($c.passed) { 'PASS' } else { 'FAIL' }; $ph = $c.phase; $nm = $c.name; $ev = $c.evidence; $lines += ("- $box **$tag** [$ph/$nm] $ev") }
$lines | Set-Content (Join-Path $out 'gate-b-saas-aut-report.md') -Encoding utf8
$logLines = foreach ($c in $checks) { $ts = $c.checkedAt; $ph = $c.phase; $nm = $c.name; $pp = $c.passed; $ev = $c.evidence; "[ts=$ts] phase=$ph name=$nm passed=$pp evidence=$ev" }; $logLines | Set-Content (Join-Path $out 'gate-b-saas-aut.log') -Encoding utf8

Write-Host "==============================================================="
Write-Host ("RESULTADO Gate B RC-SAAS-AUT: PASS=$passed FAIL=$failed")
Write-Host ("Artefatos: $out")
Write-Host "==============================================================="
if ($failed -gt 0) { Write-Warning "$failed verificação(ões) não verde(s)." ; exit 1 }
exit 0
