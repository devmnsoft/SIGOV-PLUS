param([string]$MigrationsPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'database/postgres/migrations'))
$ErrorActionPreference = 'Stop'
$files = if (Test-Path $MigrationsPath -PathType Container) { Get-ChildItem $MigrationsPath -Filter '*.sql' | Sort-Object FullName } else { Get-Item $MigrationsPath }
$dangerous = '(?i)(?:\bnow\s*\(|\bcurrent_date\b|\bdate_trunc\s*\(|\bto_char\s*\(|\btimezone\s*\(|\bextract\s*\(|\bunaccent\s*\(|::\s*(?:date|timestamp)\b)'
$conservative = '(?i)\bcoalesce\s*\('
$failures = 0
$warnings = 0
foreach ($file in $files) {
    $text = Get-Content $file.FullName -Raw
    foreach ($statement in [regex]::Matches($text, '(?is)\bcreate\s+(?:unique\s+)?index\b.*?;')) {
        $match = [regex]::Match($statement.Value, $dangerous)
        $isWarning = $false
        if (-not $match.Success) { $match = [regex]::Match($statement.Value, $conservative); $isWarning = $true }
        if (-not $match.Success) { continue }
        $line = ([regex]::Matches($text.Substring(0, $statement.Index + $match.Index), "`n")).Count + 1
        $excerpt = ($statement.Value -replace '\s+', ' ').Trim()
        if ($isWarning) {
            Write-Warning "$($file.FullName):${line}: aviso conservador para COALESCE em CREATE INDEX: $excerpt`n  recomendação: avalie uma coluna materializada para simplificar a chave do índice."
            $warnings++
        } else {
            Write-Error "$($file.FullName):${line}: expressão potencialmente não IMMUTABLE em CREATE INDEX: $excerpt`n  recomendação: materialize o valor em data_referencia, competencia ou search_text e indexe somente a coluna simples." -ErrorAction Continue
            $failures++
        }
    }
}
if ($failures -gt 0) { throw "Falha: $failures índice(s) com expressão potencialmente não IMMUTABLE." }
Write-Host "OK: $($files.Count) migration(s) sem expressões de índice não IMMUTABLE; $warnings aviso(s) conservador(es)."
