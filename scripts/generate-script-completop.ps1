param([switch]$Verify)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$migrationsDir = Join-Path $root 'database/postgres/migrations'
$out = Join-Path $root 'script_completop.sql'
if (-not (Test-Path $migrationsDir)) { throw "Diretório de migrations não encontrado: $migrationsDir" }
$migrations = Get-ChildItem $migrationsDir -Filter '*.sql' | Sort-Object Name
$names = @{}
$versions = @{}
foreach ($m in $migrations) {
    if ($names.ContainsKey($m.Name)) { throw "Migration duplicada: $($m.Name)" }
    $names[$m.Name] = $true
    $version = ($m.BaseName -split '_')[0]
    if ($versions.ContainsKey($version)) { throw "Versão duplicada: $version" }
    $versions[$version] = $true
}
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('-- SIGOV PLUS - script_completop.sql')
[void]$sb.AppendLine('-- Versão: Pós-RC 19')
[void]$sb.AppendLine('-- Data: 2026-07-21')
[void]$sb.AppendLine('-- Arquivo autônomo gerado de database/postgres/migrations, sem includes ou comandos shell.')
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
[void]$sb.AppendLine('    version text primary key,')
[void]$sb.AppendLine('    description text not null,')
[void]$sb.AppendLine('    checksum text not null,')
[void]$sb.AppendLine('    applied_at timestamptz not null default now()')
[void]$sb.AppendLine(');')
[void]$sb.AppendLine()
foreach ($m in $migrations) {
    $bytes = [System.IO.File]::ReadAllBytes($m.FullName)
    $sha = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::HashData($bytes)).Replace('-', '').ToLowerInvariant()
    $version = ($m.BaseName -split '_')[0]
    $description = $m.BaseName.Substring([Math]::Min($version.Length + 1, $m.BaseName.Length)).Replace("'", "''")
    $content = [System.Text.Encoding]::UTF8.GetString($bytes).Trim()
    [void]$sb.AppendLine('-- ==================================================')
    [void]$sb.AppendLine("-- MIGRATION: $($m.Name)")
    [void]$sb.AppendLine("-- CHECKSUM_SHA256: $sha")
    [void]$sb.AppendLine('-- ==================================================')
    [void]$sb.AppendLine($content)
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("insert into sigov.schema_migrations(version, description, checksum, applied_at) values ('$version', '$description', '$sha', now()) on conflict (version) do update set description = excluded.description, checksum = excluded.checksum;")
    [void]$sb.AppendLine()
}
[void]$sb.AppendLine("do `$`$")
[void]$sb.AppendLine('begin')
[void]$sb.AppendLine("    if not exists (select 1 from information_schema.schemata where schema_name = 'sigov') then")
[void]$sb.AppendLine("        raise exception 'Schema sigov não foi criado.';")
[void]$sb.AppendLine('    end if;')
[void]$sb.AppendLine('end')
[void]$sb.AppendLine("`$`$;")
$new = $sb.ToString().Replace("`r`n", "`n")
if ($Verify) {
    if (-not (Test-Path $out)) { throw 'script_completop.sql não existe. Execute o gerador sem -Verify.' }
    $old = [System.IO.File]::ReadAllText($out).Replace("`r`n", "`n")
    if ($old -ne $new) { throw 'script_completop.sql está desatualizado. Execute scripts/generate-script-completop.ps1.' }
    Write-Host 'script_completop.sql está sincronizado.'
    exit 0
}
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($out, $new, $utf8NoBom)
Write-Host "Gerado $out com $($migrations.Count) migrations."
