[CmdletBinding()]
param(
    [switch]$Confirm,
    [switch]$Smoke
)

$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root 'artifacts/rc50-68-local-promotion'
$Log = Join-Path $Artifacts 'promotion.log'
$Results = [System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Force $Artifacts | Out-Null
Set-Content -Path $Log -Value ''

function Protect-SigovText([string]$Text) {
    if ($null -eq $Text) { return '' }
    $Text = $Text -replace '(?i)(Password|Pwd|PGPASSWORD|SIGOV_DB_PASSWORD|Authorization|Cookie|Set-Cookie)[=:][^;\s]+', '$1=***'
    $Text = $Text -replace '(?i)(Authorization:\s*Bearer)\s+\S+', '$1 ***'
    $Text = $Text -replace '(?i)(Host|Server|Username|User ID|User Id)[=:][^;\s]+', '$1=***'
    return ($Text -replace '(?i)postgres(?:ql)?://[^/@\s]+(?::[^/@\s]+)?@', 'postgres://***:***@')
}
function Write-SigovLog([string]$Text) {
    $safe = Protect-SigovText $Text
    $line = "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK') $safe"
    Add-Content -Path $Log -Value $line
    Write-Host $line
}
function Add-SigovResult([string]$Name, [ValidateSet('PASS','FAIL','BLOCKED')][string]$Status, [string]$Detail) {
    $Results.Add([ordered]@{ name=$Name; status=$Status; detail=(Protect-SigovText $Detail) })
    Write-SigovLog "[$Status] $Name — $Detail"
}
function Test-SigovCommand([string]$Name) { return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue) }
function Invoke-SigovNative([string]$Display, [scriptblock]$Command) {
    Write-SigovLog "Executando: $Display"
    try {
        $output = & $Command 2>&1 | Out-String
        Add-Content $Log (Protect-SigovText $output)
        return ($LASTEXITCODE -eq 0)
    } catch {
        Add-Content $Log (Protect-SigovText $_.Exception.Message)
        return $false
    }
}
function Invoke-SigovPsql([string[]]$Arguments, [string]$InputSql = '') {
    $previous = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $env:SIGOV_DB_PASSWORD
        $base = @('-X','-v','ON_ERROR_STOP=1','-h',$env:SIGOV_DB_HOST,'-p',$env:SIGOV_DB_PORT,'-d',$env:SIGOV_DB_NAME,'-U',$env:SIGOV_DB_USER)
        if ($InputSql) { $output = $InputSql | & psql @base @Arguments 2>&1 }
        else { $output = & psql @base @Arguments 2>&1 }
        Add-Content $Log (Protect-SigovText ($output | Out-String))
        return [ordered]@{ Ok=($LASTEXITCODE -eq 0); Output=($output | Out-String).Trim() }
    } finally { $env:PGPASSWORD = $previous }
}

$sha = (& git -C $Root rev-parse HEAD 2>$null | Out-String).Trim()
$dotnetVersion = 'indisponível'; $psqlVersion = 'indisponível'; $nodeVersion = 'indisponível'
$missingFiles = @('script_completop.sql','database/postgres/migrations/manifest.json','sigov.runtime.slnf') | Where-Object { -not (Test-Path (Join-Path $Root $_)) }
if ($missingFiles.Count) { Add-SigovResult preflight-files FAIL "Ausentes: $($missingFiles -join ', ')" } else { Add-SigovResult preflight-files PASS 'Entradas obrigatórias presentes' }

