$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot; Set-Location $root
$out = 'artifacts/smoke/rc50_52_prod_smoke_result.txt'; New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null; Set-Content $out ''
function Run-Step([string]$Name, [scriptblock]$Action) { Add-Content $out "RUN $Name"; & $Action *>> $out; if ($LASTEXITCODE) { throw "$Name falhou" }; Add-Content $out "PASS $Name" }
Run-Step 'manifest' { python -m json.tool database/postgres/migrations/manifest.json }
Run-Step 'partial-indexes' { bash scripts/check-migration-partial-index-columns.sh database/postgres/migrations }
Run-Step 'indexes' { bash scripts/check-migration-index-columns.sh database/postgres/migrations }
Run-Step 'immutable-indexes' { bash scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations }
Run-Step 'route-conflicts' { bash scripts/check-api-route-conflicts.sh }
if (Get-Command dotnet -ErrorAction SilentlyContinue) { Run-Step 'restore' { dotnet restore sigov.runtime.slnf --locked-mode }; Run-Step 'build' { dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror } } else { Add-Content $out 'SKIP build (dotnet ausente)' }
function Probe([string]$Name,[string]$Url,[int]$Expected=200) { try { $r=Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 15; $code=[int]$r.StatusCode } catch { $code=[int]$_.Exception.Response.StatusCode }; if($code -ne $Expected){ throw "$Name HTTP $code (esperado $Expected)" }; Add-Content $out "PASS $Name HTTP $code" }
if($env:SIGOV_API_BASE_URL){ Probe 'api-health' "$($env:SIGOV_API_BASE_URL)/api/observabilidade/health"; if($env:SIGOV_SWAGGER_ENABLED -eq 'true'){ Probe 'swagger' "$($env:SIGOV_API_BASE_URL)/swagger/v1/swagger.json" } } else { Add-Content $out 'SKIP API probes (SIGOV_API_BASE_URL ausente)' }
if($env:SIGOV_WEB_BASE_URL){ Probe 'login' "$($env:SIGOV_WEB_BASE_URL)/Auth/Login" } else { Add-Content $out 'SKIP Web probes (SIGOV_WEB_BASE_URL ausente)' }
Add-Content $out 'SMOKE COMPLETO (nenhum segredo registrado)'
