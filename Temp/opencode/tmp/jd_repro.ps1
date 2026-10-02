[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
function JD([string]$d,[long]$v,[string]$k){ "ver=$v" }
$t='{"items":[{"etapaId":"31eb5caf","requisicaoId":"d0000001-0000-4000-8000-000000000001","nivel":1,"version":1,"bloqueada":false,"decisivelPorMim":true},{"etapaId":"44c48808","requisicaoId":"d0000001-0000-4000-8000-000000000001","nivel":2,"version":1,"bloqueada":false,"decisivelPorMim":false}]}'
$f=$t | ConvertFrom-Json
"Gq1 test:"
$Gq1=[guid]'d0000001-0000-4000-8000-000000000001'
$n2i=@($f.items) | Where-Object { $_.requisicaoId -eq $Gq1 -and $_.nivel -eq 2 } | Select-Object -First 1
"n2i null? $($null -eq $n2i)"
if($n2i){ "version raw: [$($n2i.version)] type=$($n2i.version.GetType().Name)" }
try{ $r=JD 'APROVAR' ([long]$n2i.version) 'jb-probe-rq1-d2-antes'; "JD OK -> $r" }catch{ "JD THROW: $($_.Exception.Message)" }
# agora sem parens no cast (como aparece na chamada real: 'APROVAR' [long]$x 'k')
try{ $r2=JD 'APROVAR' [long]$n2i.version 'jb-probe-rq1-d2-antes'; "JD2 OK -> $r2" }catch{ "JD2 THROW: $($_.Exception.Message)" }
# caso null
try{ $r3=JD 'APROVAR' [long]$null.version 'k'; "JD3 OK -> $r3" }catch{ "JD3 THROW: $($_.Exception.Message)" }
