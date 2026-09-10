# Status real dos módulos

Corte: 2026-09-10; referência inicial ec799b75a6a1b3156566afc8a21216748f82df24 (origin/main).
Inventário documental: docs/inventario-modulos-sigov.md, docs/execucao/rc50_67_plano_homologacao_integrada_real.md e docs/roadmap/saas-industria-auditoria.md. Classificação conservadora: os 15 requisitos do contrato não foram demonstrados conjuntamente em runtime.

| Domínio/módulos existentes | Status | Evidência existente e lacuna de aprovação |
|---|---|---|
| Core, identidade, segurança, permissões | PARCIAL | AuthenticationRepository, avaliador persistido, API key por hash/escopos e sessão persistente com revogação; prova runtime PostgreSQL 16 e isolamento ponta a ponta pendentes |
| Auditoria e LGPD | PARCIAL | Serviços/tabelas/rotas existentes; trilha e segregação runtime pendentes |
| SaaS, planos, contratação, administração global/cliente | AGUARDA_GATE | modulo_saas e tenant_modulo_contratado existem, mas a consolidação funcional permanece bloqueada até P0 PostgreSQL 16; catálogos/controladores duplicados ainda exigem inventário antes de P1 |
| Indústria Core | PARCIAL | Application/Industria, Infrastructure/Industria, API/Web/Views/Industria; consulta real, fluxo mutável completo não homologado |
| Indústria avançada | ESTRUTURA | Planejamento MRP/MPS/IoT; não há evidência de fluxo completo |
| Comercial, OS, manutenção industrial | PARCIAL | Serviços Enterprise e pontos de integração; contratos e transações integradas pendentes |
| Compras, licitações, contratos | PARCIAL | Migrations corretivas/FKs e controllers; convergência de banco não demonstrada |
| Almoxarifado, patrimônio, ativos | PARCIAL | Fluxos FUNC01/FUNC02; concorrência e isolamento runtime pendentes |
| Financeiro público, empresarial e tributário/NFS-e | PARCIAL | Controllers/services e operações parciais; transições e provedores não homologados |
| RH, folha, portal do servidor | PARCIAL | Contratos tipados e serviços existentes; validação integrada pendente |
| Educação | PARCIAL | Escola/turma/diário/portal existentes; migrations órfãs e jornadas pendentes |
| Saúde e assistência social | PARCIAL | Serviços/rotas existentes; dados sensíveis e segregação exigem prova runtime |
| Frotas, manutenção, obras, fiscalização e engenharia | PARCIAL | Estruturas dos blocos existentes; fluxos e integração não homologados |
| Saneamento e meio ambiente | PARCIAL | Estruturas e serviços existentes; operações ponta a ponta pendentes |
| Processos, protocolo, ouvidoria e e-SIC | PARCIAL | Persistência parcial; transação/autorização/runtime pendentes |
| Jurídico | PARCIAL | Estrutura existente; filtros e alçadas de relatórios a auditar por fluxo |
| Agro e Campo/Geo | PARCIAL | Preservar serviços e UX existentes; falta evidência runtime atual dos 15 critérios |
| Integrações, mobilidade/offline, observabilidade | PARCIAL | Outbox, adapters e rotas; providers/isolamento/runtime pendentes |
| Legislativo, transparência, diário oficial, convênios, trânsito, defesa | PARCIAL | Inventários e controllers existentes; verificar cada fluxo após P0–P3 |
| GED e assinaturas | PARCIAL | Estrutura histórica existente; catálogo rebaixado para não promover GED na RC51.00; implementação bloqueada até última fase |

Nenhum módulo foi promovido a FUNCIONAL, HOMOLOGADO ou PRODUCAO nesta execução. RC51.02 estabilizou o resolvedor estático de migrations e o saneamento de artefatos rastreados, mas P0 runtime PostgreSQL 16 permanece BLOCKED; SaaS Admin/Entitlements não foi iniciado.

