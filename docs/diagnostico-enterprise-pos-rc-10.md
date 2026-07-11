# Diagnóstico Enterprise Pós-RC 10

Base: PR #107, template ModulePage, enterprise-crud.js e CRUD Dapper existentes.

## Achados
- API Enterprise agora está marcada com [Authorize], valida tenant por X-Tenant-Id e bloqueia fallback demo em Production.
- Permissões mínimas são avaliadas por rota/ação via claims permission/permissions/scope e ADMIN_GERAL/ADMIN_TENANT.
- EnterpriseDapperCrudService usa EnterpriseExecutionContextAccessor para gravar actor real em created_by/updated_by/deleted_by e auditoria.
- Ações críticas que dependem de schema retornam SCHEMA_UNAVAILABLE em vez de falso sucesso.
- CSV permanece mascarado e sanitizado; exportação exige permissão enterprise.relatorios.exportar na API.
- Formulários por entidade foram externalizados em enterprise-form-metadata.js e ações operacionais aparecem por área.

| Área | Rota Web | API | CRUD | Permissão | Auditoria | Tenant | Status | Correção |
|---|---|---|---|---|---|---|---|---|
| Comercial clientes | /Enterprise/Comercial | /api/enterprise/clientes e /api/comercial/clientes | Criar/editar/inativar/restaurar | comercial.clientes.* | usuário real via contexto | header obrigatório em produção | Endurecido | Validar smoke com banco real |
| Comercial propostas | /Enterprise/Comercial/Propostas | /api/comercial/propostas | Aprovar/reprovar/gerar pedido | comercial.propostas.* | ação crítica auditada | tenant real | Endurecido | Validar duplicidade no banco |
| Comercial pedidos | /Enterprise/Comercial/Pedidos | /api/comercial/pedidos | confirmar/cancelar/gerar OS | comercial.pedidos.* | ação crítica auditada | tenant real | Endurecido | Validar regra pedido já convertido |
| OS | /Enterprise/OrdemServico | /api/os/ordens | status, checklist, apontamento, consumo | os.ordens.* os.apontamentos.criar os.pecas.consumir | ação crítica auditada | tenant real | Parcial | Smoke E2E cobre fluxo principal |
| Estoque | /Enterprise/Estoque | /api/estoque/produtos /api/estoque/movimentos/* | produto e movimentos | estoque.* | movimento auditado | tenant real | Endurecido | Saldo negativo bloqueado sem flag/permissão |
| Compras | /Enterprise/Compras | /api/compras/* | fornecedores/pedidos | compras.* | CRUD auditado | tenant real | Parcial | Recebimento ainda depende endpoint legado |
| Industrial | /Enterprise/Industrial | /api/industrial/* | ativos/planos/medidores/paradas | industrial.* | ação crítica auditada | tenant real | Parcial | Validar preventiva em runtime |
| Relatórios | páginas Enterprise | */export-csv | export mascarado | enterprise.relatorios.exportar | export auditável no fluxo novo | tenant real | Endurecido | Ampliar relatórios específicos |

## Validação de ambiente
- git checkout main falhou porque o repositório local possui apenas a branch work; foi criada a branch codex/pos-rc-10-enterprise-hardening-produto a partir dela.
- dotnet não está instalado no container; build/test devem rodar no GitHub Actions.
- Docker/pwsh não foram executados após a limitação de dotnet local; não declarar validação verde runtime sem CI.
