param(
  [string]$Version = '1.0.0-rc-final',
  [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = "artifacts/release/sigov-plus-$Version" }
$package = Join-Path $root $OutputDirectory
if (Test-Path $package) { Remove-Item -Recurse -Force $package }
New-Item -ItemType Directory -Force -Path $package | Out-Null

$copyFiles = @('README.md','docker-compose.yml','.env.example','docs/release-notes-v1.0.0.md','docs/checklist-go-live-pos-rc.md','docs/roteiro-demo-sigov-plus.md','docs/matriz-modulos-release-candidate.md','docs/smoke-test-release-candidate.md','docs/smoke-test-release-candidate.json','docs/release-candidate-escopo.md','docs/pos-rc-homologacao-real.md','docs/guia-homologacao-comercial.md')
foreach ($file in $copyFiles) {
  $source = Join-Path $root $file
  if (Test-Path $source) {
    $dest = Join-Path $package $file
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    Copy-Item $source -Destination $dest
  }
}
foreach ($dir in @('database/postgres/migrations','database/postgres/seeds')) {
  $source = Join-Path $root $dir
  if (Test-Path $source) { Copy-Item $source -Destination (Join-Path $package $dir) -Recurse -Force }
}
$scriptsOut = Join-Path $package 'scripts'; New-Item -ItemType Directory -Force -Path $scriptsOut | Out-Null
foreach ($name in @('apply-demo-seed','smoke-test-sigov','validate-release','package-release','schema-report','go-live-check')) {
  foreach ($ext in @('.ps1','.cmd','.sh')) { $source = Join-Path $root "scripts/$name$ext"; if (Test-Path $source) { Copy-Item $source -Destination $scriptsOut } }
}
$files = @(Get-ChildItem $package -Recurse -File | Sort-Object FullName)
$checksums = @{}
foreach ($file in $files) {
  $relative = $file.FullName.Substring($package.Length + 1).Replace('\\','/')
  if ($relative -match '(?i)(^|/)(\.env$|.*\.env$)|secret|token|api[_-]?key|certificate|\.pfx$|\.pem$|\.key$|dump|\.bak$|storage/') { throw "Item inseguro no pacote: $relative" }
  $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
  if ($content -match '(?i)(SIGOV_SMOKE_API_KEY\s*=\s*[^\s#]+|password\s*=\s*[^;\s]+)') { throw "Possível segredo em $relative" }
  $checksums[$relative] = (Get-FileHash -Algorithm SHA256 -Path $file.FullName).Hash
}
$manifest = [ordered]@{ application='sigov-plus'; version=$Version; generatedAt=(Get-Date).ToUniversalTime().ToString('o'); commitSha=$env:SIGOV_COMMIT_SHA; files=@($files | ForEach-Object { $_.FullName.Substring($package.Length + 1).Replace('\\','/') }); checksums=$checksums }
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 (Join-Path $package 'release-manifest.json')
Write-Host "Pacote de release gerado em $package"
