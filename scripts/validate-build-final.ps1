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
Invoke-Step 'docker builder prune -f' { docker builder prune -f }
Invoke-Step 'docker compose build --no-cache' { docker compose build --no-cache }

Write-Host 'Validação final concluída. O script não remove volumes Docker.' -ForegroundColor Green
