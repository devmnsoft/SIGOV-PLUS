[CmdletBinding()]
param(
    [string]$MigrationsPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'database/postgres/migrations')
)

$predicatePattern = '(?is)\bwhere\b[^;]*(?:\bis_deleted\b|\bativo\b|\bstatus\b)[^;]*;'
$warnings = 0

Get-ChildItem -LiteralPath $MigrationsPath -Filter '*.sql' | Sort-Object Name | ForEach-Object {
    $sql = Get-Content -LiteralPath $_.FullName -Raw
    $used = [regex]::Matches($sql, $predicatePattern) |
        ForEach-Object { [regex]::Matches($_.Value, '(?i)\b(is_deleted|ativo|status)\b') } |
        ForEach-Object { $_.Value.ToLowerInvariant() } |
        Sort-Object -Unique
    if (-not $used) { return }

    $missing = foreach ($column in $used) {
        $escaped = [regex]::Escape($column)
        $created = $sql -match "(?is)create\s+table\s+if\s+not\s+exists[\s\S]*?\b$escaped\b[\s\S]*?\)"
        $altered = $sql -match "(?is)alter\s+table[\s\S]*?add\s+column\s+if\s+not\s+exists\s+$escaped\b"
        if (-not ($created -and $altered)) { $column }
    }

    if ($missing) {
        $warnings++
        Write-Host "[WARN] migration $($_.Name): índice parcial usa $($missing -join ', '), mas a compatibilidade legado (CREATE + ADD COLUMN IF NOT EXISTS) não está explícita" -ForegroundColor Yellow
    } else {
        Write-Host "[OK] migration $($_.Name): colunas usadas em índices parciais estão garantidas" -ForegroundColor Green
    }
}

Write-Host "Resumo: $warnings migration(s) com possível risco; revise os avisos antes de publicar."
