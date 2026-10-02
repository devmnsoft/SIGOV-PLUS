param([Parameter(Mandatory=$true)][string]$File,[Parameter(Mandatory=$true)][string]$Needle,[int]$Radius=300)
$ErrorActionPreference = 'Stop'
$text = [System.IO.File]::ReadAllText($File, [System.Text.Encoding]::UTF8)
$i = $text.IndexOf($Needle, [System.StringComparison]::Ordinal)
if ($i -lt 0) { Write-Output 'NOT FOUND'; exit }
$start = [Math]::Max(0, $i - $Radius)
$len = [Math]::Min($text.Length - $start, ($i - $start) + $Needle.Length + $Radius)
Write-Output ("IDX=" + $i)
Write-Output $text.Substring($start, $len)
