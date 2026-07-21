$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$script = Join-Path $root 'script_completop.sql'
if (-not (Test-Path $script)) { throw 'script_completop.sql não encontrado.' }
$content = [System.IO.File]::ReadAllText($script)
if ($content -match '(?m)^\s*\\i\b') { throw 'script_completop.sql contém include psql (\i).' }
if ($content -match '(docker|docker-compose|docker compose|password\s*=|pwd\s*=|jwt|secret|api[_-]?key\s*=)') { throw 'Possível dependência externa ou segredo encontrado no script.' }
$migrations = Get-ChildItem (Join-Path $root 'database/postgres/migrations') -Filter '*.sql' | Sort-Object Name
foreach ($m in $migrations) {
    if ($content -notmatch [regex]::Escape("-- MIGRATION: $($m.Name)")) { throw "Migration ausente no script completo: $($m.Name)" }
    $sha = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::HashData([System.IO.File]::ReadAllBytes($m.FullName))).Replace('-', '').ToLowerInvariant()
    if ($content -notmatch $sha) { throw "Checksum ausente ou divergente para $($m.Name)" }
}
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'generate-script-completop.ps1') -Verify
Write-Host 'script_completop.sql validado.'
