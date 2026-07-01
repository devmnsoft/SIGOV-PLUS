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
