$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$webRoot = Join-Path $root 'src/Sigov.Web'
$findings = New-Object System.Collections.Generic.List[string]

$views = Get-ChildItem -Path (Join-Path $webRoot 'Views') -Recurse -File -Filter '*.cshtml'
foreach ($view in $views) {
  $content = Get-Content $view.FullName -Raw
  foreach ($match in [regex]::Matches($content, '<script[^>]+src=["'']([^"'']+)["'']', 'IgnoreCase')) {
    $src = $match.Groups[1].Value.Split('?')[0]
    if ($src.StartsWith('~/')) {
      $path = Join-Path (Join-Path $webRoot 'wwwroot') $src.Substring(2)
      if (-not (Test-Path $path)) { $findings.Add("Script inexistente em $($view.FullName): $src") }
    }
  }
  foreach ($match in [regex]::Matches($content, '<link[^>]+href=["'']([^"'']+)["'']', 'IgnoreCase')) {
    $href = $match.Groups[1].Value.Split('?')[0]
    if ($href.StartsWith('~/')) {
      $path = Join-Path (Join-Path $webRoot 'wwwroot') $href.Substring(2)
      if (-not (Test-Path $path)) { $findings.Add("CSS inexistente em $($view.FullName): $href") }
    }
  }
  foreach ($match in [regex]::Matches($content, '<partial\s+name=["'']([^"'']+)["'']', 'IgnoreCase')) {
    $partial = $match.Groups[1].Value
    $candidates = @(
      Join-Path $view.DirectoryName ($partial + '.cshtml'),
      Join-Path (Join-Path $webRoot 'Views/Shared') ($partial + '.cshtml')
    )
    if (-not ($candidates | Where-Object { Test-Path $_ })) { $findings.Add("Partial inexistente em $($view.FullName): $partial") }
  }
  if ($content -match '(?is)<form\b[^>]*method=["'']post["'']' -and $content -notmatch 'asp-antiforgery|AntiForgeryToken') {
    $findings.Add("Form POST sem antiforgery explícito: $($view.FullName)")
  }
}

$jsFiles = Get-ChildItem -Path (Join-Path $webRoot 'wwwroot/js') -Recurse -File -Filter '*.js'
foreach ($js in $jsFiles) {
  if ((Get-Item $js.FullName).Length -eq 0) { $findings.Add("Arquivo JS vazio: $($js.FullName)") }
}

if ($findings.Count -gt 0) {
  $findings | ForEach-Object { Write-Error $_ }
  throw "Falhas em assets web: $($findings.Count)"
}
Write-Host 'Assets web MVC/Razor validados com sucesso.'
