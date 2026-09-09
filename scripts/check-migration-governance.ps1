param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$BaseRef = 'origin/main',
    [string]$OutputDirectory = 'artifacts/p0/migrations',
    [switch]$StaticOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$migrationDir = Join-Path $root 'database/postgres/migrations'
$manifest = Get-Content -Raw -LiteralPath (Join-Path $migrationDir 'manifest.json') | ConvertFrom-Json -AsHashtable
$governance = Get-Content -Raw -LiteralPath (Join-Path $root 'database/postgres/migration-governance.json') | ConvertFrom-Json -AsHashtable
$failures = [Collections.Generic.List[string]]::new()
$inventory = [Collections.Generic.List[object]]::new()
$versions = @{}; $names = @{}; $orphans = @{}; $exceptions = @{}
function Fail([string]$Message) { $failures.Add($Message) }
function Normalized([string]$Path) { return [IO.File]::ReadAllText($Path).TrimStart([char]0xFEFF).Replace("`r`n", "`n").Replace("`r", "`n") }
function Digest([string]$Path) { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((Normalized $Path)))).ToLowerInvariant() }
function Safe-Name([string]$Name) { return -not [string]::IsNullOrWhiteSpace($Name) -and $Name -eq [IO.Path]::GetFileName($Name) -and $Name -notmatch '[\\/]' -and $Name -notin @('.', '..') }
function Optional([Collections.IDictionary]$Value, [string]$Key, [object]$Default = $null) {
    if ($Value.Contains($Key)) { return $Value[$Key] }
    return $Default
}
function Optional-Array([Collections.IDictionary]$Value, [string]$Key) {
    if (-not $Value.Contains($Key) -or $null -eq $Value[$Key]) { return @() }
    return @($Value[$Key])
}

