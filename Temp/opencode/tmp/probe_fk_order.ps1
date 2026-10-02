# Lista todas as tabelas compras_empresarial_* no banco vivo e o grafo FK filho->pai para derivar ordem de delete do reset.
$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'

$sql = @"
select 'TBL '||relname from pg_class where relname like 'compras_empresarial%' and relkind='r' order by 1;
select 'FK '||c.relname||' -> '||fc.relname from pg_constraint k join pg_class c on c.oid=k.conrelid join pg_class fc on fc.oid=k.confrelid join pg_namespace n on n.oid=c.relnamespace where n.nspname='sigov' and k.contype='f' and c.relname like 'compras_empresarial%' order by 1;
select 'CNT '||c.relname||'='||n.n_live_tup from pg_class c join pg_stat_user_tables n on n.relid=c.oid join pg_namespace ns on ns.oid=n.schemaid where ns.nspname='sigov' and c.relname like 'compras_empresarial%' order by 1;
"@
$rf = Join-Path $PSScriptRoot 'probe_fk_order.sql'
[System.IO.File]::WriteAllText($rf,$sql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$rf" 'sigov-postgres:/tmp/probe_fk_order.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/probe_fk_order.sql
docker exec sigov-postgres rm -f /tmp/probe_fk_order.sql
