# Central Executiva 360

A RC50.83 transforma dados operacionais já persistidos em acompanhamento e decisão, sem duplicar o BI360 e sem fabricar indicadores. O acesso começa em `/Executivo` e exige contexto de `tenant` e entidade.

## Arquitetura

- **Application:** contratos, filtros, regras de decisão e neutralização de CSV injection.
- **Infrastructure:** consultas Dapper/Npgsql parametrizadas e sempre isoladas por `tenant_id` e `entidade_id`.
- **Web:** dashboard e filas MVC/Razor protegidas por permissões persistidas.
- **PostgreSQL:** metas, marcos, vínculos, pendências, alertas, encaminhamentos, aprovações, decisões, briefing, sala e auditoria de exportação.

O dashboard consulta registros reais da Central e falhas da integração interna. Ausência de registros aparece como estado vazio explícito. Dados operacionais continuam pertencendo a Educação360, Saúde360/ACS360, Saneamento360, Financeiro, Jurídico360, Obras360, DefesaCivil360, Transparência, GED360 e demais módulos; `modulo`, `origem_tipo` e `origem_id` mantêm a rastreabilidade.

## Segurança

Todas as rotas são autenticadas e usam permissões `EXECUTIVO_*`. POSTs usam antiforgery. Exportações reaplicam filtros, registram execução e prefixam células iniciadas por caracteres interpretáveis como fórmula. Nenhum dado fictício é semeado.
