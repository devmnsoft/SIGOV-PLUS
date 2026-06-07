param(
  [string]$Version = 'v1.0.0',
  [string]$OutputDirectory = "artifacts/release/v1.0.0"
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$package = Join-Path $root $OutputDirectory
if (Test-Path $package) { Remove-Item -Recurse -Force $package }
New-Item -ItemType Directory -Force -Path $package | Out-Null
$copyFiles = @('VERSION','docker-compose.prod.yml','.env.production.example','docs/release-notes-v1.0.0.md')
$scriptFiles = @('validate-release','smoke-test','go-live-check','backup-db','restore-db','rollback-check')
foreach ($file in $copyFiles) {
  $source = Join-Path $root $file
  if (Test-Path $source) { Copy-Item $source -Destination (Join-Path $package (Split-Path $file -Leaf)) }
}
$scriptsOut = Join-Path $package 'scripts'
New-Item -ItemType Directory -Force -Path $scriptsOut | Out-Null
foreach ($name in $scriptFiles) {
  foreach ($ext in @('.ps1','.cmd','.sh')) {
    $source = Join-Path $root "scripts/$name$ext"
    if (Test-Path $source) { Copy-Item $source -Destination $scriptsOut }
  }
}
$migrations = @(Get-ChildItem (Join-Path $root 'database/postgres/migrations') -Filter '*.sql' | Sort-Object Name | ForEach-Object { $_.Name })
$projects = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.csproj' -Recurse | Sort-Object FullName | ForEach-Object { Resolve-Path -Relative $_.FullName })
$files = @(Get-ChildItem $package -Recurse -File | Sort-Object FullName)
$checksums = @{}
foreach ($file in $files) {
  $relative = Resolve-Path -Relative $file.FullName
  if ($relative -match '(?i)(secret|token|certificate|\.pfx|\.pem|\.key|dump|\.bak)$') { throw "Item inseguro no pacote: $relative" }
  $checksums[$relative] = (Get-FileHash -Algorithm SHA256 -Path $file.FullName).Hash
}
$manifest = [ordered]@{
  application = 'sigov'
  version = $Version
  generatedAt = (Get-Date).ToUniversalTime().ToString('o')
  commitSha = $env:SIGOV_COMMIT_SHA
  files = @($files | ForEach-Object { Resolve-Path -Relative $_.FullName })
  migrations = $migrations
  projects = $projects
  dockerImages = @($env:SIGOV_DOCKER_IMAGES -split ',' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  checksums = $checksums
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 (Join-Path $package 'release-manifest.json')
Write-Host "Pacote de release gerado em $package"
