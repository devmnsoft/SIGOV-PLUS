# Aplica o seed atualizado de cotacao (com fornecedores) no banco vivo duas vezes (idempotencia) e valida contagens.
$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
if(-not $envline){ Write-Host 'FATAL: sigov-api ausente'; exit 1 }
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'
if(-not $db -or -not $usr -or -not $pw){ Write-Host 'FATAL: contrato nao resolvido'; exit 1 }

$seed = 'C:\MNSOFT\SIGOV-PLUS\database\postgres\seeds\compras_cotacao_demo_seed.sql'
[void](docker cp "$seed" 'sigov-postgres:/tmp/compras_cotacao_demo_seed.sql')

$out1 = & docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -v ON_ERROR_STOP=1 -f /tmp/compras_cotacao_demo_seed.sql 2>&1 | Out-String
Write-Host "SEED RUN1 EXIT=$LASTEXITCODE"; Write-Host $out1
if($LASTEXITCODE -ne 0){ exit 1 }

$out2 = & docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -v ON_ERROR_STOP=1 -f /tmp/compras_cotacao_demo_seed.sql 2>&1 | Out-String
Write-Host "SEED RUN2 (idempotencia) EXIT=$LASTEXITCODE"; Write-Host $out2
if($LASTEXITCODE -ne 0){ exit 1 }

$sql = @"
select 'FORN_TOTAL='||count(*) from sigov.compras_empresarial_fornecedor where tenant_id='b0000001-0000-4000-8000-000000000001';
select f.codigo||' '||f.status||' ativo='||f.ativo from sigov.compras_empresarial_fornecedor f where f.tenant_id='b0000001-0000-4000-8000-000000000001' order by f.codigo;
select 'HIST_FORN_CRIADO='||count(*) from sigov.compras_empresarial_historico h where h.tenant_id='b0000001-0000-4000-8000-000000000001' and h.aggregate_type='FORNECEDOR' and h.acao='CRIADO';
select 'PP_9001='||count(*) from sigov.perfil_permissao where perfil_acesso_id=9001;
select 'PP_9002='||count(*) from sigov.perfil_permissao where perfil_acesso_id=9002;
"@
$rf = Join-Path $PSScriptRoot 'apply_seed_verify.sql'
[System.IO.File]::WriteAllText($rf,$sql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$rf" 'sigov-postgres:/tmp/apply_seed_verify.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/apply_seed_verify.sql
$ex=$LASTEXITCODE
docker exec sigov-postgres rm -f /tmp/apply_seed_verify.sql /tmp/compras_cotacao_demo_seed.sql
if($ex -ne 0){ exit $ex }
Write-Host 'APPLY_SEED_OK'
