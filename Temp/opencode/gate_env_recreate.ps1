$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
Set-Location C:\MNSOFT\SIGOV-PLUS
$envLines = docker inspect sigov-postgres --format '{{range .Config.Env}}{{println .}}{{end}}' | Where-Object { $_ -match '^POSTGRES_(DB|USER|PASSWORD)=' }
if (($envLines | Measure-Object).Count -ne 3) { throw 'ambient do sigov-postgres incompleto' }
$env = @('POSTGRES_PORT=5432','APP_API_HTTP_PORT=5001','APP_HTTP_PORT=8080') + $envLines
$envFile = Join-Path (Get-Location) '.env'
[System.IO.File]::WriteAllLines($envFile, $env, [System.Text.Encoding]::UTF8)
Write-Host ('WROTE .env with keys: ' + ($env | ForEach-Object { ($_ -split '=',2)[0] }) -join ', ')
& docker compose config -q 2>&1 | Out-String | ForEach-Object { if($_){ Write-Host $_ } }
Write-Host ('COMPOSE_CONFIG_EXIT=' + $LASTEXITCODE)
