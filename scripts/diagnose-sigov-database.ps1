[CmdletBinding()]
param(
    [string]$HostName = 'localhost', [int]$Port = 5432, [string]$Database = 'sigov',
    [string]$MaintenanceDatabase = 'postgres', [string]$User = 'postgres',
    [string]$PsqlPath = 'psql', [string]$SchemaName = 'sigov',
    [string]$OutputDirectory = 'artifacts/database/diagnostics'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-SqlLiteral([string]$Value) { return $Value.Replace("'", "''") }
function Invoke-Psql([string]$Target, [string]$Sql) {
    $out = & $PsqlPath -X -q -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Target -At -c $Sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($out -join "`n") }
    return ($out -join "`n").Trim()
}
function Add-Check([string]$Code, [string]$Severity, [string]$Message, [string]$Remediation = '') {
    $script:checks.Add([ordered]@{ code=$Code; severity=$Severity; message=$Message; remediation=$Remediation })
}
function Test-Scalar([string]$Code, [string]$Sql, [string]$Failure, [string]$Severity='ERROR') {
    try { if ((Invoke-Psql $Database $Sql) -eq 't') { Add-Check $Code 'OK' 'Requisito atendido.' } else { Add-Check $Code $Severity $Failure } }
    catch { Add-Check $Code 'CRITICAL' "Consulta de diagnóstico falhou: $($_.Exception.Message)" }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ($SchemaName -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,62}$') { throw 'SchemaName inválido.' }
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }
New-Item -ItemType Directory -Force $outputPath | Out-Null
$logPath = Join-Path $outputPath 'diagnostic.log'
$checks = [Collections.Generic.List[object]]::new()
$started = [DateTimeOffset]::UtcNow
try {
    if (-not (Get-Command $PsqlPath -ErrorAction SilentlyContinue)) { throw "psql não encontrado em '$PsqlPath'." }
    $version = Invoke-Psql $MaintenanceDatabase "select current_setting('server_version_num')"
    if ([int]$version -lt 160000) { Add-Check 'postgres.version' 'CRITICAL' "PostgreSQL 16+ obrigatório; encontrado $version." } else { Add-Check 'postgres.version' 'OK' "PostgreSQL server_version_num=$version." }
    $db = ConvertTo-SqlLiteral $Database
    if ((Invoke-Psql $MaintenanceDatabase "select exists(select 1 from pg_database where datname='$db')") -ne 't') {
        Add-Check 'database.exists' 'CRITICAL' "Banco '$Database' não existe." 'Execute install-sigov-database.ps1.'
    } else {
        Add-Check 'database.exists' 'OK' "Banco '$Database' acessível."
        $schema = ConvertTo-SqlLiteral $SchemaName
        Test-Scalar 'schema.exists' "select exists(select 1 from information_schema.schemata where schema_name='$schema')" "Schema '$SchemaName' ausente." 'CRITICAL'
        $essential = @('schema_migrations','tenant','entidade','exercicio','usuario','perfil_acesso','permissao','grupo_acesso','usuario_grupo','grupo_perfil','perfil_permissao','plano_saas','modulo_saas','tenant_assinatura','tenant_configuracao','tenant_feature_flag','tenant_modulo_contratado')
        foreach ($table in $essential) { Test-Scalar "table.$table" "select to_regclass('$schema.$table') is not null" "Tabela essencial $SchemaName.$table ausente." }
        foreach ($extension in @('pgcrypto','uuid-ossp')) {
            $pattern = if ($extension -eq 'pgcrypto') { 'pgcrypto|digest\s*\(' } else { 'uuid_generate_|uuid-ossp' }
            $referenced = Get-ChildItem (Join-Path $repoRoot 'database/postgres') -Filter *.sql -Recurse | Select-String -Quiet -Pattern $pattern
            if ($referenced) { Test-Scalar "extension.$extension" "select exists(select 1 from pg_extension where extname='$extension')" "Extensão referenciada '$extension' não instalada." 'WARNING' }
        }
        $requiredColumns = @(
            @('usuario','tenant_id'),@('usuario','senha_hash'),@('usuario','login'),@('usuario','is_deleted'),
            @('permissao','recurso'),@('permissao','acao'),@('tenant_configuracao','tenant_id'),@('tenant_feature_flag','tenant_id')
        )
        foreach ($pair in $requiredColumns) { $t=$pair[0]; $c=$pair[1]; Test-Scalar "column.$t.$c" "select exists(select 1 from information_schema.columns where table_schema='$schema' and table_name='$t' and column_name='$c')" "Coluna obrigatória $SchemaName.$t.$c ausente." }
        Test-Scalar 'index.tenant.slug' "select exists(select 1 from pg_indexes where schemaname='$schema' and tablename='tenant' and indexdef ilike '%unique%' and indexdef ilike '%(slug)%')" 'Índice único de tenant.slug ausente.'
        Test-Scalar 'index.permission.key' "select exists(select 1 from pg_indexes where schemaname='$schema' and tablename='permissao' and indexdef ilike '%unique%' and (indexdef ilike '%(chave)%' or indexdef ilike '%recurso%acao%'))" 'Índice único de permissão ausente.'
        Test-Scalar 'data.tenant.duplicate' "select not exists(select slug from $SchemaName.tenant where not is_deleted group by slug having count(*)>1)" 'Tenants ativos duplicados por slug.' 'CRITICAL'
        Test-Scalar 'data.admin.duplicate' "select not exists(select tenant_id,lower(login) from $SchemaName.usuario where not is_deleted and tipo_usuario='ADMINISTRADOR_GERAL' group by tenant_id,lower(login) having count(*)>1)" 'Administradores duplicados.' 'CRITICAL'
        Test-Scalar 'data.admin.hash' "select not exists(select 1 from $SchemaName.usuario where not is_deleted and tipo_usuario='ADMINISTRADOR_GERAL' and coalesce(senha_hash,'') not like 'SIGOV_PBKDF2_V1$%')" 'Administrador com hash legado/incompatível.' 'ERROR'
        Test-Scalar 'data.admin.links' "select not exists(select 1 from $SchemaName.usuario u where u.tipo_usuario='ADMINISTRADOR_GERAL' and not u.is_deleted and not exists(select 1 from $SchemaName.usuario_grupo ug where ug.usuario_id=u.id and not ug.is_deleted))" 'Administrador sem vínculo de grupo.'
        Test-Scalar 'data.feature-flag.tenant' "select not exists(select 1 from $SchemaName.tenant_feature_flag where tenant_id is null)" 'Feature flag sem tenant.'
        Test-Scalar 'data.bootstrap.parameter' "select not exists(select 1 from $SchemaName.tenant t where t.ativo and not t.is_deleted and not exists(select 1 from $SchemaName.tenant_configuracao c where c.tenant_id=t.id and c.chave='sistema.bootstrap_concluido' and c.ativo and not c.is_deleted))" 'Parâmetro sistema.bootstrap_concluido ausente.'

        $manifest = Get-Content (Join-Path $repoRoot 'database/postgres/migrations/manifest.json') -Raw | ConvertFrom-Json
        $registeredRaw = Invoke-Psql $Database "select version||'|'||coalesce(checksum,'') from $SchemaName.schema_migrations order by version"
        $registered = @{}; foreach ($line in ($registeredRaw -split "`n")) { if ($line) { $p=$line.Split('|',2); $registered[$p[0]]=$p[1] } }
        foreach ($m in $manifest.migrations | Where-Object { $_.applyAutomatically -ne $false }) {
            if (-not $registered.ContainsKey([string]$m.version)) { Add-Check "migration.$($m.version)" 'ERROR' "Migration $($m.version) não registrada." }
            else { $known = if ($m.PSObject.Properties.Name -contains 'knownChecksums') { @($m.knownChecksums) } else { @() }; $allowed=@([string]$m.checksum)+$known; if ($registered[[string]$m.version] -notin $allowed) { Add-Check "migration.$($m.version).checksum" 'ERROR' "Checksum registrado diverge do manifest." } }
        }
    }
} catch { Add-Check 'connection' 'CRITICAL' "Falha de conexão/diagnóstico: $($_.Exception.Message)" }

