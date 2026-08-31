# Financeiro, orçamento e contabilidade multi-esfera

## Escopo RC50.86

A RC50.86 estabelece o modelo persistente canônico para planejamento (PPA, LDO e LOA), execução da receita e da despesa, tesouraria, restos a pagar e contabilidade. Todo registro operacional carrega `tenant_id`, `entidade_id`, `exercicio_id`, `esfera_governo`, órgão, unidade gestora e unidade executora. A terminologia atende entidades municipais, estaduais e federais, inclusive administração direta e indireta.

A migration `20260901000000_rc50_86_financeiro_orcamento_contabilidade_multiesfera.sql` cria, sem substituir as tabelas legadas, os 38 agregados solicitados, relacionamentos internos, valores em `numeric`, checks de esfera, status, valores e percentuais, além de índices de contexto. As permissões RC50.86 são persistidas idempotentemente no banco.

## Regras asseguradas no banco

- dotações exigem exercício, unidade gestora, programa, ação, fonte e natureza;
- créditos, bloqueios, empenhos, liquidações, pagamentos e arrecadações rejeitam valor inválido;
- retenções aceitam percentuais somente entre zero e cem;
- cancelamento de restos a pagar exige justificativa;
- lançamentos contábeis usam itens de débito ou crédito positivos; o serviço que efetivar o lote deve conferir igualdade dos totais na mesma transação;
- encerramento é recusado enquanto houver pendências críticas;
- conciliação usa estados `pendente`, `conciliado`, `divergente` e `cancelado` e não representa integração bancária.

## Integrações e limites técnicos

As chaves opcionais de fornecedor e contrato preservam vínculos reais com Compras/Contratos. Receita importada registra origem e referência externa para permitir idempotência com Tributos e Royalties360. Não há adaptador bancário, PIX, SIAFI, SIAFIC, SEFAZ ou Tesouro nesta RC; nenhum resultado externo é simulado. A exposição a GED, Transparência e BI360 depende dos adaptadores reais e das políticas de mascaramento já instaladas.

## Evolução seguinte

Controllers e telas legadas continuam disponíveis. A adoção integral das tabelas canônicas pelos fluxos MVC/Dapper deve ocorrer incrementalmente, sem fallback de dados e sem conversão destrutiva do legado. Operações compostas devem validar saldo e período e gravar movimento, agregado e auditoria na mesma transação.
