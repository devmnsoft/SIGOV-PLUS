$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$views = Get-ChildItem (Join-Path $root 'src/Sigov.Web/Views') -Recurse -Filter '*.cshtml'
$violations = @()
foreach ($view in $views) {
  $content = Get-Content $view.FullName -Raw
  if ($content -match '<form' -and $content -match 'method="post"' -and $content -notmatch 'AntiForgeryToken') {
    $violations += $view.FullName
  }
}
if ($violations.Count -gt 0) { $violations | ForEach-Object { Write-Host $_ }; throw 'Forms POST sem antiforgery encontrados.' }
Write-Host 'Forms POST com antiforgery verificados.'
