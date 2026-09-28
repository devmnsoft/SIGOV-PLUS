$ErrorActionPreference='Stop'
foreach($p in @('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_nav.ps1','C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_view_probe.ps1','C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_lib.ps1','C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_jornada.ps1')){
  if(Test-Path $p){
    $c=[IO.File]::ReadAllText($p,[Text.Encoding]::UTF8)
    [IO.File]::WriteAllText($p,$c,(New-Object System.Text.UTF8Encoding($true)))
    Write-Host ('BOM written: ' + $p)
  }
}