# Compile the exact production lexer. No parallel regex SQL policy is maintained here.
if (-not ('Sigov.Infrastructure.Persistence.Migrations.MigrationSqlPolicy' -as [type])) {
    Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $root 'src/Sigov.Infrastructure/Persistence/Migrations/MigrationSqlPolicy.cs')))
}
function Check-Sql([string]$File, [string]$Version, [bool]$Legacy) {
    try { $null = [Sigov.Infrastructure.Persistence.Migrations.MigrationSqlPolicy]::PrepareForExecution($Version, (Normalized $File), $Legacy) }
    catch { Fail "SQL_POLICY $([IO.Path]::GetFileName($File)): $($_.Exception.InnerException.Message)" }
}
function Check-Compatibility([object[]]$Items, [string]$Owner) {
    $seen = @{}
    foreach ($item in $Items) {
        if ($null -eq $item) { continue }
        $name = [string]$item.file
        if (-not (Safe-Name $name) -or $seen.ContainsKey($name)) { Fail "COMPATIBILITY_PATH_OR_DUPLICATE $Owner/$name"; continue }
        $seen[$name] = $true
        $path = Join-Path $root "database/postgres/bootstrap/$name"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "COMPATIBILITY_MISSING $Owner/$name"; continue }
        if ((Digest $path) -ne $item.checksum) { Fail "COMPATIBILITY_CHECKSUM $Owner/$name" }
        Check-Sql $path "$Owner/$name" $false
    }
}
foreach ($entry in $governance.orphans) {
    if ($orphans.ContainsKey($entry.file)) { Fail "ORPHAN_DUPLICATE $($entry.file)" }
    $orphans[$entry.file] = $entry
    if ($entry.classification -notin @('substituida', 'baseline_only', 'auxiliar', 'obsoleta', 'incompativel', 'necessaria', 'duplicada') -or [string]::IsNullOrWhiteSpace($entry.reason)) { Fail "ORPHAN_UNCLASSIFIED $($entry.file)" }
}
foreach ($entry in $governance.prefixExceptions) {
    if ($exceptions.ContainsKey($entry.file)) { Fail "PREFIX_EXCEPTION_DUPLICATE $($entry.file)" }
    $exceptions[$entry.file] = $entry
}
$order = 0; $previous = $null
foreach ($entry in $manifest.migrations) {
    $order++
    $file = [string]$entry.file; $version = [string]$entry.version
    if ([string]::IsNullOrWhiteSpace($version) -or [string]::IsNullOrWhiteSpace($entry.category) -or [string]::IsNullOrWhiteSpace($entry.description)) { Fail "ENTRY_INVALID $file" }
    if ($versions.ContainsKey($version)) { Fail "VERSION_DUPLICATE $version" }
    if ($names.ContainsKey($file)) { Fail "FILE_DUPLICATE $file" }
    if ($null -ne $previous -and [string]::CompareOrdinal($previous, $version) -ge 0) { Fail "ORDER $previous >= $version" }
    $versions[$version] = $order; $names[$file] = $entry; $previous = $version
    if (-not (Safe-Name $file)) { Fail "FILE_PATH $file"; continue }
    if (-not $file.StartsWith($version + '_', [StringComparison]::Ordinal)) {
        if (-not $exceptions.ContainsKey($file) -or $exceptions[$file].version -ne $version -or [string]::IsNullOrWhiteSpace($exceptions[$file].reason)) { Fail "PREFIX_UNJUSTIFIED $version/$file" }
    }
    $dependencies = Optional-Array $entry 'dependencies'
    $knownChecksums = Optional-Array $entry 'knownChecksums'
    $compatibilityBefore = Optional-Array $entry 'compatibilityBefore'
    $postConditionSql = [string](Optional $entry 'postConditionSql' '')
    $applyAutomatically = Optional $entry 'applyAutomatically' $null
    $includeInBaseline = Optional $entry 'includeInBaseline' $null
    if ($applyAutomatically -isnot [bool] -or $includeInBaseline -isnot [bool]) { Fail "ENTRY_FLAGS $file" }
    foreach ($dependency in $dependencies) {
        if ($null -ne $dependency -and (-not $versions.ContainsKey([string]$dependency) -or $versions[[string]$dependency] -ge $order)) { Fail "DEPENDENCY_NOT_PRIOR $file/$dependency" }
    }
    foreach ($known in $knownChecksums) {
        if ($null -eq $known) { continue }
        if ($known -notmatch '^[0-9a-fA-F]{64}$') { Fail "KNOWN_CHECKSUM_INVALID $file" }
        if ([string]::IsNullOrWhiteSpace($postConditionSql) -or $postConditionSql -match '^\s*select\s+(true|1)\s*;?\s*$') { Fail "KNOWN_CHECKSUM_WITHOUT_SPECIFIC_POSTCONDITION $file" }
    }
    Check-Compatibility $compatibilityBefore $version
    $path = Join-Path $migrationDir $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "FILE_MISSING $file"; continue }
    $checksum = Digest $path
    if ($checksum -ne $entry.checksum) { Fail "CHECKSUM_CURRENT $file" }
    Check-Sql $path $version ((Optional $entry 'legacyTransactionWrapper' $false) -eq $true)
    $inventory.Add([ordered]@{
        version=$version; file=$file; checksum=$checksum; manifestChecksum=$entry.checksum
        inManifest=$true; order=$order; category=$entry.category
        applyAutomatically=$applyAutomatically; includeInBaseline=$includeInBaseline
        dependencies=@($dependencies | Where-Object { $null -ne $_ }); dependencyEvidence='Only declared dependencies; PostgreSQL execution required for semantic dependencies'
        compatibilityBefore=@($compatibilityBefore | Where-Object { $null -ne $_ })
        knownChecksums=@($knownChecksums | Where-Object { $null -ne $_ }); postConditionSql=$postConditionSql
        postConditionProbes=@((Optional-Array $entry 'postConditionProbes') | Where-Object { $null -ne $_ })
        classification=$(if ($applyAutomatically) { 'registered_automatic' } elseif ($includeInBaseline) { 'baseline_only' } else { 'registered_excluded' })
    })
}
Check-Compatibility @($manifest.compatibilityAfterAll) 'AFTER_ALL'
$sqlFiles = @(Get-ChildItem -LiteralPath $migrationDir -Filter *.sql -File | Sort-Object Name)
foreach ($file in $sqlFiles) {
    if ($names.ContainsKey($file.Name)) {
        if ($orphans.ContainsKey($file.Name)) { Fail "ORPHAN_ALREADY_REGISTERED $($file.Name)" }
        continue
    }
    $decision = $orphans[$file.Name]
    if ($null -eq $decision) { Fail "UNCLASSIFIED $($file.Name)"; $decision = @{} }
    $checksum = Digest $file.FullName
    if ($checksum -ne $decision.checksum) { Fail "ORPHAN_CHECKSUM $($file.Name)" }
    Check-Sql $file.FullName $file.Name $false
    $inventory.Add([ordered]@{
        version=$file.Name.Split('_')[0]; file=$file.Name; checksum=$checksum; manifestChecksum=$null
        inManifest=$false; order=$null; category='unregistered'; applyAutomatically=$false; includeInBaseline=$false
        dependencies=@(); dependencyEvidence=$decision.reason; compatibilityBefore=@(); knownChecksums=@(); postConditionSql=$null
        classification=$decision.classification; reason=$decision.reason; disposition=$decision.disposition
    })
}
foreach ($name in $orphans.Keys) { if (-not (Test-Path -LiteralPath (Join-Path $migrationDir $name) -PathType Leaf)) { Fail "ORPHAN_FILE_MISSING $name" } }
foreach ($name in $exceptions.Keys) { if (-not $names.ContainsKey($name)) { Fail "STALE_PREFIX_EXCEPTION $name" } }
$collisions = @($sqlFiles | Group-Object { $_.Name.Split('_')[0] } | Where-Object Count -gt 1)
foreach ($collision in $collisions) {
    $decisions = @($governance.prefixCollisions | Where-Object prefix -EQ $collision.Name)
    if ($decisions.Count -ne 1) { Fail "PREFIX_COLLISION_UNRESOLVED $($collision.Name)"; continue }
    $decision = $decisions[0]
    if ([string]::IsNullOrWhiteSpace($decision.reason) -or @(Compare-Object @($collision.Group.Name | Sort-Object) @($decision.files | Sort-Object)).Count -gt 0) { Fail "PREFIX_COLLISION_UNRESOLVED $($collision.Name)" }
}

