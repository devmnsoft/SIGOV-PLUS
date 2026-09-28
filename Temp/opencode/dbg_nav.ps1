[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$jar='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\nav_cookies.txt'
$lines = Get-Content $jar
$sig = $lines | Where-Object { $_ -match '^\#HttpOnly_municipio-demo\.sigov\.local\s+\S+\s+/.*\sSIGOV\.AUTH\s+' } | Select-Object -First 1
if (-not $sig) { Write-Host 'FATAL: SIGOV.AUTH nao encontrado no jar'; exit 2 }
# colunas netscape: [0]=#HttpOnly_domain [1]=domainMatch [2]=path [3]=secure [4]=expires [5]=name [6..]=value
$cols = $sig -split "`t"
$val = ($cols[6..($cols.Count-1)] -join "`t")
Write-Host ("cookie found len=" + $val.Length)
$url='http://municipio-demo.sigov.local:8080/ComprasEmpresariais/Aprovacoes'
$r = & curl.exe -s --resolve 'municipio-demo.sigov.local:8080:127.0.0.1' -D 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\nav_dbg4_headers.txt' -o 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\nav_dbg4.html' -H ("Cookie: SIGOV.AUTH={0}" -f $val) $url
$h0=(Get-Content 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\nav_dbg4_headers.txt' -TotalCount 1)
Write-Host ("explicit-cookie => " + $h0)
$b=[IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\nav_dbg4.html',[Text.Encoding]::UTF8)
Write-Host ("body=" + $b.Length)
