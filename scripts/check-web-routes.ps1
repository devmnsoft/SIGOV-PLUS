param(
    [string]$BaseUrl = "",
    [string]$OutputPath = "artifacts/web/routes-report.json"
)

$ErrorActionPreference = "Stop"
$routes = @(
    "/", "/MinhaCentral", "/Dashboard", "/Protocolo", "/Protocolo/Novo", "/Protocolo/Meus", "/Protocolo/Pendentes",
    "/Ged/Dashboard", "/Ged/Novo", "/Ged/Documentos", "/Ged/Pendentes", "/Ged/Lixeira",
    "/Tarefas/Minhas", "/Tarefas/Equipe", "/Tarefas/Nova", "/Notificacoes", "/Busca?q=protocolo",
    "/Seguranca/Usuarios", "/Seguranca/Usuarios/Novo", "/Seguranca/Perfis", "/Seguranca/Permissoes",
    "/Saas/Implantacao", "/Auditoria/Trilhas", "/Lgpd/Dashboard"
)

$controllerText = (Get-ChildItem "src/Sigov.Web/Controllers" -Filter "*.cs" -Recurse | Get-Content -Raw) -join "`n"
$results = foreach ($route in $routes) {
    $path = ($route -split '\?')[0]
    $leaf = $path.Trim('/').Split('/')[0]
    $declared = $path -eq "/" -or $controllerText.Contains('"' + $path + '"') -or $controllerText.Contains('"/' + $leaf + '")')
    $status = if ($declared) { "declared" } else { "missing" }
    $httpStatus = $null
    if ($BaseUrl) {
        try {
            $response = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $route) -MaximumRedirection 0 -SkipHttpErrorCheck
            $httpStatus = [int]$response.StatusCode
            if ($httpStatus -eq 404) { $status = "http-404" } else { $status = "http-ok-or-auth" }
        } catch { $status = "http-error" }
    }
    [ordered]@{ route = $route; status = $status; httpStatus = $httpStatus }
}

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    mode = $(if ($BaseUrl) { "http-and-static" } else { "static" })
    total = $results.Count
    failures = @($results | Where-Object { $_.status -in @("missing", "http-404", "http-error") }).Count
    routes = @($results)
}
$directory = Split-Path $OutputPath -Parent
if ($directory) { New-Item -ItemType Directory -Force $directory | Out-Null }
$report | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $OutputPath
Write-Host "Relatório: $OutputPath ($($report.failures) falhas)"
if ($report.failures -gt 0) { exit 1 }

