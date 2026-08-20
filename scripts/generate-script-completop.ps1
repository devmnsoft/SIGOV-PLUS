param([switch]$Verify, [switch]$IncludeDevelopmentSeed)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$migrationsDir = Join-Path $root 'database/postgres/migrations'
$bootstrapDir = Join-Path $root 'database/postgres/bootstrap'
$manifestPath = Join-Path $migrationsDir 'manifest.json'
$versionPath = Join-Path $root 'eng/version.json'
$out = Join-Path $root 'database/postgres/script_completo.sql'
$devOut = Join-Path $root 'database/postgres/script_completo_dev.sql'
$developmentSeeds = @(
    (Join-Path $root 'database/postgres/seeds/development/999_super_admin_access_guard.sql'),
    (Join-Path $root 'database/postgres/seeds/rc50_68a_perfis_autorizacao.sql')
)
$compatibilityOutputs = @(
    (Join-Path $root 'database/script_completo.sql'),
    (Join-Path $root 'script_completop.sql')
)
if (-not (Test-Path $manifestPath)) { throw "Manifest de migrations não encontrado: $manifestPath" }
if (-not (Test-Path $versionPath)) { throw "Arquivo de versão não encontrado: $versionPath" }

$versionInfo = Get-Content $versionPath -Raw | ConvertFrom-Json
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$names = @{}
$versions = @{}
$included = @()
$excluded = @()

foreach ($entry in $manifest.migrations) {
    if ($names.ContainsKey($entry.file)) { throw "Migration duplicada no manifest: $($entry.file)" }
    if ($versions.ContainsKey($entry.version)) { throw "Versão duplicada no manifest: $($entry.version)" }
    $names[$entry.file] = $true
    $versions[$entry.version] = $true
    if ($entry.includeInBaseline -eq $true) { $included += $entry } else { $excluded += $entry }
}

function Get-NormalizedText([string]$Path) {
    if (-not (Test-Path $Path)) { throw "Arquivo ausente: $Path" }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return [System.Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF).Replace("`r`n", "`n").Replace("`r", "`n")
}

function Add-CompatibilityFile([System.Text.StringBuilder]$Builder, [object]$Compatibility, [string]$Stage) {
    $fileName = [string]$Compatibility.file
    if ([IO.Path]::IsPathRooted($fileName) -or $fileName -ne [IO.Path]::GetFileName($fileName)) { throw "Path de compatibilidade inválido: $fileName" }
    $path = Join-Path $bootstrapDir $fileName
    $normalized = Get-NormalizedText $path
    $shaBytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($normalized))
    $sha = [System.BitConverter]::ToString($shaBytes).Replace('-', '').ToLowerInvariant()
    if ($sha -ne [string]$Compatibility.checksum) { throw "Checksum divergente na compatibilidade $fileName" }
    [void]$Builder.AppendLine('-- ==================================================')
    [void]$Builder.AppendLine("-- COMPATIBILITY: $FileName")
    [void]$Builder.AppendLine("-- STAGE: $Stage")
    [void]$Builder.AppendLine('-- ==================================================')
    [void]$Builder.AppendLine($normalized.Trim())
    [void]$Builder.AppendLine()
}

