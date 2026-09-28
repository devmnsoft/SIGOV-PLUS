[Console]::OutputEncoding=[Text.Encoding]::UTF8
Set-Location C:\MNSOFT\SIGOV-PLUS
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '<na>' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
"R0 CONTRACT db=$db user=$usr (password len=$($pw.Length))"
if ($db -eq '<na>' -or $usr -eq '<na>' -or [string]::IsNullOrEmpty($pw) -or $pw -eq '<na>') { 'FATAL: contrato nao resolvido'; exit 2 }
$env:POSTGRES_DB = $db; $env:POSTGRES_USER = $usr; $env:POSTGRES_PASSWORD = $pw
docker compose build worker 2>&1 | Out-File -Append -Encoding utf8 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_build_images.log'
"BUILD_WORKER2_EXIT=$LASTEXITCODE"
docker compose up -d --no-deps --force-recreate worker 2>&1 | Out-String | Out-File -Encoding utf8 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_runtime_up5.log'
"UP5_EXIT=$LASTEXITCODE"
Start-Sleep -Seconds 15
docker inspect --format '{{.State.Status}} restarts={{.RestartCount}}' sigov-worker
