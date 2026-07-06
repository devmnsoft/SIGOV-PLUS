# Schema report — API, Mobile, Assinatura, BI e Integrações

Ambiente local sem `DATABASE_URL` disponível nesta execução. A consulta oficial está em `database/diagnostics/schema-report-api-mobile-integracoes.sql`.

Nenhuma API, webhook, assinatura ou sincronização deve assumir persistência se as tabelas `sigov.api_key`, `sigov.webhook_configuracao`, `sigov.campo_sincronizacao`, `sigov.assinatura_documento`, `sigov.portal_validacao_documento`, `sigov.bi_indicador` e correlatas não existirem.
