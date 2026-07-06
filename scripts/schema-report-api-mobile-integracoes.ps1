$ErrorActionPreference = "Stop"
$sql = Join-Path $PSScriptRoot "../database/diagnostics/schema-report-api-mobile-integracoes.sql"
$out = Join-Path $PSScriptRoot "../docs/schema-report-api-mobile-integracoes-local.md"
$cs = $env:DATABASE_URL
if (-not $cs) { $cs = $env:SIGOV_DATABASE_URL }
"# Schema report — API, Mobile, Assinatura, BI e Integrações`n" | Set-Content $out -Encoding UTF8
"Gerado em: $(Get-Date -Format o)`n" | Add-Content $out -Encoding UTF8
if (-not $cs) { "DATABASE_URL/SIGOV_DATABASE_URL não configurada. Consulta preparada em $sql; nenhuma persistência foi presumida." | Add-Content $out -Encoding UTF8; exit 0 }
psql $cs -f $sql | Add-Content $out -Encoding UTF8
