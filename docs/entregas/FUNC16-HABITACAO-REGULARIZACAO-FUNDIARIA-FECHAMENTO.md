# Fechamento FUNC16

## Entrega

- 19 tabelas idempotentes, índices contextuais, constraints, RBAC e scripts consolidados sincronizados.
- Repository Dapper/Npgsql transacional, isolamento tenant/entidade, auditoria e CSV seguro.
- Dashboard e telas MVC/Razor operacionais com filtros, tabelas, formulários CSRF e estados vazios.
- Sidebar e DI integradas.

## Limites confirmados

Não há upload real; documentos, matrículas, cartório, processo e termos são metadados. InovaGED, GED e Protocolo não foram alterados. Não foram criados mocks, fallback oficial, secrets ou classes/projetos de teste.

## Validação

Executar `dotnet build`, validação JSON e, quando PostgreSQL estiver disponível, `psql -v ON_ERROR_STOP=1 -f database/postgres/migrations/20260825100000_func16_habitacao_regularizacao_fundiaria.sql`. Qualquer indisponibilidade externa deve ser registrada como BLOCKED.
