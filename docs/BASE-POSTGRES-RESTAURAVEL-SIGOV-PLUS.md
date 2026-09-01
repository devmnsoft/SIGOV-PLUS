# Base PostgreSQL restaurável do SIGOV PLUS

O artefato canônico da RC50.95 é `database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql`: SQL plain PostgreSQL 16, formado pelo baseline oficial sincronizado e pelos seeds locais. Consulte `database/README_RESTAURAR_POSTGRES.md` para restauração, credencial local e smoke checks.

Não há `DROP` destrutivo. Migrations publicadas permanecem imutáveis; evoluções usam nova migration idempotente. Os dados de exemplo são fictícios e identificados como `DEVELOPMENT`.

## RC50.96

A restauração operacional ganhou wrappers Bash e PowerShell com `ON_ERROR_STOP`, configuração por variáveis PostgreSQL e consultas pós-carga. A distinção entre SQL plain (`psql`) e arquivo custom (`pg_restore`) está consolidada no padrão de restore.
