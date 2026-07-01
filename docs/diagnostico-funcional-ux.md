# Diagnóstico funcional e UX

## Antes

- Consultas de tenant assumiam colunas opcionais como `slug`, `documento`, `plano`, `ativo`, `metadados`, `is_deleted`, `created_at` e `updated_at`.
- A tela de health declarava API/worker/storage como online sem prova real.
- Não havia roteiro versionado para relatório local do schema PostgreSQL.

## Depois

- Listagem e gravação de tenants usam inspeção de schema antes de montar SQL.
- Health da Web diferencia `Online`, `Atenção`, `Offline` e `Não monitorado` com probes reais para PostgreSQL e storage.
- Worker só é marcado como monitorável quando há tabela de heartbeat; caso contrário fica `Não monitorado`.
- Foram adicionados script e SQL de diagnóstico do schema.

## Pendências recomendadas

- Executar Docker e smoke tests em ambiente com `dotnet` disponível.
- Evoluir editor de parâmetros para edição por ID e restauração por valor padrão em todos os formatos de schema encontrados.
- Capturar screenshots desktop/tablet/mobile após o runtime estar disponível.

## Atualização da sprint de runtime, Health e Parâmetros — 2026-07-01

### Validação de ambiente

- O container do agente não possui `dotnet` nem `docker`; por isso `dotnet restore`, `dotnet build`, Docker Compose, smoke tests HTTP e geração do schema real ficaram pendentes de execução em host com .NET 6 SDK e Docker.
- A evidência operacional foi estruturada em `docs/runtime-smoke-tests.md`, `docs/testes-manuais-runtime.md`, `database/diagnostics/schema-report.sql` e `scripts/schema-report.ps1` para reexecução no ambiente local real.

### Parâmetros SaaS

- `/Saas/Parametros` passou a trabalhar diretamente com `sigov.parametro_sistema` em modo schema-safe, consultando colunas reais antes de montar filtros e queries.
- O editor valida os tipos `string`, `int`, `decimal`, `bool`, `json` e `date` antes de persistir.
- Chaves sensíveis (`senha`, `password`, `token`, `secret`, `chave`, `key`, `api_key`, `client_secret`, `certificado`) são mascaradas em listagem e auditoria.
- A restauração de padrão só executa quando a coluna `valor_padrao` existe; caso contrário a tela informa que não houve simulação.

### Health

- O fallback visual de Health deixou de declarar API, Worker e Storage como online sem prova real.
- Status sem probe real são exibidos como `Não monitorado` ou `Atenção`, preservando a confiabilidade da operação.

### Próximas pendências recomendadas

- Executar a validação final em runner com .NET 6 SDK e Docker.
- Popular `docs/schema-report-local.md` com o schema real via `./scripts/schema-report.ps1`.
- Atualizar `docs/runtime-smoke-tests.md` com status codes reais e correções adicionais encontradas em runtime.
- Evoluir Protocolo, GED e Tributário com serviços schema-safe dedicados caso o schema real local contenha as tabelas mínimas.
