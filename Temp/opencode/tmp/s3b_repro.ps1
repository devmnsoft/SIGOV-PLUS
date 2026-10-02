[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
. (Join-Path $PSScriptRoot '..\jornada_lib.ps1')
$script:JDbMode='none'
function JApi($method,$path,$token,$json=$null,$key=$null){
  $uri = if($path -match '^http'){$path} else { "$($script:JBase)$path" }
  $hostFor = switch($token){
    $script:TokA { 'municipio-demo.sigov.local' }
    $script:TokB { 'municipio-demo.sigov.local' }
    $script:TokD { 'sigov-local.sigov.local' }
    default { throw 'token sem host mapeado' }
  }
  $stg=Join-Path $PSScriptRoot '.'
  $t=[guid]::NewGuid().ToString('N')
  $htmp=Join-Path $stg "ja_$t.hdr"; $btmp=Join-Path $stg "ja_$t.body"
  $cl='curl.exe -s -X ' + $method + ' --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: ' + $hostFor + '"'
  $cl += ' -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
  & cmd.exe /c $cl 2>&1 | Out-Null
  $bodytxt=''; if(Test-Path $btmp){ $bodytxt=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8) }
  $parsed=$null
  if($bodytxt -and ($bodytxt.TrimStart().StartsWith('{'))){ try{ $parsed=$bodytxt | ConvertFrom-Json }catch{ Write-Host ('!! parse fail: ' + $_.Exception.Message) } }
  return @{ status=200; body=$bodytxt; json=$parsed }
}
$e1=(JApi 'GET' '/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001' $script:TokA).json
"itens type: $($e1.itens.GetType().FullName)"
$it1=@($e1.itens)[0]
"it1 type: $($it1.GetType().FullName)"
if($it1 -is [string]){
  "it1 len: $($it1.Length)"
  "it1 head: [$($it1.Substring(0,[Math]::Min(100,$it1.Length)))]"
  "it1 tail: [$($it1.Substring([Math]::Max(0,$it1.Length-60)))]"
}
$b=(JApi 'GET' '/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001' $script:TokA).body
$i=$b.IndexOf('itens')
"raw ctx: [$($b.Substring([Math]::Max(0,$i-5),[Math]::Min(160,$b.Length-[Math]::Max(0,$i-5))))]"
