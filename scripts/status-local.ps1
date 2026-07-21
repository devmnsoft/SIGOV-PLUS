$runDir = Join-Path (Split-Path -Parent $PSScriptRoot) '.local/run'
if (-not (Test-Path $runDir)) { Write-Host 'Nenhum processo local registrado.'; exit 0 }
Get-ChildItem $runDir -Filter '*.pid' | ForEach-Object { $pidValue=[int](Get-Content $_.FullName); $p=Get-Process -Id $pidValue -ErrorAction SilentlyContinue; [pscustomobject]@{Name=$_.BaseName;Pid=$pidValue;Running=($null -ne $p)} } | Format-Table