# Compare every historical SQL with the requested base, including staged and unstaged changes.
& git -C $root rev-parse --verify "$BaseRef^{commit}" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "HISTORY_BASE_MISSING $BaseRef" }
else {
    $changed = @(& git -C $root diff --name-only --diff-filter=MDRT $BaseRef -- 'database/postgres/migrations/*.sql')
    if ($LASTEXITCODE -ne 0) { Fail 'HISTORY_DIFF_FAILED' }
    foreach ($file in $changed) { Fail "HISTORICAL_SQL_MODIFIED $file" }
}
$pwsh = (Get-Process -Id $PID).Path
$sync = & $pwsh -NoProfile -File (Join-Path $root 'scripts/generate-script-completop.ps1') -Verify -IncludeDevelopmentSeed 2>&1
if ($LASTEXITCODE -ne 0) { Fail 'CONSOLIDATED_DRIFT (execute generate-script-completop.ps1 -Verify -IncludeDevelopmentSeed for details)' }
$outDir = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
$null = New-Item -ItemType Directory -Path $outDir -Force
$report = [ordered]@{
    staticStatus=$(if ($failures.Count) { 'FAIL' } else { 'PASS' }); p0Status='BLOCKED'
    sqlCount=$sqlFiles.Count; manifestCount=$manifest.migrations.Count; orphanCount=$orphans.Count
    prefixCollisions=@($collisions | ForEach-Object { [ordered]@{prefix=$_.Name;files=@($_.Group.Name)} })
    compatibilityAfterAll=@($manifest.compatibilityAfterAll); baseRef=$BaseRef
    failures=@($failures); runtimeRequired=@(
        'PostgreSQL 16 empty and sanitized legacy: apply, reapply, postconditions, schema equivalence',
        'PostgreSQL semantic validation: incompatible FK types, columns before indexes, missing constraint targets',
        'Runner/baseline/apply_all_required_migrations convergence and preservation of historical ledger',
        'Reconcile seven necessary orphans before enabling their consumers; no automatic registration'
    ); inventory=@($inventory)
}
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $outDir 'inventory.json') -Encoding utf8NoBOM
$lines = @('# Inventário de migrations P0', '', "SQLs: $($sqlFiles.Count); manifesto: $($manifest.migrations.Count); órfãs: $($orphans.Count).", '', "Gate estático: $($report.staticStatus). P0: BLOCKED; validação semântica exige PostgreSQL 16.", '', '| Ordem | Versão | Arquivo | Classificação | Automática | Baseline | SHA-256 normalizado |', '|---|---|---|---|---|---|---|')
foreach ($item in $inventory) { $lines += "| $($item.order) | $($item.version) | $($item.file) | $($item.classification) | $($item.applyAutomatically) | $($item.includeInBaseline) | $($item.checksum) |" }
$lines += @('', '## Falhas estáticas', '') + @($failures) + @('', 'Campos completos, dependências declaradas, compatibilidades, knownChecksums e pós-condições estão em inventory.json. Ausência de dependência declarada não comprova independência SQL.')
$lines | Set-Content -LiteralPath (Join-Path $outDir 'inventory.md') -Encoding utf8NoBOM
Write-Output "Migrations: SQL=$($sqlFiles.Count); manifest=$($manifest.migrations.Count); orphans=$($orphans.Count); static=$($report.staticStatus); P0=BLOCKED"
foreach ($failure in $failures) { Write-Output "FAIL: $failure" }
if ($failures.Count) { exit 1 }
if (-not $StaticOnly) { Write-Output 'BLOCKED: execute PostgreSQL 16 empty/legacy runtime gates; -StaticOnly validates only repository governance.'; exit 2 }
