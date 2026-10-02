$ErrorActionPreference = 'Stop'
$enc   = [System.Text.Encoding]::UTF8
$desc  = 'Correcao aditiva: tabela base do outbox transacional de integracao (sigov.integracao_outbox)'
$version = '20261001090000'
$migFile = 'C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\20261001090000_integracao_outbox_base.sql'
$files = @(
  'C:\MNSOFT\SIGOV-PLUS\script_completo.sql',
  'C:\MNSOFT\SIGOV-PLUS\script_completop.sql',
  'C:\MNSOFT\SIGOV-PLUS\script_completo_dev.sql',
  'C:\MNSOFT\SIGOV-PLUS\database\script_completo.sql',
  'C:\MNSOFT\SIGOV-PLUS\database\postgres\script_completo.sql',
  'C:\MNSOFT\SIGOV-PLUS\database\postgres\script_completo_dev.sql'
)
$manifestPath = 'C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\manifest.json'

# checksum = sha256 do conteudo normalizado (BOM removido, CRLF/CR -> LF), como em check-migration-catalog.sh
$migRaw = [System.IO.File]::ReadAllText($migFile, $enc)
if ($migRaw.StartsWith([char]0xFEFF)) { $migRaw = $migRaw.Substring(1) }
$norm = $migRaw.Replace("`r`n", "`n").Replace("`r", "`n")
$shaAlgo = [System.Security.Cryptography.SHA256]::Create()
$sha = ([BitConverter]::ToString($shaAlgo.ComputeHash($enc.GetBytes($norm)))).Replace('-','').ToLower()
Write-Host ("MIGRATION_SHA256=" + $sha)

foreach ($f in $files) {
  $bytes = [System.IO.File]::ReadAllBytes($f)
  $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  $text = [System.Text.Encoding]::UTF8.GetString($bytes)
  if ($hasBom) { $text = $text.TrimStart([char]0xFEFF) }
  if ($text.Contains("`r`n")) { $nl = "`r`n" } else { $nl = "`n" }

  # --- Edit 1: registration tuple -----------------------------------------
  $regOld = "('20260930180000', array['dc5909484078eeb5bf0b312c8e144fa3f447eb004d6a11daf442fb12029745e6']::text[])"
  $regCnt = ([regex]::Matches($text, [regex]::Escape($regOld))).Count
  if ($regCnt -ne 1) { throw "$f : regOld count=$regCnt (expected 1)" }
  $regNew = $regOld + ',' + $nl + "         ('$version', array['$sha']::text[])"
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

  $body = $norm.TrimEnd()
  $body = $body.Replace("`n", $nl)

  $insB = "insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values ('$version', '$desc', '$sha', 'functional', 'script_completop', true, null, now()) on conflict (version) do update set description = excluded.description, checksum = excluded.checksum, category = excluded.category, source = excluded.source, success = true;"

  $toInsert = "-- ==================================================" + $nl +
    "-- MIGRATION: ${version}_integracao_outbox_base.sql" + $nl +
    "-- CATEGORY: functional" + $nl +
    "-- CHECKSUM_SHA256: $sha" + $nl +
    "-- ==================================================" + $nl +
    $body + $nl + $nl +
    $insB + $nl + $nl +
    $resetBlock

  $text = $text.Substring(0, $idx) + $toInsert + $text.Substring($idx)

  $occ = ([regex]::Matches($text, [regex]::Escape($version))).Count
  if ($occ -ne 3) { throw "$f : post-edit $version count=$occ (expected 3)" }

  $outEnc = New-Object System.Text.UTF8Encoding($hasBom)
  [System.IO.File]::WriteAllText($f, $text, $outEnc)
  Write-Host ("OK {0} (bom={1}, nl={2}, versionOccurrences=3)" -f (Split-Path $f -Leaf), $hasBom, $(if ($nl -eq "`r`n") { 'CRLF' } else { 'LF' }))
}

# --- manifest.json ----------------------------------------------------------
$mBytes = [System.IO.File]::ReadAllBytes($manifestPath)
$mHasBom = ($mBytes.Length -ge 3 -and $mBytes[0] -eq 0xEF -and $mBytes[1] -eq 0xBB -and $mBytes[2] -eq 0xBF)
$mText = [System.Text.Encoding]::UTF8.GetString($mBytes)
if ($mHasBom) { $mText = $mText.TrimStart([char]0xFEFF) }
if ($mText.Contains("`r`n")) { $mNl = "`r`n" } else { $mNl = "`n" }

$anchorTail = "indexname='ux_ce_cotacao_item_tenant_cotacao_rq_item')`"" + $mNl + "    }" + $mNl + "  ],"
$aCnt = ([regex]::Matches($mText, [regex]::Escape($anchorTail))).Count
if ($aCnt -ne 1) { throw "manifest.json : anchor count=$aCnt (expected 1)" }
$entryObj = "    {" + $mNl +
  "      `"version`": `"$version`"," + $mNl +
  "      `"description`": `"$desc`"," + $mNl +
  "      `"category`": `"functional`"," + $mNl +
  "      `"file`": `"${version}_integracao_outbox_base.sql`"," + $mNl +
  "      `"checksum`": `"$sha`"," + $mNl +
  "      `"applyAutomatically`": true," + $mNl +
  "      `"includeInBaseline`": true," + $mNl +
  "      `"dependencies`": [" + $mNl +
  "        `"20260930180000`"" + $mNl +
  "      ]," + $mNl +
  "      `"postConditionSql`": `"select to_regclass('sigov.integracao_outbox') is not null and to_regclass('sigov.ix_integracao_outbox_tenant_tipo') is not null`"" + $mNl +
  "    }"
$mText = $mText.Replace($anchorTail, "indexname='ux_ce_cotacao_item_tenant_cotacao_rq_item')`"" + $mNl + "    }," + $mNl + $entryObj + $mNl + "  ],")
$mOcc = ([regex]::Matches($mText, [regex]::Escape($version))).Count
if ($mOcc -ne 2) { throw "manifest.json : post-edit $version count=$mOcc (expected 2)" }
$mOutEnc = New-Object System.Text.UTF8Encoding($mHasBom)
[System.IO.File]::WriteAllText($manifestPath, $mText, $mOutEnc)
Write-Host ("OK manifest.json (bom={0}, nl={1}, versionOccurrences=2)" -f $mHasBom, $(if ($mNl -eq "`r`n") { 'CRLF' } else { 'LF' }))
Write-Host 'CONSOLIDATED_SYNC_C_DONE'
