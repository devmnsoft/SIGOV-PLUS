$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    Write-Host "OK: $Name" -ForegroundColor Green
}

Invoke-Step 'dotnet clean sigov.sln' { dotnet clean sigov.sln }
Invoke-Step 'dotnet restore sigov.sln' { dotnet restore sigov.sln }
Invoke-Step 'dotnet build sigov.sln' { dotnet build sigov.sln }
Invoke-Step 'dotnet test sigov.sln' { dotnet test sigov.sln }
Invoke-Step 'docker compose down' { docker compose down }
Invoke-Step 'docker builder prune -f' { docker builder prune -f }
Invoke-Step 'docker compose build --no-cache' { docker compose build --no-cache }
Invoke-Step 'docker compose up -d' { docker compose up -d }
Invoke-Step 'docker compose ps' { docker compose ps }
Invoke-Step 'docker compose logs --tail=100 db-migrations' { docker compose logs --tail=100 db-migrations }
Invoke-Step 'docker compose logs --tail=100 postgres' { docker compose logs --tail=100 postgres }
Invoke-Step 'docker compose logs --tail=100 api' { docker compose logs --tail=100 api }
Invoke-Step 'docker compose logs --tail=100 web' { docker compose logs --tail=100 web }
Invoke-Step 'docker compose logs --tail=100 worker' { docker compose logs --tail=100 worker }

Write-Host 'Validação final concluída. O script não remove volumes Docker.' -ForegroundColor Green
