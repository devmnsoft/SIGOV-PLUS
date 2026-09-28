# gate_reset.ps1 - Restaura o estado virgem do demo de compras apos a jornada (P1 fix do enviar)
$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$stg = Join-Path $PSScriptRoot 'tmp'
New-Item -ItemType Directory -Force -Path $stg | Out-Null

# --- contrato do banco vivo (regra 18: segredo vem do ambiente do container) ---
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
if(-not $envline){ Write-Host 'FATAL: sigov-api / contrato de conexao ausente'; exit 1 }
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'
if(-not $db -or -not $usr -or -not $pw){ Write-Host 'FATAL: contrato nao resolvido'; exit 1 }

$T='b0000001-0000-4000-8000-000000000001'
$ids="'d0000001-0000-4000-8000-000000000001','d0000001-0000-4000-8000-000000000002','d0000001-0000-4000-8000-000000000003','d0000001-0000-4000-8000-000000000004','ce2981ae-29bc-4ffe-80d6-d2968420840a'"

$resetSql = @"
begin;
delete from sigov.compras_empresarial_aprovacao where tenant_id='$T';
delete from sigov.compras_empresarial_aprovacao_politica_nivel where tenant_id='$T';
delete from sigov.compras_empresarial_aprovacao_politica where tenant_id='$T';
delete from sigov.compras_empresarial_idempotencia where chave like 'jornada-%';
delete from sigov.compras_empresarial_historico where aggregate_id in ($ids);
delete from sigov.compras_empresarial_requisicao_item where requisicao_id in ($ids);
delete from sigov.compras_empresarial_requisicao where id in ($ids);
delete from sigov.pendencia_operacional where tenant_id=1 and tipo ilike 'APROVACAO%';
commit;
"@
$rf = Join-Path $stg 'gate_reset.sql'
[System.IO.File]::WriteAllText($rf,$resetSql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$rf" 'sigov-postgres:/tmp/gate_reset.sql')
$out = & docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -v ON_ERROR_STOP=1 -f /tmp/gate_reset.sql 2>&1 | Out-String
$ex=$LASTEXITCODE
[void](docker exec sigov-postgres rm -f /tmp/gate_reset.sql)
Write-Host "RESET EXIT=$ex"; Write-Host $out
if($ex -ne 0){ exit 1 }

# --- re-aplica o seed idempotente (identico ao usado na reparacao: /tmp/gate_seed.sql) ---
$out2 = & docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -v ON_ERROR_STOP=1 -f /tmp/gate_seed.sql 2>&1 | Out-String
$ex2=$LASTEXITCODE
Write-Host "SEED EXIT=$ex2"; Write-Host $out2
if($ex2 -ne 0){ exit 1 }

# --- verificacao do estado virgem ---
$verifySql = @"
select r.numero,r.status,r.version,r.valor_estimado from sigov.compras_empresarial_requisicao r where r.id in ('d0000001-0000-4000-8000-000000000001','d0000001-0000-4000-8000-000000000002','d0000001-0000-4000-8000-000000000003','d0000001-0000-4000-8000-000000000004') order by r.numero;
select 'POLITICA='||count(*) from sigov.compras_empresarial_aprovacao_politica where tenant_id='$T';
select 'ETAPAS='||count(*) from sigov.compras_empresarial_aprovacao where requisicao_id in ('d0000001-0000-4000-8000-000000000001','d0000001-0000-4000-8000-000000000002','d0000001-0000-4000-8000-000000000003','d0000001-0000-4000-8000-000000000004');
select 'CHAVES_JORNADA='||count(*) from sigov.compras_empresarial_idempotencia where chave like 'jornada-%';
select 'RQ5='||count(*) from sigov.compras_empresarial_requisicao where id='ce2981ae-29bc-4ffe-80d6-d2968420840a';
select 'ITENS_'||left(requisicao_id::text,13)||' n='||count(*)||' total='||round(sum(quantidade*valor_estimado),2) from sigov.compras_empresarial_requisicao_item where requisicao_id in ('d0000001-0000-4000-8000-000000000001','d0000001-0000-4000-8000-000000000002','d0000001-0000-4000-8000-000000000003','d0000001-0000-4000-8000-000000000004') group by requisicao_id order by requisicao_id;
select 'USUARIOS='||string_agg(id::text||':'||login||':t'||tenant_id||':e'||coalesce(entidade_id::text,'null'),', ') from sigov.usuario where id in (101,102);
select 'PP_9001='||count(*) from sigov.perfil_permissao where perfil_acesso_id=9001;
select 'PP_9002='||count(*) from sigov.perfil_permissao where perfil_acesso_id=9002;
select 'ALCADA_P'||pp.perfil_acesso_id||'='||coalesce(pp.alcada_valor::text,'null') from sigov.perfil_permissao pp join sigov.permissao p on p.id=pp.permissao_id where p.modulo='compras_empresariais' and p.recurso='compras_empresariais.aprovacoes' and p.acao='aprovar' order by pp.perfil_acesso_id;
select 'PENDENCIAS_DEMO='||count(*) from sigov.pendencia_operacional where tenant_id=1 and tipo ilike 'APROVACAO%';
"@
$vf = Join-Path $stg 'gate_verify.sql'
[System.IO.File]::WriteAllText($vf,$verifySql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$vf" 'sigov-postgres:/tmp/gate_verify.sql')
$out3 = & docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/gate_verify.sql 2>&1 | Out-String
$ex3=$LASTEXITCODE
[void](docker exec sigov-postgres rm -f /tmp/gate_verify.sql)
Write-Host "VERIFY EXIT=$ex3"; Write-Host $out3
if($ex3 -ne 0){ exit 1 }
Write-Host 'RESET_OK'
