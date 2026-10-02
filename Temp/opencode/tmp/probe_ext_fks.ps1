# FKs externas (familia fora de compras_empresarial referenciando tabelas da familia) para garantir que o delete do reset nao viola constraint.
$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'
$sql = @"
select 'EXT_FK '||n.nspname||'.'||c.relname||' -> '||fc.relname from pg_constraint k join pg_class c on c.oid=k.conrelid join pg_class fc on fc.oid=k.confrelid join pg_namespace n on n.oid=c.relnamespace where k.contype='f' and fc.relname like 'compras_empresarial%' order by 1;
"@
$rf = Join-Path $PSScriptRoot 'probe_ext_fks.sql'
[System.IO.File]::WriteAllText($rf,$sql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$rf" 'sigov-postgres:/tmp/probe_ext_fks.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/probe_ext_fks.sql
docker exec sigov-postgres rm -f /tmp/probe_ext_fks.sql
