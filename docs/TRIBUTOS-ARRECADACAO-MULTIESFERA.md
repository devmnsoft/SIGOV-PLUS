# Tributos e arrecadação multi-esfera — RC50.87

O catálogo tributário é parametrizado por `tenant_id`, `entidade_id`, `exercicio_id` e `esfera_governo`; órgão e unidades gestora/executora acompanham os movimentos quando aplicáveis. Assim, IPTU/ISS/ITBI, IPVA/ITCMD e receitas federais são configurações da respectiva esfera, não regras municipais hardcoded.

O lançamento referencia contribuinte e tipo, registra base, valor, regra e memória JSON auditável, veda negativos e vencimento anterior à emissão. Pagamentos possuem referência de integração real e estorno justificado; a aplicação deve bloquear valor superior ao saldo na mesma transação. Baixa manual e estorno exigem permissão e justificativa.

Parcelamento conserva entrada, parcelas, saldo, juros, multa, correção e estado. Dívida ativa exige débito vencido, notificação e elegibilidade verificadas. Protesto e execução fiscal somente serão ativados com integração real ou vínculo formal. Certidões usam validade, hash do conteúdo e código público de verificação, expondo apenas dados indispensáveis.

A RC consolida o núcleo (`tributo_parametro_esfera`, `tributo_tipo`, `tributo_contribuinte`, `tributo_lancamento`, `tributo_pagamento`, `tributo_baixa`, `tributo_parcelamento`, `tributo_divida_ativa`, `tributo_certidao`). Cadastros especializados existentes continuam compatíveis e serão convergidos de forma aditiva.