$counts=@{}; foreach($s in @('OK','WARNING','ERROR','CRITICAL')){$counts[$s]=@($checks|Where-Object severity -eq $s).Count}
$exitCode = if($counts.CRITICAL -gt 0){2}elseif(($counts.WARNING+$counts.ERROR)-gt 0){1}else{0}
$result=[ordered]@{ tool='diagnose-sigov-database'; generatedAt=[DateTimeOffset]::UtcNow.ToString('o'); database=$Database; schema=$SchemaName; status=if($exitCode-eq 0){'HEALTHY'}elseif($exitCode-eq 1){'ATTENTION'}else{'CRITICAL'}; exitCode=$exitCode; counts=$counts; checks=$checks }
$result|ConvertTo-Json -Depth 8|Set-Content (Join-Path $outputPath 'diagnostic-result.json') -Encoding utf8
$md=@("# Diagnóstico SIGOV+","","**Banco:** ``$Database``  ","**Status:** $($result.status)  ","**Gerado:** $($result.generatedAt)","","| Severidade | Código | Mensagem |","|---|---|---|")
foreach($c in $checks){$md += "| $($c.severity) | ``$($c.code)`` | $($c.message.Replace('|','\|')) |"}; $md|Set-Content (Join-Path $outputPath 'diagnostic-report.md') -Encoding utf8
@("[$started] início", ($checks|ForEach-Object{"[$($_.severity)] $($_.code): $($_.message)"}), "exitCode=$exitCode")|Set-Content $logPath -Encoding utf8
Write-Host "Diagnóstico: $($result.status) — OK=$($counts.OK), WARNING=$($counts.WARNING), ERROR=$($counts.ERROR), CRITICAL=$($counts.CRITICAL). Relatório: $outputPath"
exit $exitCode
