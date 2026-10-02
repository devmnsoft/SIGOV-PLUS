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
  if(-not (Test-Path $stg)){ New-Item -ItemType Directory -Path $stg | Out-Null }
  $t=[guid]::NewGuid().ToString('N')
  $jtmp=Join-Path $stg "ja_$t.json"; $htmp=Join-Path $stg "ja_$t.hdr"; $btmp=Join-Path $stg "ja_$t.body"
  $cl='curl.exe -s -X ' + $method + ' --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: ' + $hostFor + '"'
  if($key){ $cl += ' -H "Idempotency-Key: ' + $key + '"' }
  if($null -ne $json){ [IO.File]::WriteAllText($jtmp,$json,(New-Object System.Text.UTF8Encoding($false))); $cl += ' --data-binary @"' + $jtmp + '" -H "Content-Type: application/json; charset=utf-8"' }
  $cl += ' -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
  $err=& cmd.exe /c $cl 2>&1 | Out-String
  $ce=$LASTEXITCODE
  if($ce -ne 0){ Write-Host ("!! curl exit={0} para {1} {2}: {3}" -f $ce,$method,$uri,($err.Trim())) }
  $statusLine=(Get-Content $htmp -ErrorAction SilentlyContinue | Where-Object { $_ -match '^HTTP' } | Select-Object -First 1)
  $code=0
  if($statusLine){ $m=[regex]::Match($statusLine,'(\d{3})'); if($m.Success){ $code=[int]$m.Groups[1].Value } }
  $bodytxt=''; if(Test-Path $btmp){ $bodytxt=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8) }
  foreach($f in @($jtmp,$htmp,$btmp)){ Remove-Item $f -Force -ErrorAction SilentlyContinue }
  $parsed=$null
  if($bodytxt -and ($bodytxt.TrimStart().StartsWith('{') -or $bodytxt.TrimStart().StartsWith('['))){ try{ $parsed=$bodytxt | ConvertFrom-Json }catch{ Write-Host ('!! falha ao parsear JSON: ' + $_.Exception.Message) } }
  return @{ status=$code; body=$bodytxt; json=$parsed }
}
$e1=(JApi 'GET' "/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001" $script:TokA).json
"DEBUG e1 null? $($null -eq $e1)"
$it1=@($e1.itens)[0]; $it2=@($e1.itens)[1]
$IT1=$it1.requisicaoItemId; $IT2=$it2.requisicaoItemId
$Q1=[decimal]$it1.quantidade; $Q2=[decimal]$it2.quantidade
$EI1=$it1.exigeInspecao; $EI2=$it2.exigeInspecao
"DEBUG it1 null? $($null -eq $it1)"
"DEBUG it1 type: $($it1.GetType().FullName)"
if($it1){ "DEBUG props: $(($it1 | Get-Member -MemberType NoteProperty | ForEach-Object { $_.Name }) -join ', ')" }
"DEBUG quantidade raw: [$($it1.quantidade)]"
$dump=$e1 | ConvertTo-Json -Depth 4 -Compress
"DEBUG dump: $($dump.Substring(0,[Math]::Min(300,$dump.Length)))"
"DEBUG Q1=$Q1 Q2=$Q2"