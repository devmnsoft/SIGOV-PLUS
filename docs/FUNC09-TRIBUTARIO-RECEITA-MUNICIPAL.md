# FUNC09 — Tributário e Receita Municipal

## Escopo

O FUNC09 consolida cadastro de contribuintes, imobiliário e mobiliário; exercícios, tributos e parâmetros versionados; lançamentos, guias sem artefatos bancários fictícios, arrecadação, inadimplência, dívida ativa, parcelamentos, fiscalização, notificações, autos, certidões e relatórios CSV.

## Arquitetura e autoridade

A persistência usa exclusivamente PostgreSQL via Dapper/Npgsql e isolamento obrigatório por `tenant_id` e `entidade_id`. As páginas MVC consultam as tabelas FUNC09; indisponibilidade de schema ou contexto resulta em erro explícito, sem fallback. PKs novas são `bigint identity`, exclusão é lógica e operações sensíveis geram `tributario_auditoria` com dados minimizados.

## Segurança e LGPD

As rotas são autenticadas e protegidas pelas permissões `TRIBUTARIO_*` persistidas. A validação pública de certidão exige código interno de alta entropia e contexto municipal. CPF/CNPJ não é incluído nos CSVs de outros recursos; o relatório de contribuintes requer permissão específica de exportação. Logs registram somente identificadores técnicos e correlation ID.

## Regras financeiras

Valores não podem ser negativos. Diferença de pagamento exige justificativa. Guia somente pode ficar paga com pagamento confirmado, e dívida ativa somente aceita lançamento vencido. Cancelamentos e estornos exigem justificativa. `codigo_barras` e `pix_payload` permanecem nulos: o módulo não inventa boleto, Pix ou linha digitável. Não é criado lançamento contábil quando não houver integração homologada com Tesouraria.

## Operação

Aplicar `database/postgres/migrations/20260825030000_func09_tributario_receita_municipal.sql` com `psql -v ON_ERROR_STOP=1`. Configure `ConnectionStrings__DefaultConnection`; não versionar segredos. O dashboard e os CSVs refletem dados existentes no banco.
