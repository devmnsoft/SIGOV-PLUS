# Schema report - implantação, suporte e POC

Execução local pendente neste ambiente: `dotnet`, `docker` e PostgreSQL local não estão disponíveis no container de edição. O script `scripts/schema-report-implantacao-suporte.ps1` gera este arquivo a partir de `database/diagnostics/schema-report-implantacao-suporte.sql` quando `SIGOV_DATABASE_URL` estiver configurada.

Critério aplicado no código: todas as áreas consultam `IDatabaseSchemaInspector` antes de exibir persistência real; sem tabela física, a tela mostra fallback honesto e não simula salvamento.
