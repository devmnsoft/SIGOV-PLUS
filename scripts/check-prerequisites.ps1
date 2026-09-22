[CmdletBinding()]
param()

$failed = $false
function Report([string]$Name, [string]$Status, [string]$Detail) {
    "{0,-20} {1,-16} {2}" -f $Name, $Status, $Detail
    if ($Status -ne 'OK') { $script:failed = $true }
}

Report 'OS' 'OK' ([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)
foreach ($tool in @('dotnet', 'pwsh', 'psql', 'node')) {
    $command = Get-Command $tool -ErrorAction SilentlyContinue
    if ($command) { Report $tool 'OK' $command.Source }
    else { Report $tool 'MISSING_TOOL' 'não encontrado no PATH' }
}

if (Get-Command psql -ErrorAction SilentlyContinue) {
    $version = (& psql --version) -join ''
    if ($version -match '(\d+)(?:\.\d+)?$' -and [int]$Matches[1] -ge 16) { Report 'PostgreSQL' 'OK' "major $($Matches[1])" }
    else { Report 'PostgreSQL' 'INVALID_VERSION' "esperado 16 ou superior; encontrado: $version" }
}
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) { Report 'ConnectionString' 'MISSING_ENV' 'defina ConnectionStrings__DefaultConnection sem registrar o valor' }
else { Report 'ConnectionString' 'OK' 'ConnectionStrings__DefaultConnection definida' }
if (Test-Path 'global.json') { Report 'global.json' 'OK' 'presente' } else { Report 'global.json' 'MISSING_ENV' 'ausente' }

foreach ($port in @(5000, 5001, 5432)) {
    try {
        $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
        if ($listener) { Report "port:$port" 'PORT_IN_USE' 'há um listener' } else { Report "port:$port" 'OK' 'disponível' }
    } catch { Report "port:$port" 'MISSING_TOOL' 'Get-NetTCPConnection indisponível' }
}
if ($failed) { exit 1 }
