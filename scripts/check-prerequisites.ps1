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
    if ($version -match '(\d+)(?:\.\d+)?$' -and $Matches[1] -eq '16') { Report 'PostgreSQL' 'OK' 'major 16' }
    else { Report 'PostgreSQL' 'INVALID_VERSION' "esperado 16; encontrado: $version" }
}
if ([string]::IsNullOrWhiteSpace($env:SIGOV_DB_PASSWORD)) { Report 'SIGOV_DB_PASSWORD' 'MISSING_ENV' 'defina sem registrar o valor' }
else { Report 'SIGOV_DB_PASSWORD' 'OK' 'definida' }
if (Test-Path '.env.local') { Report '.env.local' 'OK' 'presente' } else { Report '.env.local' 'MISSING_ENV' 'copie .env.local.example' }
if (Test-Path 'global.json') { Report 'global.json' 'OK' 'presente' } else { Report 'global.json' 'MISSING_ENV' 'ausente' }

foreach ($port in @(7000, 7001, 5432)) {
    try {
        $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
        if ($listener) { Report "port:$port" 'PORT_IN_USE' 'há um listener' } else { Report "port:$port" 'OK' 'disponível' }
    } catch { Report "port:$port" 'MISSING_TOOL' 'Get-NetTCPConnection indisponível' }
}
if ($failed) { exit 1 }
