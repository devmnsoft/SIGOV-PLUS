param([string]$BaseUrl = $(if ($env:SIGOV_WEB_URL) {$env:SIGOV_WEB_URL} else {'https://localhost:7002'}), [string]$Output = 'artifacts/smoke/rc50_52_pages_result.txt')
$ErrorActionPreference='Continue'; New-Item (Split-Path $Output) -ItemType Directory -Force | Out-Null; Set-Content $Output ''
$failed=$false
foreach($path in @('/Auth/Login','/MinhaCentral','/SystemHealth/ProjectStatus','/Observabilidade/Dashboard','/Seguranca/Dashboard','/Auditoria/Dashboard','/Lgpd/Dashboard')) {
 try {$r=Invoke-WebRequest "$BaseUrl$path" -SkipCertificateCheck -MaximumRedirection 0; $code=[int]$r.StatusCode} catch {$code=[int]$_.Exception.Response.StatusCode}
 $ok=$code -in 200,302,401,403; if(-not $ok){$failed=$true}; "$(if($ok){'OK'}else{'FAIL'}) $path HTTP $code" | Tee-Object -FilePath $Output -Append
}
if($failed){exit 1}
