$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]
function Require-File([string]$relative) {
    if (-not (Test-Path (Join-Path $root $relative))) { $failures.Add("Arquivo obrigatório ausente: $relative") }
}
function Require-NoMatch([string]$relative, [string]$pattern, [string]$message) {
    $path = Join-Path $root $relative
    if (-not (Test-Path $path)) { return }
    $item = Get-Item $path
    if ($item.PSIsContainer) {
        if (Get-ChildItem $path -Recurse -File | Select-String -Pattern $pattern -Quiet) { $failures.Add($message) }
        return
    }
    if (Select-String -Path $path -Pattern $pattern -Quiet) { $failures.Add($message) }
}
$views = @(
 'src/Sigov.Web/Views/Processos/Index.cshtml','src/Sigov.Web/Views/Processos/Criar.cshtml','src/Sigov.Web/Views/Processos/Detalhe.cshtml',
 'src/Sigov.Web/Views/Protocolos/Index.cshtml','src/Sigov.Web/Views/Ouvidoria/Index.cshtml','src/Sigov.Web/Views/DiarioOficial/Index.cshtml',
 'src/Sigov.Web/Views/Financeiro/Dashboard.cshtml','src/Sigov.Web/Views/Financeiro/Empenhos.cshtml','src/Sigov.Web/Views/Financeiro/Liquidacoes.cshtml','src/Sigov.Web/Views/Financeiro/Pagamentos.cshtml','src/Sigov.Web/Views/Financeiro/Receitas.cshtml',
 'src/Sigov.Web/Views/Tributario/Dashboard.cshtml','src/Sigov.Web/Views/Tributario/Contribuintes.cshtml','src/Sigov.Web/Views/Tributario/Lancamentos.cshtml','src/Sigov.Web/Views/Tributario/Parcelas.cshtml','src/Sigov.Web/Views/Tributario/DamBoletos.cshtml','src/Sigov.Web/Views/Tributario/PixPagamentos.cshtml','src/Sigov.Web/Views/Tributario/Certidoes.cshtml','src/Sigov.Web/Views/Tributario/DividaAtiva.cshtml','src/Sigov.Web/Views/Tributario/Carnes.cshtml'
)
$views | ForEach-Object { Require-File $_ }
$js = @(
 'src/Sigov.Web/wwwroot/js/modules/processos.digital.js','src/Sigov.Web/wwwroot/js/modules/processos.protocolo.js','src/Sigov.Web/wwwroot/js/modules/processos.ouvidoria.js','src/Sigov.Web/wwwroot/js/modules/processos.diario-oficial.js',
 'src/Sigov.Web/wwwroot/js/modules/financeiro.dashboard.js','src/Sigov.Web/wwwroot/js/modules/financeiro.empenhos.js','src/Sigov.Web/wwwroot/js/modules/financeiro.liquidacoes.js','src/Sigov.Web/wwwroot/js/modules/financeiro.pagamentos.js','src/Sigov.Web/wwwroot/js/modules/financeiro.receitas.js',
 'src/Sigov.Web/wwwroot/js/modules/tributario.dashboard.js','src/Sigov.Web/wwwroot/js/modules/tributario.contribuintes.js','src/Sigov.Web/wwwroot/js/modules/tributario.lancamentos.js','src/Sigov.Web/wwwroot/js/modules/tributario.parcelas.js','src/Sigov.Web/wwwroot/js/modules/tributario.dam-boletos.js','src/Sigov.Web/wwwroot/js/modules/tributario.pix.js','src/Sigov.Web/wwwroot/js/modules/tributario.certidoes.js','src/Sigov.Web/wwwroot/js/modules/tributario.divida-ativa.js','src/Sigov.Web/wwwroot/js/modules/tributario.carnes.js'
)
$js | ForEach-Object { Require-File $_ }
@('sigov-brand.css','sigov-layout.css','sigov-components.css','sigov-forms.css','sigov-grids.css','sigov-dashboard.css') | ForEach-Object { Require-File "src/Sigov.Web/wwwroot/css/$_" }
Get-ChildItem (Join-Path $root 'src/Sigov.Web/Views') -Recurse -Filter '*.cshtml' |
    Where-Object { $_.FullName -match '(Processos|Protocolos|Ouvidoria|DiarioOficial|Financeiro|Tributario)' } |
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        if ($content -match '<form' -and $content -match 'method="post"' -and $content -notmatch 'AntiForgeryToken') { $failures.Add("Form POST sem antiforgery: $($_.FullName)") }
    }
Get-ChildItem (Join-Path $root 'src/Sigov.Web/Views') -Recurse -Filter '*.cshtml' | ForEach-Object {
    Select-String -Path $_.FullName -Pattern '~/js/[^"'']+\.js' -AllMatches | ForEach-Object {
        foreach ($match in $_.Matches) {
            $relative = $match.Value.Replace('~/','src/Sigov.Web/wwwroot/')
            Require-File $relative
        }
    }
}
Require-NoMatch 'database/postgres/migrations' '(?i)(create\s+schema\s+(processos|workflow|financeiro|fin|tributario|trib)|set\s+search_path\s*=\s*(processos|workflow|financeiro|fin|tributario|trib)|(from|join|into|update|delete\s+from)\s+(processos|workflow|financeiro|fin|tributario|trib)\.)' 'Schema SQL antigo detectado em migrations.'
Get-ChildItem (Join-Path $root 'src') -Recurse -Include '*.cs','*.cshtml','*.js' |
    ForEach-Object {
        $text = Get-Content $_.FullName -Raw
        if ($text -match 'DAM fake|PIX dev|pagamento dev' -and $text -notmatch 'Production|Development|data-dev-resource|ambiente') { $failures.Add("Recurso dev sem indicação de proteção por ambiente: $($_.FullName)") }
    }
if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Error $_ }; exit 1 }
Write-Host 'check-module-lote2: OK'
