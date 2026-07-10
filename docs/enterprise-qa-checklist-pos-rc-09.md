# QA Checklist Enterprise Pós-RC 09

## Build e runtime

- [ ] `dotnet clean sigov.sln`
- [ ] `dotnet restore sigov.sln`
- [ ] `dotnet build sigov.sln --configuration Release`
- [ ] `dotnet test sigov.sln --configuration Release`
- [ ] `docker compose down -v`
- [ ] `docker compose build --no-cache`
- [ ] `docker compose up -d`
- [ ] Validar logs de `api`, `web`, `worker`, `db-migrations` e `postgres`.

## Web

- [ ] Abrir todas as rotas de Comércio, OS, Estoque, Compras, Industrial e Indústria sem 404/500.
- [ ] Confirmar tabela responsiva, loading, empty state, toast, modal e offcanvas.
- [ ] Confirmar que não há botão sem ação nem `href="#"` operacional.

## API e jornadas

- [ ] CRUD Cliente, Produto, Fornecedor e OS.
- [ ] Proposta aprovada gera pedido; proposta reprovada não gera pedido.
- [ ] Pedido confirmado gera OS; pedido cancelado não gera OS.
- [ ] OS inicia, registra checklist/apontamento, consome peça e conclui.
- [ ] Estoque bloqueia saldo negativo sem permissão.
- [ ] Plano preventivo gera OS.

## Segurança, LGPD e auditoria

- [ ] CSV aplica tenant, filtros e mascaramento.
- [ ] Usuário sem permissão não vê/executa ações críticas.
- [ ] Ações críticas registram auditoria.
- [ ] Nenhum segredo, token, storage path completo ou dado real aparece em tela, CSV, smoke ou logs de evidência.
