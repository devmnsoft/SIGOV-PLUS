# Diagnóstico de consolidação Pós-RC 13

Base analisada: PR #111 como referência funcional declarada e artefatos atuais de `EnterpriseModulesController`, `EnterpriseDapperCrudService`, `enterprise-crud.js`, `enterprise-form-metadata.js`, `ModulePage.cshtml`, migration `enterprise_anexo`, importação CSV, ações em lote, GED, Protocolo, Minha Central, Dashboard, Busca, Relatórios, Notificações, Tarefas, Outbox, Auditoria, Agenda, SLA, menus, layout, smoke, CI/CD, Docker, seeds e migrations.

> Critério: nenhuma funcionalidade é marcada como homologada sem execução runtime. Quando a dependência é PostgreSQL, storage, autenticação ou provedor externo, o status indica fallback honesto ou pendência de runtime.

| Área | Funcionalidade | Status atual | Risco | Evolução necessária | Prioridade |
|---|---|---|---|---|---|
| Enterprise | CRUD por tenant | Funcional real / Pendente de runtime | Migration não aplicada impede persistência | Validar PostgreSQL com seed idempotente e smoke | Alta |
| Enterprise | `EnterpriseModulesController` | Parcial | Rotas amplas exigem tenant/permissão e fallback consistente | Ampliar evidência de importação, lote e anexos | Alta |
| Enterprise | `EnterpriseDapperCrudService` | Funcional real / Fallback honesto | Schema ausente retorna indisponibilidade | Cobrir jornadas proposta→pedido→OS e estoque | Alta |
| Front Enterprise | `enterprise-crud.js` e metadados | Parcial | Ação crítica sem feedback causa falso sucesso | Manter confirmação, resultado parcial e relatório | Alta |
| Enterprise | `ModulePage.cshtml` detalhe/offcanvas | Parcial | Abas/timeline dependem de APIs reais | Consolidar abas Resumo, Dados, Ações, Anexos, Auditoria, Tarefas, Notificações e Timeline | Média |
| GED/Enterprise | `sigov.enterprise_anexo` | Fallback honesto | Storage/GED indisponível pode quebrar download | Usar 503 honesto, auditar acesso e nunca expor storage path | Alta |
| CSV | Import template/preview/confirm | Parcial | Duplicidade/tipos/status podem falhar em runtime | Validar colunas, tipos, duplicidade e gerar relatório | Alta |
| Lote | `POST /api/enterprise/{area}/batch` | Parcial | Sucesso parcial precisa UX clara | Retornar item a item, 409 se todos falharem e relatório | Alta |
| Protocolo | CRUD/tramitação | Funcional real / Pendente de runtime | Seed/migration local ausente | Smoke criar e tramitar protocolo | Alta |
| GED | Documento/anexo/validação | Parcial / Dependente de provedor | Storage externo indisponível | Fallback 503 e validação de documento | Alta |
| Minha Central | Pendências e atalhos | Parcial | Cards demonstrativos podem confundir | Exibir fonte real/fallback, tenant, perfil, ações com rotas reais | Alta |
| Dashboard | KPIs e drill-down | Parcial | KPI demo sem fonte | Cards com badge de fonte e links filtrados | Alta |
| Busca | Busca global | Parcial | Escopo incompleto | Incluir Enterprise, anexos, importações, lote, tarefas e notificações | Média |
| Relatórios | CSV seguro | Parcial | Fórmula maliciosa/dado pessoal | UTF-8 BOM, mascaramento, tenant e auditoria | Alta |
| Notificações | Eventos operacionais | Parcial | Evento não cria tarefa/outbox | Centralizar em serviço operacional idempotente | Alta |
| Tarefas | Automáticas e Kanban | Parcial | Status incompatível | Regras por evento, SLA e auditoria | Alta |
| Outbox | Eventos externos | Funcional real / Pendente de runtime | Reprocessamento duplica evento | Idempotência por correlationId | Alta |
| Auditoria | Timeline e ator real | Funcional real | Consultas sensíveis sem auditoria | Auditar exportação, anexo, lote e status | Alta |
| Agenda | `/Agenda` | Parcial | Visão demonstrativa | Fontes OS, planos, leads, propostas, pedidos, tarefas | Média |
| SLA | Prazos/status | Parcial | Sem campos físicos uniformes | Status lógico NO_PRAZO, ATENCAO, VENCIDO, CONCLUIDO | Média |
| Kanban | `/Kanban` | Parcial / Fallback honesto | Rota ausente gera 404 | Visão simples sem drag-and-drop e auditoria em mudança | Alta |
| Menus/Layout | Navegação | Parcial | Links mortos/`#` | Smoke de rotas principais e revisão visual | Alta |
| CI/CD | Workflow | Parcial | Sem despacho manual bloqueia homologação | `workflow_dispatch`, artifacts e jobs obrigatórios | Alta |
| Docker | Compose E2E | Pendente de runtime | Imagem/health pode falhar | Logs API/Web/Worker/db-migrations/PostgreSQL como artifact | Alta |
| Seeds | Demo homologação | Pendente de runtime | Duplicidade | Rodar duas vezes e bloquear produção sem flag | Alta |
| Migrations | PostgreSQL | Pendente de runtime | Ordem/incompatibilidade | `sql-validate` com schema report | Alta |
