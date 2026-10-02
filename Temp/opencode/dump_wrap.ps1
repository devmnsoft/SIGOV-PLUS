param([string]$Path, [string]$Out, [int]$Width = 190)
$sb = New-Object System.Text.StringBuilder
$lines = Get-Content $Path -Encoding UTF8
for ($i = 0; $i -lt $lines.Count; $i++) {
    $ln = [string]$lines[$i]
    $n = ($i + 1).ToString().PadLeft(5)
    if ($ln.Length -le $Width) {
        [void]$sb.AppendLine($n + ": " + $ln)
    } else {
        [void]$sb.AppendLine($n + ": [" + $ln.Length + " chars]")
        for ($p = 0; $p -lt $ln.Length; $p += $Width) {
            $len = [Math]::Min($Width, $ln.Length - $p)
            [void]$sb.AppendLine("      | " + $ln.Substring($p, $len))
        }
    }
}
[System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
"written " + (Get-Item $Out).Length + " bytes"
