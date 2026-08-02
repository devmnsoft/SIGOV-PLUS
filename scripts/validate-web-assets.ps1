[CmdletBinding()]
param([string]$WebRoot = (Join-Path $PSScriptRoot '../src/Sigov.Web'))

$ErrorActionPreference = 'Stop'
$views = Join-Path $WebRoot 'Views'
$wwwroot = Join-Path $WebRoot 'wwwroot'
$sprite = Join-Path $wwwroot 'icons/sigov-icons.svg'
$registry = Join-Path $WebRoot 'Services/Visual/IconRegistry.cs'
$aliasesFile = Join-Path $wwwroot 'icons/icons-allowed-aliases.json'
$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path $sprite)) { $errors.Add("Sprite canônico ausente: $sprite") }
else {
  [xml]$spriteXml = Get-Content $sprite -Raw
  $symbols = @($spriteXml.svg.symbol)
  $ids = @($symbols | ForEach-Object { $_.id })
  $duplicates = $ids | Group-Object | Where-Object Count -gt 1
  foreach ($duplicate in $duplicates) { $errors.Add("ID SVG duplicado: $($duplicate.Name)") }
  if ((Get-Content $sprite -Raw) -match '<script\b') { $errors.Add('Script não autorizado no sprite SVG') }
  $aliases = if (Test-Path $aliasesFile) { @((Get-Content $aliasesFile -Raw | ConvertFrom-Json).aliases) } else { @() }
  $geometry = $symbols | ForEach-Object {
    if (-not $_.viewBox) { $errors.Add("viewBox ausente: $($_.id)") }
    elseif ($_.viewBox -ne '0 0 24 24') { $errors.Add("viewBox não canônico: $($_.id)") }
    $xml = ($_.InnerXml -replace '\s+', ' ' -replace '>\s+<', '><').Trim()
    [pscustomobject]@{ Id = $_.id; Hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($xml))) }
  }
  $geometry | Group-Object Hash | Where-Object Count -gt 1 | ForEach-Object {
    $names = @($_.Group.Id | Sort-Object)
    $aliasKey = $names -join '|'
    if ($aliases -notcontains $aliasKey) { $errors.Add("Geometria duplicada sem alias autorizado: $($names -join ', ')") }
  }
  $spriteText = Get-Content $sprite -Raw
  if ($spriteText -match '(?:fill|stroke)="(?!none|currentColor)[^\"]+"') { $errors.Add('Cor fixa não autorizada no sprite') }

  $registered = [regex]::Matches((Get-Content $registry -Raw), '"([a-z][a-z-]+)"') |
    ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notin @('navigation', 'action', 'state') } | Sort-Object -Unique
  foreach ($name in $registered) {
    if ($ids -notcontains "sigov-icon-$name") { $errors.Add("Ícone registrado sem símbolo: $name") }
  }
  foreach ($id in $ids) {
    $name = $id -replace '^sigov-icon-', ''
    if ($registered -notcontains $name) { $errors.Add("Símbolo sem registro: $id") }
  }

  Get-ChildItem $views -Filter *.cshtml -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    foreach ($match in [regex]::Matches($content, '<sigov-icon(?:-button)?\b[^>]*(?:name|icon)="([^"]+)"')) {
      if ($ids -notcontains "sigov-icon-$($match.Groups[1].Value)") { $errors.Add("Ícone não catalogado em $($_.FullName): $($match.Groups[1].Value)") }
    }
    foreach ($match in [regex]::Matches($content, '<img\b[^>]*>')) {
      if ($match.Value -notmatch '\balt=') { $errors.Add("Imagem sem alt em $($_.FullName)") }
    }
    if ($content -match '<(?:img|script|link)\b[^>]+(?:src|href)="https?://') { $errors.Add("Asset visual externo em $($_.FullName)") }
    if ($_.Name -ne '_IconSprite.cshtml' -and $content -match '<svg\b') { $errors.Add("SVG inline fora do sprite compartilhado em $($_.FullName)") }
  }
}

Get-ChildItem $wwwroot -Filter *.svg -Recurse | ForEach-Object {
  try { [xml](Get-Content $_.FullName -Raw) | Out-Null } catch { $errors.Add("SVG inválido: $($_.FullName)") }
  if ((Get-Content $_.FullName -Raw) -match '<script\b') { $errors.Add("Script não autorizado em SVG: $($_.FullName)") }
}

if ($errors.Count -gt 0) { $errors | ForEach-Object { Write-Error $_ }; exit 1 }
Write-Host "Assets web validados: sprite, catálogo, SVGs, acessibilidade básica e dependências externas."
