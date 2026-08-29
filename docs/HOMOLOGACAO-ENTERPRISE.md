# Homologação Enterprise — RC50.81

## Escopo operacional

A central de homologação usa `sigov.homologacao_item` como fonte de autoridade, sempre segmentada por `tenant_id` e, quando aplicável, por entidade, unidade e exercício. O fluxo permitido é **pendente**, **em validação**, **aprovado**, **reprovado** ou **bloqueado**; criticidade aceita baixa, média, alta e crítica.

Cada item admite responsável selecionado dentre usuários do contexto, evidência textual e vínculo opcional com documento do GED. Toda transição deve produzir histórico imutável com usuário, horário e justificativa. A migração RC50.81 cria índices para dashboard, filtros contextuais e histórico.

## Rotas e gates de aceite

A entrega comercial deve expor `/Homologacao/Dashboard`, `/Checklist`, `/Modulos`, `/Rotas`, `/Permissoes`, `/Scripts`, `/Integracoes`, `/Relatorios` e `/Erros` somente quando as respectivas actions persistentes estiverem habilitadas. O gate proíbe telas decorativas: ausência de schema ou permissão deve falhar explicitamente.

Exportações devem ser autorizadas por `HOMOLOGACAO_RELATORIO_EXPORT`, registrar evento de auditoria, aplicar filtros de tenant e neutralizar células iniciadas por `=`, `+`, `-`, `@`, tabulação ou retorno de carro.

## Checklist navegacional

- confirmar controller, action, view e ViewModel compatíveis;
- confirmar autorização fail-closed e contexto de tenant;
- confirmar estado vazio acionável e paginação;
- confirmar antiforgery e validação associada aos campos em todo POST;
- confirmar que usuários, documentos, entidades e unidades são seleções, nunca IDs digitados;
- validar os módulos estratégicos existentes sem criar catálogo paralelo.
