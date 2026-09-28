[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$td='C:\MNSOFT\SIGOV-PLUS\Temp\opencode'
Set-Location C:\MNSOFT\SIGOV-PLUS

# D1: contrato do ambiente vivo (fonte: env do contêiner api rodando)
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '<na>' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
"D1 CONTRACT db=$db user=$usr (password len=$($pw.Length))"
if ($db -eq '<na>' -or $usr -eq '<na>' -or [string]::IsNullOrEmpty($pw)) { 'FATAL: contrato nao resolvido'; exit 2 }

# D2: parar aplicações para aplicar schema sem conexoes ativas
docker compose stop api web worker 2>&1 | Out-String | Out-File -Encoding utf8 "$td\gate_runtime_stop.log"
"D2 STOP_EXIT=$LASTEXITCODE"

# D3: fluxo oficial de migrations (consolidado idempotente + replay versionado)
$o = docker compose run --rm -e "POSTGRES_DB=$db" -e "POSTGRES_USER=$usr" -e "POSTGRES_PASSWORD=$pw" db-migrations 2>&1
($o | Out-String) | Out-File -Encoding utf8 "$td\gate_runtime_migrations.log"
"D3 MIGRATIONS_EXIT=$LASTEXITCODE (esperado 0)"
$o | Select-String -Pattern 'ja aplicada|Aplicando|sucesso|ERROR' | Select-Object -Last 6 | ForEach-Object { '   ' + $_.Line.Trim() }

# D4: seed institucional (dados ficticios de desenvolvimento/homologacao)
$o = docker exec sigov-postgres psql -U postgres -d $db -v ON_ERROR_STOP=1 -f /tmp/gate_seed.sql 2>&1
($o | Out-String) | Out-File -Encoding utf8 "$td\gate_runtime_seed.log"
"D4 SEED_EXIT=$LASTEXITCODE (esperado 0)"

# D5: verificacao dos fixtures e do schema novo
function Probe([string]$c) { docker exec sigov-postgres psql -U postgres -d $db -A -t -c $c }
$v = @()
$v += 'RASCUNHO_DEMO(4)=' + (Probe 'select count(*) from sigov.compras_empresarial_requisicao where id in (''d0000001-0000-4000-8000-000000000001''::uuid,''d0000001-0000-4000-8000-000000000002''::uuid,''d0000001-0000-4000-8000-000000000003''::uuid,''d0000001-0000-4000-8000-000000000004''::uuid)')
$v += 'REQUISICAO_ITENS(5)=' + (Probe "select count(*) from sigov.compras_empresarial_requisicao_item")
$v += 'PERMISSAO_MODULO(9)=' + (Probe 'select count(*) from sigov.permissao where id in (694,700,701,702,703,705,706,726,727) and ativo and not is_deleted')
$v += 'PERFIL_PERM(11)=' + (Probe 'select count(*) from sigov.perfil_permissao where perfil_acesso_id in (9001,9002)')
$v += 'ALCADA_706(50000/250000)=' + (Probe "select perfil_acesso_id||':'||coalesce(alcada_valor::text,'null') from sigov.perfil_permissao where permissao_id=706 order by perfil_acesso_id")
$v += 'POLITICA_ROWS(0)=' + (Probe 'select count(*) from sigov.compras_empresarial_aprovacao_politica')
$v += 'USUARIOS_DEMO(2)=' + (Probe 'select count(*) from sigov.usuario where id in (101,102)')
$v += 'TENANT_MAPPING(4)=' + (Probe 'select count(*) from sigov.enterprise_tenant_mapping where id in (4101,4102,4103,4104)')
$v += 'ENTIDADES(3)=' + (Probe 'select count(*) from sigov.entidade where id in (9101,9102,9103)')
$v += 'OS_TECNICO(2)=' + (Probe "select count(*) from sigov.os_tecnico where id in (''c0000001-0000-4000-8000-000000000101''::uuid,''c0000001-0000-4000-8000-000000000102''::uuid)")
$v += 'LEN_CODIGO(32)=' + (Probe "select coalesce(character_maximum_length::text,'ausente') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo'")
$v += 'SCHEMA_MIG_MAX=' + (Probe 'select coalesce(max(version),''<null>'') from sigov.schema_migrations')
$v += 'DOCKER_MIG_COUNT=' + (Probe 'select count(*) from sigov.docker_schema_migrations')
($v | Out-String) | Out-File -Encoding utf8 "$td\gate_runtime_verify.txt"
"D5 VERIFY:"
$v | ForEach-Object { '   ' + $_ }

# D6: reiniciar aplicacoes contra o schema novo
docker compose up -d --force-recreate api web worker 2>&1 | Out-String | Out-File -Encoding utf8 "$td\gate_runtime_up.log"
"D6 UP_EXIT=$LASTEXITCODE"

# D7: aguardar healthchecks (até 120s)
$ok = $false
for ($i = 0; $i -lt 24; $i++) {
  Start-Sleep -Seconds 5
  $st = docker inspect --format '{{.State.Health.Status}}' sigov-api 2>$null
  $sw = docker inspect --format '{{.State.Health.Status}}' sigov-web 2>$null
  if ($st -eq 'healthy' -and $sw -eq 'healthy') { $ok = $true; break }
}
"D7 HEALTH api=$(docker inspect --format '{{.State.Health.Status}}' sigov-api) web=$(docker inspect --format '{{.State.Health.Status}}' sigov-web) ok=$ok"
