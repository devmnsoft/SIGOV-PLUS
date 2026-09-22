# Auditoria do ciclo de estabilização — 2026-09-22

## 1. Decisão do gate

**Ciclo bloqueado no Gate A; nenhuma funcionalidade dos Blocos A–J foi promovida.**

Foram lidos o `README.md`, as regras do repositório, o status real, o changelog, o
manifesto, o inventário de migrations e scripts, a auditoria anterior e as superfícies
de projetos, controllers, serviços, repositórios, views e testes. A ausência de SDK
.NET 10, PowerShell, `psql`/PostgreSQL 16+, `ConnectionStrings__DefaultConnection` e
navegador autenticado impede comprovar compilação, aplicação limpa/upgrade, startup,
login, menu, autorização e segregação. Por isso, existência estática não foi classificada
como funcionalidade pronta.

## 2. Falha estrutural encontrada e correção

O diagnóstico PowerShell divergia do contrato oficial: exigia `SIGOV_DB_PASSWORD` e
`.env.local`, sondava as portas antigas 7000/7001 e aceitava exclusivamente PostgreSQL
16. O diagnóstico Bash também rejeitava versões posteriores. Além disso, os smokes
RC50.68 consultavam endpoints antigos e limitavam o servidor a PostgreSQL 16.x.

Os gates agora:

- usam `ConnectionStrings__DefaultConnection` sem registrar seu valor;
- verificam as portas oficiais 5000, 5001 e 5432;
- aceitam PostgreSQL 16 ou superior;
- consultam exclusivamente `/api/health/live` no smoke;
- continuam fail-closed: ferramenta, configuração ou runtime ausente resulta em
  `MISSING_TOOL`, `MISSING_ENV`, `BLOCKED` ou falha, nunca em sucesso simulado.

Não houve alteração de schema, migration, script consolidado, tela ou menu.

## 3. Estado real dos 18 módulos

| Área | Funcionalidade | Estado anterior | Estado final | Evidência | Pendência |
|---|---|---|---|---|---|
| SaaS/Administração | tenants, entidades, usuários, módulos e onboarding | Parcial | Parcial | catálogo, serviços, controllers e UI inventariados | fluxo transacional, bloqueios e auditoria em PostgreSQL/runtime |
| Segurança/Autenticação/Permissões | login, políticas e contexto | Parcial | Parcial | avaliador e contexto persistidos no checkout | login por perfis, negação direta e isolamento A/B |
| Auditoria/LGPD | trilhas, mascaramento e exportações | Parcial | Parcial | tabelas/serviços/rotas e sanitização localizados | provar cobertura, histórico e segregação em runtime |
| Financeiro/SIAFIC | empenho, liquidação e pagamento | Parcial | Parcial | migrations, Dapper, APIs/Web e testes existentes | provar saldo, exercício e pagamento somente após liquidação |
| Compras/Licitações/Contratos/Atas | contratação e recebimento | Parcial | Parcial | migrations corretivas e jornadas parciais | convergência, vencimentos, sanções e rollback concorrente |
| Almoxarifado | estoque, requisição e transferência | Parcial | Parcial | serviços transacionais e UI existentes | provar saldo não negativo, locks, retry e inventário |
| Patrimônio | tombamento, custódia, baixa e inventário | Parcial | Parcial | migrations/serviços/views existentes | provar histórico, baixa e transferência em escopo |
| Frotas | utilização, abastecimento, manutenção e OS | Parcial | Parcial | regras/serviços/views existentes | provar hodômetro, veículo inativo e integração atômica |
| RH/Folha/Portal | pessoal, folha e autosserviço | Parcial | Parcial | ampla estrutura persistida e UI | cálculo, fechamento, LGPD e acesso aos próprios dados |
| Educação | ingresso, matrícula, transferência e diário | Parcial | Parcial | regras transacionais recentes e UI existentes | concorrência de vaga, chamada e calendário em PostgreSQL |
| Saúde/ACS | território, cidadão, visita e atenção básica | Parcial | Parcial | migrations, serviços, rotas e telas existentes | território, deduplicação, dado sensível e histórico |
| Saneamento | leitura, faturamento e operação | Parcial | Parcial | migrations e jornadas Web/API existentes | tarifa, leitura, arrecadação e isolamento ponta a ponta |
| Meio Ambiente | licenças, condicionantes e fiscalização | Parcial | Parcial | migration e superfície Web localizadas | confirmar Dapper/API, prazos, anexos e auditoria |
| Protocolo Digital | abertura, tramitação e prazos | Parcial | Parcial | estruturas persistidas e rotas localizadas | conclusão/reabertura, escopo setorial, anexos e alertas |
| Jurídico | processo, parecer, movimento e prazo | Parcial | Parcial | políticas persistidas e estrutura existente | histórico imutável, alteração auditada e filtros/alçadas |
| Relatórios | filtros, CSV e totalizadores | Parcial | Parcial | exportações dispersas por módulo | paginação, escopo e auditoria sensível por relatório |
| Dashboards | operacional e SuperAdmin | Parcial | Parcial | consultas e views existentes | KPIs, escopo e navegação autenticada sem dados fictícios |
| Menu/template/design | navegação e design system | Parcial | Parcial | layouts, assets e menus existentes | entitlement, rotas e responsividade em navegador real |

Nenhuma área recebeu **pronto com evidência**, pois os critérios exigidos não puderam
ser demonstrados conjuntamente.

## 4. Validações executadas

### Passaram

- sintaxe Bash dos scripts versionados;
- JSON do manifesto;
- gate de artefatos rastreados;
- 630 rotas API sem conflito direto;
- colisões estáticas do SQL consolidado;
- verificadores de índices concluídos com avisos conservadores legados.

### Bloqueadas

- `dotnet restore`, `dotnet build` e `dotnet test`: SDK 10.0.100 ausente;
- clean install, reaplicação e upgrade: `psql`, PostgreSQL e connection string ausentes;
- validação PowerShell dos consolidados: `pwsh` ausente;
- aplicação, login, dashboard, menu, estados de UI e matriz de segurança: runtime e
  navegador autenticado indisponíveis.

Uma tentativa inicial de `node --test tests/js/*.test.mjs` falhou porque a suíte real
usa extensão `.js`; isso é erro de comando da auditoria, não falha do produto. A suíte
correta deve ser executada como `node --test tests/js/*.test.js`.

## 5. Riscos e próximo ciclo recomendado

O risco permanece crítico para autorização/isolamento e alto para convergência do banco
e regras transacionais. O próximo ciclo deve primeiro prover SDK 10.0.100, PowerShell,
PostgreSQL 16+ descartável e a connection string oficial; depois executar build/testes,
clean install/reaplicação/upgrade, startup e uma matriz autenticada com SuperAdmin,
admin e operador em dois tenants, duas entidades e exercícios aberto/encerrado. Somente
com o Gate A verde deve ser escolhida uma jornada vertical para evolução funcional.
