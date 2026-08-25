# FUNC15 — Ouvidoria, Atendimento ao Cidadão, e-SIC e Carta de Serviços

## Escopo entregue
Módulo MVC operacional com dashboard, cidadãos, Carta de Serviços, demandas, Ouvidoria, e-SIC/LAI, encaminhamentos, agendas, agendamentos, SLA/alertas, satisfação, base de conhecimento, relatórios e auditoria. Não há armazenamento de documentos: integrações aceitam somente referência textual e metadados.

## Banco e tabelas
A migration `20260825090000_func15_ouvidoria_atendimento_esic.sql` cria as 17 tabelas `sigov.atendimento_*` requeridas: cidadão, contato, serviço, canal, demanda, manifestação, pedido e-SIC, encaminhamento, histórico, agenda, agendamento, parâmetro SLA, alerta, satisfação, base de conhecimento, referência de integração e auditoria. Todas têm contexto institucional, exclusão lógica e trilha de autoria; constraints protegem fluxos e justificativas.

## RBAC e rotas
As 25 permissões `ATENDIMENTO_*`, `OUVIDORIA_*` e `ESIC_*` são persistidas e cadastradas no catálogo fail-closed. Rotas: `/AtendimentoCidadao`, `/Dashboard`, `/Cidadaos`, `/CartaServicos`, `/Demandas`, `/Ouvidoria`, `/Esic`, `/Encaminhamentos`, `/Agendas`, `/Agendamentos`, `/Sla`, `/Satisfacao`, `/BaseConhecimento`, `/Relatorios` e `/Auditoria`.

## Relatórios
CSV de cidadãos, serviços, demandas, Ouvidoria, e-SIC, encaminhamentos, agendamentos, SLA/atrasos, satisfação e auditoria. Células iniciadas por `=`, `+`, `-` ou `@` são neutralizadas.

## Segurança e LGPD
Queries filtram `tenant_id`, `entidade_id` e `is_deleted=false`; SQL parametrizado; escrita transacional com auditoria; CSRF; CPF/CNPJ opcional; consentimento; exclusão lógica; conteúdo de manifestação sigilosa é mascarado sem `OUVIDORIA_SIGILO_VIEW`. Contexto ausente falha explicitamente.

## Limites
InovaGED, GED e Protocolo não foram alterados. Não foram criados testes ou dados fictícios. Prazo e-SIC é parametrizável em `atendimento_sla_parametro`.