function Add-TemporaryHelperReset([System.Text.StringBuilder]$Builder) {
    # Migrations antigas redefinem helpers pg_temp com a mesma assinatura, mas com
    # nomes de parâmetros diferentes. PostgreSQL não aceita CREATE OR REPLACE nesse
    # cenário quando todas são concatenadas na mesma sessão. Cada migration usa o
    # helper somente dentro do seu próprio bloco; removê-lo antes da próxima é seguro.
    [void]$Builder.AppendLine('-- Reset de helpers temporários entre migrations concatenadas.')
    [void]$Builder.AppendLine('drop function if exists pg_temp.create_index_when_columns_exist(text,text,text,text[],text);')
    [void]$Builder.AppendLine('drop function if exists pg_temp.create_index_when_columns_exist(text,text,text,text[],text,text);')
    [void]$Builder.AppendLine('drop function if exists pg_temp.ensure_schema_safe_index(text,text,text,text[],text);')
    [void]$Builder.AppendLine()
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('-- SIGOV PLUS - script_completop.sql')
[void]$sb.AppendLine("-- Produto: $($versionInfo.product)")
[void]$sb.AppendLine("-- Versão: $($versionInfo.version)")
[void]$sb.AppendLine('-- Fonte: database/postgres/migrations/manifest.json')
[void]$sb.AppendLine('-- Gerado de forma determinística')
[void]$sb.AppendLine('-- Arquivo autônomo sem includes, comandos shell ou seeds demonstrativos.')
[void]$sb.AppendLine('-- Inclui compatibilidade idempotente para contratos legados e migrations concatenadas.')
[void]$sb.AppendLine('-- Checksum oficial: SHA-256 do conteúdo UTF-8 normalizado com LF.')
[void]$sb.AppendLine()
[void]$sb.AppendLine("do `$`$")
[void]$sb.AppendLine('begin')
[void]$sb.AppendLine("    if current_setting('server_version_num')::int < 160000 then")
[void]$sb.AppendLine("        raise exception 'PostgreSQL 16 ou superior é obrigatório. Versão atual: %', version();")
[void]$sb.AppendLine('    end if;')
[void]$sb.AppendLine('end')
[void]$sb.AppendLine("`$`$;")
[void]$sb.AppendLine()
[void]$sb.AppendLine('create schema if not exists sigov;')
[void]$sb.AppendLine('create table if not exists sigov.schema_migrations (')
[void]$sb.AppendLine('    id bigint generated always as identity primary key,')
[void]$sb.AppendLine('    version varchar(50) not null unique,')
[void]$sb.AppendLine('    description varchar(250) not null,')
[void]$sb.AppendLine('    checksum varchar(128) not null,')
[void]$sb.AppendLine('    category varchar(40) not null default ''schema'',')
[void]$sb.AppendLine('    source varchar(40) not null default ''manifest'',')
[void]$sb.AppendLine('    success boolean not null default true,')
[void]$sb.AppendLine('    execution_ms bigint null,')
[void]$sb.AppendLine('    applied_at timestamptz not null default now()')
[void]$sb.AppendLine(');')
[void]$sb.AppendLine('alter table sigov.schema_migrations')
[void]$sb.AppendLine('    add column if not exists category varchar(40) not null default ''schema'',')
[void]$sb.AppendLine('    add column if not exists source varchar(40) not null default ''manifest'',')
[void]$sb.AppendLine('    add column if not exists success boolean not null default true,')
[void]$sb.AppendLine('    add column if not exists execution_ms bigint null;')
[void]$sb.AppendLine()

foreach ($entry in $included) {
    $path = Join-Path $migrationsDir $entry.file
    $normalized = Get-NormalizedText $path
    $shaBytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($normalized))
    $sha = [System.BitConverter]::ToString($shaBytes).Replace('-', '').ToLowerInvariant()
    if ($sha -ne $entry.checksum) { throw "Checksum divergente em $($entry.file): manifest=$($entry.checksum) atual=$sha" }

    Add-TemporaryHelperReset -Builder $sb

    foreach ($compatibility in @($entry.compatibilityBefore)) {
        Add-CompatibilityFile -Builder $sb -Compatibility $compatibility -Stage "BEFORE $($entry.file)"
    }

    $description = [string]$entry.description -replace "'", "''"
    $category = [string]$entry.category -replace "'", "''"
    [void]$sb.AppendLine('-- ==================================================')
    [void]$sb.AppendLine("-- MIGRATION: $($entry.file)")
    [void]$sb.AppendLine("-- CATEGORY: $category")
    [void]$sb.AppendLine("-- CHECKSUM_SHA256: $sha")
    [void]$sb.AppendLine('-- ==================================================')
    [void]$sb.AppendLine($normalized.Trim())
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values ('$($entry.version)', '$description', '$sha', '$category', 'script_completop', true, null, now()) on conflict (version) do update set description = excluded.description, checksum = excluded.checksum, category = excluded.category, source = excluded.source, success = true;")
    [void]$sb.AppendLine()
}

Add-TemporaryHelperReset -Builder $sb
foreach ($compatibility in @($manifest.compatibilityAfterAll)) {
    Add-CompatibilityFile -Builder $sb -Compatibility $compatibility -Stage 'AFTER ALL MIGRATIONS'
}

foreach ($entry in $excluded) {
    [void]$sb.AppendLine("-- EXCLUDED_FROM_BASELINE: $($entry.file) [$($entry.category)]")
}

$new = $sb.ToString().Replace("`r`n", "`n")
$devNew = $null
if ($IncludeDevelopmentSeed) {
    $devNew = $new + "`n-- DEVELOPMENT ONLY: seeds fictícias idempotentes`n"
    foreach ($developmentSeed in $developmentSeeds) {
        $devNew += (Get-NormalizedText $developmentSeed).Trim() + "`n"
    }
}
if ($Verify) {
    if (-not (Test-Path $out)) { throw 'database/postgres/script_completo.sql não existe. Execute o gerador sem -Verify.' }
    $old = [System.IO.File]::ReadAllText($out).Replace("`r`n", "`n")
    if ($old -ne $new) {
        for ($i = 0; $i -lt [Math]::Min($old.Length, $new.Length); $i++) {
            if ($old[$i] -ne $new[$i]) { throw "script_completop.sql desatualizado. Primeira diferença no byte $i." }
        }
        throw "database/postgres/script_completo.sql desatualizado. Tamanhos: atual=$($old.Length), gerado=$($new.Length)."
    }
    foreach ($compatibilityOutput in $compatibilityOutputs) {
        if (-not (Test-Path $compatibilityOutput)) { throw "Artefato de compatibilidade ausente: $compatibilityOutput" }
        $compatibilityText = [System.IO.File]::ReadAllText($compatibilityOutput).Replace("`r`n", "`n")
        if ($compatibilityText -ne $new) { throw "Artefato de compatibilidade divergente: $compatibilityOutput" }
    }
    if ($IncludeDevelopmentSeed) {
        if (-not (Test-Path $devOut)) { throw 'database/postgres/script_completo_dev.sql não existe.' }
        $oldDev = [IO.File]::ReadAllText($devOut).Replace("`r`n", "`n")
        if ($oldDev -ne $devNew) { throw 'script_completo_dev.sql desatualizado.' }
    }
    Write-Host 'Scripts completos estão sincronizados com as migrations.'
    exit 0
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($out, $new, $utf8NoBom)
foreach ($compatibilityOutput in $compatibilityOutputs) {
    [System.IO.File]::WriteAllText($compatibilityOutput, $new, $utf8NoBom)
}
if ($IncludeDevelopmentSeed) { [System.IO.File]::WriteAllText($devOut, $devNew, $utf8NoBom) }
if ($IncludeDevelopmentSeed) { Write-Host "Gerado $devOut (estrutura + seed Development)." }
Write-Host "Gerado $out com $($included.Count) migrations incluídas, $($excluded.Count) excluídas e compatibilidade incorporada."
