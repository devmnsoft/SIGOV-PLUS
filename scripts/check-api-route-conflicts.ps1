[CmdletBinding()]
param(
    [string] $ControllersPath = (Join-Path $PSScriptRoot '../src/Sigov.Api/Controllers')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ControllersPath).Path
$routes = @{}

function Normalize-Route([string] $controllerRoute, [string] $actionRoute, [string] $controller) {
    $path = if ($actionRoute -match '^/?api/') { $actionRoute } else { (@($controllerRoute, $actionRoute) | Where-Object { $_ }) -join '/' }
    $path = $path.Replace('[controller]', ($controller -replace 'Controller$', '')).Replace('[action]', '')
    return (($path.Trim('/') -replace '/+', '/').ToLowerInvariant())
}

Get-ChildItem -LiteralPath $root -Filter '*.cs' -File -Recurse | Sort-Object FullName | ForEach-Object {
    $file = $_
    $content = Get-Content -LiteralPath $file.FullName -Raw
    # Remove block and line comments before inspecting attributes.
    $content = [regex]::Replace($content, '(?s)/\*.*?\*/', { param($match) ('`n' * (($match.Value -split "`n").Count - 1)) })
    $lines = $content -split "`r?`n"
    $controller = ''
    $controllerRoute = ''
    $pendingRoute = $null
    $pendingHttp = [System.Collections.Generic.List[object]]::new()

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = ($lines[$index] -replace '//.*$', '').Trim()
        if (-not $line) { continue }

        foreach ($match in [regex]::Matches($line, '\[Route\("([^"]*)"\)\]')) { $pendingRoute = $match.Groups[1].Value }
        foreach ($match in [regex]::Matches($line, '\bHttp(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?')) {
            $pendingHttp.Add([pscustomobject]@{ Verb = $match.Groups[1].Value.ToUpperInvariant(); Path = $match.Groups[2].Value; Line = $index + 1 })
        }

        $classMatch = [regex]::Match($line, '\bclass\s+(\w+)')
        if ($classMatch.Success) {
            $controller = $classMatch.Groups[1].Value
            $controllerRoute = if ($null -ne $pendingRoute) { $pendingRoute } else { '' }
            $pendingRoute = $null
            $pendingHttp.Clear()
            continue
        }

        $methodMatch = [regex]::Match($line, '\b(?:public|internal|protected|private)\s+(?:async\s+)?[^=;]+?\s+(\w+)\s*\(')
        if ($controller -and $pendingHttp.Count -gt 0 -and $methodMatch.Success) {
            $method = $methodMatch.Groups[1].Value
            foreach ($attribute in $pendingHttp) {
                $path = Normalize-Route $controllerRoute $attribute.Path $controller
                $key = "$($attribute.Verb) $path"
                if (-not $routes.ContainsKey($key)) { $routes[$key] = [System.Collections.Generic.List[object]]::new() }
                $routes[$key].Add([pscustomobject]@{
                    File = [IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName).Replace('\', '/')
                    Controller = $controller
                    Method = $method
                    Line = $attribute.Line
                    Verb = $attribute.Verb
                    Route = $path
                })
            }
            $pendingHttp.Clear()
            $pendingRoute = $null
        }
    }
}

$conflicts = @($routes.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 } | Sort-Object Key)
if ($conflicts.Count -gt 0) {
    Write-Host 'Conflitos de rota API encontrados:' -ForegroundColor Red
    foreach ($conflict in $conflicts) {
        Write-Host "  $($conflict.Key)" -ForegroundColor Red
        foreach ($entry in $conflict.Value) {
            Write-Host "    - $($entry.File):$($entry.Line) | $($entry.Controller).$($entry.Method) | $($entry.Verb) $($entry.Route)"
        }
    }
    exit 1
}

Write-Host "Nenhum conflito direto em $($routes.Count) rotas API ($root)." -ForegroundColor Green
