[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$td='C:\MNSOFT\SIGOV-PLUS\Temp\opencode'
Set-Location C:\MNSOFT\SIGOV-PLUS
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '<na>' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
"R0 CONTRACT db=$db user=$usr (password len=$($pw.Length))"
if ($db -eq '<na>' -or $usr -eq '<na>' -or [string]::IsNullOrEmpty($pw) -or $pw -eq '<na>') { 'FATAL: contrato nao resolvido'; exit 2 }
$env:POSTGRES_DB = $db; $env:POSTGRES_USER = $usr; $env:POSTGRES_PASSWORD = $pw

# V1: validação sintática do compose alterado (regra 19) com senha redigida
$c = docker compose config 2>&1
$cv = ($c | Out-String) -replace 'Password=[^;\r\n]*', 'Password=<redacted>'
$cv | Out-File -Encoding utf8 "$td\gate_compose_config.log"
"COMPOSE_CONFIG_EXIT=$LASTEXITCODE"
Select-String -Path "$td\gate_compose_config.log" -Pattern 'ConnectionStrings__DefaultConnection' | ForEach-Object { '   ' + $_.Line.Trim() }

# V2: rebuild do worker (mesmo reparo de infra das imagens)
docker compose build worker 2>&1 | Out-File -Append -Encoding utf8 "$td\gate_build_images.log"
"BUILD_WORKER_EXIT=$LASTEXITCODE"

# V3: subir as tres aplicacoes contra o schema novo, sem reexecutar one-shot de migrations
docker compose up -d --no-deps --force-recreate api web worker 2>&1 | Out-String | Out-File -Encoding utf8 "$td\gate_runtime_up4.log"
"UP4_EXIT=$LASTEXITCODE"

$ok = $false
for ($i = 0; $i -lt 36; $i++) {
  Start-Sleep -Seconds 5
  $st = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sigov-api 2>$null)
  $sw = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sigov-web 2>$null)
  $sr = (docker inspect --format '{{.State.Status}}' sigov-worker 2>$null)
  "   iter=$i api=$st web=$sw worker=$sr"
  if ($st -eq 'healthy' -and $sw -eq 'healthy') { $ok = $true; break }
}
"HEALTH_OK=$ok"
