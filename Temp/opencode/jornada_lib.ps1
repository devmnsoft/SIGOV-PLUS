$script:JBase='http://localhost:5001/api/compras-empresariais'
$script:JOut='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_evidence.txt'
$script:TokA='jornada-analista-rc5068a-2026-sigov-demo-token'
$script:TokB='jornada-gestor-rc5068a-2026-sigov-demo-token'
$script:TokD='jornada-admin-rc5068a-2026-sigov-demo-token'
$script:SubA='700ef0a9-9a4c-4ba2-7156-d2a5edcc5f26'
$script:SubB='dcf09912-e174-4566-3086-078f0dd24378'
$script:SubD='136e6a9a-1552-16d4-0cad-44e4e3ab97de'
$script:Rq1='d0000001-0000-4000-8000-000000000001'
$script:Rq2='d0000001-0000-4000-8000-000000000002'
$script:Rq3='d0000001-0000-4000-8000-000000000003'
$script:Rq4='d0000001-0000-4000-8000-000000000004'

function JLog($m){ [void]([System.IO.File]::AppendAllText($script:JOut, $m + [Environment]::NewLine, [System.Text.Encoding]::UTF8)) }
function JHead($m){ JLog ''; JLog ('===== ' + $m); Write-Host ('===== ' + $m) }

function JApi($method,$path,$token,$json=$null,$key=$null){
  $uri = if($path -match '^http'){$path} else { "$($script:JBase)$path" }
  $hostFor = switch($token){
    $script:TokA { 'municipio-demo.sigov.local' }
    $script:TokB { 'municipio-demo.sigov.local' }
    $script:TokD { 'sigov-local.sigov.local' }
    default { throw 'token sem host mapeado' }
  }
  $h=@{ Authorization="Bearer $token"; Host=$hostFor }
  if($key){ $h['Idempotency-Key']=$key }
  $p=@{ Method=$method; Uri=$uri; Headers=$h; UseBasicParsing=$true; TimeoutSec=90 }
  if($null -ne $json){ $p.Body=[System.Text.Encoding]::UTF8.GetBytes($json); $p.ContentType='application/json; charset=utf-8' }
  try { $r=Invoke-WebRequest @p } catch {
    $resp=$_.Exception.Response
    if($null -eq $resp){ JLog ("!! {0} {1} : SEM RESPOSTA ({2})" -f $method,$path,$_.Exception.Message); return @{ status=-1; body=''; json=$null } }
    $r=$resp
  }
  $txt = $null
  if ($null -ne $r.PSObject.Properties['Content']) { $txt=[string]$r.Content } else { $ms=New-Object System.IO.MemoryStream; [void]$r.GetResponseStream().CopyTo($ms); $txt=[System.Text.Encoding]::UTF8.GetString($ms.ToArray()) }
  JLog ("--- {0} {1} => HTTP {2}" -f $method,$path,$r.StatusCode.value__)
  JLog $txt
  $parsed=$null
  if($txt -and ($txt.TrimStart().StartsWith('{') -or $txt.TrimStart().StartsWith('['))){ try{ $parsed=$txt | ConvertFrom-Json }catch{} }
  return @{ status=[int]$r.StatusCode.value__; body=$txt; json=$parsed }
}

# Resolve o contrato do banco vivo a partir do ambiente assado em sigov-api
# (regra 18: nenhum segredo literal no gate).
function JResolveDbContract(){
  $envline = (docker inspect sigov-api --format '{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'ConnectionStrings__DefaultConnection=*' } | Select-Object -First 1
  if(-not $envline){ throw 'JDb(docker): sigov-api nao encontrado / contrato de conexao ausente' }
  $cs = ($envline -split '=', 2)[1]
  $pairs = @($cs -split ';')
  function GetCv([string]$k) { $p = $pairs | Where-Object { $_ -like "$k=*" } | Select-Object -First 1; if ($p) { $p.Substring($k.Length+1) } else { '' } }
  return @{ db=(GetCv 'Database'); usr=(GetCv 'Username'); pw=(GetCv 'Password') }
}

function JDb($sql){
  $tmp=Join-Path $env:TEMP ('jdb_'+[guid]::NewGuid().ToString('N')+'.sql')
  [System.IO.File]::WriteAllText($tmp,$sql,(New-Object System.Text.UTF8Encoding($true)))
  $exit=0
  if ($script:JDbMode -eq 'docker') {
    $c=JResolveDbContract
    if(-not $c.db -or -not $c.usr -or -not $c.pw){ throw 'JDb(docker): contrato nao resolvido (db/usuario/senha)' }
    [void](docker cp "$tmp" 'sigov-postgres:/tmp/jdb_gate.sql')
    $out = & docker exec -e PGPASSWORD="$($c.pw)" sigov-postgres psql -U "$($c.usr)" -d "$($c.db)" -A -F ' | ' -f /tmp/jdb_gate.sql 2>&1 | Out-String
    $exit=$LASTEXITCODE
    [void](docker exec sigov-postgres rm -f /tmp/jdb_gate.sql)
  } else {
    if([string]::IsNullOrEmpty($env:PGPASSWORD)){ throw 'JDb(local): defina $env:PGPASSWORD antes de usar o modo local' }
    $out = & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -h 127.0.0.1 -U postgres -d sigov_rc_validation -A -F ' | ' -f $tmp 2>&1 | Out-String
    $exit=$LASTEXITCODE
  }
  Remove-Item $tmp -Force
  JLog ('### SQL (exit={0}): {1}' -f $exit, (($sql -replace '\s+',' ').Trim()))
  JLog $out
  Write-Host $out
  return $out
}

function JEtapa($queue,$rq,$nivel,$sub){
  $item=$queue.json.items | Where-Object { $_.requisicaoId -eq $rq -and $_.nivel -eq $nivel -and ($_.aprovadorId -eq $sub -or $_.aprovadorId -eq $null) } | Select-Object -First 1
  return $item
}