if (Test-SigovCommand dotnet) {
    $dotnetVersion = (& dotnet --version 2>$null | Out-String).Trim()
    if (Invoke-SigovNative 'dotnet --info' { dotnet --info }) { Add-SigovResult preflight-dotnet PASS ".NET $dotnetVersion" } else { Add-SigovResult preflight-dotnet FAIL 'dotnet --info falhou' }
} else { Add-SigovResult preflight-dotnet BLOCKED 'dotnet não encontrado' }
if (Test-SigovCommand psql) {
    $psqlVersion = (& psql --version 2>$null | Out-String).Trim()
    if (Invoke-SigovNative 'psql --version' { psql --version }) { Add-SigovResult preflight-psql PASS $psqlVersion } else { Add-SigovResult preflight-psql FAIL 'psql --version falhou' }
} else { Add-SigovResult preflight-psql BLOCKED 'psql não encontrado' }
if (Test-SigovCommand node) {
    $nodeVersion = (& node --version 2>$null | Out-String).Trim()
    if (Invoke-SigovNative 'node --version' { node --version }) { Add-SigovResult preflight-node PASS $nodeVersion } else { Add-SigovResult preflight-node FAIL 'node --version falhou' }
} else { Add-SigovResult preflight-node BLOCKED 'node ausente; validação JavaScript indisponível' }

$required = @('SIGOV_DB_HOST','SIGOV_DB_PORT','SIGOV_DB_NAME','SIGOV_DB_USER','SIGOV_DB_PASSWORD')
$missingVariables = $required | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) }
$dbSafe = $false
if ($missingVariables.Count) { Add-SigovResult connection-preflight BLOCKED "Variáveis ausentes: $($missingVariables -join ', ')" }
elseif ($env:SIGOV_DB_HOST -match '(?i)prod(?:uction)?') { Add-SigovResult connection-preflight FAIL 'Host recusado: o destino parece ser de produção' }
elseif ($env:SIGOV_DB_NAME -notmatch '(?i)(rc50|homolog|local|dev|test)') { Add-SigovResult connection-preflight FAIL 'Nome do banco recusado: deve conter rc50, homolog, local, dev ou test' }
elseif ($env:ASPNETCORE_ENVIRONMENT -match '(?i)^Production$' -or $env:SIGOV_ENVIRONMENT -match '(?i)^Production$') { Add-SigovResult connection-preflight FAIL 'Ambiente Production recusado' }
elseif (-not $Confirm) { Add-SigovResult connection-preflight BLOCKED 'Use -Confirm após conferir o destino mascarado; nenhuma alteração foi feita' }
else {
    Write-SigovLog "Destino confirmado: host=*** port=$env:SIGOV_DB_PORT database=$env:SIGOV_DB_NAME user=*** (senha omitida)"
    Add-SigovResult connection-preflight PASS 'Banco local/homologação confirmado; destino sensível mascarado'
    $dbSafe = $true
}

