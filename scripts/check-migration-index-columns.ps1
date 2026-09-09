param([string]$MigrationsPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'database/postgres/migrations'))
$ErrorActionPreference = 'Stop'
$checker = Join-Path $PSScriptRoot 'check-migration-index-columns.sh'
if (Get-Command wsl.exe -ErrorAction SilentlyContinue) {
    $resolvedMigrations = (Resolve-Path -LiteralPath $MigrationsPath).Path
    $linuxChecker = (& wsl.exe --exec wslpath -a $checker).Trim()
    $linuxMigrations = (& wsl.exe --exec wslpath -a $resolvedMigrations).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($linuxChecker) -or [string]::IsNullOrWhiteSpace($linuxMigrations)) {
        throw 'Não foi possível converter os caminhos do validador para o WSL.'
    }
    & wsl.exe --exec bash $linuxChecker $linuxMigrations
    exit $LASTEXITCODE
}
if (Get-Command bash -ErrorAction SilentlyContinue) { & bash $checker $MigrationsPath; exit $LASTEXITCODE }
throw 'bash não está disponível; execute o validador em WSL ou Git Bash.'
