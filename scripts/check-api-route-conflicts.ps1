[CmdletBinding()]
param(
    [string] $ControllersPath = (Join-Path $PSScriptRoot '../src/Sigov.Api/Controllers')
)

$ErrorActionPreference = 'Stop'
$routes = @{}

Get-ChildItem -LiteralPath $ControllersPath -Filter '*.cs' -File | Sort-Object FullName | ForEach-Object {
    $file = $_
    $controllerRoute = ''
    $pendingRoute = $null
    $pendingHttp = [System.Collections.Generic.List[object]]::new()
    $lineNumber = 0

    Get-Content -LiteralPath $file.FullName | ForEach-Object {
        $lineNumber++
        $line = $_.Trim()

        if ($line -match '^\[Route\("([^"]*)"\)\]$') {
            $pendingRoute = $Matches[1]
        }
        elseif ($line -match '^\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]$') {
            $pendingHttp.Add([pscustomobject]@{
                Method = $Matches[1].ToUpperInvariant()
                Path = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
                Line = $lineNumber
            })
        }
        elseif ($line -match '\bclass\s+\w+') {
            $controllerRoute = if ($null -ne $pendingRoute) { $pendingRoute } else { '' }
            $pendingRoute = $null
            $pendingHttp.Clear()
        }
        elseif ($pendingHttp.Count -gt 0 -and $line -match '\b(public|internal|protected|private)\b.*\(') {
            foreach ($attribute in $pendingHttp) {
                $parts = if ($attribute.Path -match '^api/') { @($attribute.Path) } else { @($controllerRoute, $attribute.Path) }
                $path = (($parts | Where-Object { $_ } | ForEach-Object { $_.Trim('/') }) -join '/').ToLowerInvariant()
                $key = "$($attribute.Method) $path"
                if (-not $routes.ContainsKey($key)) { $routes[$key] = [System.Collections.Generic.List[string]]::new() }
                $routes[$key].Add("$($file.Name):$($attribute.Line)")
            }
            $pendingHttp.Clear()
            $pendingRoute = $null
        }
        elseif ($line -and -not $line.StartsWith('[') -and -not $line.StartsWith('//')) {
            $pendingRoute = $null
        }
    }
}

$conflicts = @($routes.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 } | Sort-Object Key)
if ($conflicts.Count -gt 0) {
    Write-Error ((@('Conflitos de rota API encontrados:') + @($conflicts | ForEach-Object {
        "  $($_.Key)`n" + (($_.Value | ForEach-Object { "    - $_" }) -join "`n")
    })) -join "`n")
    exit 1
}

Write-Host "Nenhum conflito direto em $($routes.Count) rotas API ($ControllersPath)."
