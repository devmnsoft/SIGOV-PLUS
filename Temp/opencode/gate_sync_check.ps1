[Console]::OutputEncoding=[Text.Encoding]::UTF8
$files = @(
 'C:\MNSOFT\SIGOV-PLUS\script_completo.sql',
 'C:\MNSOFT\SIGOV-PLUS\script_completop.sql',
 'C:\MNSOFT\SIGOV-PLUS\script_completo_dev.sql',
 'C:\MNSOFT\SIGOV-PLUS\database\script_completo.sql',
 'C:\MNSOFT\SIGOV-PLUS\database\postgres\script_completo.sql',
 'C:\MNSOFT\SIGOV-PLUS\database\postgres\script_completo_dev.sql'
)
foreach ($f in $files) {
  if (Test-Path $f) { $h = (Get-FileHash $f -Algorithm SHA256).Hash; "{0}  {1}  {2}" -f $h.Substring(0,12), (Get-Item $f).Length, $f.Replace('C:\MNSOFT\SIGOV-PLUS\','') }
  else { "MISSING  $f" }
}
# confirm tightened constraint text present in all copies containing 20260927
foreach ($f in $files) {
  if (Test-Path $f) {
    $c = Get-Content $f -Raw
    $n = ([regex]::Matches($c,'resultado_codigo is not null and resultado_codigo in')).Count
    "{0}  TIGHTENED_COUNT={1}" -f $f.Replace('C:\MNSOFT\SIGOV-PLUS\',''), $n
  }
}
