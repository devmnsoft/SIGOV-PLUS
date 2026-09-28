[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$here = Split-Path $MyInvocation.MyCommand.Path -Parent
. (Join-Path $here 'jornada_lib.ps1')
$script:JDbMode='docker'

$sql=@"
select r.numero, r.status, r.version, r.valor_estimado, r.ciclo_aprovacao
from sigov.compras_empresarial_requisicao r
where r.id in ('d0000001-0000-4000-8000-000000000001','d0000001-0000-4000-8000-000000000002','d0000001-0000-4000-8000-000000000003','d0000001-0000-4000-8000-000000000004')
order by r.numero;
select count(*) as politica from sigov.compras_empresarial_aprovacao_politica;
select count(*) as etapas from sigov.compras_empresarial_aprovacao;
select count(*) as chaves_jornada from sigov.compras_empresarial_idempotencia where chave like 'jornada-%';
select count(*) as sessoes_gate from sigov.identidade_sessao where user_agent_sanitizado='gate-jornada-rc5068a';
"@
JDb $sql
