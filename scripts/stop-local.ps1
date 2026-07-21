$runDir = Join-Path (Split-Path -Parent $PSScriptRoot) '.local/run'
if (Test-Path $runDir) { Get-ChildItem $runDir -Filter '*.pid' | ForEach-Object { $pidValue = Get-Content $_.FullName -ErrorAction SilentlyContinue; if ($pidValue) { Stop-Process -Id ([int]$pidValue) -ErrorAction SilentlyContinue }; Remove-Item $_.FullName -Force } }
