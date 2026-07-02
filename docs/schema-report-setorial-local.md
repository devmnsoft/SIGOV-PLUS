# Schema report setorial local

Validação inicial em 2026-07-02: o ambiente de execução não possui `dotnet` nem `docker`; a consulta PostgreSQL local não foi executada aqui.

Use `scripts/schema-report-setorial.ps1` para gerar o relatório real a partir de `database/diagnostics/schema-report-setorial.sql` em ambiente com `psql` e banco disponível.

## Tabelas inspecionadas

Educação, Saúde/ACS, Saneamento, Social, Agro, Portal, Ouvidoria, Mobile/Campo, GIS e `auditoria_evento` conforme script SQL versionado.
