# Diagnóstico Enterprise Pós-RC 09

## Estado da branch

- Branch de trabalho criada: `codex/pos-rc-09-enterprise-qa-produto`.
- Branch base local disponível no container: `work`; a referência `main` não existe no clone local, portanto `git checkout main` não pôde ser executado neste ambiente.
- Último commit local: `4fe5c08 Merge pull request #106 from devmnsoft/codex/validar-enterprise-ponta-a-ponta`.
- Último PR mergeado identificado no histórico local: PR #106.

## Arquivos Enterprise do PR #106 revisados

- `src/Sigov.Infrastructure/EnterpriseDapperCrudService.cs`: CRUD Dapper, ações operacionais, estoque, auditoria e fallback honesto.
- `src/Sigov.Api/Controllers/EnterpriseModulesController.cs`: rotas REST genéricas, rotas legadas, CSV e ações de jornada.
- `src/Sigov.Web/Controllers/EnterprisePagesControllers.cs`: telas MVC/Razor para Comércio, OS, Estoque, Compras e Industrial.
- `src/Sigov.Web/Views/Enterprise/ModulePage.cshtml`: template operacional Enterprise.
- `src/Sigov.Web/wwwroot/js/enterprise-crud.js`: listagem, formulário, edição, inativação e CSV.
- `scripts/smoke-test-sigov.ps1`: rotas Web/API Enterprise no smoke.
- `database/postgres/seeds/enterprise_demo_seed.sql`: seed demonstrativa Enterprise.

## Telas Enterprise existentes

Comércio: Dashboard, Clientes, Produtos, Orçamentos, Pedidos, Vendas, Tabelas de Preço e Comissões. OS: Dashboard, Ordens, Agenda, Checklist e Apontamentos. Estoque: Dashboard, Produtos, Almoxarifados, Movimentos, Saldos e Requisições. Compras: Fornecedores e Pedidos. Industrial: Dashboard, Ativos, Planos de Manutenção, Programadas, Medidores e Paradas. Indústria: Dashboard e telas produtivas já mapeadas.

## Endpoints Enterprise existentes

- Genéricos: `GET/POST /api/enterprise/{area}`, `GET/PUT/DELETE /api/enterprise/{area}/{id}`, `POST /api/enterprise/{area}/{id}/restaurar`, `GET /api/enterprise/{area}/export-csv`.
- Legados: comercial, comércio, OS, estoque, compras, industrial e indústria.
- Operacionais: aprovar/reprovar proposta, gerar pedido, confirmar/cancelar pedido, gerar OS, agendar/iniciar/pausar/concluir/cancelar OS, checklist/apontamentos, consumir peça, saldos/movimentos de estoque, gerar OS preventiva e registrar leitura.

## CRUDs realmente implementados

CRUD completo via serviço Dapper e rotas REST para clientes, leads, oportunidades, propostas, pedidos, produtos, almoxarifados, requisições, fornecedores, pedidos de compra, ordens de serviço, ativos, planos, medidores, paradas e entidades produtivas mapeadas. O fluxo inclui listar, filtrar/paginar em memória após consulta limitada, detalhar, criar, atualizar por ID, soft delete, restaurar e CSV mascarado.

## Ações operacionais realmente implementadas

Aprovação/reprovação de proposta, geração de pedido, confirmação/cancelamento de pedido, geração de OS, alterações de status de OS, registro de apontamento/checklist, consumo de estoque com bloqueio de saldo negativo, movimentos de entrada/saída/ajuste, geração de OS preventiva e leitura de medidor.

## Pontos ainda parciais e riscos

- Dashboard usa contagem/saldos do módulo, mas ainda não consolida todos os KPIs comerciais solicitados por jornada.
- Busca global depende da integração da aplicação com `SearchAsync`; validar no runtime autenticado.
- Permissões existem como strings nas telas/seeds, mas a aplicação real por botão/endpoints precisa de homologação com usuários sem permissão.
- `ResolveTenantId` usa tenant demo apenas em Development; isso é aceitável para demo local, mas produção deve exigir tenant resolvido.
- CSV aplica mascaramento nos campos sensíveis expostos, mas filtros do export genérico devem ser validados em homologação.
- Fallback honesto evita falso sucesso quando schema não existe; não deve ser usado como evidência funcional de produção.
- Docker, migrations e seed não puderam ser validados neste container por ausência de toolchain local completo.
- Build/test falharam por ausência do SDK `dotnet` no ambiente.

## Correções Pós-RC 09 aplicadas

- `enterprise-crud.js` passou a enviar filtros/paginação para a API, manter loading state, exibir detalhes com dados mascarados, editar por ID, inativar, restaurar, exportar CSV via `fetch` com `X-Tenant-Id`, bloquear clique durante operações e mostrar toasts de sucesso/erro.
- `ModulePage.cshtml` recebeu paginação/estado de consulta no rodapé da tabela e botão Novo identificável pelo JS.
