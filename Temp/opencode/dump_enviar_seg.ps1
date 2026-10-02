$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$lines = Get-Content 'C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs' -Encoding UTF8
$line = $lines[79]
$start = $line.IndexOf('public async Task<RequisicaoEnvioResultado> EnviarAsync')
$endMark = $line.IndexOf('RequisicaoCabecalho')
if ($endMark -lt $start) { $endMark = $line.Length }
$seg = $line.Substring($start, $endMark - $start)
Write-Output "SEGMENT_LENGTH=$($seg.Length)"
$chunk = 200
for ($i = 0; $i -lt $seg.Length; $i += $chunk) {
  $len = [Math]::Min($chunk, $seg.Length - $i)
  Write-Output ("[{0:D5}] " -f $i) + $seg.Substring($i, $len)
}
