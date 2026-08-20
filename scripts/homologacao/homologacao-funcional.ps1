[CmdletBinding()]
param(
    [string]$WebUrl = "http://127.0.0.1:5080",
    [string]$ApiUrl = "http://127.0.0.1:5081",
    [switch]$NoStart
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$artifact = Join-Path $root "artifacts/homologacao"
New-Item -ItemType Directory -Force -Path $artifact | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$report = Join-Path $artifact "homologacao-$stamp.jsonl"
$processes = @()

function Write-Result([string]$Phase, [string]$Status, [string]$Detail) {
    [ordered]@{ timestamp=(Get-Date).ToUniversalTime().ToString("o"); phase=$Phase; status=$Status; detail=$Detail } |
        ConvertTo-Json -Compress | Add-Content -Encoding utf8 $report
}

foreach ($tool in @("dotnet", "psql", "pg_dump", "pg_restore")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Result prerequisites BLOCKED "ferramenta ausente: $tool"
        throw "P0 ambiental: ferramenta ausente: $tool"
    }
}

try {
    Push-Location $root
    & psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql
    if ($LASTEXITCODE) { throw "script_completo_dev.sql falhou" }
    Write-Result database PASS "script_completo_dev.sql aplicado"
    & psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/seeds/seed_homologacao_funcional.sql
    if ($LASTEXITCODE) { throw "seed_homologacao_funcional.sql falhou" }
    Write-Result seed PASS "seed funcional aplicado"
    & dotnet restore sigov.runtime.slnf --locked-mode
    if ($LASTEXITCODE) { throw "restore falhou" }
    & dotnet build sigov.runtime.slnf -c Release --no-restore --nologo -warnaserror
    if ($LASTEXITCODE) { throw "build falhou" }
    Write-Result build PASS "runtime Release compilado com warnings como erros"

    if (-not $NoStart) {
        $processes += Start-Process dotnet -PassThru -NoNewWindow -ArgumentList "run --project src/Sigov.Api --no-build -c Release --urls $ApiUrl"
        $processes += Start-Process dotnet -PassThru -NoNewWindow -ArgumentList "run --project src/Sigov.Web --no-build -c Release --urls $WebUrl"
        $processes += Start-Process dotnet -PassThru -NoNewWindow -ArgumentList "run --project src/Sigov.Worker --no-build -c Release"
        Start-Sleep -Seconds 8
    }
    $failed = $false
    $manifest = Get-Content scripts/homologacao/homologacao-funcional-http.json -Raw | ConvertFrom-Json
    foreach ($probe in $manifest.probes) {
        $base = if ($probe.target -eq "WEB") { $WebUrl } else { $ApiUrl }
        try { $status = [int](Invoke-WebRequest -UseBasicParsing -MaximumRedirection 0 -SkipHttpErrorCheck -Uri "$base$($probe.path)" -TimeoutSec 15).StatusCode }
        catch { $status = 0 }
        if (($probe.allowed -contains $status) -and ($status -notin @(404,500,501))) {
            Write-Result $probe.group PASS "$($probe.path) HTTP $status"
        } else {
            Write-Result $probe.group FAIL "$($probe.path) HTTP $status"
            $failed = $true
        }
    }
    Write-Result result $(if ($failed) { "FAIL" } else { "PASS" }) "artifact sanitizado; sem cookies, tokens ou senhas"
    if ($failed) { exit 1 }
} finally {
    $processes | Where-Object { $_ -and -not $_.HasExited } | Stop-Process -Force -ErrorAction SilentlyContinue
    Pop-Location
}
