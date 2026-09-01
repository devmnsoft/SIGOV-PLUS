# Assistência Social360 e SUAS

A RC50.91 organiza a proteção social do SIGOV PLUS para clientes e entidades municipais, estaduais e federais. O contexto obrigatório combina tenant, entidade, exercício, esfera, órgão e unidades gestora/executora. CRAS, CREAS, Centro POP, acolhimento, gestão SUAS, conselhos e demais unidades são parametrizados no banco.

## Segurança e LGPD

Famílias, pessoas, vulnerabilidades, violência, acolhimentos e medidas protetivas são dados sensíveis. As listagens devem mascarar CPF, NIS, CNS, telefone e endereço; cada acesso sensível é auditável e exportações dependem de permissão própria. CSV deve prefixar células iniciadas por `=`, `+`, `-` ou `@` e nunca publicar relatos técnicos.

## Integrações

Cidadão360 fornece a identidade canônica. RH360 pode referenciar técnicos; Almoxarifado e Financeiro somente recebem vínculos persistidos em fluxos existentes. Saúde360, Educação360, DefesaCivil360, Jurídico360 e GED são acessados apenas mediante vínculo autorizado. Não existe adaptador nesta RC para CadÚnico, Gov.br, SUASWeb ou APIs oficiais: a indisponibilidade deve ser informada, nunca simulada.
