# Schema report workflow local

Relatório preparado para execução via `scripts/schema-report-workflow.ps1`.

Estado neste ambiente de agente: `dotnet` não está instalado e o banco local não foi inspecionado via psql. O aplicativo, entretanto, usa `IDatabaseSchemaInspector` em runtime para detectar as tabelas antes de ativar persistência real.
