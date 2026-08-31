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
