# =============================================================================
# SIGOV PLUS · Ciclo 1 (Educação Avançada) · TIER2 — apps locais reprodutíveis
# =============================================================================
# Sobe Sigov.Web (:5099) e Sigov.Api (:5100) contra o banco p1_dev do container
# oficial sigov-postgres via forwarder socat p1fwd (porta 5433; a 5432 do host
# pode estar sombreada por PostgreSQL nativo — regra 13: não mascarar).
# A API roda com MigrationMode=Disabled (o banco é governado pelo runner oficial).
#
# Uso:   powershell -File scripts\c1_start_local_apps.ps1
# Parar: powershell -File scripts\c1_start_local_apps.ps1 -Stop
# Senha do container (env POSTGRES_PASSWORD) é fixture dev documentada (regra 10).
# =============================================================================
param([switch]$Stop)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root 'artifacts/c1_local_apps'
$pw   = '123456' # senha do container sigov-postgres (env POSTGRES_PASSWORD, fixture dev)

if ($Stop) {
  Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -match 'Sigov\.(Web|Api)\.dll' } | ForEach-Object {
    try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch {}
  }
  'apps locais finalizados'
  return
}
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$conn = "Host=localhost;Port=5433;Database=p1_dev;Username=postgres;Password=$pw;Search Path=sigov"
foreach ($app in @(@('Web','5099'), @('Api','5100'))) {
  $name = $app[0]; $port = $app[1]
  $bin  = Join-Path $root "src/Sigov.$name/bin/Release/net10.0"
  $exe = Join-Path $bin "$name.dll"
  if (-not (Test-Path $exe)) { throw "binário ausente: $exe (execute dotnet build antes)." }
  $pinfo = New-Object System.Diagnostics.ProcessStartInfo
  $pinfo.FileName = 'dotnet'
  $pinfo.Arguments = "`"$exe`""
  $pinfo.WorkingDirectory = $bin
  $pinfo.UseShellExecute = $false
  $pinfo.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Development'
  $pinfo.EnvironmentVariables['ASPNETCORE_URLS'] = "http://localhost:$port"
  $pinfo.EnvironmentVariables['ConnectionStrings__DefaultConnection'] = "$conn;Application Name=sigov.$([string]$name.ToLowerInvariant())"
  if ($name -eq 'Api') { $pinfo.EnvironmentVariables['Sigov__Database__MigrationMode'] = 'Disabled' }
  $logFile = Join-Path $logs "c1_$([string]$name.ToLowerInvariant()).log"
  # StandardOutputFilename/StandardErrorFilename precisam de redirecionamento desligado:
  $pinfo.RedirectStandardOutput = $false
  $pinfo.RedirectStandardError = $false
  $pinfo.StandardOutputFilename = $logFile
  $pinfo.StandardErrorFilename = "$logFile.err"
  $proc = [System.Diagnostics.Process]::Start($pinfo)
  Write-Host ("{0} iniciado pid={1} url=http://localhost:{2} log={3}" -f $name, $proc.Id, $port, $logFile)
}
Write-Host 'aguarde readiness e execute: powershell -File scripts\c1_tier2_e2e.ps1'
