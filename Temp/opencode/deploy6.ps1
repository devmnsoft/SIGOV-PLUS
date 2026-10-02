$ErrorActionPreference = 'Stop'
Set-Location C:\MNSOFT\SIGOV-PLUS

function Stop([int]$code, [string]$msg) { Write-Output "DEPLOY6_FAIL: $msg"; exit $code }

# Container must be stopped for clean rootfs copy
$state = docker inspect sigov-api --format '{{.State.Status}}'
if ($state -ne 'exited') { docker stop sigov-api | Out-Null; $state = docker inspect sigov-api --format '{{.State.Status}}'; if ($state -ne 'exited') { Stop 1 "container state=$state" } }

# 1) publish output -> /app (contents merge)
docker cp "Temp\opencode\api_pub6\." sigov-api:/app/
if ($LASTEXITCODE -ne 0) { Stop 2 "docker cp api_pub6 -> /app (exit=$LASTEXITCODE)" }

# 2) repo migrations dir -> container (manifest + sql), contents merge
docker cp "database\postgres\migrations\." sigov-api:/app/database/postgres/migrations/
if ($LASTEXITCODE -ne 0) { Stop 3 "docker cp migrations (exit=$LASTEXITCODE)" }

# 3) readback verification
docker cp sigov-api:/app/database/postgres/migrations/manifest.json Temp\opencode\rback_manifest.json
if ($LASTEXITCODE -ne 0) { Stop 4 "readback manifest (exit=$LASTEXITCODE)" }
docker cp sigov-api:/app/database/postgres/migrations/20261001090000_integracao_outbox_base.sql Temp\opencode\rback_outbox.sql
if ($LASTEXITCODE -ne 0) { Stop 5 "readback outbox sql (exit=$LASTEXITCODE)" }
docker cp sigov-api:/app/Sigov.Infrastructure.dll Temp\opencode\rback_infra.dll
if ($LASTEXITCODE -ne 0) { Stop 7 "readback Infra dll (exit=$LASTEXITCODE)" }
$hLocal = (Get-FileHash .\database\postgres\migrations\manifest.json -Algorithm SHA256).Hash.ToLower()
$hBack  = (Get-FileHash .\Temp\opencode\rback_manifest.json -Algorithm SHA256).Hash.ToLower()
$dLocal = (Get-FileHash .\Temp\opencode\api_pub6\Sigov.Infrastructure.dll -Algorithm SHA256).Hash.ToLower()
$dBack  = (Get-FileHash .\Temp\opencode\rback_infra.dll -Algorithm SHA256).Hash.ToLower()
Write-Output "manifest_local=$hLocal"
Write-Output "manifest_inapp=$hBack"
Write-Output "dll_local=$dLocal"
Write-Output "dll_inapp=$dBack"
if ($hLocal -ne $hBack) { Stop 8 "manifest mismatch after copy" }
if ($dLocal -ne $dBack) { Stop 9 "Sigov.Infrastructure.dll mismatch after copy" }

# 4) start
docker start sigov-api | Out-Null
Start-Sleep 25
$status = docker inspect sigov-api --format '{{.State.Status}}'
Write-Output "state=$status"
$logs = docker logs sigov-api --tail 30 2>&1 | Out-String
Write-Output "----- logs -----"
Write-Output $logs
try {
  $hr = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:5001/api/health/live" -TimeoutSec 20
  Write-Output "HEALTH_STATUS=$($hr.StatusCode)"
} catch {
  Write-Output "HEALTH_FAIL: $($_.Exception.Message)"
}
Write-Output "DEPLOY6_DONE"
