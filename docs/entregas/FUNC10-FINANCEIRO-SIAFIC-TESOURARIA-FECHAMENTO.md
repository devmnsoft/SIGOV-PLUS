# Fechamento FUNC10 — Financeiro/SIAFIC/Tesouraria

## Evidências

* Migration idempotente com PK bigint identity, FKs, índices, checks, triggers, auditoria,
  soft delete, permissões e deduplicação de arrecadação.
* Rotas mínimas de Financeiro e Tesouraria ligadas a tabelas reais e APIs Dapper existentes.
* Dashboard existente consulta agregados orçamentários reais; nenhuma métrica operacional
  é semeada.
* FUNC01 a FUNC09 permanecem no repositório; InovaGED/Protocolo não integra esta mudança.

## Pendências reais / BLOCKED

* Validação SQL de duas aplicações idênticas depende de PostgreSQL 16 e credenciais via
  `ConnectionStrings__DefaultConnection`; quando indisponível, o gate deve registrar BLOCKED.
* Smoke autenticado depende de banco migrado, tenant, entidade, exercício e usuário com
  permissões persistidas.
* Vínculos físicos por FK a Compras, Contratos, Almoxarifado, Patrimônio e Saneamento não
  foram impostos porque os schemas publicados possuem variantes; os IDs são referências
  auditáveis e a resolução deve falhar se a origem informada não existir.
* Certificação SIAFIC, integração bancária/CNAB, PIX e boleto estão fora do escopo e não são
  simulados.

Nenhuma release foi criada ou promovida. RC50.68 continua BLOCKED por gates externos e
RC50.69 continua não iniciada.
