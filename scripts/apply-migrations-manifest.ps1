param(
    [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
    [string]$ManifestPath = "database/postgres/migrations/manifest.json"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestFile = Join-Path $root $ManifestPath
if (-not (Test-Path $manifestFile)) { throw "Manifest não encontrado: $manifestFile" }
$manifest = Get-Content $manifestFile -Raw | ConvertFrom-Json
$seenVersions = @{}
$seenFiles = @{}
foreach ($entry in $manifest.migrations) {
    if (-not $entry.version -or -not $entry.description -or -not $entry.category -or -not $entry.file -or -not $entry.checksum) { throw "Entrada inválida no manifest." }
    if ($seenVersions.ContainsKey($entry.version)) { throw "Versão duplicada: $($entry.version)" }
    if ($seenFiles.ContainsKey($entry.file)) { throw "Arquivo duplicado: $($entry.file)" }
    $seenVersions[$entry.version] = $true
    $seenFiles[$entry.file] = $true
    $file = Join-Path $root (Join-Path 'database/postgres/migrations' $entry.file)
    if (-not (Test-Path $file)) { throw "Migration ausente: $($entry.file)" }
    $actual = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.checksum) { throw "Checksum divergente: $($entry.file)" }
    if ($entry.applyAutomatically -ne $true) { Write-Host "Ignorada: $($entry.file)"; continue }
    if (-not $ConnectionString) { Write-Host "Validada: $($entry.file)"; continue }
    $start = Get-Date
    psql $ConnectionString -v ON_ERROR_STOP=1 -f $file
    if ($LASTEXITCODE -ne 0) { throw "Falha ao aplicar $($entry.file)" }
    $elapsed = [int64]((Get-Date) - $start).TotalMilliseconds
    Write-Host "Aplicada: $($entry.file) em ${elapsed}ms"
}
