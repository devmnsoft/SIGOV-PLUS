# RC50.59 — plano dos fluxos de arrecadação e financeiro

Data: 2026-08-19. Escopo: consolidar o núcleo já existente, sem abrir módulo e sem criar testes.

## Inventário encontrado

| Domínio | API controllers | Web controllers | Serviços/repositórios | Estruturas principais |
|---|---|---|---|---|
| Tributário | `TributarioController`, `TributarioAvancadoControllerBase`, `TributarioCarnesBoletosController`, `TributarioFiscalizacaoController`, `TributarioNfseController` | `TributarioController`, `TributarioCarnesBoletosController`, `TributarioFiscalizacaoController`, `TributarioNfseController` | núcleo Bloco 5 e `TributarioAvancadoRepository` | `tributario_contribuinte`, `tributario_lancamento`, `tributario_guia`, `tributario_pagamento`, `tributario_divida_ativa`, `tributario_dam` |
| Financeiro | `PlanoContas`, `Orcamento`, `Empenhos`, `Liquidacoes`, `Pagamentos`, `Receitas`, dashboard/exportação e endpoints empresariais | `FinanceiroController`, `TesourariaController`, `SiaficController` | `FinanceiroServices`, `FinanceiroRepositories` e núcleo empresarial | `receita_lancamento`, `receita_arrecadacao`, `pagamento`, `financeiro_conta_receber`, movimentos e integrações de origem |
| Saneamento | consumidores, ligações, unidades, hidrômetros, leituras, faturas, arrecadações, OS, dashboard/exportação e avançados | `SaneamentoController`, Comercial, Faturamento, Operação e GIS/Qualidade | `SaneamentoService`, `SaneamentoRepository`, serviço/repositório avançado | consumidor, unidade, ligação, hidrômetro, leitura, fatura, pagamento, inadimplência, parcelamento e eventos |

Endpoints principais são os CRUDs REST em `api/tributario`, `api/financeiro/*` e `api/saneamento/*`, incluindo ações de emissão, cancelamento, pagamento, geração de fatura, execução de OS e exportação. A varredura estática não encontrou `501`/`NotImplemented` essencial. As views possuem módulos JavaScript próprios para dashboards, contribuintes, lançamentos, DAM/carnês, receitas/pagamentos, consumidores, ligações, hidrômetros, leituras, faturas e OS; nenhum botão foi classificado como corrigido sem smoke autenticado.

## Estado das regras e integrações

Já existem persistência Dapper parametrizada, filtro por tenant/entidade, catálogo de permissões no banco, auditoria de mutações, máscaras em consultas de consumidores, emissão de guia/DAM, arrecadação tributária, faturamento de saneamento e outbox/ponte financeira. Nesta entrega serão endurecidos: catálogo canônico granular, negação padrão do Saneamento, auditoria de negativas, valor positivo de pagamento/arrecadação, justificativa de estorno/cancelamento, leitura regressiva justificada, série obrigatória e total de fatura não negativo.

Permanecem para execução/homologação: prova concorrente de baixa única, transição financeira completa DAM/fatura → título → baixa/estorno, elegibilidade de dívida/corte/religação, substituição histórica de hidrômetro e smoke de todos os menus/cards. Integrações externas bancárias continuam preparatórias; não serão representadas como liquidação produtiva.
