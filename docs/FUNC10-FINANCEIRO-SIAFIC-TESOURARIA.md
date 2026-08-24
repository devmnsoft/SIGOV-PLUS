# FUNC10 — Financeiro, SIAFIC e Tesouraria

## Escopo entregue

O FUNC10 consolida orçamento, dotação e movimentação orçamentária, empenho (ordinário,
global e estimativo), liquidação, ordem e registro de pagamento, contas e conciliação
bancária manual, receita, restos a pagar e suprimento de fundos. PostgreSQL é a fonte de
autoridade; o acesso ocorre por Dapper/Npgsql e depende do contexto de tenant, entidade e
exercício. Ausência de schema ou permissão é erro explícito, sem dados simulados.

As rotas MVC estão agrupadas em `/Financeiro`, `/Siafic` e `/Tesouraria`. As telas usam o
layout do produto, filtros e estados vazios. Os fluxos já existentes de orçamento,
empenho, liquidação, pagamento e receita consomem os endpoints reais `api/financeiro/*`.
A migration FUNC10 acrescenta ordens, conciliação, arrecadação deduplicada, restos e
suprimentos, além das constraints transacionais concorrentes.

## Segurança e integridade

As 25 permissões `FINANCEIRO_*` e `TESOURARIA_*` são persistidas. O avaliador existente
permanece fail-closed. Alterações críticas exigem justificativa, preservam soft delete e
registram usuário/correlation id. Documento de credor e documento hábil devem ser exibidos
somente a usuários autorizados e minimizados nos logs. Triggers bloqueiam empenho sem
saldo, liquidação acima do empenho, ordem acima da liquidação e pagamento acima da ordem
autorizada.

## Integrações

* Compras/Contratos: IDs opcionais preservam o vínculo real; não são criados contratos.
* Almoxarifado/Patrimônio: IDs opcionais de recebimento/incorporação somente podem ser
  preenchidos a partir de registros reais.
* Tributário/Saneamento: `origem + origem_id` é único por escopo e evita dupla arrecadação.
* Integração bancária automática, PIX, boleto e CNAB não foram implementados.

## Aderência SIAFIC

Esta entrega oferece **aderência funcional parcial**, não certificação ou declaração de
conformidade integral ao Decreto 10.540/2020. Permanecem pendentes homologação contábil,
PCASP/eventos completos, matriz legal por ente, integração bancária contratada, assinatura
qualificada, trilhas de segregação validadas pelo controle interno e testes oficiais de
fechamento/continuidade. Esses itens devem ser tratados em RC própria; RC50.68 permanece
BLOCKED e nenhuma RC50.69/release foi criada.

## Operação

Aplicar `database/postgres/migrations/20260825040000_func10_financeiro_siafic_tesouraria.sql`
com `psql -v ON_ERROR_STOP=1`. O ambiente deve fornecer
`ConnectionStrings__DefaultConnection`. Relatórios CSV exigem `FINANCEIRO_RELATORIO_EXPORT`
e nunca exportam dados fictícios.
