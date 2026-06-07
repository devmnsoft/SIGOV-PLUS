$ErrorActionPreference = 'Stop'
$productionSettings = 'src/Sigov.Api/appsettings.Production.json'
if (-not (Test-Path $productionSettings)) { throw 'appsettings.Production.json não encontrado.' }
$json = Get-Content $productionSettings -Raw | ConvertFrom-Json
if ($json.ConnectionStrings.DefaultConnection) { throw 'Production não deve versionar connection string real.' }
if ($json.Sigov.Security.SwaggerEnabledInProduction -eq $true) { throw 'Swagger está habilitado em Production.' }
if ($json.Sigov.Security.CorsAllowedOrigins -contains '*') { throw 'CORS wildcard em Production.' }
if ($json.Sigov.Seed.Demo -eq $true) { throw 'Seed demo habilitado em Production.' }
if ((Get-Content $productionSettings -Raw) -match 'Password=|Senha=|api_key|private_key') { throw 'Possível segredo hardcoded em appsettings Production.' }
Write-Host 'Security check concluído sem achados críticos.'
