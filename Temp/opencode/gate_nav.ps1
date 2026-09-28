[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$here=Split-Path $MyInvocation.MyCommand.Path -Parent
Set-Location C:\MNSOFT\SIGOV-PLUS

# regra 18: nenhuma senha literal no gate — a credencial Development canônica
# é extraída da fonte (const DevelopmentAuthDiagnosticService.AdminPassword)
$src = Get-Content 'src\Sigov.Web\Services\Development\DevelopmentAuthDiagnosticService.cs' -Raw
if ($src -notmatch 'AdminPassword\s*=\s*"([^"]+)"') { Write-Host 'FATAL: senha canônica Development nao encontrada na fonte'; exit 2 }
$senha = $Matches[1]

$hostHeader='municipio-demo.sigov.local'
# delimitador {} obrigatorio: "$var:8080..." seria lido pelo PS como variavel drive-qualificada
$base="http://${hostHeader}:8080"
$resolve="${hostHeader}:8080:127.0.0.1"
$jar="$here\nav_cookies.txt"
Remove-Item $jar -Force -ErrorAction SilentlyContinue
$returnUrl='%2FComprasEmpresariais%2FAprovacoes'

# passo 1: GET do login (anti-forgery + cookies)
$g = curl.exe -s --resolve $resolve -c $jar -D "$here\nav_login_headers.txt" "$base/Auth/Login?ReturnUrl=$returnUrl"
$loginHtml = [string]$g
Write-Host "P1 GET login: $($loginHtml.Length) chars"
$inputTag = [regex]::Match($loginHtml,'<input[^>]*__RequestVerificationToken[^>]*>')
if ($inputTag.Success -and $inputTag.Value -match 'value="([^"]+)"') {
  $token = $Matches[1]
} else {
  Write-Host 'FATAL: __RequestVerificationToken nao encontrado na pagina de login'; exit 3
}
Write-Host "P1 token presente (len=$($token.Length))"

# passo 2: POST do login como o analista demo (tenant unico -> sem ambiguidade de organização)
$p = curl.exe -s --resolve $resolve -b $jar -c $jar -D "$here\nav_post_headers.txt" -o "$here\nav_post_body.html" -X POST `
  --data-urlencode 'Login=compras.demo.analista' `
  --data-urlencode "Senha=$senha" `
  --data-urlencode "__RequestVerificationToken=$token" `
  "$base/Auth/Login?ReturnUrl=$returnUrl"
$code = (Select-String -Path "$here\nav_post_headers.txt" -Pattern '^HTTP' | Select-Object -First 1).Line.Trim()
$loc  = (Select-String -Path "$here\nav_post_headers.txt" -Pattern '^Location:' | Select-Object -First 1).Line
Write-Host "P2 POST login: $code | $loc"
if ($code -notlike '*302*') {
  $body=[IO.File]::ReadAllText("$here\nav_post_body.html",[Text.Encoding]::UTF8)
  $err = [regex]::Matches($body,'MensagemErro[^>]*>\s*([^<]+)<') | Select-Object -First 1
  Write-Host "P2 CORPO: $($body.Length) chars; erro=$(if($err){$err.Groups[1].Value}else{'<sem MensagemErro>'})"
  exit 4
}

# passo 3: página de aprovações autenticada
$h = curl.exe -s --resolve $resolve -b $jar -D "$here\nav_page_headers.txt" -o "$here\nav_evidence.html" "$base/ComprasEmpresariais/Aprovacoes"
$pageCode = (Select-String -Path "$here\nav_page_headers.txt" -Pattern '^HTTP' | Select-Object -First 1).Line.Trim()
Write-Host "P3 GET Aprovacoes: $pageCode"
$html=[IO.File]::ReadAllText("$here\nav_evidence.html",[Text.Encoding]::UTF8)
Write-Host "saved -> $here\nav_evidence.html ($($html.Length) chars)"
if ($pageCode -notlike '*200*') { Write-Host 'FATAL: pagina de aprovacoes nao retornou 200'; exit 5 }

$fail=0
function Check([string]$label,[string]$needle,[bool]$mandatory=$true){
  $ok = $html.Contains($needle)
  $tag = if($mandatory){'OBRIG'}else{'DADO '}
  if(-not $ok -and $mandatory){ $script:fail=1 }
  Write-Host ("[{0}] {1,-46} {2}" -f $tag,$label,$(if($ok){'SIM'}else{'NAO'}))
}

# estrutura permanente da tela (renderizada mesmo com fila vazia)
Check 'nav parcial <nav class=compras-nav>'    '<nav class="compras-nav"'
Check 'nav separador (compras-nav-sep)'        'compras-nav-sep'
Check 'header Fila de aprovações'              'Fila de aprovações'
Check 'painel 1 Pendências de decisão'         'Pendências de decisão'
Check 'painel 2 Devolvidas para correção'      'Devolvidas para correção'
Check 'painel 3 Concluídas recentemente'       'Concluídas recentemente'
Check 'ajuda da jornada'                       'Ajuda da jornada'
Check 'busca field'                            'name="busca"'
Check 'urgencia select'                        'name="urgencia"'
Check 'exportar csv'                           'Exportar CSV'

# marcadores dependentes de dados (SIM após a jornada comportamental)
Check 'link Detalhe na fila'                   '/ComprasEmpresariais/Aprovacoes/' $false
Check 'marca Decisão sua (linha decidível)'    'Decisão sua' $false
Check 'marca Aguarda configuração (bloqueada)' 'Aguarda configuração' $false

Write-Host '--- active marker ---'
$m=[regex]::Matches($html,'aria-current="page"')
Write-Host ("aria-current=page occurrences: {0}" -f $m.Count)
if ($m.Count -lt 1) { $fail=1; Write-Host 'FATAL: nenhum link ativo marcado com aria-current=page' }
exit $fail
