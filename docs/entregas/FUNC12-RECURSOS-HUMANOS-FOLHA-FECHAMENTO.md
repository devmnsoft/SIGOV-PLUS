# Fechamento FUNC12 — RH e Folha

## Entrega
- Migration idempotente e scripts consolidados sincronizados.
- 19 tabelas normalizadas: `rh_servidor`, `rh_cargo`, `rh_funcao`, `rh_unidade_lotacao`, `rh_vinculo`, `rh_lotacao_historico`, `rh_dependente`, `rh_frequencia`, `rh_ferias`, `rh_afastamento`, `rh_evento_folha`, `rh_evento_recorrente`, `rh_evento_variavel`, `rh_folha_competencia`, `rh_folha_calculo`, `rh_folha_calculo_item`, `rh_holerite_acesso`, `rh_integracao_financeira` e `rh_auditoria`.
- Dashboard e CRUD MVC/Dapper existentes evoluídos com rotas de dependentes, frequência, folha, holerite, relatórios e auditoria.
- RBAC `RH_*`, LGPD, auditoria, CSV e integração financeira não destrutiva.

## Validações
Registrar neste fechamento o resultado efetivamente executado de `dotnet build`, testes existentes, parse do manifest, invariantes SQL e sincronismo dos consolidados. Banco PostgreSQL indisponível deve ser reportado como **BLOCKED**, nunca como PASS.

## Limitações
PDF de holerite não entregue. Regras legais são dados parametrizados e não constantes de código. Integração não lança execução orçamentária diretamente.
