$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root 'script_completop.sql'
$manifestPath = Join-Path $root 'database/postgres/migrations/manifest.json'
if (-not (Test-Path $script)) { throw 'script_completop.sql não encontrado.' }
if (-not (Test-Path $manifestPath)) { throw 'manifest.json não encontrado.' }
$content = [System.IO.File]::ReadAllText($script)
if ($content -match '(?m)^\s*\\i\b') { throw 'script_completop.sql contém include psql (\i).' }
$forbidden = @('SigovDevLocal','DEV_ONLY','admin@sigov.local','senha documentada','BEGIN RSA','BEGIN PRIVATE KEY','JWT_SECRET','API_KEY=')
foreach ($item in $forbidden) { if ($content -match [regex]::Escape($item)) { throw "Conteúdo proibido no script_completop.sql: $item" } }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
foreach ($m in $manifest.migrations) {
    if ($m.includeInBaseline -eq $true) {
        if ($content -notmatch [regex]::Escape("-- MIGRATION: $($m.file)")) { throw "Migration ausente no script completo: $($m.file)" }
        if ($content -notmatch $m.checksum) { throw "Checksum ausente ou divergente para $($m.file)" }
    } elseif ($content -match [regex]::Escape("-- MIGRATION: $($m.file)")) {
        throw "Migration excluída do baseline foi incorporada: $($m.file)"
    }
}
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'generate-script-completop.ps1') -Verify
Write-Host 'script_completop.sql validado.'
