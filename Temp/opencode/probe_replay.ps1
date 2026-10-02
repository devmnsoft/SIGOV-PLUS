$ErrorActionPreference = 'Stop'
$w = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
$i = $w.IndexOf("operacao='REQUISICAO_ENVIAR' and chave=@key")
Write-Output ('### replay-region IDX=' + $i)
Write-Output $w.Substring($i, 2300)
