# SIGOV Pós-Build 08 — Tributário Avançado e Fiscal Integrado

O módulo Tributário Avançado amplia o SIGOV PLUS com gestão municipal fiscal multi-tenant para IPTU, ISS, taxas municipais, dívida ativa, parcelamentos, arrecadação, emissão simulada de DAM, integração básica simulada de NFS-e, livro eletrônico e relatórios fiscais consolidados.

## Escopo entregue

- Tabelas idempotentes no schema `sigov` para `tributos_impostos`, `iptu`, `iss`, `taxas_municipais`, `contribuinte`, `parcela`, `arrecadacao`, `documento_arrecadacao_municipal`, `livro_eletronico_tributario`, `parcelamento_divida_ativa` e `integracao_nfse`.
- Índices por `tenant_id`, inscrições e vencimentos.
- Triggers de `updated_at` para todas as tabelas fiscais avançadas.
- Seeds iniciais de contribuintes, tributos, perfis, permissões e pacote SaaS `GOV_TRIBUTARIO_PLUS`.
- APIs por tenant com listagem paginada, filtros, DAM simulado, NFS-e simulada, arrecadação e livro eletrônico versionado.
- Telas navegáveis para dashboard, IPTU, ISS, taxas, dívida ativa, parcelamentos, arrecadação, NFS-e, livro eletrônico e relatórios fiscais.

## Auditoria, LGPD e multi-tenancy

Todas as operações administrativas gravam auditoria com `tenant_id`, usuário, `correlationId`, payload reduzido e timestamp. Os endpoints exigem tenant resolvido e validam contribuinte/inscrição antes de emissões simuladas. Dados pessoais de contribuinte são tratados com mascaramento em listagens legadas e consentimento LGPD no cadastro avançado.

## Integrações

- **SaaS:** catálogo do módulo aponta para `/Tributario/Dashboard` e permissões granulares são seedadas.
- **Financeiro:** criação de parcelas e DAMs gera contas a receber com origem tributária.
- **Comercial/OS/Produção:** ISS aceita `origem` e `origem_id` para registrar vínculo fiscal com eventos externos sem emissão fiscal real.

## Limitações intencionais

Não há integração real com SEFAZ, PGFN, Receita Federal, NF-e ou NFC-e. A DAM e a NFS-e são simulações operacionais para demonstração, homologação e integração interna.
