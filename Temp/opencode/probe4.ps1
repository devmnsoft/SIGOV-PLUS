$ErrorActionPreference='Continue'
$a='a'; $b='b'
$t1 = (('a','b') | Where-Object { $false }).Count
$t2 = (('a','b') | Where-Object { $_ -eq 'a' }).Count
$t3 = (('a','b') | Where-Object { $_ -ne 'z' }).Count
$t4 = @(('a','b') | Where-Object { $_ -eq 'a' }).Count
$t5 = (('a','b') | Where-Object { $_ -eq 'a' } | Measure-Object).Count
$nullc = $null
$t6 = $nullc.Count
Write-Output ('t1 zero-match  .Count      : [' + $t1 + '] isNull=' + ($null -eq $t1))
Write-Output ('t2 one-match   .Count      : [' + $t2 + '] isNull=' + ($null -eq $t2))
Write-Output ('t3 two-match   .Count      : [' + $t3 + '] isNull=' + ($null -eq $t3))
Write-Output ('t4 @() 1-match .Count      : [' + $t4 + '] isNull=' + ($null -eq $t4))
Write-Output ('t5 MeasureObj  .Count      : [' + $t5 + '] isNull=' + ($null -eq $t5))
Write-Output ('t6 $null.Count             : [' + $t6 + '] isNull=' + ($null -eq $t6))
Write-Output ('test: t1 -eq 0 -> ' + ($t1 -eq 0))
Write-Output ('test: t2 -eq 1 -> ' + ($t2 -eq 1))
Write-Output 'PROBE4_DONE'
