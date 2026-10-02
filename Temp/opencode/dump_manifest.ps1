$m = Get-Content -Raw -Encoding UTF8 C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\manifest.json
$i = $m.IndexOf('20260927120000')
[IO.File]::WriteAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\manifest_tail.json', $m.Substring([Math]::Max(0,$i-1500)), (New-Object Text.UTF8Encoding($false)))
"total-len=" + $m.Length
