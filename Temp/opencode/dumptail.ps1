$ErrorActionPreference = 'Stop'
Set-Location C:\MNSOFT\SIGOV-PLUS
$line = (Get-Content src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs -Encoding UTF8)[79]
[System.IO.File]::WriteAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tail2.txt', $line.Substring(8400), (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("SUBLEN=" + ($line.Length - 8400))
