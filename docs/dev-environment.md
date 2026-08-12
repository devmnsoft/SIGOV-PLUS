# Ambiente de desenvolvimento reproduzível

Esta alternativa Linux/Docker complementa (e não substitui) o ambiente Windows. Use somente credenciais locais; nenhuma senha de produção deve ser copiada para estes arquivos.

## PostgreSQL/PostGIS 16

1. Copie `.env.local.example` para `.env.local`, defina `SIGOV_DB_PASSWORD` no terminal e execute `./scripts/check-prerequisites.sh` (ou `pwsh ./scripts/check-prerequisites.ps1`).
2. Suba o banco com `docker compose --env-file .env.local -f docker-compose.dev.yml up -d --wait`.
3. Provisione banco, migrations e contas Development com `pwsh ./scripts/setup-dev.ps1 -PostgresPassword $env:POSTGRES_PASSWORD` (PowerShell) ou informe os parâmetros equivalentes.

Para aplicar diretamente o artefato Development em banco limpo, gere-o com `pwsh ./scripts/generate-script-completop.ps1 -IncludeDevelopmentSeed` e execute `psql -X -v ON_ERROR_STOP=1 -U postgres -d sigov -f database/postgres/script_completo_dev.sql`. O script estrutural `script_completo.sql` não contém credenciais Development.

## Acesso local

As contas locais provisionadas são `admin` e `superadmin`; as credenciais Development canônicas estão documentadas no seed guard e devem ser trocadas fora do ambiente local. O setup não imprime senha de banco.

| Recurso | URL |
|---|---|
| Web / login | `https://localhost:7000/Auth/Login` |
| Diagnóstico (somente Development, loopback/token) | `https://localhost:7000/Dev/Auth` |
| API | `https://localhost:7001` |
| Swagger | `https://localhost:7001/swagger` |

Encerre o banco com `docker compose -f docker-compose.dev.yml down`; acrescente `-v` apenas quando quiser apagar deliberadamente os dados locais.
