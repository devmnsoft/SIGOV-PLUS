param([string]$Sql, [string]$Extra = '')
$ErrorActionPreference='Continue'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$stg = Join-Path $PSScriptRoot 'tmp'
New-Item -ItemType Directory -Force -Path $stg | Out-Null
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
if(-not $envline){ Write-Host 'FATAL: sigov-api / contrato de conexao ausente'; exit 1 }
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
$db=GetCv 'Database'; $usr=GetCv 'Username'; $pw=GetCv 'Password'
if(-not $db -or -not $usr -or -not $pw){ Write-Host 'FATAL: contrato nao resolvido'; exit 1 }
$name = [IO.Path]::GetFileName($Sql).Replace('.sql','.run.sql')
$rf = Join-Path $stg $name
[System.IO.File]::Copy($Sql,$rf,$true)
[void](docker cp "$rf" "sigov-postgres:/tmp/$name")
$out = & docker exec -e PGPASSWORD="$pw" sigov-postgres psql -U "$usr" -d "$db" -A -F ' | ' -v ON_ERROR_STOP=1 $Extra "-f" "/tmp/$name" 2>&1 | Out-String
$ex=$LASTEXITCODE
[void](docker exec sigov-postgres rm -f "/tmp/$name")
Write-Host "EXIT=$ex"; Write-Host $out
exit $ex
