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
  'src/Sigov.Web/wwwroot/lib/bootstrap/css/bootstrap.min.css'
  'src/Sigov.Web/wwwroot/lib/bootstrap/js/bootstrap.bundle.min.js'
)
foreach ($asset in $assets) {
  if (-not (Test-Path (Join-Path $root $asset))) { throw "Asset obrigatório não encontrado: $asset" }
}

$bootstrapCss = Get-Content (Join-Path $root 'src/Sigov.Web/wwwroot/lib/bootstrap/css/bootstrap.min.css') -Raw
$bootstrapJs = Get-Content (Join-Path $root 'src/Sigov.Web/wwwroot/lib/bootstrap/js/bootstrap.bundle.min.js') -Raw
if ($bootstrapCss.Length -lt 5000 -or $bootstrapCss -match 'placeholder') {
  throw 'A camada CSS local está incompleta ou voltou a ser um placeholder.'
}
foreach ($contract in @('Modal','Toast','Collapse','Dropdown','Tooltip','Popover','Offcanvas')) {
  if ($bootstrapJs -notmatch "class $contract") { throw "Componente local ausente no runtime de UI: $contract" }
}
if ($bootstrapJs -match 'https?://|import\s*\(') {
  throw 'O runtime local de UI não pode carregar código de CDN em tempo de execução.'
}
Write-Host 'Assets web SIGOV verificados.'
