# Roadmap de módulos estratégicos — 2026

## Governança transversal RC50.81

Todos os módulos entregues passam pelo mesmo checklist de rotas, autorização, isolamento de tenant, acessibilidade, exportação segura, performance e observabilidade. A ordem de homologação prioriza Administração/Segurança, Educação360, Saúde360/ACS360, Saneamento360/SIGCOS, Cidadão360, Jurídico360, Obras360, DefesaCivil360, Ativos360, SST360, Carbono360, Energia360, Royalties360, BI/Integrações/Transparência, GED360, Financeiro, Protocolo/Ouvidoria e Fiscaliza360.

Cada módulo somente avança quando não houver pendência crítica, suas rotas reais estiverem navegáveis, permissões vierem do banco e scripts idempotentes estiverem validados no PostgreSQL 16+.

## RC50.82 — Qualidade transversal

- Central de Qualidade e Consistência com fila, evidência, atribuição e histórico: **entregue**.
- Validações persistidas de rotas, Razor, permissões, migrations e integrações: **entregue**.
- Cobertura operacional progressiva dos módulos estratégicos por checklist: **em evolução contínua**, sem catálogos hardcoded.

## RC50.83 — Central Executiva 360

Central decisória integrada ao BI360 e aos módulos operacionais, com Sala de Situação, plano de governo, alertas rastreáveis, pendências, aprovações, briefing e exportações auditadas. Entregue sem catálogos paralelos ou dados simulados.

## RC50.84 — retomada dos módulos estruturantes multi-esfera

Fundação de contexto e histórico entregue para Governança, Protocolo/Ouvidoria/SIC,
Compras/Contratos, Financeiro/Orçamento, Tributos e RH. A próxima promoção exige
homologação no PostgreSQL 16 e build com .NET 10, seguida de validação funcional
por esfera, entidade, órgão, unidade e exercício.
# RC50.85 — Compras e contratos multi-esfera

Base de dados do ciclo completo de contratação, licitação, atas e fiscalização entregue na migration `20260831230000`. Próximo incremento: serviços Dapper e jornadas MVC/Razor reais sobre o novo modelo, seguido de homologação das integrações canônicas Financeiro, GED, Obras360, Ativos360 e Almoxarifado. Integrações externas permanecem desativadas até existir adaptador oficial.

## RC50.86 — Financeiro, orçamento, contabilidade e tesouraria multi-esfera

Modelo canônico multi-esfera entregue para PPA/LDO/LOA, execução de receitas e despesas, tesouraria, conciliação, restos a pagar e prestação de contas. Próximos incrementos devem migrar cada fluxo Dapper legado transacionalmente e habilitar integrações somente quando houver adaptador real homologado.

## RC50.87 — SaaS MNSOFT, IAM e Tributos multi-esfera

Núcleo persistente entregue para clientes, planos, cobrança, bloqueios, perfis e sessões, com precedência de bloqueio global e contexto multi-esfera. Tributos avança com parametrização por esfera/exercício e trilha de lançamento até dívida ativa e certidão. Próxima etapa: adoção transacional pelos serviços Dapper existentes, aliases de navegação somente quando funcionais e homologação PostgreSQL 16/.NET 10.

## RC50.88 — RH360, Folha e Portal do Servidor

Núcleo multi-esfera de servidores, vínculos, atos, frequência, férias, licenças, afastamentos, folha, eSocial, previdência, consignações e auditoria LGPD, com portal pessoal e integrações condicionadas a contrato técnico real.

## RC50.89 — Patrimônio360 e Gestão de Ativos

Consolidação multi-esfera de patrimônio, imóveis, almoxarifado, frota e manutenção, com depreciação, inventário, transferências, alertas, auditoria e referências reais aos módulos transversais. Entrega técnica documentada em `docs/entregas/RC50-89-PATRIMONIO-ALMOXARIFADO-FROTAS.md`.


## RC50.90 — Saúde360 (setembro/2026)
Base multi-esfera de UBS, ACS, agenda, fila, atendimento, regulação, farmácia, vigilância e portal do cidadão entregue com LGPD reforçada. Próximas etapas: homologação dos adaptadores oficiais efetivamente contratados e evolução do prontuário administrativo sob governança clínica.

## RC50.91 — Assistência Social360 (entregue)

Base multi-esfera de SUAS, CRAS/CREAS, famílias, benefícios, acompanhamento, visitas, acolhimento, conselho tutelar, rede de proteção e portal autenticado, com LGPD reforçada e integrações somente por vínculos reais.

## RC50.92 — Saneamento360 e Meio Ambiente360

Consolidação multi-esfera de água, esgoto, drenagem, resíduos, coleta, licenciamento, fiscalização, denúncias, indicadores e transparência, com integrações por referência real ao Cidadão360 e módulos canônicos. Entrega estruturada para municípios, estados, União, autarquias, consórcios, agências e fundos ambientais.
