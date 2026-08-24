# Fechamento FUNC09 — Tributário e Receita Municipal

## Entrega

- Modelo PostgreSQL idempotente com 23 tabelas, vínculos, índices, constraints, triggers de integridade, soft delete e auditoria.
- RBAC persistido com permissões granulares FUNC09 e concessão idempotente ao perfil sistêmico `SUPERADMIN`.
- MVC/Razor com dashboard real, cadastros e consultas paginadas, cadastro server-side de contribuinte, validação de certidão e 11 exportações CSV.
- Dapper/Npgsql como acesso único; falha explícita e sem dados demonstrativos/fallback.

## Limites reais

Não foram implementados código de barras, boleto, Pix, linha digitável ou contabilização fictícia. Emissão documental PDF depende de infraestrutura homologada futura; a guia simples e a certidão permanecem em HTML/dados internos. O módulo InovaGED/Protocolo e releases RC50.68/RC50.69 não foram alterados.

## Gates

Executar restore/build, checksum/manifest e sintaxe SQL. Aplicação/reaplicação PostgreSQL e smoke autenticado ficam **BLOCKED** quando não houver PostgreSQL 16 e credenciais/identidades RBAC no ambiente, devendo ser registrados com o motivo exato no PR.
