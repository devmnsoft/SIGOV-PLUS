$ErrorActionPreference = 'Stop'
$t = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\efb_compras.cs', [System.Text.Encoding]::UTF8)
$i = $t.IndexOf('not is_deleted for update')
Write-Output ('### guard-region IDX=' + $i)
Write-Output $t.Substring($i, 1900)
Write-Output '----'
$j = 0; $k = -1
while (($k = $t.IndexOf('Status!=', $j)) -ge 0) {
  Write-Output ('### Status!= at ' + $k)
  Write-Output $t.Substring([Math]::Max(0,$k-260), 420)
  Write-Output '----'
  $j = $k + 1
}
