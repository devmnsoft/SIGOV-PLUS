param([string]$ApiBaseUrl = $env:SIGOV_PROD_API_BASE_URL, [switch]$AllowWarnings)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/go-live-check.ps1" -ApiBaseUrl $ApiBaseUrl -AllowWarnings:$AllowWarnings
