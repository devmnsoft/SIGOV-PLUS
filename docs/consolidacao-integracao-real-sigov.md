# Consolidação e integração real SIGOV PLUS

## Fluxos priorizados

1. Protocolo + GED + Workflow + Tarefas + Notificações: persistir somente com tabelas homologadas; caso contrário, manter fallback honesto e auditoria de tentativa.
2. Compras + Licitações + Contratos + Financeiro/SIAFIC: demonstrar etapas administrativas sem simular ato oficial, contrato, empenho ou pagamento.
3. Patrimônio + Almoxarifado + Obras: vincular contratos, bens, inventário e obras apenas onde houver schema e storage validados.
4. Portal/Ouvidoria/Atendimento + Protocolo: proteger dados pessoais, gerar protocolo somente com persistência real disponível.

## Padrão de aceite técnico

- Dapper preservado.
- Migration aditiva e idempotente.
- Auditoria e LGPD aplicadas nas ações críticas.
- Outbox com `correlation_id` para eventos operacionais.
- Busca, Dashboard e Relatórios consultam fontes reais apenas quando a tabela existe.
