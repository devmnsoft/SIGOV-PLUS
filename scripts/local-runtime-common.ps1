Set-StrictMode -Version Latest
function Import-SigovEnv([string]$Path) {
  if (-not (Test-Path $Path)) { throw "Arquivo de ambiente ausente: $Path. Execute ./scripts/setup-local-sigov.ps1." }
  Get-Content $Path | ForEach-Object {
    $line=$_.Trim(); if (!$line -or $line.StartsWith('#')) { return }
    $parts=$line.Split('=',2); if ($parts.Count -eq 2) { [Environment]::SetEnvironmentVariable($parts[0].Trim(),$parts[1].Trim().Trim('"'),'Process') }
  }
}
function Assert-SigovVariables {
  param([string[]]$Names=@('SIGOV_DB_HOST','SIGOV_DB_PORT','SIGOV_DB_NAME','SIGOV_DB_USER','SIGOV_DB_PASSWORD','SIGOV_WEB_URL','SIGOV_API_URL','SIGOV_MIGRATION_MODE'))
  $missing=@($Names | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
  if ($missing.Count) { throw "Variáveis obrigatórias ausentes: $($missing -join ', '). Execute ./scripts/setup-local-sigov.ps1." }
}
function Invoke-RuntimePsql([string]$Sql) {
  $env:PGPASSWORD=$env:SIGOV_DB_PASSWORD
  $output=& psql -X -v ON_ERROR_STOP=1 -h $env:SIGOV_DB_HOST -p $env:SIGOV_DB_PORT -U $env:SIGOV_DB_USER -d $env:SIGOV_DB_NAME -Atqc $Sql 2>&1
  if ($LASTEXITCODE) {
    $text="$output"
    if ($text -match '28P01|password authentication failed|autenticação do tipo senha falhou') { throw "senha do usuário PostgreSQL $env:SIGOV_DB_USER não confere com SIGOV_DB_PASSWORD. Corrija com: ./scripts/provision-sigov-db-user.ps1 -Database $env:SIGOV_DB_NAME -AdminUser postgres -AppDbUser $env:SIGOV_DB_USER" }
    if ($text -match '3D000|does not exist|não existe') { throw "Banco $env:SIGOV_DB_NAME não existe. Execute ./scripts/install-sigov-database.ps1 -Database $env:SIGOV_DB_NAME" }
    throw "Falha de conexão PostgreSQL para $env:SIGOV_DB_USER@$env:SIGOV_DB_HOST/$env:SIGOV_DB_NAME (segredo omitido): $text"
  }
  return $output
}
function Write-SafeJson($Value,[string]$Path) { New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null; $Value | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $Path }
