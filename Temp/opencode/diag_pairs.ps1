$ErrorActionPreference = 'Stop'
$pairsFile = 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\repl_pairs.txt'
$raw = [System.IO.File]::ReadAllText($pairsFile, [System.Text.Encoding]::UTF8)
Write-Output ("RAWLEN=" + $raw.Length + " ENDSWITHNEWLINE=" + $raw.EndsWith("`n"))
$pairs = New-Object System.Collections.Generic.List[object[]]
$mode = ''
$oldText = $null
$newText = $null
foreach ($line in ($raw -split "`n")) {
  $line = $line.TrimEnd("`r")
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
for ($i = 0; $i -lt $pairs.Count; $i++) {
  $o = $pairs[$i][0]; $n = $pairs[$i][1]
  Write-Output ("PAIR" + ($i + 1) + " oldLen=" + $o.Length + " newLen=" + $n.Length + " newHead=[" + $n.Substring(0, [Math]::Min(60, $n.Length)) + "]")
}
# markers in target
$text = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
$markers = @(
  'System.Guid.Parse(r.GetProperty("id").GetString()!)',
  'alcadaInsuficiente=politica is not null&&niveis.Count>0',
  'status,causa_bloqueio,regra_snapshot,ciclo',
  "jsonb_build_object('id',i.id,'ordem',i.ordem",
  'topoAlcada=alcadaInsuficiente&&e.Item1==etapas[^1].Item1',
  'causa=topoAlcada?"ALCADA_INSUFICIENTE":"SEM_APROVADOR"'
)
foreach ($m in $markers) {
  $c = ([regex]::Matches($text, [regex]::Escape($m))).Count
  Write-Output ("MARKER " + (if ($c -gt 0) { 'FOUND(' + $c + ')' } else { 'MISSING' }) + " :: " + $m.Substring(0, [Math]::Min(60, $m.Length)))
}
