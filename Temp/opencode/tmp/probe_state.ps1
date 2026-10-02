$csEnv = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($csEnv -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length + 1) } else { '' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
$sql = @"
select 'RQ '||left(r.id::text,13)||' '||r.numero||' '||r.status||' v'||r.version from sigov.compras_empresarial_requisicao r where r.tenant_id='b0000001-0000-4000-8000-000000000001' order by r.numero;
select 'FORN='||count(*) from sigov.compras_empresarial_fornecedor where tenant_id='b0000001-0000-4000-8000-000000000001';
select 'COTACAO='||count(*) from sigov.compras_empresarial_cotacao where tenant_id='b0000001-0000-4000-8000-000000000001';
select 'PEDIDO='||count(*) from sigov.compras_empresarial_pedido where tenant_id='b0000001-0000-4000-8000-000000000001';
select 'PRODUTOS='||count(*) from sigov.estoque_produto;
"@
$stg = $PSScriptRoot
$rf = Join-Path $stg 'probe_state.sql'
[System.IO.File]::WriteAllText($rf, $sql, (New-Object System.Text.UTF8Encoding($true)))
[void](docker cp $rf 'sigov-postgres:/tmp/probe_state.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/probe_state.sql
docker exec sigov-postgres rm -f /tmp/probe_state.sql
