$ErrorActionPreference = "Stop"
$views = Get-ChildItem -Path "src/Sigov.Web/Views" -Filter "*.cshtml" -Recurse
$issues = New-Object System.Collections.Generic.List[string]
foreach ($view in $views) {
  $content = Get-Content $view.FullName -Raw
  if ($content -match '<img\b' -and $content -notmatch 'alt=') { $issues.Add("Imagem sem alt em $($view.FullName)") }
  if ($content -match '<form\b[^>]*method="post"' -and $content -notmatch 'AntiForgeryToken') { $issues.Add("Form POST sem antiforgery em $($view.FullName)") }
  if ($content -match '<button\b[^>]*>\s*</button>' -and $content -notmatch 'aria-label=') { $issues.Add("Botão sem texto/aria-label em $($view.FullName)") }
}
if ($issues.Count -gt 0) {
  $issues | ForEach-Object { Write-Warning $_ }
  exit 1
}
Write-Host "Acessibilidade básica SIGOV validada."
