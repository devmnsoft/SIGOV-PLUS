param(
    [string]$HostName = $env:SIGOV_DB_HOST,
    [int]$Port = 5432,
    [string]$Database = $env:SIGOV_DB_NAME,
    [string]$User = $env:SIGOV_DB_USER,
    [string]$ManifestPath = "database/postgres/migrations/manifest.json",
    [switch]$ValidateOnly,
    [string]$SslMode = $env:SIGOV_DB_SSLMODE,
    [string]$PsqlPath = 'psql'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:SIGOV_DB_PORT -and $PSBoundParameters.ContainsKey('Port') -eq $false) { $Port = [int]$env:SIGOV_DB_PORT }
$root = Split-Path -Parent $PSScriptRoot
$manifestFile = Join-Path $root $ManifestPath
$logFile = Join-Path $root 'migration.log'
$bootstrapDir = [IO.Path]::GetFullPath((Join-Path $root 'database/postgres/bootstrap'))
function Write-MigrationLog([object]$entry) { ($entry | ConvertTo-Json -Compress -Depth 6) | Add-Content -Path $logFile -Encoding UTF8 }
function Sanitize-Error([string]$message) { if (-not $message) { return '' }; return ($message -replace '(?i)(password|pwd)\s*=\s*[^;\s]+','$1=***' -replace 'postgres(ql)?://[^\s]+','postgres://***') }
function Get-NormalizedSha256([string]$Path) {
    $content = [System.IO.File]::ReadAllText($Path, [System.Text.UTF8Encoding]::new($false)).TrimStart([char]0xFEFF).Replace("`r`n", "`n").Replace("`r", "`n")
    $shaBytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($content))
    return [System.BitConverter]::ToString($shaBytes).Replace('-', '').ToLowerInvariant()
}
function Invoke-SqlFile([string]$Path, [string]$Stage) {
    if (-not (Test-Path $Path)) { throw "Arquivo SQL de $Stage ausente: $Path" }
    $start = Get-Date; $result = 'success'; $errorMessage = ''
    try {
        & $PsqlPath -h $HostName -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -f $Path
        if ($LASTEXITCODE -ne 0) { throw "psql saiu com código $LASTEXITCODE" }
    }
    catch {
        $result = 'failed'
        $errorMessage = Sanitize-Error $_.Exception.Message
        throw
    }
    finally {
        $end = Get-Date
        Write-MigrationLog ([ordered]@{ version=$Stage; file=[IO.Path]::GetFileName($Path); category='compatibility'; checksum=(Get-NormalizedSha256 $Path); startedAt=$start.ToString('o'); finishedAt=$end.ToString('o'); durationMs=[int64]($end-$start).TotalMilliseconds; result=$result; error=$errorMessage })
    }
}
function Resolve-Compatibility([object]$compatibility) {
    $name = [string]$compatibility.file
    if ([IO.Path]::IsPathRooted($name) -or $name -ne [IO.Path]::GetFileName($name)) { throw "Path de compatibilidade inválido: $name" }
    $path = [IO.Path]::GetFullPath((Join-Path $bootstrapDir $name))
    if (-not $path.StartsWith($bootstrapDir + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Compatibilidade fora do bootstrap: $name" }
    if (-not (Test-Path $path)) { throw "Compatibilidade ausente: $name" }
    if ((Get-NormalizedSha256 $path) -ne [string]$compatibility.checksum) { throw "Checksum divergente na compatibilidade: $name" }
    return $path
}
if (-not (Test-Path $manifestFile)) { throw "Manifest não encontrado: $manifestFile" }
if (-not (Get-Command $PsqlPath -ErrorAction SilentlyContinue)) { throw "psql não encontrado em '$PsqlPath'." }
$manifest = Get-Content $manifestFile -Raw | ConvertFrom-Json
$seenVersions = @{}; $seenFiles = @{}
foreach ($entry in $manifest.migrations) {
    if (-not $entry.version -or -not $entry.description -or -not $entry.category -or -not $entry.file -or -not $entry.checksum) { throw 'Entrada inválida no manifest.' }
    if ($seenVersions.ContainsKey($entry.version)) { throw "Versão duplicada: $($entry.version)" }
    if ($seenFiles.ContainsKey($entry.file)) { throw "Arquivo duplicado: $($entry.file)" }
    $seenVersions[$entry.version] = $true; $seenFiles[$entry.file] = $true
    $file = Join-Path $root (Join-Path 'database/postgres/migrations' $entry.file)
    if (-not (Test-Path $file)) { throw "Migration ausente: $($entry.file)" }
    $actual = Get-NormalizedSha256 $file
    if ($actual -ne $entry.checksum) { throw "Checksum divergente: $($entry.file)" }
    $seenCompatibility = @{}
    foreach ($compatibility in @($entry.compatibilityBefore)) {
        if ($seenCompatibility.ContainsKey([string]$compatibility.file)) { throw "Compatibilidade duplicada em $($entry.file): $($compatibility.file)" }
        $seenCompatibility[[string]$compatibility.file] = $true
        $null = Resolve-Compatibility $compatibility
    }
}
foreach ($compatibility in @($manifest.compatibilityAfterAll)) { $null = Resolve-Compatibility $compatibility }

$canExecute = -not $ValidateOnly -and $HostName -and $Database -and $User
if ($canExecute) {
    if ([string]::IsNullOrWhiteSpace($SslMode)) {
        Remove-Item Env:PGSSLMODE -ErrorAction SilentlyContinue
    }
    else {
        $env:PGSSLMODE = $SslMode
    }
}

foreach ($entry in $manifest.migrations) {
    if ($entry.applyAutomatically -ne $true) { Write-Host "Ignorada: $($entry.file)"; continue }
    if (-not $canExecute) { Write-Host "Validada: $($entry.file)"; continue }

    $file = Join-Path $root (Join-Path 'database/postgres/migrations' $entry.file)
    $start = Get-Date; $result = 'success'; $errorMessage = ''
    try {
        $versionLiteral = ([string]$entry.version).Replace("'", "''")
        $descriptionLiteral = ([string]$entry.description).Replace("'", "''")
        $checksumLiteral = ([string]$entry.checksum).Replace("'", "''")
        $categoryLiteral = ([string]$entry.category).Replace("'", "''")
        $registrationSql = "insert into sigov.schema_migrations(version,description,checksum,category,source,success) values ('$versionLiteral','$descriptionLiteral','$checksumLiteral','$categoryLiteral','manifest',true) on conflict(version) do update set description=excluded.description, category=excluded.category, source='manifest', success=true where sigov.schema_migrations.checksum=excluded.checksum;"
        $psqlArgs = @('-X', '-q', '-1', '-h', $HostName, '-p', $Port, '-U', $User, '-d', $Database, '-v', 'ON_ERROR_STOP=1')
        foreach ($compatibility in @($entry.compatibilityBefore)) { $psqlArgs += @('-f', (Resolve-Compatibility $compatibility)) }
        $psqlArgs += @('-f', $file)
        if ($entry.postConditionSql) {
            $condition = [string]$entry.postConditionSql
            $psqlArgs += @('-c', "do `$`$ begin if not ($condition) then raise exception 'postConditionSql reprovada para $versionLiteral'; end if; end `$`$;")
        }
        $psqlArgs += @('-c', $registrationSql)
        & $PsqlPath @psqlArgs
        if ($LASTEXITCODE -ne 0) { throw "migration transacional saiu com código $LASTEXITCODE" }
    } catch { $result = 'failed'; $errorMessage = Sanitize-Error $_.Exception.Message; throw } finally {
        $end = Get-Date
        Write-MigrationLog ([ordered]@{ version=$entry.version; file=$entry.file; category=$entry.category; checksum=$entry.checksum; startedAt=$start.ToString('o'); finishedAt=$end.ToString('o'); durationMs=[int64]($end-$start).TotalMilliseconds; result=$result; error=$errorMessage })
    }
}

if ($canExecute) {
    foreach ($compatibility in @($manifest.compatibilityAfterAll)) {
        Invoke-SqlFile -Path (Resolve-Compatibility $compatibility) -Stage 'POST_MIGRATION_COMPATIBILITY'
    }
}
