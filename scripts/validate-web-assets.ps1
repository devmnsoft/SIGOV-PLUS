[CmdletBinding()]
param([string]$WebRoot = (Join-Path $PSScriptRoot '../src/Sigov.Web'))

$ErrorActionPreference = 'Stop'
$views = Join-Path $WebRoot 'Views'
$wwwroot = Join-Path $WebRoot 'wwwroot'
$sprite = Join-Path $wwwroot 'icons/sigov-icons.svg'
$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path $sprite)) { $errors.Add("Sprite canônico ausente: $sprite") }
else {
  [xml]$spriteXml = Get-Content $sprite -Raw
  $ids = @($spriteXml.svg.symbol | ForEach-Object { $_.id })
  $duplicates = $ids | Group-Object | Where-Object Count -gt 1
  foreach ($duplicate in $duplicates) { $errors.Add("ID SVG duplicado: $($duplicate.Name)") }
  if ((Get-Content $sprite -Raw) -match '<script\b') { $errors.Add('Script não autorizado no sprite SVG') }

  Get-ChildItem $views -Filter *.cshtml -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    foreach ($match in [regex]::Matches($content, '<sigov-icon(?:-button)?\b[^>]*(?:name|icon)="([^"]+)"')) {
      if ($ids -notcontains $match.Groups[1].Value) { $errors.Add("Ícone não catalogado em $($_.FullName): $($match.Groups[1].Value)") }
    }
    foreach ($match in [regex]::Matches($content, '<img\b[^>]*>')) {
      if ($match.Value -notmatch '\balt=') { $errors.Add("Imagem sem alt em $($_.FullName)") }
    }
    if ($content -match '<(?:img|script|link)\b[^>]+(?:src|href)="https?://') { $errors.Add("Asset visual externo em $($_.FullName)") }
  }
}

Get-ChildItem $wwwroot -Filter *.svg -Recurse | ForEach-Object {
  try { [xml](Get-Content $_.FullName -Raw) | Out-Null } catch { $errors.Add("SVG inválido: $($_.FullName)") }
  if ((Get-Content $_.FullName -Raw) -match '<script\b') { $errors.Add("Script não autorizado em SVG: $($_.FullName)") }
}

if ($errors.Count -gt 0) { $errors | ForEach-Object { Write-Error $_ }; exit 1 }
Write-Host "Assets web validados: sprite, catálogo, SVGs, acessibilidade básica e dependências externas."
