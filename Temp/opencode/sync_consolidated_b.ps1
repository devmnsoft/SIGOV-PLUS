$ErrorActionPreference = 'Stop'
$enc   = [System.Text.Encoding]::UTF8
$sha   = 'dc5909484078eeb5bf0b312c8e144fa3f447eb004d6a11daf442fb12029745e6'
$desc  = 'Itens de cotacao por linha com saldo transacional e selecao auditada com geracao atomica de pedidos'
$files = @(
  'C:\MNSOFT\SIGOV-PLUS\script_completo.sql',
  'C:\MNSOFT\SIGOV-PLUS\script_completop.sql',
  'C:\MNSOFT\SIGOV-PLUS\script_completo_dev.sql',
  'C:\MNSOFT\SIGOV-PLUS\database\script_completo.sql',
  'C:\MNSOFT\SIGOV-PLUS\database\postgres\script_completo.sql',
  'C:\MNSOFT\SIGOV-PLUS\database\postgres\script_completo_dev.sql'
)
$migFile = 'C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\20260930180000_compras_cotacao_itens_selecao.sql'
$migBodyRaw = [System.IO.File]::ReadAllText($migFile, $enc)

foreach ($f in $files) {
  $bytes = [System.IO.File]::ReadAllBytes($f)
  $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  $text = [System.Text.Encoding]::UTF8.GetString($bytes)
  if ($hasBom) { $text = $text.TrimStart([char]0xFEFF) }

  # Detect newline style from existing content
  if ($text.Contains("`r`n")) { $nl = "`r`n" } else { $nl = "`n" }

  # --- Edit 1: registration tuple -----------------------------------------
  $regOld = "('20260930120000', array['f7a8d7b2a9702eec2698e9f5af25c3280b610cbedb105f823028431d11cc99b5']::text[])"
  $regCnt = ([regex]::Matches($text, [regex]::Escape($regOld))).Count
  if ($regCnt -ne 1) { throw "$f : regOld count=$regCnt (expected 1)" }
  $regNew = $regOld + ',' + $nl + "         ('20260930180000', array['$sha']::text[])"
  $text = $text.Replace($regOld, $regNew)

  # --- Edit 2: migration section before COMPATIBILITY ----------------------
  $anchor2 = "-- ==================================================" + $nl + "-- COMPATIBILITY: 850_post_migration_compatibility.sql"
  $a2cnt = ([regex]::Matches($text, [regex]::Escape($anchor2))).Count
  if ($a2cnt -ne 1) { throw "$f : COMPATIBILITY anchor count=$a2cnt (expected 1)" }
  $idx = $text.IndexOf($anchor2)
  $rs  = $text.LastIndexOf("-- Reset de helpers", $idx)
  if ($rs -lt 0) { throw "$f : Reset de helpers not found before COMPATIBILITY" }
  $resetBlock = $text.Substring($rs, $idx - $rs)
  if (-not $resetBlock.Contains("ensure_schema_safe_index")) { throw "$f : reset block looks wrong" }

  # Body of the migration, normalized to the file's newline style, no trailing blank
  $body = $migBodyRaw.Replace("`r`n", "`n").Replace("`n", $nl)
  $body = $body.TrimEnd()

  $insB = "insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values ('20260930180000', '$desc', '$sha', 'functional', 'script_completop', true, null, now()) on conflict (version) do update set description = excluded.description, checksum = excluded.checksum, category = excluded.category, source = excluded.source, success = true;"

  $toInsert = "-- ==================================================" + $nl +
    "-- MIGRATION: 20260930180000_compras_cotacao_itens_selecao.sql" + $nl +
    "-- CATEGORY: functional" + $nl +
    "-- CHECKSUM_SHA256: $sha" + $nl +
    "-- ==================================================" + $nl +
    $body + $nl + $nl +
    $insB + $nl + $nl +
    $resetBlock

  $text = $text.Substring(0, $idx) + $toInsert + $text.Substring($idx)

  # Post-edit sanity: 3 occurrences of the new version
  $occ = ([regex]::Matches($text, [regex]::Escape('20260930180000'))).Count
  if ($occ -ne 3) { throw "$f : post-edit 20260930180000 count=$occ (expected 3)" }

  $outEnc = New-Object System.Text.UTF8Encoding($hasBom)
  [System.IO.File]::WriteAllText($f, $text, $outEnc)
  Write-Host ("OK {0} (bom={1}, nl={2}, shaOccurrences=3)" -f (Split-Path $f -Leaf), $hasBom, $(if ($nl -eq "`r`n") { 'CRLF' } else { 'LF' }))
}
Write-Host 'CONSOLIDATED_SYNC_DONE'
