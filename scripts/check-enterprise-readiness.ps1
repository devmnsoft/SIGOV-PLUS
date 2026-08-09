param([string]$BaseUrl = '', [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts/enterprise-readiness'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check($name, $status, $detail) { $checks.Add([ordered]@{ name=$name; status=$status; detail=$detail }) }
function Test-Text($name, $path, $patterns) {
  $text = Get-Content (Join-Path $root $path) -Raw
  $missing = @($patterns | Where-Object { $text -notmatch [regex]::Escape($_) })
  Add-Check $name $(if($missing.Count){'fail'}else{'pass'}) $(if($missing.Count){"Ausentes: $($missing -join ', ')"}else{$path})
}
try {
  if (-not $SkipBuild -and (Get-Command dotnet -ErrorAction SilentlyContinue)) { & dotnet build (Join-Path $root 'sigov.sln') --nologo; Add-Check 'build' 'pass' 'dotnet build sigov.sln' }
  else { Add-Check 'build' 'warning' 'SDK indisponível ou build ignorado' }
  Test-Text 'rotas-auth' 'src/Sigov.Web/Controllers/AuthController.cs' @('Auth/Login','Auth/Logout','Auth/TrocarSenhaInicial','Auth/AlterarSenha','Auth/EsqueciSenha','Auth/RedefinirSenha','Auth/SolicitacaoEnviada')
  Test-Text 'auth-admin' 'src/Sigov.Web/Controllers/SegurancaController.cs' @('Seguranca/Usuarios/Novo','ResetSenha','PermissoesPerfil')
  Test-Text 'tabelas-transversais' 'database/postgres/migrations/20260807120000_rc46_operacao_integrada.sql' @('favorito','item_recente','filtro_salvo','acompanhamento')
  foreach($area in @('Protocolo','Ged','Tarefa','Notificacao','Relatorios','Health')) {
    $found = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter "*$area*Controller.cs" -ErrorAction SilentlyContinue
    Add-Check $area.ToLowerInvariant() $(if($found){'pass'}else{'fail'}) $(if($found){$found[0].FullName.Substring($root.Length+1)}else{'controller não encontrado'})
  }
  if($BaseUrl) { foreach($route in @('/health','/Auth/Login')) { try { $r=Invoke-WebRequest "$BaseUrl$route" -UseBasicParsing; Add-Check "http:$route" 'pass' "HTTP $($r.StatusCode)" } catch { Add-Check "http:$route" 'fail' $_.Exception.Message } } }
  $suspect = Get-ChildItem $out -File -ErrorAction SilentlyContinue | Select-String -Pattern '(?i)(password|senha|token|hash)\s*[:=]\s*\S+' -ErrorAction SilentlyContinue
  Add-Check 'artifacts-sem-secrets' $(if($suspect){'fail'}else{'pass'}) $(if($suspect){'conteúdo sensível potencial encontrado'}else{'nenhum padrão sensível'})
} catch { Add-Check 'execucao' 'fail' $_.Exception.Message }
$result=[ordered]@{ generatedAt=(Get-Date).ToUniversalTime().ToString('o'); success=(@($checks|Where-Object status -eq 'fail').Count -eq 0); checks=$checks }
$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'readiness-result.json') -Encoding utf8
@('# SIGOV+ enterprise readiness','',"Gerado em $($result.generatedAt)",'') + @($checks|ForEach-Object{"- **$($_.status.ToUpperInvariant())** — $($_.name): $($_.detail)"}) | Set-Content (Join-Path $out 'readiness-report.md') -Encoding utf8
$checks | ForEach-Object{"$($_.status.ToUpperInvariant()) $($_.name) $($_.detail)"} | Set-Content (Join-Path $out 'readiness.log') -Encoding utf8
if(-not $result.success){ exit 1 }
