# Entrega RC50.92 — Saneamento360 e Meio Ambiente360

## Entregue

- Schema PostgreSQL idempotente com 25 agregados operacionais e ambientais, chaves `bigint identity`, validações geográficas, temporais e financeiras e índices de contexto multi-esfera.
- Permissões mínimas de dashboard, gestão, aprovação, dados sensíveis, exportação e Portal do Cidadão, sem concessão automática a perfis.
- Rotas MVC para água, esgoto, ocorrência, OS, drenagem, resíduos, coleta, rotas, ecopontos, destinação, licenças, pareceres, fiscalização, autos, notificações, embargos, recursos e denúncias.
- Mini manual recolhível nas áreas compartilhadas e mensagens auditáveis já integradas aos fluxos persistidos.
- Scripts completos e manifest sincronizados com checksum SHA-256.

## Integrações

Cidadão360, Tributos, Financeiro, Contabilidade, Frotas, Patrimônio, Fiscaliza360, Jurídico360, GED e Transparência são usados somente por vínculo persistido real. Sensores, balanças e APIs externas permanecem bloqueados na ausência de adaptador oficial.
