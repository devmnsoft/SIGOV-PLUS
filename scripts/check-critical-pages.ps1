param([string]$BaseUrl = $(if ($env:SIGOV_WEB_BASE_URL) {$env:SIGOV_WEB_BASE_URL} else {'http://localhost:5002'}), [string]$ApiUrl = $(if ($env:SIGOV_API_BASE_URL) {$env:SIGOV_API_BASE_URL} else {'http://localhost:5001'}), [string]$Output = 'artifacts/smoke/rc50_54_critical_pages_result.txt')
$ErrorActionPreference='Continue'; New-Item (Split-Path $Output) -ItemType Directory -Force | Out-Null; Set-Content $Output ''
$failed=$false
foreach($path in @('/health','/api/observabilidade/health','/api/observabilidade/liveness','/swagger/v1/swagger.json')) {
 try {$r=Invoke-WebRequest "$ApiUrl$path" -SkipCertificateCheck -MaximumRedirection 0; $code=[int]$r.StatusCode} catch {$code=[int]$_.Exception.Response.StatusCode}
 $ok=$code -in 200,302,401,403; if(-not $ok){$failed=$true}; "$(if($ok){'OK'}else{'FAIL'}) API $path HTTP $code" | Tee-Object -FilePath $Output -Append
}
foreach($path in @('/Auth/Login','/MinhaCentral','/SystemHealth/ProjectStatus','/Observabilidade/Dashboard','/Seguranca/Dashboard','/Auditoria/Dashboard','/Lgpd/Dashboard','/Tributario/Dashboard','/Educacao/Dashboard','/Saude/Dashboard','/Saneamento/Dashboard')) {
 try {$r=Invoke-WebRequest "$BaseUrl$path" -SkipCertificateCheck -MaximumRedirection 0; $code=[int]$r.StatusCode} catch {$code=[int]$_.Exception.Response.StatusCode}
 $ok=$code -in 200,302,401,403; if(-not $ok){$failed=$true}; "$(if($ok){'OK'}else{'FAIL'}) $path HTTP $code" | Tee-Object -FilePath $Output -Append
}
if($failed){exit 1}
