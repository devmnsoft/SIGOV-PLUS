[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
Set-Location C:\MNSOFT\SIGOV-PLUS
$log = 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_build_api_web.log'
cmd /c "docker compose build api web > `"$log`" 2>&1"
$exit = $LASTEXITCODE
Get-Content $log -Tail 6
Write-Host ('BUILD_EXIT=' + $exit)
if ($exit -ne 0) { exit 1 }
