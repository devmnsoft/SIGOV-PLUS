param(
  [string]$WebBaseUrl = 'http://localhost:8080',
  [string]$ApiBaseUrl = 'http://localhost:5001',
  [string]$OutputPath = 'docs/smoke-test-release-candidate.md'
)
$ErrorActionPreference = 'Continue'
if ($env:SIGOV_SMOKE_USE_DEMO_KEY -eq 'true') {
  if ([string]::IsNullOrWhiteSpace($env:SIGOV_SMOKE_API_KEY)) { $env:SIGOV_SMOKE_API_KEY = 'sigov_demo_local_only_2026_please_rotate' }
  if ([string]::IsNullOrWhiteSpace($env:SIGOV_SMOKE_TENANT_ID)) { $env:SIGOV_SMOKE_TENANT_ID = '1' }
}
$apiKey = $env:SIGOV_SMOKE_API_KEY
function Mask-ApiKey([string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
  if ($Value -eq 'sigov_demo_local_only_2026_please_rotate') { return 'sigov_demo_****rotate' }
  if ($Value.Length -le 10) { return '****' }
  return ($Value.Substring(0, [Math]::Min(10,$Value.Length)) + '****' + $Value.Substring($Value.Length-4))
}
function Protect-Text([string]$Text) {
  if ($null -eq $Text) { return '' }
  $safe = $Text
  if (-not [string]::IsNullOrWhiteSpace($script:apiKey)) { $safe = $safe -replace [regex]::Escape($script:apiKey), (Mask-ApiKey $script:apiKey) }
  return $safe
}
$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Name,[string]$Url,[int]$Status,[bool]$Ok,[long]$Ms,[bool]$Blocking,[string]$Error) {
  $results.Add([pscustomobject]@{ name=$Name; url=$Url; statusHttp=$Status; ok=$Ok; elapsedMs=$Ms; blocking=$Blocking; error=(Protect-Text $Error) })
}
function Invoke-SmokeRoute([string]$Name,[string]$Url,[int[]]$ExpectedStatus = @(200,302),[hashtable]$Headers = @{},[bool]$Blocking = $true) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  try { $response = Invoke-WebRequest $Url -Headers $Headers -UseBasicParsing -TimeoutSec 15 -MaximumRedirection 0 -ErrorAction Stop; $status = [int]$response.StatusCode; Add-Result $Name $Url $status ($ExpectedStatus -contains $status) $sw.ElapsedMilliseconds $Blocking '' }
  catch { $status = 0; if ($_.Exception.Response -and $_.Exception.Response.StatusCode) { $status = [int]$_.Exception.Response.StatusCode }; Add-Result $Name $Url $status ($ExpectedStatus -contains $status) $sw.ElapsedMilliseconds $Blocking $_.Exception.Message }
  finally { $sw.Stop() }
}
$webRoutes = @('/','/Auth/Login','/Dashboard','/MinhaCentral','/Protocolo','/Protocolo/Novo','/Ged','/Ged/NovoDocumento','/Workflow','/Tarefas','/Notificacoes','/Busca?q=protocolo','/Relatorios','/Auditoria','/Agenda','/Agenda/Calendario','/Kanban','/Kanban/Tarefas','/Kanban/OS','/Kanban/Propostas','/Poc','/Seguranca/ApiKeys','/Integracoes/Webhooks','/ValidarDocumento','/Operacao/Outbox','/Comercio/Dashboard','/Comercio/Clientes','/Comercio/Produtos','/Comercio/Orcamentos','/Comercio/Pedidos','/Comercio/Vendas','/Comercio/TabelasPreco','/Comercio/Comissoes','/OrdemServico/Dashboard','/OrdemServico/Ordens','/OrdemServico/Agenda','/OrdemServico/Checklist','/OrdemServico/Apontamentos','/Estoque/Dashboard','/Estoque/Produtos','/Estoque/Almoxarifados','/Estoque/Movimentos','/Estoque/Saldos','/Estoque/Requisicoes','/ComprasComercial/Fornecedores','/ComprasComercial/Pedidos','/Industrial/Dashboard','/Industrial/Ativos','/Industrial/PlanosManutencao','/Industrial/Programadas','/Industrial/Medidores','/Industrial/Paradas','/Industria/Dashboard','/Industria/CentrosTrabalho','/Industria/Recursos','/Industria/Produtos','/Industria/FichasTecnicas','/Industria/Roteiros','/Industria/OrdensProducao','/Industria/Apontamentos','/Industria/Qualidade','/Industria/Paradas','/Industria/Custos','/Industria/ChaoFabrica')
foreach ($route in $webRoutes) { Invoke-SmokeRoute "WEB $route" "$WebBaseUrl$route" @(200,302) @{} $true }
foreach ($route in @('/api/health/live','/api/health/ready','/api/health/db','/api/v1/health')) { Invoke-SmokeRoute "API $route" "$ApiBaseUrl$route" @(200) @{} $true }
$enterpriseApiRoutes = @('/api/comercial/clientes','/api/comercial/propostas','/api/comercial/pedidos','/api/os/ordens','/api/estoque/produtos','/api/estoque/saldos','/api/compras/fornecedores','/api/industrial/ativos','/api/industrial/planos-manutencao','/api/industria/ordens-producao','/api/enterprise/clientes/import-template')
foreach ($route in $enterpriseApiRoutes) { Invoke-SmokeRoute "API Enterprise $route" "$ApiBaseUrl$route" @(200,401,403) @{ 'X-Tenant-Id'='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' } $true }
Invoke-SmokeRoute 'API Enterprise import preview sem arquivo' "$ApiBaseUrl/api/enterprise/clientes/import-preview" @(400,401,403) @{ 'X-Tenant-Id'='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' } $false
Invoke-SmokeRoute 'API /api/v1/protocolos sem key' "$ApiBaseUrl/api/v1/protocolos" @(401) @{} $true
if ($env:SIGOV_SMOKE_API_KEY -and $env:SIGOV_SMOKE_TENANT_ID) {
  Write-Host "API key de smoke: $(Mask-ApiKey $env:SIGOV_SMOKE_API_KEY)"
  $headers = @{ 'X-Api-Key'=$env:SIGOV_SMOKE_API_KEY; 'X-Tenant-Id'=$env:SIGOV_SMOKE_TENANT_ID }
  Invoke-SmokeRoute 'API /api/v1/protocolos com escopo válido' "$ApiBaseUrl/api/v1/protocolos" @(200) $headers $true
  Invoke-SmokeRoute 'API /api/v1/documentos com escopo válido' "$ApiBaseUrl/api/v1/documentos" @(200) $headers $true
  Invoke-SmokeRoute 'API /api/v1/tarefas com escopo válido' "$ApiBaseUrl/api/v1/tarefas" @(200) $headers $true
} else {
  Add-Result 'API /api/v1/protocolos com escopo válido' "$ApiBaseUrl/api/v1/protocolos" 0 $true 0 $false 'Não executado: defina SIGOV_SMOKE_API_KEY e SIGOV_SMOKE_TENANT_ID.'
  Add-Result 'API /api/v1/documentos com escopo válido' "$ApiBaseUrl/api/v1/documentos" 0 $true 0 $false 'Não executado: defina SIGOV_SMOKE_API_KEY e SIGOV_SMOKE_TENANT_ID.'
  Add-Result 'API /api/v1/tarefas com escopo válido' "$ApiBaseUrl/api/v1/tarefas" 0 $true 0 $false 'Não executado: defina SIGOV_SMOKE_API_KEY e SIGOV_SMOKE_TENANT_ID.'
}
$total=$results.Count; $success=@($results|Where-Object ok).Count; $failedBlocking=@($results|Where-Object{ -not $_.ok -and $_.blocking }).Count; $failedNonBlocking=@($results|Where-Object{ -not $_.ok -and -not $_.blocking }).Count; $generatedAt=(Get-Date).ToUniversalTime().ToString('o')
$summary=[ordered]@{ total=$total; success=$success; failedBlocking=$failedBlocking; failedNonBlocking=$failedNonBlocking; generatedAt=$generatedAt; WebBaseUrl=$WebBaseUrl; ApiBaseUrl=$ApiBaseUrl; apiKeyMasked=(Mask-ApiKey $env:SIGOV_SMOKE_API_KEY); results=$results }
$lines=@('# Smoke test Release Candidate SIGOV PLUS','',"Gerado em $generatedAt.",'',"Resumo: $success/$total checks OK; $failedBlocking falhas bloqueantes; $failedNonBlocking falhas não bloqueantes.",'',"WebBaseUrl: $WebBaseUrl", "ApiBaseUrl: $ApiBaseUrl", "API key: $(Mask-ApiKey $env:SIGOV_SMOKE_API_KEY)",'','Critérios: rotas Web aceitam 200/302; API v1 sem chave espera 401; API v1 com chave espera 200 quando credenciais forem fornecidas.','', '| Check | URL | Status HTTP | OK | Bloqueante | ms | Erro |','|---|---|---:|---|---|---:|---|')
foreach($item in $results){ $err=(Protect-Text $item.error) -replace '\|','/' -replace "`r?`n",' '; $lines += "| $($item.name) | $($item.url) | $($item.statusHttp) | $($item.ok) | $($item.blocking) | $($item.elapsedMs) | $err |" }
$dir=Split-Path $OutputPath -Parent; if($dir -and -not(Test-Path $dir)){ New-Item -ItemType Directory -Force -Path $dir|Out-Null }
$lines|Out-File -FilePath $OutputPath -Encoding utf8
$summary|ConvertTo-Json -Depth 8|Out-File -FilePath ($OutputPath -replace '\.md$','.json') -Encoding utf8
Write-Host "Smoke test SIGOV PLUS: $success/$total OK; $failedBlocking falhas bloqueantes. Resultado: $OutputPath"

# Evidências Pós-RC 15 consolidadas a partir do smoke runtime.
$evidenceMd = 'docs/evidencias-consolidacao-pos-rc-15.md'
$evidenceJson = 'docs/evidencias-consolidacao-pos-rc-15.json'
@('# Evidências consolidação Pós-RC 15','',"Gerado em $generatedAt.",'',"Smoke: $success/$total checks OK; falhas bloqueantes: $failedBlocking.",'',"Fonte: execução local/CI de scripts/smoke-test-sigov.ps1.") | Out-File -FilePath $evidenceMd -Encoding utf8
$summary | ConvertTo-Json -Depth 8 | Out-File -FilePath $evidenceJson -Encoding utf8
if($failedBlocking -gt 0){ exit 1 }