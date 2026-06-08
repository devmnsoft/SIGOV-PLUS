$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]
function Assert-File([string]$relative) {
  if (-not (Test-Path (Join-Path $root $relative))) { $failures.Add("Arquivo ausente: $relative") }
}
function Assert-Contains([string]$relative, [string]$pattern, [string]$message) {
  $path = Join-Path $root $relative
  if ((Test-Path $path) -and -not (Select-String -Path $path -Pattern $pattern -Quiet)) { $failures.Add($message) }
}
$views = @(
  'src/Sigov.Web/Views/Rh/Dashboard.cshtml','src/Sigov.Web/Views/Rh/Servidores.cshtml','src/Sigov.Web/Views/Rh/Cargos.cshtml','src/Sigov.Web/Views/Rh/Lotacoes.cshtml','src/Sigov.Web/Views/Rh/Vinculos.cshtml','src/Sigov.Web/Views/Rh/Folhas.cshtml','src/Sigov.Web/Views/Rh/Pontos.cshtml','src/Sigov.Web/Views/Rh/Ferias.cshtml','src/Sigov.Web/Views/Rh/Afastamentos.cshtml','src/Sigov.Web/Views/Rh/SaudeOcupacional.cshtml','src/Sigov.Web/Views/Rh/Esocial.cshtml','src/Sigov.Web/Views/Rh/Portal.cshtml',
  'src/Sigov.Web/Views/Educacao/Dashboard.cshtml','src/Sigov.Web/Views/Educacao/Escolas.cshtml','src/Sigov.Web/Views/Educacao/AnosLetivos.cshtml','src/Sigov.Web/Views/Educacao/Cursos.cshtml','src/Sigov.Web/Views/Educacao/Turmas.cshtml','src/Sigov.Web/Views/Educacao/Alunos.cshtml','src/Sigov.Web/Views/Educacao/Matriculas.cshtml','src/Sigov.Web/Views/Educacao/Professores.cshtml','src/Sigov.Web/Views/Educacao/Frequencias.cshtml','src/Sigov.Web/Views/Educacao/Avaliacoes.cshtml','src/Sigov.Web/Views/Educacao/PreMatriculas.cshtml','src/Sigov.Web/Views/Educacao/Educacenso.cshtml','src/Sigov.Web/Views/Educacao/Portal.cshtml'
)
$views | ForEach-Object { Assert-File $_ }
$assets = @(
  'src/Sigov.Web/wwwroot/css/sigov-brand.css','src/Sigov.Web/wwwroot/css/sigov-layout.css','src/Sigov.Web/wwwroot/css/sigov-components.css','src/Sigov.Web/wwwroot/css/sigov-forms.css','src/Sigov.Web/wwwroot/css/sigov-grids.css','src/Sigov.Web/wwwroot/css/sigov-dashboard.css',
  'src/Sigov.Web/wwwroot/js/sigov.core.js','src/Sigov.Web/wwwroot/js/sigov.ajax.js','src/Sigov.Web/wwwroot/js/sigov.forms.js','src/Sigov.Web/wwwroot/js/sigov.grid.js','src/Sigov.Web/wwwroot/js/sigov.modal.js','src/Sigov.Web/wwwroot/js/sigov.toast.js','src/Sigov.Web/wwwroot/js/sigov.validation.js','src/Sigov.Web/wwwroot/js/sigov.masks.js','src/Sigov.Web/wwwroot/js/sigov.money.js',
  'src/Sigov.Web/wwwroot/js/modules/rh.dashboard.js','src/Sigov.Web/wwwroot/js/modules/rh.servidores.js','src/Sigov.Web/wwwroot/js/modules/rh.cargos.js','src/Sigov.Web/wwwroot/js/modules/rh.lotacoes.js','src/Sigov.Web/wwwroot/js/modules/rh.vinculos.js','src/Sigov.Web/wwwroot/js/modules/rh.folhas.js','src/Sigov.Web/wwwroot/js/modules/rh.ponto.js','src/Sigov.Web/wwwroot/js/modules/rh.ferias.js','src/Sigov.Web/wwwroot/js/modules/rh.afastamentos.js','src/Sigov.Web/wwwroot/js/modules/rh.saude-ocupacional.js','src/Sigov.Web/wwwroot/js/modules/rh.esocial.js','src/Sigov.Web/wwwroot/js/modules/rh.portal.js',
  'src/Sigov.Web/wwwroot/js/modules/educacao.dashboard.js','src/Sigov.Web/wwwroot/js/modules/educacao.escolas.js','src/Sigov.Web/wwwroot/js/modules/educacao.anos-letivos.js','src/Sigov.Web/wwwroot/js/modules/educacao.cursos.js','src/Sigov.Web/wwwroot/js/modules/educacao.turmas.js','src/Sigov.Web/wwwroot/js/modules/educacao.alunos.js','src/Sigov.Web/wwwroot/js/modules/educacao.matriculas.js','src/Sigov.Web/wwwroot/js/modules/educacao.professores.js','src/Sigov.Web/wwwroot/js/modules/educacao.frequencias.js','src/Sigov.Web/wwwroot/js/modules/educacao.avaliacoes.js','src/Sigov.Web/wwwroot/js/modules/educacao.notas.js','src/Sigov.Web/wwwroot/js/modules/educacao.pre-matriculas.js','src/Sigov.Web/wwwroot/js/modules/educacao.educacenso.js','src/Sigov.Web/wwwroot/js/modules/educacao.portal.js'
)
$assets | ForEach-Object { Assert-File $_ }
Assert-Contains 'src/Sigov.Web/Views/Rh/_Registro.cshtml' '@Html.AntiForgeryToken' 'Form genérico RH sem antiforgery.'
Assert-Contains 'src/Sigov.Web/Views/Shared/_Sidebar.cshtml' '/Rh/Dashboard' 'Menu RH sem Dashboard.'
Assert-Contains 'src/Sigov.Web/Views/Shared/_Sidebar.cshtml' '/Educacao/Dashboard' 'Menu Educação sem Dashboard.'
$sqlFiles = Get-ChildItem -Path (Join-Path $root 'src'), (Join-Path $root 'database') -Recurse -File -Include *.sql,*.cs -ErrorAction SilentlyContinue
$legacySchema = $sqlFiles | Select-String -Pattern '(?i)\b(rh|folha|educacao|ensino|escola)\s*\.' | Where-Object { $_.Line -notmatch '(?i)sigov\.' }
if ($legacySchema) { $failures.Add('Referência potencial a schema legado em SQL/C#: ' + ($legacySchema[0].Path)) }
if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Error $_ }; exit 1 }
Write-Host 'Lote 3B: views, assets, menus, antiforgery e schema único verificados.'
