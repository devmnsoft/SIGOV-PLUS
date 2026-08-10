param(
    [string]$ManifestPath = 'database/postgres/migrations/manifest.json',
    [string]$HostName = $env:SIGOV_DB_HOST,
    [int]$Port = 5432,
    [string]$Database = $env:SIGOV_DB_NAME,
    [string]$User = $env:SIGOV_DB_USER,
    [string]$PsqlPath = 'psql',
    [string]$OutputPath,
    [switch]$NoReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestFile = [IO.Path]::GetFullPath((Join-Path $root $ManifestPath))
$migrationDir = Split-Path -Parent $manifestFile
if (-not $OutputPath) { $OutputPath = Join-Path $root 'artifacts/migration-checksum-report.json' }

function Get-NormalizedSha256([string]$Path) {
    $text = [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false)).TrimStart([char]0xFEFF).Replace("`r`n", "`n").Replace("`r", "`n")
    $hash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text))
    return [BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
}

if (-not (Test-Path $manifestFile)) { throw "Manifest não encontrado: $manifestFile" }
$manifest = Get-Content $manifestFile -Raw | ConvertFrom-Json
$history = @{}
$databaseAvailable = $HostName -and $Database -and $User -and (Get-Command $PsqlPath -ErrorAction SilentlyContinue)
if ($databaseAvailable) {
    $rows = & $PsqlPath -X -A -t -F "`t" -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -c "select version, checksum, success::text from sigov.schema_migrations order by version"
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível consultar sigov.schema_migrations (psql=$LASTEXITCODE)." }
    foreach ($row in @($rows)) {
        if (-not $row) { continue }
        $columns = $row -split "`t", 3
        $history[$columns[0]] = [pscustomobject]@{ checksum=$columns[1]; success=($columns[2] -eq 'true') }
    }
} else {
    Write-Warning 'Banco não consultado: informe HostName, Database e User e disponibilize psql. O diagnóstico de manifest/arquivos continuará.'
}

$manifestVersions = @{}
$report = foreach ($entry in $manifest.migrations) {
    $version = [string]$entry.version
    $manifestVersions[$version] = $true
    $path = Join-Path $migrationDir ([string]$entry.file)
    $fileChecksum = if (Test-Path $path) { Get-NormalizedSha256 $path } else { $null }
    $databaseRow = if ($history.ContainsKey($version)) { $history[$version] } else { $null }
    $databaseChecksum = if ($databaseRow) { [string]$databaseRow.checksum } else { $null }
    $known = @($entry.knownChecksums) -contains $databaseChecksum
    $hasPostCondition = -not [string]::IsNullOrWhiteSpace([string]$entry.postConditionSql)
    $postConditionPassed = $null
    $status = 'OK'
    $reason = 'Manifest, arquivo e histórico consultado estão consistentes.'

    if (-not $fileChecksum) { $status='MIGRATION_FILE_MISSING'; $reason='Arquivo declarado no manifest não existe.' }
    elseif ($fileChecksum -ne [string]$entry.checksum) {
        if ($databaseChecksum -eq [string]$entry.checksum) { $status='FILE_CHANGED'; $reason='Banco e manifest concordam, mas o arquivo físico foi alterado.' }
        else { $status='MANIFEST_OUTDATED'; $reason='SHA-256 UTF-8/LF do arquivo difere do manifest e o histórico não comprova qual conteúdo é canônico.' }
    }
    elseif ($databaseRow -and -not $databaseRow.success) { $status='DATABASE_HISTORY_INCONSISTENT'; $reason='Registro histórico está marcado como sem sucesso.' }
    elseif ($databaseChecksum -and $databaseChecksum -ne $fileChecksum) {
        if ($known -and -not $hasPostCondition) { $status='POSTCONDITION_MISSING'; $reason='Checksum histórico conhecido não possui pós-condição.' }
        elseif ($known -and $databaseAvailable) {
            $conditionResult = & $PsqlPath -X -A -t -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -c ([string]$entry.postConditionSql)
            if ($LASTEXITCODE -ne 0) { $postConditionPassed=$false; $status='POSTCONDITION_FAILED'; $reason='A consulta de pós-condição falhou.' }
            else {
                $postConditionPassed = (@($conditionResult | Where-Object { $_ -match '^\s*(t|true)\s*$' }).Count -gt 0)
                if ($postConditionPassed) { $status='KNOWN_CHECKSUM_ACCEPTED'; $reason='Checksum histórico declarado e postConditionSql aprovada; histórico foi preservado.' }
                else { $status='POSTCONDITION_FAILED'; $reason='postConditionSql não comprovou os objetos esperados.' }
            }
        }
        elseif ($known) { $status='KNOWN_CHECKSUM_ACCEPTED'; $reason='Checksum histórico declarado; postConditionSql será obrigatoriamente avaliada pelo MigrationRunner (banco indisponível neste diagnóstico).' }
        else { $status='DATABASE_HISTORICAL_CHECKSUM'; $reason='Checksum do banco diverge e não consta em knownChecksums; requer investigação segura.' }
    }

    [pscustomobject][ordered]@{
        version=$version; file=[string]$entry.file; description=[string]$entry.description
        manifestChecksum=[string]$entry.checksum; fileChecksum=$fileChecksum; databaseChecksum=$databaseChecksum
        status=$status; knownHistorical=$known; hasPostCondition=$hasPostCondition; postConditionPassed=$postConditionPassed; reason=$reason
    }
}

foreach ($version in $history.Keys) {
    if (-not $manifestVersions.ContainsKey($version)) {
        $report += [pscustomobject][ordered]@{ version=$version; file=$null; description=$null; manifestChecksum=$null; fileChecksum=$null; databaseChecksum=$history[$version].checksum; status='DATABASE_HISTORY_INCONSISTENT'; knownHistorical=$false; hasPostCondition=$false; postConditionPassed=$null; reason='Versão existe no banco, mas não no manifest.' }
    }
}

$report | Format-Table version, file, status, manifestChecksum, fileChecksum, databaseChecksum -AutoSize
$summary = $report | Group-Object status | Sort-Object Name | ForEach-Object { [pscustomobject]@{ status=$_.Name; count=$_.Count } }
$summary | Format-Table -AutoSize
if (-not $NoReport) {
    $directory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    [pscustomobject][ordered]@{ generatedAt=(Get-Date).ToUniversalTime().ToString('o'); databaseConsulted=[bool]$databaseAvailable; normalization='UTF-8 sem BOM, CRLF/CR convertidos para LF, SHA-256'; summary=$summary; migrations=@($report) } |
        ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding utf8
    Write-Host "Relatório: $OutputPath"
}

if (@($report | Where-Object { $_.status -notin @('OK','KNOWN_CHECKSUM_ACCEPTED') }).Count -gt 0) { exit 2 }
