# ADR — Catálogo SaaS e entitlements

Status: PROPOSTO; implementação RC51.02 no código; ACEITO somente após testes runtime PostgreSQL 16 verdes.

## Contexto

O banco deve ser a autoridade para catálogo comercial, contratação por tenant, vigência, suspensão e permissões. Coleções hardcoded, catálogos demo e consultas diretas de controllers às tabelas de contratação criam decisões divergentes.

## Decisão

Quando P1 for liberado, o catálogo canônico será persistido em `modulo_saas`; a contratação e sua vigência serão persistidas em `tenant_modulo_contratado`. Um avaliador único de entitlement combinará autenticação, tenant ativo, contexto de entidade, contratação vigente e permissão efetiva. Controllers e menus consumirão esse avaliador, sem consultar diretamente `tenant_modulo_contratado`, `tenant_modulo` ou `modulo_saas` para decidir acesso.

Compatibilidade legada será forward-only, auditável e sem remoção de tabelas nesta sprint. Ausência de schema ou configuração falhará explicitamente.

O aplicador operacional deve recusar versões e checksums de ledger ausentes do manifesto. Um checksum histórico somente é compatível quando consta em `knownChecksums` e a migration possui pós-condição específica, reavaliada contra o estado final; configuração comercial mutável não integra essa invariável estrutural.

## Consequências

- Catálogo não concede acesso por si só.
- Contratação não substitui permissão do usuário.
- Menu e API devem produzir a mesma decisão.
- A implementação de catálogo persistido, `IModuleEntitlementEvaluator` e SuperAdmin de contratação entrou na RC51.02. Homologação permanece `AGUARDA_GATE` até PostgreSQL 16 runtime.

## Compatibilidade e suspensão — correção 20260914130000

A autoridade permanece `sigov.tenant_modulo_contratado`; `sigov.tenant_modulo` é somente uma projeção legada unidirecional. A projeção separa `contratado` de `habilitado`: suspensão, inadimplência, cancelamento, expiração, inatividade, vigência futura/encerrada e cancelamento agendado vencido nunca resultam em `habilitado=true`. Atualizações de preço, parâmetros ou descrição não alteram essa decisão porque ela é sempre recalculada a partir dos campos canônicos da mesma linha.

A reativação administrativa remove apenas a suspensão específica do módulo. O serviço recusa reativação quando o tenant está suspenso/cancelado e o avaliador continua verificando tenant, vigência, status contratual, dependências, contexto auditado e permissão do usuário. Operações já iniciadas seguem a transação corrente; toda nova autorização consulta a autoridade persistida, sem cache local de entitlement.

A falha reportada em `20260910120000` foi um falso negativo confirmável no histórico do repositório: a probe procurava o fragmento textual `case when new.ativo and new.status`, enquanto a função publicada expressava a regra equivalente com `new.status in (...)`. A validação agora usa probes independentes dos catálogos PostgreSQL para existência/tabela, habilitação normal, eventos/momento/granularidade, função/schema/assinatura e privilégios/search path. A migration corretiva recria função e trigger de modo idempotente para também reparar instalações realmente ausentes, desabilitadas ou divergentes.

### Diagnóstico somente leitura no ambiente afetado

```sql
select n.nspname as schema_tabela, c.relname as tabela, t.tgname,
       t.tgenabled, t.tgtype, t.tgqual,
       pn.nspname as schema_funcao, p.proname,
       pg_get_function_identity_arguments(p.oid) as assinatura,
       pg_get_triggerdef(t.oid, true) as definicao_trigger,
       pg_get_functiondef(p.oid) as definicao_funcao
from pg_trigger t
join pg_class c on c.oid=t.tgrelid
join pg_namespace n on n.oid=c.relnamespace
join pg_proc p on p.oid=t.tgfoid
join pg_namespace pn on pn.oid=p.pronamespace
where not t.tgisinternal
  and (t.tgname='trg_tenant_modulo_compatibilizar'
       or t.tgrelid=to_regclass('sigov.tenant_modulo_contratado'))
order by n.nspname, c.relname, t.tgname;
```

Essa consulta não executa mutações. O estado concreto do banco informado pelo usuário continua dependente de sua execução naquele ambiente; o diagnóstico do repositório não deve ser apresentado como inspeção do banco real.
