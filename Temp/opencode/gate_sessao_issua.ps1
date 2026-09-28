[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
. (Join-Path $PSScriptRoot 'jornada_lib.ps1')
$script:JDbMode='docker'

function JVal($sql){
  $out=JDb $sql
  $line=($out -split "`r?`n" | Where-Object { $_.Trim() -and $_ -notlike '### SQL*' -and $_ -notmatch '^\(\d+ rows?\)$' } | Select-Object -Last 1)
  return ([string]$line).Trim()
}

# standalone: session issuance mirror of IdentitySessionService.CreateAsync
function JIssua([long]$uid,[long]$tid,[long]$ent,[long]$exer,[string]$tok,[string]$corr){
  $exExpr = if($exer -gt 0){ "$exer" } else { 'null' }
  [void](JDb ("delete from sigov.identidade_sessao where token_hash = encode(sha256(convert_to('$tok','UTF8')),'hex'); insert into sigov.identidade_sessao (tenant_id, entidade_id, exercicio_id, usuario_id, token_hash, expira_at, auth_version, user_agent_sanitizado, correlation_id, created_by) values ($tid, $ent, $exExpr, $uid, encode(sha256(convert_to('$tok','UTF8')),'hex'), now() + interval '12 hours', 1, 'gate-jornada-rc5068a', '$corr', $uid);"))
  $n=JVal "select count(*) from sigov.identidade_sessao where correlation_id='$corr';"
  Write-Host ("issua uid={0} tid={1} ent={2} => sessoes={3}" -f $uid,$tid,$ent,$n)
}
JIssua 101 1 9101 0 $script:TokA 'f1000000-0000-4000-8000-0000000000a1'
JIssua 102 1 9101 0 $script:TokB 'f1000000-0000-4000-8000-0000000000b2'
JIssua 1   5 1    1 $script:TokD 'f1000000-0000-4000-8000-0000000000c3'

# prova: GET detalhe Rq1 como analista (deve responder 200 RASCUNHO, nao 401)
$t=[guid]::NewGuid().ToString('N')
$jtmp="$env:TEMP\ja_$t.json"; $htmp="$env:TEMP\ja_$t.hdr"; $btmp="$env:TEMP\ja_$t.body"
$args=@('-s','-X','GET','--max-time','90','-H',"Authorization: Bearer $($script:TokA)",'-H','Host: municipio-demo.sigov.local','-D',$htmp,'-o',$btmp,"$($script:JBase)/requisicoes/$($script:Rq1)")
& curl.exe @args 2>&1 | Out-String
$statusLine=(Get-Content $htmp -ErrorAction SilentlyContinue | Where-Object { $_ -match '^HTTP' } | Select-Object -First 1)
Write-Host ("STATUS: {0}" -f $statusLine)
Write-Host ("BODY: {0}" -f ([IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8)))
foreach($f in @($jtmp,$htmp,$btmp)){ Remove-Item $f -Force -ErrorAction SilentlyContinue }
