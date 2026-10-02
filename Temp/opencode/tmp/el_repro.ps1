[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
function JEqD($a,$b){ [math]::Abs([decimal][double]$a - [decimal]$b) -lt ([decimal]0.005) }
$j='{"items":[{"requisicaoId":"d0000001-0000-4000-8000-000000000002","numero":"RC-DEMO-0002","valorEstimado":40000.0,"itensElegiveis":1,"saldoDisponivelTotal":8.0},{"requisicaoId":"d0000001-0000-4000-8000-000000000001","numero":"RC-DEMO-0001","valorEstimado":80000.0,"itensElegiveis":2,"saldoDisponivelTotal":15.0}]}'
$el=$j | ConvertFrom-Json
$Gq1=[guid]'d0000001-0000-4000-8000-000000000001'
$Gq2=[guid]'d0000001-0000-4000-8000-000000000002'
$Gq4=[guid]'d0000001-0000-4000-8000-000000000004'
$el1=@($el.items) | Where-Object { $_.requisicaoId -eq $Gq1 } | Select-Object -First 1
$el2=@($el.items) | Where-Object { $_.requisicaoId -eq $Gq2 } | Select-Object -First 1
$el4=@($el.items) | Where-Object { $_.requisicaoId -eq $Gq4 } | Select-Object -First 1
$Q1=[decimal]10; $Q2=[decimal]5; $Q3=[decimal]8
"el1 found: $($null -ne $el1); el2: $($null -ne $el2); el4 null: $($null -eq $el4)"
"c1: $($null -ne $el1)"
"c2: $($el1.itensElegiveis -eq 2)"
$c3=JEqD $el1.saldoDisponivelTotal ([decimal]($Q1+$Q2)); "c3: $c3"
"c4: $($null -ne $el2)"
"c5: $($el2.itensElegiveis -eq 1)"
$c6=JEqD $el2.saldoDisponivelTotal $Q3; "c6: $c6"
"c7: $($null -eq $el4)"
$r = (($null -ne $el1) -and $el1.itensElegiveis -eq 2 -and (JEqD $el1.saldoDisponivelTotal ([decimal]($Q1+$Q2))) -and ($null -ne $el2) -and $el2.itensElegiveis -eq 1 -and (JEqD $el2.saldoDisponivelTotal $Q3) -and ($null -eq $el4))
"FULL: $r"
