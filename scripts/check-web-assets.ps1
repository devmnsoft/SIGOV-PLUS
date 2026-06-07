$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = @(
  'src/Sigov.Web/wwwroot/css/sigov-brand.css',
  'src/Sigov.Web/wwwroot/css/sigov-layout.css',
  'src/Sigov.Web/wwwroot/css/sigov-components.css',
  'src/Sigov.Web/wwwroot/css/sigov-forms.css',
  'src/Sigov.Web/wwwroot/css/sigov-grids.css',
  'src/Sigov.Web/wwwroot/js/sigov.core.js',
  'src/Sigov.Web/wwwroot/js/sigov.ajax.js',
  'src/Sigov.Web/wwwroot/js/sigov.forms.js',
  'src/Sigov.Web/wwwroot/js/sigov.grid.js',
  'src/Sigov.Web/wwwroot/js/sigov.modal.js',
  'src/Sigov.Web/wwwroot/js/sigov.toast.js',
  'src/Sigov.Web/wwwroot/js/sigov.validation.js',
  'src/Sigov.Web/wwwroot/js/sigov.masks.js'
)
foreach ($asset in $assets) {
  if (-not (Test-Path (Join-Path $root $asset))) { throw "Asset obrigatório não encontrado: $asset" }
}
Write-Host 'Assets web SIGOV verificados.'
