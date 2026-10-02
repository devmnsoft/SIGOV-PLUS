$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'
$sql = @"
select 'HAS_TENANT '||t.table_name||'='||case when ic.column_name is null then 'N' else 'Y' end
from information_schema.tables t
left join information_schema.columns ic on ic.table_schema=t.table_schema and ic.table_name=t.table_name and ic.column_name='tenant_id'
where t.table_schema='sigov' and t.table_name like 'compras_empresarial%' order by 1;
"@
$rf = Join-Path $PSScriptRoot 'probe_tenant_cols.sql'
[System.IO.File]::WriteAllText($rf,$sql,(New-Object System.Text.UTF8Encoding($true)))
[void](docker cp "$rf" 'sigov-postgres:/tmp/probe_tenant_cols.sql')
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -f /tmp/probe_tenant_cols.sql
docker exec sigov-postgres rm -f /tmp/probe_tenant_cols.sql
