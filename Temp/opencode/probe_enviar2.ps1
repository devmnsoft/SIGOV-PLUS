$path='C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs'
$lines=[IO.File]::ReadAllText($path) -split "`r?`n"
for($li=78;$li -lt 82;$li++){
  $line80=$lines[$li]
  foreach($needle in @('ALCADA_INSUFICIENTE','SEM_APROVADOR','SEM_POLITICA','topoAlcada','alcadaInsuficiente')){
    $start=0; $count=0
    while(($i=$line80.IndexOf($needle,$start)) -ge 0){
      $count++
      $s=[Math]::Max(0,$i-500); $len=[Math]::Min(1100,$line80.Length-$s)
      Write-Output ("### line{0} {1} occ{2} @{3}" -f ($li+1),$needle,$count,$i)
      Write-Output $line80.Substring($s,$len)
      Write-Output '====='
      $start=$i+$needle.Length
    }
  }
}
