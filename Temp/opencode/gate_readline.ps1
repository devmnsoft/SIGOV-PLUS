[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$lines = Get-Content 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs' -Encoding UTF8
$l = $lines[80]
Write-Host ("LEN=" + $l.Length)
Write-Host "=== chunk 2 (2000..4500) ==="
Write-Host $l.Substring(2000, [Math]::Min(2500, $l.Length - 2000))
Write-Host "=== chunk 3 (4500..) ==="
if($l.Length -gt 4500){ Write-Host $l.Substring(4500) }
