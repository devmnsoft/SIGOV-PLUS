[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$td='C:\MNSOFT\SIGOV-PLUS\Temp\opencode'
Set-Location C:\MNSOFT\SIGOV-PLUS

# contrato do ambiente vivo
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '<na>' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
"R0 CONTRACT db=$db user=$usr (password len=$($pw.Length))"
if ($db -eq '<na>' -or $usr -eq '<na>' -or [string]::IsNullOrEmpty($pw) -or $pw -eq '<na>') { 'FATAL: contrato nao resolvido'; exit 2 }
$env:POSTGRES_DB = $db; $env:POSTGRES_USER = $usr; $env:POSTGRES_PASSWORD = $pw

# E1: log completo do one-shot oficial que falhou (evidencia do bloqueio preexistente)
docker logs sigov-db-migrations > "$td\gate_runtime_official_flow_fail.log" 2>&1
"logs capturados -> gate_runtime_official_flow_fail.log ($((Get-Item "$td\gate_runtime_official_flow_fail.log").Length) bytes)"

# T1b: sondagem do fluxo oficial contra o banco NOVO (mesmo bloqueio em DB limpo)
function ProbeErr([string]$c) { (docker exec sigov-postgres psql -U postgres -d $db -A -t -c $c 2>&1 | Out-String).Trim() }
$t1 = @()
$t1 += 'DB_NOVO_COLS_FILEPATH_CHECKSUM(0)=' + (ProbeErr "select count(*) from information_schema.columns where table_schema='sigov' and table_name='docker_schema_migrations' and column_name in ('file_path','checksum')")
$t1 += 'DB_NOVO_ONCONFLICT_NAME=' + (ProbeErr "insert into sigov.docker_schema_migrations (name) values ('gate_probe_onconflict2') on conflict (name) do nothing;")
$t1 += 'DB_NOVO_LEDGER_COLS=' + (ProbeErr "select string_agg(column_name, ',' order by ordinal_position) from information_schema.columns where table_schema='sigov' and table_name='docker_schema_migrations'")
$runnerQuote = (Select-String -Path 'database\docker\apply-migrations.sh' -Pattern "on conflict" | Select-Object -First 2 | ForEach-Object { $_.Line.Trim() })
$t1 += 'RUNNER_CONTRATO_L28=' + ((Select-String -Path 'database\docker\apply-migrations.sh' -Pattern "where version=" | Select-Object -First 1).Line.Trim())
$t1 += 'RUNNER_CONTRATO_L36=' + (($runnerQuote | Where-Object { $_ -like '*insert*' } | Select-Object -First 1))
$t1 += 'DDL_CONTRATO(apply_all_required_migrations.sql L6-L10)=' + ((Get-Content 'database\apply_all_required_migrations.sql')[5..9] -join ' ')
($t1 | Out-String) | Out-File -Encoding utf8 "$td\gate_runtime_official_attempt.log"
"T1b OFFICIAL_FLOW_NOVO_DB:"
$t1 | ForEach-Object { '   ' + $_ }

# F5b: verificação completa novamente com probes corrigidos (string PS dupla para SQL)
function Probe([string]$c) { (docker exec sigov-postgres psql -U postgres -d $db -A -t -c $c 2>&1 | Out-String).Trim() }
$v = @()
$v += 'RASCUNHO_DEMO(4)=' + (Probe "select count(*) from sigov.compras_empresarial_requisicao where id in ('d0000001-0000-4000-8000-000000000001'::uuid,'d0000001-0000-4000-8000-000000000002'::uuid,'d0000001-0000-4000-8000-000000000003'::uuid,'d0000001-0000-4000-8000-000000000004'::uuid)")
$v += 'REQUISICAO_ITENS(5)=' + (Probe 'select count(*) from sigov.compras_empresarial_requisicao_item')
$v += 'PERMISSAO_MODULO(9)=' + (Probe 'select count(*) from sigov.permissao where id in (694,700,701,702,703,705,706,726,727) and ativo and not is_deleted')
$v += 'PERFIL_PERM(11)=' + (Probe 'select count(*) from sigov.perfil_permissao where perfil_acesso_id in (9001,9002)')
$v += 'ALCADA_706=' + (Probe "select pp.perfil_acesso_id||'|'||pa.nome||'|'||coalesce(pp.alcada_valor::text,'null') from sigov.perfil_permissao pp join sigov.perfil_acesso pa on pa.id=pp.perfil_acesso_id where pp.permissao_id=706 order by pp.perfil_acesso_id")
$v += 'POLITICA_ROWS(0)=' + (Probe 'select count(*) from sigov.compras_empresarial_aprovacao_politica')
$v += 'USUARIOS_DEMO(2)=' + (Probe 'select count(*) from sigov.usuario where id in (101,102)')
$v += 'USUARIO_ADMIN(1)=' + (Probe 'select count(*) from sigov.usuario where id = 1 and not is_deleted')
$v += 'TENANT_MAPPING(4)=' + (Probe 'select count(*) from sigov.enterprise_tenant_mapping where id in (4101,4102,4103,4104)')
$v += 'ENTIDADES(3)=' + (Probe 'select count(*) from sigov.entidade where id in (9101,9102,9103)')
$v += 'OS_TECNICO(2)=' + (Probe "select count(*) from sigov.os_tecnico where id in ('c0000001-0000-4000-8000-000000000101'::uuid,'c0000001-0000-4000-8000-000000000102'::uuid)")
$v += 'LEN_CODIGO(32)=' + (Probe "select coalesce(character_maximum_length::text,'ausente') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo'")
$v += 'CONSTRAINTS_20260927(2)=' + (Probe "select count(*) from pg_constraint where conrelid = 'sigov.compras_empresarial_recebimento_divergencia'::regclass and conname in ('ck_comp_recb_div_resultado_codigo','ck_comp_recb_div_codigo_encerrado')")
$v += 'CICLO_COL(1)=' + (Probe "select count(*) from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name='ciclo'")
$v += 'APROVADOR_NULLABLE(YES)=' + (Probe "select case is_nullable when 'YES' then 'YES' else 'NO' end from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name='aprovador_id'")
$v += 'SCHEMA_MIG_MAX(20260927120000)=' + (Probe 'select coalesce(max(version),''<null>'') from sigov.schema_migrations')
($v | Out-String) | Out-File -Encoding utf8 "$td\gate_runtime_verify.txt"
"F5b VERIFY (corrigido):"
$v | ForEach-Object { '   ' + $_ }

# F6b: subir as aplicações sem reexecutar dependências one-shot (postgres já healthy;
# db-migrations tem o bloqueio preexistente documentado em T1b/official_flow_fail.log)
docker compose up -d --no-deps --force-recreate api web worker 2>&1 | Out-String | Out-File -Encoding utf8 "$td\gate_runtime_up2.log"
"F6b UP_NODEPS_EXIT=$LASTEXITCODE"

# F7b: aguardar healthchecks (até 150s)
$ok = $false
for ($i = 0; $i -lt 30; $i++) {
  Start-Sleep -Seconds 5
  $st = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sigov-api 2>$null)
  $sw = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sigov-web 2>$null)
  "   iter=$i api=$st web=$sw"
  if ($st -eq 'healthy' -and $sw -eq 'healthy') { $ok = $true; break }
}
"F7b HEALTH ok=$ok"
