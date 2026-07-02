# Schema operacional local

Validação atual executada em ambiente sem `dotnet` e sem `docker`; portanto a inspeção real do PostgreSQL local não pôde ser rodada nesta sessão. A aplicação agora faz a inspeção em runtime via `IDatabaseSchemaInspector`/`information_schema` para as tabelas abaixo e degrada para fallback honesto quando ausentes.

- Protocolo: `sigov.protocolo`, `sigov.processo`, `sigov.tramite`, `sigov.protocolo_movimento`, `sigov.protocolo_anexo`, `sigov.arquivo`.
- GED/OCR: `sigov.documento`, `sigov.ged_documento`, `sigov.ged_pasta`, `sigov.pasta`, `sigov.documento_versao`, `sigov.arquivo`, `sigov.ocr_fila`.
- Tributário: `sigov.contribuinte`, `sigov.imovel`, `sigov.debito`, `sigov.guia`, `sigov.divida_ativa`.
- Contratos: `sigov.contrato`, `sigov.contrato_aditivo`, `sigov.contrato_fiscal`, `sigov.contrato_documento`.
- Jurídico: `sigov.processo_juridico`, `sigov.parecer_juridico`, `sigov.prazo_juridico`, `sigov.audiencia_juridica`.
- Financeiro: `sigov.conta_pagar`, `sigov.conta_receber`, `sigov.caixa_movimento`, `sigov.categoria_financeira`.

Quando uma tabela existir, as colunas são descobertas com `GetColumnsAsync` e a consulta seleciona apenas colunas presentes.
