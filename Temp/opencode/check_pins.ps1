$ErrorActionPreference = 'Stop'
$repo = 'C:\MNSOFT\SIGOV-PLUS'
if ($args.Count -ge 1) { $repo = $args[0] }
$files = @{
  'F1' = 'src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs'
  'F2' = 'src\Sigov.Infrastructure\ComprasEmpresariais\AprovacaoRequisicaoRepository.cs'
  'F3' = 'src\Sigov.Web\Views\ComprasEmpresariais\Aprovacoes\Detalhe.cshtml'
  'F4' = 'src\Sigov.Web\Views\ComprasEmpresariais\Relatorios\Aprovacoes.cshtml'
  'F5' = 'src\Sigov.Web\Views\ComprasEmpresariais\Configuracao\Politica.cshtml'
  'F6' = 'src\Sigov.Web\Views\ComprasEmpresariais\Workspace.cshtml'
  'F7' = 'src\Sigov.Web\Views\ComprasEmpresariais\Recebimentos\Relatorio.cshtml'
}
$cache = [System.Collections.Generic.Dictionary[string,string]]::new()
function Get-Text([string]$key) {
  if ($cache.ContainsKey($key)) { return $cache[$key] }
  $t = [System.IO.File]::ReadAllText((Join-Path $repo $files[$key]), [System.Text.Encoding]::UTF8)
  $cache[$key] = $t
  return $t
}
$raw = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\pins.txt', [System.Text.Encoding]::UTF8)
foreach ($line in ($raw -split "`n")) {
  $line = $line.TrimEnd("`r")
  if ($line.Length -eq 0) { continue }
  $i1 = $line.IndexOf('|')
  $i2 = $line.IndexOf('|', $i1 + 1)
  $grp = $line.Substring(0, $i1)
  $fkey = $line.Substring($i1 + 1, $i2 - $i1 - 1)
  $pin = $line.Substring($i2 + 1)
  $text = Get-Text $fkey
  $c = ([regex]::Matches($text, [regex]::Escape($pin))).Count
  Write-Output ("$(if ($c -ge 1) { 'FOUND(' + $c + ')' } else { 'MISSING' }) [$grp/$fkey] " + $pin.Substring(0, [Math]::Min(80, $pin.Length)))
}
