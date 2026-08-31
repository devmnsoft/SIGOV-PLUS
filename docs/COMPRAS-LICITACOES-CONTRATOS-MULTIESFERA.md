# Compras, licitações, contratos e atas multi-esfera — RC50.85

## Escopo persistente

A RC50.85 acrescenta uma base PostgreSQL aditiva e idempotente para o ciclo de contratação dos órgãos e entidades municipais, estaduais e federais. O contexto oficial é composto por `tenant_id`, `entidade_id`, `esfera_governo`, órgão, unidade gestora, unidade executora e exercício. A migration não converte nem remove identificadores legados.

O modelo cobre PCA, demanda, DFD, ETP, mapa de riscos, termo de referência, pesquisa e fontes de preço, configuração de modalidade, licitação, lotes, itens, sessões, propostas, julgamento, habilitação, recursos, adjudicação e homologação. Contratos administrativos abrangem itens, fiscais, gestores, garantias, eventos de execução, aditivos, apostilamentos, sanções e vínculos reais com Financeiro e GED.

## Regras implementadas

- Valores são não negativos; quantidades operacionais são positivas e percentuais ficam entre zero e cem.
- Vigências, sessões, designações e sanções possuem intervalos de datas coerentes.
- Reprovação e cancelamento do planejamento exigem justificativa.
- Cotação discrepante exige justificativa e fornecedor canônico selecionado por chave estrangeira.
- Proposta se refere a lote ou item, nunca aos dois; desclassificação exige justificativa.
- Adjudicação referencia uma proposta vencedora e homologação referencia a adjudicação.
- Saldo contratual não pode ficar negativo nem superar o valor inicial; saldo de item não supera a quantidade contratada.
- Sanção exige motivo, fundamento, prazo e responsável.

## Autoridade, permissões e relatórios

As 23 permissões da RC50.85 são persistidas no catálogo oficial, sem catálogo hardcoded. Exportações devem exigir `COMPRAS_RELATORIO_EXPORT`, aplicar o contexto operacional completo, auditar o evento e neutralizar células iniciadas por `=`, `+`, `-`, `@`, tabulação ou retorno de carro antes de produzir CSV.

## Integrações e limites técnicos

Financeiro e GED são representados apenas por vínculos a registros reais. Não há simulação de PNCP, Compras.gov, SIAFI ou SIAFIC. Obras360, Ativos360 e Almoxarifado somente podem consumir eventos de execução quando existir adaptador e registro canônico. A entrega de schema não declara uma integração externa como ativa.

As telas legadas de Compras continuam disponíveis, mas as jornadas MVC completas de PCA, DFD, ETP, TR, licitação e fiscalização dependem da implementação Application/Infrastructure sobre as novas tabelas. Nenhuma tela decorativa ou persistência simulada foi adicionada.

## Operação

Aplicar `20260831230000_rc50_85_compras_contratos_multiesfera.sql` pelo manifesto. O `postConditionSql` confirma PCA, licitação, contrato administrativo e permissão do dashboard. A migration usa apenas `CREATE ... IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, índices idempotentes e inserts condicionais.
