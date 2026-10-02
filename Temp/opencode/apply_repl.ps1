$ErrorActionPreference = 'Stop'
$repo = 'C:\MNSOFT\SIGOV-PLUS'
$target = Join-Path $repo 'src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs'
$pairsFile = Join-Path $repo 'Temp\opencode\repl_pairs.txt'

$bytes = [System.IO.File]::ReadAllBytes($target)
$hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
$text = [System.IO.File]::ReadAllText($target, [System.Text.Encoding]::UTF8)

$raw = [System.IO.File]::ReadAllText($pairsFile, [System.Text.Encoding]::UTF8)
$pairs = New-Object System.Collections.Generic.List[object[]]
$mode = ''
$oldText = $null
$newText = $null
foreach ($line in ($raw -split "`n")) {
  $line = $line.TrimEnd("`r")
  if ($line.Length -eq 0) { continue }
  if ($line -eq '===OLD===') { $mode = 'old'; continue }
  if ($line -eq '===NEW===') { $mode = 'new'; continue }
  if ($line -like '===PAIR*===') {
    if ($null -ne $oldText) { $pairs.Add(@($oldText, $newText)); $oldText = $null; $newText = $null }
    $mode = ''; continue
  }
  if ($mode -eq 'old') { $oldText = $line }
  elseif ($mode -eq 'new') { $newText = $line }
}
if ($null -ne $oldText) { $pairs.Add(@($oldText, $newText)) }
Write-Output ("PAIRS=" + $pairs.Count)

for ($i = 0; $i -lt $pairs.Count; $i++) {
  $o = $pairs[$i][0]
  $n = $pairs[$i][1]
  $count = ([regex]::Matches($text, [regex]::Escape($o))).Count
  if ($count -ne 1) {
    Write-Output ("FAIL pair " + ($i + 1) + " occurrences=" + $count)
    Write-Output ("ANCHOR-HEAD: " + $o.Substring(0, [Math]::Min(90, $o.Length)))
    exit 1
  }
  $text = $text.Replace($o, $n)
  Write-Output ("OK pair " + ($i + 1))
}

$enc = New-Object System.Text.UTF8Encoding($hasBom)
[System.IO.File]::WriteAllText($target, $text, $enc)
Write-Output ("APPLIED ALL" + " BOM=" + $hasBom + " NEWLEN=" + $text.Length)
