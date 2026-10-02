$ErrorActionPreference = 'Continue'
$csEnv = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($csEnv -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length + 1) } else { '' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
$sql = "select column_name||' '||data_type||coalesce(' null='||is_nullable,'')||coalesce(' def='||column_default,'') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_historico' order by ordinal_position;"
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -c $sql
