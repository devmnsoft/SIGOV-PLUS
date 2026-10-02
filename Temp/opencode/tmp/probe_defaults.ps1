$ErrorActionPreference = 'Continue'
$csEnv = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($csEnv -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length + 1) } else { '' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
$sql = "select column_name||' def='||coalesce(column_default,'<none>') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_fornecedor' and column_name in ('nome','ativo','created_at','updated_at','version','is_deleted','score','status','prazo_medio','correlation_id','created_by','updated_by');"
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -c $sql
Write-Host '--- historico aggregate_type values sample:'
$sql3 = "select distinct aggregate_type from sigov.compras_empresarial_historico;"
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -c $sql3
