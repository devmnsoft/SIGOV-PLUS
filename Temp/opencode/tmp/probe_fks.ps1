$ErrorActionPreference = 'Continue'
$csEnv = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($csEnv -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length + 1) } else { '' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
$sql = @"
select c.relname||' -> '||fc.relname from pg_constraint k join pg_class c on c.oid=k.conrelid join pg_class fc on fc.oid=k.confrelid where k.contype='f' and c.relname like 'compras_empresarial_%' and fc.relname like 'compras_empresarial_%' order by 1;
select 'TBL '||c.relname||' tenant='||(case when exists(select 1 from information_schema.columns ic where ic.table_schema='sigov' and ic.table_name=c.relname and ic.column_name='tenant_id') then 'Y' else 'N' end) from pg_class c where c.relname like 'compras_empresarial_%' and c.relkind='r' order by 1;
"@
$stg = $PSScriptRoot
$rf = Join-Path $stg 'probe_fks.sql'
[System.IO.File]::WriteAllText($rf, $sql, (New-Object System.Text.UTF8Encoding($true)))
[void](docker cp $rf 'sigov-postgres:/tmp/probe_fks.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/probe_fks.sql
docker exec sigov-postgres rm -f /tmp/probe_fks.sql