if (Invoke-SigovNative 'git diff --check' { git -C $Root diff --check }) { Add-SigovResult git-diff-check PASS 'Sem erros de whitespace' } else { Add-SigovResult git-diff-check FAIL 'git diff --check falhou' }
try {
    $manifest = Get-Content (Join-Path $Root 'database/postgres/migrations/manifest.json') -Raw | ConvertFrom-Json
    Add-SigovResult manifest-json PASS 'manifest.json é JSON válido'
    $bad = @()
    foreach ($migration in $manifest.migrations) {
        $path = Join-Path $Root "database/postgres/migrations/$($migration.file)"
        if (-not (Test-Path $path) -or (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $migration.checksum) { $bad += $migration.version }
    }
    if ($bad.Count) { Add-SigovResult manifest-checksums FAIL "Checksums divergentes: $($bad -join ', ')" } else { Add-SigovResult manifest-checksums PASS 'Arquivos das migrations conferem com o manifest' }
} catch { Add-SigovResult manifest-json FAIL 'manifest.json inválido'; Add-SigovResult manifest-checksums BLOCKED 'Parse do manifest não concluído'; $manifest=$null }
if (Test-SigovCommand node) {
    if (Invoke-SigovNative 'node --check src/Sigov.Web/wwwroot/js/saas-authorization-admin.js' { node --check (Join-Path $Root 'src/Sigov.Web/wwwroot/js/saas-authorization-admin.js') }) { Add-SigovResult javascript PASS 'JavaScript válido' } else { Add-SigovResult javascript FAIL 'node --check falhou' }
}
if (Test-SigovCommand bash) {
    if (Invoke-SigovNative 'bash -n scripts/rc50-68-local-promotion.sh' { bash -n (Join-Path $Root 'scripts/rc50-68-local-promotion.sh') }) { Add-SigovResult shell-syntax PASS 'Script Linux válido' } else { Add-SigovResult shell-syntax FAIL 'bash -n falhou' }
} else { Add-SigovResult shell-syntax BLOCKED 'bash indisponível no Windows' }
if (Test-SigovCommand actionlint) {
    if (Invoke-SigovNative 'actionlint .github/workflows/*.yml' { Get-ChildItem (Join-Path $Root '.github/workflows/*.yml') | ForEach-Object { actionlint $_.FullName; if ($LASTEXITCODE) { throw 'actionlint falhou' } } }) { Add-SigovResult workflow-yaml PASS 'Workflows validados por actionlint' } else { Add-SigovResult workflow-yaml FAIL 'actionlint falhou' }
} else { Add-SigovResult workflow-yaml BLOCKED 'actionlint ausente; YAML não recebeu PASS inferido' }

if (Test-SigovCommand dotnet) {
    $solution = Join-Path $Root 'sigov.runtime.slnf'
    $buildOk = (Invoke-SigovNative 'dotnet clean sigov.runtime.slnf' { dotnet clean $solution }) -and
        (Invoke-SigovNative 'dotnet restore sigov.runtime.slnf --locked-mode' { dotnet restore $solution --locked-mode }) -and
        (Invoke-SigovNative 'dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror' { dotnet build $solution --configuration Release --no-restore --nologo -warnaserror })
    if ($buildOk) { Add-SigovResult runtime-build PASS 'Clean, restore locked e build Release concluídos' } else { Add-SigovResult runtime-build FAIL 'Build runtime falhou; consulte o log sanitizado' }
} else { Add-SigovResult runtime-build BLOCKED '.NET indisponível' }

if ($dbSafe -and (Test-SigovCommand psql)) {
    $version = Invoke-SigovPsql @('-Atqc','show server_version_num')
    $serverNumber = 0
    if ($version.Ok -and [int]::TryParse($version.Output, [ref]$serverNumber) -and $serverNumber -ge 160000 -and $serverNumber -lt 170000) {
        Add-SigovResult postgres-version PASS "PostgreSQL 16 confirmado (server_version_num=$serverNumber)"
        $apply = Invoke-SigovPsql @('-f',(Join-Path $Root 'script_completop.sql'))
        if ($apply.Ok) { Add-SigovResult baseline-apply PASS 'Baseline aplicado' } else { Add-SigovResult baseline-apply FAIL 'Primeira aplicação falhou' }
        $reapply = Invoke-SigovPsql @('-f',(Join-Path $Root 'script_completop.sql'))
        if ($reapply.Ok) { Add-SigovResult baseline-reapply PASS 'Reexecução idempotente concluída' } else { Add-SigovResult baseline-reapply FAIL 'Reexecução falhou' }
        $sql = @'
do $$ declare n integer; missing text; begin
 if not exists(select 1 from information_schema.schemata where schema_name='sigov') then raise exception 'schema sigov ausente'; end if;
 if to_regclass('sigov.schema_migrations') is null then raise exception 'schema_migrations ausente'; end if;
 select string_agg(x, ', ') into missing from unnest(array['perfil_acesso','grupo_acesso','permissao','usuario_grupo','grupo_perfil','perfil_permissao','autorizacao_decisao_auditoria','autorizacao_admin_auditoria']) x where to_regclass('sigov.'||x) is null;
 if missing is not null then raise exception 'tabelas ausentes: %',missing; end if;
 select count(*) into n from sigov.permissao where chave='saas.superadmin.autorizacao.administrar' and ativo and not is_deleted; if n<>1 then raise exception 'permissão administrar inválida'; end if;
 select count(*) into n from sigov.permissao where chave in ('saas.superadmin.dashboard.visualizar','saas.superadmin.dashboard.exportar') and ativo and not is_deleted; if n<>2 then raise exception 'permissões dashboard inválidas'; end if;
 select count(*) into n from sigov.permissao where not is_deleted group by chave having count(*)>1 limit 1; if n is not null then raise exception 'permissões duplicadas'; end if;
 select count(*) into n from sigov.perfil_acesso where not is_deleted group by codigo_externo having count(*)>1 limit 1; if n is not null then raise exception 'perfis duplicados'; end if;
 select count(*) into n from sigov.grupo_acesso where not is_deleted group by tenant_id,codigo having count(*)>1 limit 1; if n is not null then raise exception 'grupos duplicados'; end if;
end $$;
'@
        $authority = Invoke-SigovPsql @() $sql
        if ($authority.Ok) { Add-SigovResult database-authority PASS 'Schema, ledger, tabelas, permissões e ausência de duplicatas validados' } else { Add-SigovResult database-authority FAIL 'Asserções persistentes falharam' }
        if ($manifest) {
            $values = ($manifest.migrations | Where-Object includeInBaseline | ForEach-Object { "('$($_.version)','$($_.checksum)')" }) -join ','
            $ledgerSql = "do `$`$ declare bad text; begin select string_agg(v.version, ', ') into bad from (values $values) v(version,checksum) left join sigov.schema_migrations sm on sm.version=v.version and sm.success where sm.version is null or sm.checksum<>v.checksum; if bad is not null then raise exception 'ledger/manifest divergente: %',bad; end if; end `$`$;"
            $ledger = Invoke-SigovPsql @() $ledgerSql
            if ($ledger.Ok) { Add-SigovResult ledger-manifest PASS 'Ledger corresponde ao manifest para migrations do baseline' } else { Add-SigovResult ledger-manifest FAIL 'Ledger/checksum diverge do manifest' }
        } else { Add-SigovResult ledger-manifest BLOCKED 'Manifest inválido' }
    } else {
        Add-SigovResult postgres-version FAIL 'É obrigatório servidor PostgreSQL 16.x'
        foreach ($name in @('baseline-apply','baseline-reapply','database-authority','ledger-manifest')) { Add-SigovResult $name BLOCKED 'Versão do servidor recusada' }
    }
} else {
    Add-SigovResult postgres-version BLOCKED 'Conexão segura não confirmada ou psql ausente'
    foreach ($name in @('baseline-apply','baseline-reapply','database-authority','ledger-manifest')) { Add-SigovResult $name BLOCKED 'Banco não disponível com confirmação explícita' }
}

# O smoke é opcional e nunca transforma ausência de credencial em PASS.
if (-not $Smoke) {
    Add-SigovResult smoke-health BLOCKED 'use -Smoke com banco confirmado, dotnet e curl'
    Add-SigovResult smoke-unauthenticated BLOCKED 'use -Smoke com banco confirmado, dotnet e curl'
    Add-SigovResult smoke-authenticated BLOCKED 'Credencial local segura e smoke preparado são obrigatórios'
} elseif (-not $dbSafe -or -not (Test-SigovCommand dotnet) -or -not (Test-SigovCommand curl)) {
    foreach ($name in @('smoke-health','smoke-unauthenticated','smoke-authenticated')) { Add-SigovResult $name BLOCKED 'Pré-requisito de smoke indisponível' }
} else {
    # Reutiliza o launcher oficial e não registra connection string/cookie.
    if ([string]::IsNullOrWhiteSpace($env:SIGOV_API_URL)) { $env:SIGOV_API_URL = 'http://localhost:5001' }
    if ([string]::IsNullOrWhiteSpace($env:SIGOV_WEB_URL)) { $env:SIGOV_WEB_URL = 'http://localhost:5000' }
    $env:ConnectionStrings__DefaultConnection = "Host=$env:SIGOV_DB_HOST;Port=$env:SIGOV_DB_PORT;Database=$env:SIGOV_DB_NAME;Username=$env:SIGOV_DB_USER;Password=$env:SIGOV_DB_PASSWORD"
    & (Join-Path $PSScriptRoot 'start-local.ps1') -SkipBuild 2>&1 | ForEach-Object { Add-Content $Log (Protect-SigovText ($_ | Out-String)) }
    Start-Sleep -Seconds 5
    try { Invoke-WebRequest "${env:SIGOV_API_URL}/api/health" -UseBasicParsing | Out-Null; Add-SigovResult smoke-health PASS 'API health respondeu' } catch { Add-SigovResult smoke-health FAIL 'Health não respondeu' }
    try { $response=Invoke-WebRequest "${env:SIGOV_WEB_URL}/SaasAdmin/Autorizacao" -MaximumRedirection 0 -SkipHttpErrorCheck; if ($response.StatusCode -in 302,401,403) { Add-SigovResult smoke-unauthenticated PASS "Rota protegida recusou acesso anônimo ($($response.StatusCode))" } else { Add-SigovResult smoke-unauthenticated FAIL "Resposta anônima inesperada ($($response.StatusCode))" } } catch { Add-SigovResult smoke-unauthenticated FAIL 'Consulta anônima falhou de forma inesperada' }
    if ([string]::IsNullOrWhiteSpace($env:SIGOV_LOCAL_AUTH_COOKIE)) { Add-SigovResult smoke-authenticated BLOCKED 'SIGOV_LOCAL_AUTH_COOKIE não fornecido; nenhum PASS autenticado foi inferido' }
    else { try { $headers=@{Cookie=$env:SIGOV_LOCAL_AUTH_COOKIE}; $r=Invoke-WebRequest "${env:SIGOV_WEB_URL}/SaasAdmin/Autorizacao" -Headers $headers -SkipHttpErrorCheck; if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { Add-SigovResult smoke-authenticated PASS "Tela autenticada respondeu $($r.StatusCode) (cookie omitido)" } else { Add-SigovResult smoke-authenticated FAIL "Tela autenticada respondeu $($r.StatusCode) (cookie omitido)" } } catch { Add-SigovResult smoke-authenticated FAIL 'Tela autenticada falhou (cookie omitido)' } }
    Get-ChildItem (Join-Path $Root '.local/run/*.pid') -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id ([int](Get-Content $_)) -ErrorAction SilentlyContinue }
}

