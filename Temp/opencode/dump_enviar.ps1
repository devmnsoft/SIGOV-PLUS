$s = [IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs')
$i = $s.IndexOf('public async Task<RequisicaoEnvioResultado> EnviarAsync')
$j = $s.IndexOf('public async Task', $i + 10)
$m = $s.Substring($i, $j - $i)
# Insert a newline after every ';' to make the giant single line readable
$t = $m.Replace(';', ";`n")
$lines = $t -split "`n"
$out = @()
$n = 0
foreach ($ln in $lines) {
  $n++
  # hard-wrap very long statements at 220 chars
  while ($ln.Length -gt 220) {
    $out += ("L{n}: " + $ln.Substring(0, 220))
    $ln = $ln.Substring(220)
  }
  $out += ("L{n}: " + $ln)
}
[IO.File]::WriteAllLines('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\enviar_method.txt', $out)
"lines=" + $out.Count
