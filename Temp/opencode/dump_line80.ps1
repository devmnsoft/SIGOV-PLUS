$ErrorActionPreference = 'Stop'
Set-Location C:\MNSOFT\SIGOV-PLUS
$line = (Get-Content src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs -Encoding UTF8)[79]
Write-Output ("LEN=" + $line.Length)
$sb = New-Object System.Text.StringBuilder
$start = [Math]::Max(0, 9400)
for ($i = $start; $i -lt $line.Length; $i += 120) {
  $len = [Math]::Min(120, $line.Length - $i)
  [void]$sb.AppendLine(("{0:D5} " -f $i) + $line.Substring($i, $len))
}
[System.IO.File]::WriteAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\enviar_tail.txt', $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "done"
