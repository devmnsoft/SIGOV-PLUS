$ErrorActionPreference = 'Continue'
$hits = Get-ChildItem 'C:\MNSOFT\SIGOV-PLUS' -Recurse -File -Include *.sql -ErrorAction SilentlyContinue |
    Where-Object { $PSItem.FullName -notmatch '\\(node_modules|bin|obj|Temp|\.git)\\' } |
    Select-String -Pattern 'compras_empresarial_fornecedor\s*\(' -List |
    Select-Object -First 12 Path
foreach ($h in $hits) { Write-Host $h.Path }
Write-Host '--- live schema da tabela:'
$csEnv = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($csEnv -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length + 1) } else { '' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
$sql = "select column_name||' '||data_type||coalesce('('||character_maximum_length||')','')||coalesce(' '||is_nullable,'') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_fornecedor' order by ordinal_position;"
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -c $sql
$sql2 = "select indexdef from pg_indexes where schemaname='sigov' and tablename='compras_empresarial_fornecedor';"
& docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -c $sql2
