[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
Set-Location C:\MNSOFT\SIGOV-PLUS
$envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
$cs = ($envline -split '=', 2)[1]
$pairs = @($cs -split ';')
function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '<na>' } }
$db = GetCv 'Database'; $usr = GetCv 'Username'; $pw = GetCv 'Password'
"R0 CONTRACT db=$db user=$usr (password len=$($pw.Length))"
if ($db -eq '<na>' -or $usr -eq '<na>' -or [string]::IsNullOrEmpty($pw) -or $pw -eq '<na>') { 'FATAL: contrato nao resolvido'; exit 2 }
$env:POSTGRES_DB = $db; $env:POSTGRES_USER = $usr; $env:POSTGRES_PASSWORD = $pw
$td='C:\MNSOFT\SIGOV-PLUS\Temp\opencode'
"BUILD start (imagens de 3 meses estao desatualizadas sem o modulo de compras)"
docker compose build web api 2>&1 | Out-File -Encoding utf8 "$td\gate_build_images.log"
$e=$LASTEXITCODE
"BUILD_EXIT=$e" | Out-File -Append -Encoding utf8 "$td\gate_build_images.log"
"BUILD_EXIT=$e (esperado 0)"
if ($e -ne 0) { exit $e }
docker compose up -d --no-deps --force-recreate web api 2>&1 | Out-String | Out-File -Encoding utf8 "$td\gate_runtime_up3.log"
"UP3_EXIT=$LASTEXITCODE"
$ok = $false
for ($i = 0; $i -lt 36; $i++) {
  Start-Sleep -Seconds 5
  $st = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sigov-api 2>$null)
  $sw = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' sigov-web 2>$null)
  "   iter=$i api=$st web=$sw"
  if ($st -eq 'healthy' -and $sw -eq 'healthy') { $ok = $true; break }
}
"HEALTH_OK=$ok"
