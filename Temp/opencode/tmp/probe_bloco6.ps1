$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'
$sql = @"
select 'COL '||column_name||' '||data_type from information_schema.columns where table_schema='sigov' and table_name='bloco6_compras_pedido_item' order by ordinal_position;
select 'BLOCO6_N='||count(*) from sigov.bloco6_compras_pedido_item;
select 'BLOCO6_X_DEMO='||count(*) from sigov.bloco6_compras_pedido_item b join sigov.compras_empresarial_pedido p on p.id=b.pedido_id where p.tenant_id='b0000001-0000-4000-8000-000000000001';
"@
$rf = Join-Path $PSScriptRoot 'probe_bloco6.sql'
[System.IO.File]::WriteAllText($rf,$sql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$rf" 'sigov-postgres:/tmp/probe_bloco6.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/probe_bloco6.sql
docker exec sigov-postgres rm -f /tmp/probe_bloco6.sql
