$ErrorActionPreference = 'Stop'
$patterns = @('SIGOV_PLUS','SIGOV\+','SIGOV-PLUS','SigovPlus','dbo\.','nvarchar','datetime2','uniqueidentifier','rowversion','schema core\.','schema sec\.','schema audit\.','schema lgpd\.','schema fin\.','schema trib\.','schema rh\.','schema educacao\.','schema saude\.','schema saneamento\.','schema social\.','schema suporte\.','schema operacao\.','schema integracao\.','senha fixa','token em texto puro','secret em texto puro','connection string com senha versionada','stack trace para usuário final','projeto é uma POC','apenas demonstração')
$files = Get-ChildItem -Recurse -File | Where-Object { $_.FullName -notmatch '\\.git\\|bin\\|obj\\' -and $_.FullName -notmatch 'check-residues\.ps1$' }
$findings = @()
foreach ($file in $files) {
  $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
  foreach ($pattern in $patterns) {
    if ($content -match $pattern) { $findings += [pscustomobject]@{ File = $file.FullName; Pattern = $pattern } }
  }
}
if ($findings.Count -gt 0) {
  $findings | Format-Table -AutoSize
  throw "Resíduos encontrados: $($findings.Count)"
}
Write-Host 'Nenhum resíduo crítico encontrado.'