$overall = if ($Results.status -contains 'FAIL') {'FAIL'} elseif ($Results.status -contains 'BLOCKED') {'BLOCKED'} else {'PASS'}
$promotion = if ($overall -eq 'PASS') { 'PROMOVÍVEL LOCALMENTE' } else { 'BLOCKED' }
$evidence = [ordered]@{ release='RC50.68E-R6'; status=$overall; promotion=$promotion; officialCi='não executado; gate distinto'; validatedSha=$sha; generatedAtLocal=(Get-Date).ToString('o'); versions=[ordered]@{dotnet=$dotnetVersion;psql=$psqlVersion;node=$nodeVersion}; steps=$Results }
$evidence | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Artifacts 'result.json') -Encoding utf8
$lines = @('# Evidência local RC50.68','',"- Resultado da execução: **$overall**","- Decisão local: **$promotion**",'- CI oficial: **não executado; gate distinto**',"- SHA validado: ``$sha``","- Data/hora local: $($evidence.generatedAtLocal)",'','| Etapa | Status | Detalhe |','|---|---|---|')
foreach ($result in $Results) { $lines += "| $($result.name) | **$($result.status)** | $($result.detail -replace '\|','/') |" }
$lines | Set-Content (Join-Path $Artifacts 'summary.md') -Encoding utf8
Write-SigovLog "Evidências: artifacts/rc50-68-local-promotion (resultado=$overall; decisão local=$promotion)"
if ($overall -eq 'FAIL') { exit 1 }; if ($overall -eq 'BLOCKED') { exit 2 }; exit 0
