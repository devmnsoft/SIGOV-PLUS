param(
    [ValidateSet('Auto','Docker','Psql')][string]$Mode = 'Auto',
    [string]$Container = 'sigov-postgres',
    [string]$Database = $env:PGDATABASE,
    [string]$User = $env:PGUSER,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path $repoRoot 'database/diagnostics/schema-report.sql'
if (-not (Test-Path $sqlPath)) { throw "SQL não encontrado: $sqlPath" }
if ([string]::IsNullOrWhiteSpace($Database)) { $Database = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { 'sigov' } }
if ([string]::IsNullOrWhiteSpace($User)) { $User = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'sigov' } }

function Test-DockerContainer([string]$Name) {
    return (Get-Command docker -ErrorAction SilentlyContinue) -and ((docker ps --format '{{.Names}}' 2>$null) -contains $Name)
}
function Test-PsqlEnv { return (Get-Command psql -ErrorAction SilentlyContinue) -and $env:PGHOST -and $env:PGPORT -and $env:PGDATABASE -and $env:PGUSER }

if ($Mode -eq 'Auto') {
    if (Test-DockerContainer $Container) { $Mode = 'Docker' }
    elseif (Test-PsqlEnv) { $Mode = 'Psql' }
    else { throw 'Não foi possível detectar Docker local nem psql com variáveis PG*.' }
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = if ($Mode -eq 'Psql') { 'docs/schema-report-ci.md' } else { 'docs/schema-report-local.md' }
}
$outPath = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

if ($Mode -eq 'Docker') {
    if (-not (Test-DockerContainer $Container)) { throw "Container Docker não encontrado/ativo: $Container" }
    $tmp = '/tmp/schema-report.sql'
    Get-Content $sqlPath -Raw | docker exec -i $Container sh -lc "cat > $tmp"
    $result = docker exec -i $Container psql -U $User -d $Database -f $tmp --pset border=2 --pset pager=off
} else {
    if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw 'psql não encontrado para Mode=Psql.' }
    $result = psql -v ON_ERROR_STOP=1 -f $sqlPath --pset border=2 --pset pager=off 2>&1
    if ($LASTEXITCODE -ne 0) { throw "schema-report via psql falhou: $result" }
}
New-Item -ItemType Directory -Force -Path (Split-Path $outPath -Parent) | Out-Null
@("# Schema report $Mode", '', "Gerado em: $(Get-Date -Format o)", "Database: $Database", "User: $User", '', '```text', $result, '```') | Set-Content -Path $outPath -Encoding UTF8
Write-Host "Relatório salvo em $outPath"
