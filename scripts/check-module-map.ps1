$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$doc = Join-Path $root 'docs/modulos-rotas-validacao.md'
if (-not (Test-Path $doc)) { throw "Matriz de módulos não encontrada: $doc" }

$findings = New-Object System.Collections.Generic.List[string]
$content = Get-Content $doc -Raw

$expectedModules = @('Core','Segurança','Auditoria','LGPD','SaaS Admin','Processos','Financeiro','Tributário','Compras','RH','Educação','Saúde','Saneamento','Social','Relatórios/BI','Transparência','Integrações','Suporte','Operação')
foreach ($module in $expectedModules) {
  if ($content -notmatch [regex]::Escape("| $module |")) { $findings.Add("Módulo ausente na matriz: $module") }
}

$controllerTokens = [regex]::Matches($content, '`([^`]+Controller\.cs)`') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($token in $controllerTokens) {
  $matches = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -File -Filter (Split-Path $token -Leaf) | Where-Object { $_.FullName -like "*$token" -or $_.Name -eq (Split-Path $token -Leaf) }
  if (-not $matches) { $findings.Add("Controller referenciado não encontrado: $token") }
}

$viewTokens = [regex]::Matches($content, '`([^`]+\.cshtml)`') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($token in $viewTokens) {
  $path = Join-Path $root $token
  if (-not (Test-Path $path)) { $findings.Add("View referenciada não encontrada: $token") }
}

$jsTokens = [regex]::Matches($content, '`([^`]+\.js)`') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($token in $jsTokens) {
  $path = Join-Path $root $token
  if (-not (Test-Path $path)) { $findings.Add("JS referenciado não encontrado: $token") }
}

$migrationTokens = [regex]::Matches($content, '`([^`]+\.sql)`') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($token in $migrationTokens) {
  $path = Join-Path $root $token
  if (-not (Test-Path $path)) { $findings.Add("Migration referenciada não encontrada: $token") }
}

$sqlFiles = Get-ChildItem -Path (Join-Path $root 'database/postgres/migrations') -File -Filter '*.sql'
$forbiddenSchemas = @('core','sec','audit','lgpd','fin','trib','compras','rh','educacao','saude','saneamento','social','suporte','operacao','integracao','bi','transparencia')
foreach ($file in $sqlFiles) {
  $sql = Get-Content $file.FullName -Raw
  foreach ($schema in $forbiddenSchemas) {
    if ($sql -match "(?i)\bcreate\s+schema\s+(if\s+not\s+exists\s+)?$schema\b") { $findings.Add("Schema legado proibido em $($file.Name): $schema") }
  }
}

if ($findings.Count -gt 0) {
  $findings | ForEach-Object { Write-Error $_ }
  throw "Falhas na matriz de módulos: $($findings.Count)"
}
Write-Host 'Matriz de módulos validada com sucesso.'
