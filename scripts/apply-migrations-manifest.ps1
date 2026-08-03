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
$ErrorActionPreference = 'Stop'
if ($env:SIGOV_DB_PORT -and $PSBoundParameters.ContainsKey('Port') -eq $false) { $Port = [int]$env:SIGOV_DB_PORT }
$root = Split-Path -Parent $PSScriptRoot
$manifestFile = Join-Path $root $ManifestPath
$logFile = Join-Path $root 'migration.log'
$preflightFile = Join-Path $root 'database/postgres/bootstrap/000_preflight_legacy_compatibility.sql'
$osFinancialBridgeFile = Join-Path $root 'database/postgres/bootstrap/010_pre_rc32_optional_financial_bridge.sql'
$purchasesCompatibilityFile = Join-Path $root 'database/postgres/bootstrap/020_pre_rc37b_compras_compatibility.sql'
$postMigrationFile = Join-Path $root 'database/postgres/bootstrap/850_post_migration_compatibility.sql'
$preflightTargets = @(
    '20260608120000_plantao_pro_white_label_b2b_launch.sql',
    '20260730180000_pos_rc_30_financeiro_empresarial_real.sql',
    '20260730090000_pos_rc_32_ordem_servico.sql'
)
function Write-MigrationLog([object]$entry) { ($entry | ConvertTo-Json -Compress -Depth 6) | Add-Content -Path $logFile -Encoding UTF8 }
function Sanitize-Error([string]$message) { if (-not $message) { return '' }; return ($message -replace '(?i)(password|pwd)\s*=\s*[^;\s]+','$1=***' -replace 'postgres(ql)?://[^\s]+','postgres://***') }
function Get-NormalizedSha256([string]$Path) {
    $content = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8).Replace("`r`n", "`n")
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
}

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

    # Essas migrations pressupõem colunas que podem ter sido criadas em formato
    # legado por versões anteriores. O preflight roda exatamente antes delas.
    if ($preflightTargets -contains [string]$entry.file) {
        Invoke-SqlFile -Path $preflightFile -Stage "PRE_FLIGHT_$($entry.version)"
    }

    # A RC32 cria um índice incondicional sobre uma tabela de integração opcional.
    # Garantir o contrato mínimo preserva a migration histórica e seu checksum.
    if ([string]$entry.file -eq '20260730090000_pos_rc_32_ordem_servico.sql') {
        Invoke-SqlFile -Path $osFinancialBridgeFile -Stage 'PRE_RC32_FINANCIAL_BRIDGE'
    }

    # O módulo básico de compras criou fornecedor e pedido com contrato reduzido.
    # A RC37B usa CREATE TABLE IF NOT EXISTS e presume o contrato full stack.
    if ([string]$entry.file -eq '20260802210000_pos_rc_37b_compras_empresariais_fullstack.sql') {
        Invoke-SqlFile -Path $purchasesCompatibilityFile -Stage 'PRE_RC37B_PURCHASES_COMPATIBILITY'
    }

    $file = Join-Path $root (Join-Path 'database/postgres/migrations' $entry.file)
    $start = Get-Date; $result = 'success'; $errorMessage = ''
    try {
        & $PsqlPath -h $HostName -p $Port -U $User -d $Database -v ON_ERROR_STOP=1 -f $file
        if ($LASTEXITCODE -ne 0) { throw "psql saiu com código $LASTEXITCODE" }
    } catch { $result = 'failed'; $errorMessage = Sanitize-Error $_.Exception.Message; throw } finally {
        $end = Get-Date
        Write-MigrationLog ([ordered]@{ version=$entry.version; file=$entry.file; category=$entry.category; checksum=$entry.checksum; startedAt=$start.ToString('o'); finishedAt=$end.ToString('o'); durationMs=[int64]($end-$start).TotalMilliseconds; result=$result; error=$errorMessage })
    }
}

if ($canExecute) {
    Invoke-SqlFile -Path $postMigrationFile -Stage 'POST_MIGRATION_COMPATIBILITY'
}
