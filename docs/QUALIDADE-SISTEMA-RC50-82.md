# Central de Qualidade e Consistência — RC50.82

## Escopo entregue

A Central em `/QualidadeSistema` concentra dashboard contextual, fila filtrável de inconsistências, checklist, smoke de rotas, formulários, permissões, SQL/migrations, integrações e relatório CSV. O dado persistido é a única fonte de autoridade; ausência de schema resulta em falha explícita.

As gravações exigem autenticação, permissão específica, `tenant_id`, `entidade_id` e usuário. A criação valida o responsável ativo no tenant, e mudanças de estado usam transação e histórico obrigatório. Estados aceitos: `ABERTA`, `EM_ANALISE`, `CORRIGIDA` e `IGNORADA_COM_JUSTIFICATIVA`.

## Segurança e operação

- queries parametrizadas e limitadas a 500 linhas;
- autorização fail-closed por capacidade;
- AntiForgery em todos os POSTs;
- CSV neutraliza prefixos de fórmula e respeita filtros/contexto;
- criação, tratamento e exportação registram auditoria sem evidência/payload sensível;
- responsável é selecionado da base, nunca digitado como ID;
- migration idempotente, somente aditiva, com checks e índices contextuais.

## Rotas

`Dashboard`, `Checklist`, `Inconsistencias`, `Rotas`, `Formularios`, `Permissoes`, `Sql`, `Integracoes` e `Relatorios` são actions reais do controller MVC. A tela responsiva reutiliza Bootstrap e um stylesheet isolado.
