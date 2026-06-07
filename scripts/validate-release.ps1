param(
  [switch]$SkipDockerBuild,
  [switch]$SkipGoLiveCheck
)
$ErrorActionPreference = 'Stop'
$steps = New-Object System.Collections.Generic.List[string]
function Invoke-Step([string]$Name, [scriptblock]$Command) {
  Write-Host "== $Name =="
  & $Command
  if ($LASTEXITCODE -ne $null -and $LASTEXITCODE -ne 0) { throw "$Name falhou com exit code $LASTEXITCODE" }
  $steps.Add($Name)
}
Invoke-Step 'dotnet restore' { dotnet restore sigov.sln }
Invoke-Step 'dotnet build Release' { dotnet build sigov.sln --configuration Release --no-restore }
Invoke-Step 'dotnet test Release' { dotnet test sigov.sln --configuration Release --no-build }
Invoke-Step 'docker compose config' { docker compose config }
if (-not $SkipDockerBuild) { Invoke-Step 'docker compose build' { docker compose build } }
if (Test-Path 'docker-compose.prod.yml') {
  Invoke-Step 'docker compose prod config' { docker compose -f docker-compose.prod.yml config }
  if (-not $SkipDockerBuild) { Invoke-Step 'docker compose prod build' { docker compose -f docker-compose.prod.yml build } }
}
if (Test-Path "$PSScriptRoot/check-residues.ps1") { Invoke-Step 'check residues' { & "$PSScriptRoot/check-residues.ps1" } }
if (Test-Path "$PSScriptRoot/security-check.ps1") { Invoke-Step 'security check' { & "$PSScriptRoot/security-check.ps1" } }
if (-not $SkipGoLiveCheck -and (Test-Path "$PSScriptRoot/go-live-check.ps1")) { Invoke-Step 'go-live check' { & "$PSScriptRoot/go-live-check.ps1" -StaticOnly -AllowWarnings } }
Write-Host '== Resumo validate-release =='
$steps | ForEach-Object { Write-Host "PASS $_" }
