# Schema report - matriz de aderência

O relatório local não pôde consultar o PostgreSQL neste container porque Docker/psql/runtime não estão disponíveis.

O script `scripts/schema-report-matriz-aderencia.ps1` foi criado para detectar o schema real antes de ativar consultas e ações reais. A consulta fica em `database/diagnostics/schema-report-matriz-aderencia.sql`.

Critério operacional: se tabelas como `sigov.edital`, `sigov.edital_requisito`, `sigov.edital_evidencia` ou `sigov.edital_poc_*` não existirem, o módulo deve operar em fallback honesto, sem simular persistência nem aprovação.
