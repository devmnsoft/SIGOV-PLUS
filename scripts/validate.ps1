$ErrorActionPreference = 'Stop'
Write-Host '== sigov validate =='
dotnet restore sigov.sln
dotnet build sigov.sln --no-restore
dotnet test sigov.sln --no-build
if (Get-Command docker -ErrorAction SilentlyContinue) {
  docker compose config | Out-Null
  if (Test-Path docker-compose.prod.yml) { docker compose -f docker-compose.prod.yml config | Out-Null }
} else {
  Write-Warning 'Docker não disponível; validação de compose ignorada.'
}
Write-Host 'Validação concluída.'
