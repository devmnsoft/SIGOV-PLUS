param(
  [string]$Version = '',
  [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $root 'eng/version.json'
if ([string]::IsNullOrWhiteSpace($Version) -and (Test-Path $versionFile)) { $Version = (Get-Content $versionFile -Raw | ConvertFrom-Json).version }
if ([string]::IsNullOrWhiteSpace($Version)) { throw 'Versão não informada e eng/version.json ausente.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = "artifacts/release/sigov-plus-$Version" }
$package = Join-Path $root $OutputDirectory
if (Test-Path $package) { Remove-Item -Recurse -Force $package }
New-Item -ItemType Directory -Force -Path $package | Out-Null
function Sanitize-EnvExample([string]$Source,[string]$Destination) {
  $content = Get-Content $Source -Raw
  $content = $content -replace '(?m)^POSTGRES_PASSWORD=.*$', 'POSTGRES_PASSWORD=change_me_local_only'
  $content = $content -replace '(?m)^Sigov__Jwt__Secret=.*$', 'Sigov__Jwt__Secret=change_me_local_only'
  $content = $content -replace '(?m)Password=123456', 'Password=change_me_local_only'
  Set-Content -Path $Destination -Encoding UTF8 -Value $content
}
function Assert-SafePath([string]$Relative) {
  if ($Relative -match '(?i)(^|/)\.env$|\.pfx$|\.pem$|\.key$|dump|(^|/)storage/|\.bak$') { throw "Item inseguro no pacote: $Relative" }
}
function Assert-SafeContent([string]$Path,[string]$Relative) {
  $content = Get-Content $Path -Raw -ErrorAction SilentlyContinue
  $allowedDemoSmoke = ($Relative -eq 'scripts/smoke-test-sigov.ps1' -and $content -match 'sigov_demo_local_only_2026_please_rotate')
  $allowedScannerSource = ($Relative -eq 'scripts/package-release.ps1' -and $content -match 'SIGOV_SMOKE_API_KEY')
  if ($Relative -ne '.env.example' -and -not $allowedDemoSmoke -and -not $allowedScannerSource -and $content -match '(?i)(SIGOV_SMOKE_API_KEY\s*=\s*[^\s#]+|POSTGRES_PASSWORD\s*=\s*[^\s#]+|Password=123456|private[_-]?key|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY)') { throw "Possível segredo em $Relative" }
}
$copyFiles = @('README.md','docker-compose.yml','.env.example','script_completop.sql','global.json','eng/version.json','docs/release-notes-v1.0.0.md','docs/checklist-go-live-pos-rc.md','docs/roteiro-demo-sigov-plus.md','docs/diagnostico-pos-rc-23a-inicial.md','docs/diagnostico-ci-pos-rc-23a.md','docs/diagnostico-build-pos-rc-20.md','docs/diagnostico-migrations-pos-rc-20.md','docs/diagnostico-docker-pos-rc-20.md','docs/testes-pos-rc-20.md','docs/diagnostico-pos-rc-20-final.md','docs/evidencias-pos-rc-20.md','docs/evidencias-pos-rc-20.json','docs/manual-usuario-sigov-pos-rc-15.md','docs/manual-admin-sigov-pos-rc-15.md','docs/jornadas-operacionais-pos-rc-15.md','docs/matriz-funcional-pos-rc-15.md','docs/matriz-crud-enterprise-pos-rc-15.md','docs/security-lgpd-pos-rc-15.md','docs/checklist-homologacao-pos-rc-15.md','docs/importacao-enterprise-pos-rc-15.md','docs/acoes-lote-enterprise-pos-rc-15.md','docs/anexos-enterprise-ged-pos-rc-15.md','docs/agenda-sla-kanban-pos-rc-15.md','docs/smoke-test-release-candidate.md','docs/smoke-test-release-candidate.json')
foreach ($file in $copyFiles) {
  $source = Join-Path $root $file
  if (Test-Path $source) {
    $dest = Join-Path $package $file
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    if ($file -eq '.env.example') { Sanitize-EnvExample $source $dest } else { Copy-Item $source -Destination $dest }
  }
}
foreach ($dir in @('database/postgres/migrations','database/postgres/seeds/homologacao')) { $source = Join-Path $root $dir; if (Test-Path $source) { Copy-Item $source -Destination (Join-Path $package $dir) -Recurse -Force } }
$scriptsOut = Join-Path $package 'scripts'; New-Item -ItemType Directory -Force -Path $scriptsOut | Out-Null
foreach ($name in @('apply-demo-seed','smoke-test-sigov','validate-release','package-release','schema-report','go-live-check','setup-postgres-local','start-local','stop-local','status-local','create-initial-admin','apply-migrations-manifest')) { foreach ($ext in @('.ps1','.cmd','.sh')) { $source = Join-Path $root "scripts/$name$ext"; if (Test-Path $source) { Copy-Item $source -Destination $scriptsOut } } }
$files = @(Get-ChildItem $package -Recurse -File | Sort-Object FullName)
$checksums = [ordered]@{}
foreach ($file in $files) { $relative=$file.FullName.Substring($package.Length+1).Replace('\\','/'); Assert-SafePath $relative; Assert-SafeContent $file.FullName $relative; $checksums[$relative]=(Get-FileHash -Algorithm SHA256 -Path $file.FullName).Hash }
$manifest=[ordered]@{ application='sigov-plus'; version=$Version; generatedAt=(Get-Date).ToUniversalTime().ToString('o'); commitSha=$env:SIGOV_COMMIT_SHA; files=@($files|ForEach-Object{$_.FullName.Substring($package.Length+1).Replace('\\','/')}); checksums=$checksums }
$manifest|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 (Join-Path $package 'release-manifest.json')
Write-Host "Pacote de release gerado em $package"
