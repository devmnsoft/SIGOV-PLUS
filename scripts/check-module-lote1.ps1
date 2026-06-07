$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
  'src/Sigov.Web/Views/Pessoas/Index.cshtml',
  'src/Sigov.Web/Views/Pessoas/_FormPessoa.cshtml',
  'src/Sigov.Web/Views/Seguranca/Usuarios.cshtml',
  'src/Sigov.Web/Views/Seguranca/Permissoes.cshtml',
  'src/Sigov.Web/Views/SaasAdmin/Tenants.cshtml',
  'src/Sigov.Web/Views/Auditoria/Trilhas.cshtml',
  'src/Sigov.Web/Views/Lgpd/Index.cshtml',
  'src/Sigov.Web/wwwroot/js/modules/core.pessoas.js',
  'src/Sigov.Web/wwwroot/js/modules/seguranca.usuarios.js',
  'src/Sigov.Web/wwwroot/js/modules/saas.tenants.js',
  'src/Sigov.Web/wwwroot/js/modules/auditoria.trilhas.js',
  'src/Sigov.Web/wwwroot/js/modules/lgpd.dashboard.js',
  'src/Sigov.Web/wwwroot/css/sigov-forms.css',
  'src/Sigov.Web/wwwroot/css/sigov-grids.css'
)
foreach ($item in $required) {
  if (-not (Test-Path (Join-Path $root $item))) { throw "Arquivo obrigatório do lote 1 não encontrado: $item" }
}
& (Join-Path $PSScriptRoot 'check-forms-antiforgery.ps1')
$layout = Get-Content (Join-Path $root 'src/Sigov.Web/Views/Shared/_Layout.cshtml') -Raw
foreach ($script in @('sigov.api.js','sigov.ui.js','sigov.forms.js','sigov.grid.js')) {
  if ($layout -notmatch [regex]::Escape($script)) { throw "Layout não referencia $script" }
}
Write-Host 'Lote 1 MVC/Razor, assets, antiforgery e links principais verificados.'
