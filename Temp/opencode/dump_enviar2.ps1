$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$line = (Get-Content 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs' -Encoding UTF8)[79]
$i = $line.IndexOf('public async Task<RequisicaoEnvioResultado> EnviarAsync')
$end = $line.IndexOf('RequisicaoCabecalho', $i)
$seg = $line.Substring($i, $end - $i)
# split into 150-char chunks with position markers for exact reconstruction
$start = 0
$n = 0
while ($start -lt $seg.Length) {
  $len = [Math]::Min(150, $seg.Length - $start)
  Write-Output ("[{0:D4}..{1:D4}] " -f $start, ($start + $len)) + $seg.Substring($start, $len)
  $start += $len; $n++
}
Write-Output "TOTALLEN=$($seg.Length)"
