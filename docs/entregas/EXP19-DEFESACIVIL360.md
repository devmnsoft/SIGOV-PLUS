# EXP19 — DefesaCivil360

Entrega da expansão operacional do FUNC19, sem módulo paralelo.

## Entregue

- migration idempotente `20260826180000_exp19_defesacivil360_func19.sql`, com estruturas operacionais, índices, checks e RBAC;
- rotas MVC/Razor em `/DefesaCivil`, incluindo dashboard, risco, contingência, resposta, abrigos, suprimentos, alertas, comunicação, evidências e relatórios;
- persistência Dapper parametrizada e isolada por contexto, seleção rotulada de relacionamentos, antiforgery, validação e auditoria;
- relatórios CSV com neutralização de CSV injection;
- integração por referência com pessoa, patrimônio/frotas, Fiscaliza360, evidência transversal e outbox.

## Limites explícitos

Não foi simulado provedor meteorológico/hidrológico, upload ou publicação externa. Sem adaptador oficial, indisponibilidade e erro sanitizado permanecem explícitos. Ativos360, Cidadão360, Jurídico360 e FUNC21–FUNC24 não fazem parte desta entrega.
