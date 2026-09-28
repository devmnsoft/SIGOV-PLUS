$ErrorActionPreference='Stop'
. 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_lib.ps1'

JHead 'J0 fila de aprovacoes inicial (analista) - expectativa: 200 vazia'
$r = JApi GET '/aprovacoes?pagina=1&tamanho=50' $script:TokA
Write-Host ('J0 status=' + $r.status)

JHead 'J1 enviar RC-DEMO-0002 (40 mil) ANTES de existir politica => ciclo bloqueado nivel 1 limite 0 + pendencia APROVACAO_SEM_POLITICA'
$r = JApi POST ("/requisicoes/{0}/enviar?version=1" -f $script:Rq2) $script:TokA $null 'jr-envio-rq2'
Write-Host ('J1 status=' + $r.status)

JDb @'
select r.numero, r.status, r.valor_estimado, r.version from sigov.compras_empresarial_requisicao r where r.id='d0000001-0000-4000-8000-000000000002';
select a.nivel,a.limite,a.status,a.aprovador_id,a.ciclo,a.version from sigov.compras_empresarial_aprovacao a where a.requisicao_id='d0000001-0000-4000-8000-000000000002' order by a.ciclo,a.nivel;
select p.modulo,p.recurso,p.tipo,p.entidade,p.entidade_id,p.gravidade,p.status,p.titulo from sigov.pendencia_operacional p where p.entidade='compras_empresarial_requisicao' and p.entidade_id='d0000001-0000-4000-8000-000000000002';
select h.aggregate_id::text,h.acao,h.detalhes,h.created_by from sigov.compras_empresarial_historico h where h.aggregate_id='d0000001-0000-4000-8000-000000000002' order by h.id;
'@
