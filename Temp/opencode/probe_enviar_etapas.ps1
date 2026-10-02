$ErrorActionPreference = 'Stop'
$w = [System.IO.File]::ReadAllText('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs', [System.Text.Encoding]::UTF8)
$i = $w.IndexOf('var nucleo=await AprovacaoRequisicaoRepository.ResolverTenantNucleoAsync')
Write-Output ('### IDX=' + $i)
if ($i -ge 0) { Write-Output $w.Substring($i, 3400) }
