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

## 6. Reexecução solicitada e classificação dos artefatos

Na reexecução deste ciclo foram inventariados 288 controllers, 201 arquivos de serviço
ou repositório, 1.013 views Razor e 199 fontes de teste C#/JavaScript. Esses números
demonstram somente a superfície inspecionada; não são evidência de jornada concluída.

| Item exigido | Classificação | Evidência desta execução |
|---|---|---|
| README, regras, status e changelogs | Pronto com evidência | arquivos lidos e confrontados com o checkout |
| Manifesto e scripts consolidados | Pronto apenas no gate estático | JSON válido e catálogo/paridade estática aprovados |
| C# e Razor | Não verificado em compilação | SDK .NET 10 ausente |
| DI, startup e rotas runtime | Não verificado | aplicação não pôde iniciar |
| Banco limpo, reaplicação e upgrade | Não verificado | PostgreSQL 16+, `psql` e connection string ausentes |
| Login, menu, SuperAdmin e perfis | Não verificado | runtime e sessão autenticada indisponíveis |
| Isolamento e módulos contratados | Não verificado | exige dois tenants e acesso HTTP autenticado |
| Jornadas funcionais dos Blocos A–F | Parcial | artefatos existem, mas falta evidência conjunta ponta a ponta |
| Gate de páginas críticas | Bloqueado, não quebrado | todas as sondas retornaram HTTP 000 com os processos parados |
| Teste JavaScript disponível | Pronto com evidência | uma suíte/um teste aprovados |

## 7. Regras de negócio consolidadas

Nenhuma regra recebeu promoção nesta reexecução. As regras de tenant, entidade,
exercício, permissões, auditoria, saldo, liquidação, calendário, tramitação e histórico
continuam classificadas como **parciais** até serem exercitadas no servidor e no
PostgreSQL com casos positivos, negativos e concorrentes. Não foi removida validação,
relaxada autorização nem criada autoridade hardcoded.

## 8. Migrations criadas

Nenhuma. O ciclo ficou no Gate A e não houve necessidade comprovada de DDL. As 185
migrations registradas (181 no baseline) e os dez SQLs históricos excluídos permaneceram
inalterados.

## 9. Scripts de banco atualizados

Nenhum. Como não houve alteração de schema, `manifest.json`, `script_completo.sql`,
`script_completo_dev.sql`, `database/script_completo.sql` e `script_completop.sql`
foram preservados. O gate portátil confirmou a paridade estática dos seis consolidados;
equivalência semântica continua bloqueada sem banco.

## 10. Telas alteradas

Nenhuma. Alterar UI antes de build, banco e autorização passarem violaria a ordem do
ciclo. Login, dashboards, formulários, estados vazios, responsividade e mensagens
obrigatórias não foram declarados validados.

## 11. Menus alterados

Nenhum. A existência de links e controllers não comprova contratação, permissão ou
navegação. A validação de menu por SuperAdmin, admin e operador continua pendente.

## 12. Relatórios criados

Nenhum. As exportações existentes permanecem parciais até prova de filtro por tenant,
entidade e exercício, autorização, sanitização e auditoria de dado sensível.

## 13. Dashboards criados

Nenhum. Não foram adicionados indicadores sem uma fonte PostgreSQL executável. Os
dashboards existentes continuam parciais e não foram promovidos por inspeção estática.

## 14. Testes e verificações executados

- `bash -n` aprovou os scripts Bash selecionados para preflight, catálogo, rotas,
  páginas críticas, colisões e artefatos;
- `python3 -m json.tool database/postgres/migrations/manifest.json` aprovou o JSON;
- `bash scripts/check-migration-catalog.sh` aprovou catálogo e paridade estática;
- `bash scripts/check-api-route-conflicts.sh` não encontrou conflito direto nas 630
  rotas inventariadas;
- `bash scripts/check-one-shot-object-collisions.sh` aprovou o consolidado canônico;
- `bash scripts/check-tracked-artifacts.sh` aprovou o gate de artefatos;
- `node --test tests/js/*.test.js` aprovou uma suíte/um teste;
- `bash scripts/check-critical-pages.sh` retornou código 2/**BLOCKED**, corretamente,
  porque API e Web não estavam em execução;
- `bash scripts/check-prerequisites.sh` retornou código 1 e registrou as dependências
  ausentes sem imprimir segredo.

Uma chamada inicial com o padrão inexistente `tests/js/*.test.mjs` retornou erro de
comando. Ela foi corrigida e não foi contabilizada como defeito do produto.

## 15. Evidências de build

**BLOCKED.** `dotnet restore`, `dotnet build` e `dotnet test` não foram simulados:
`dotnet` não existe no `PATH`, embora `global.json` fixe o SDK 10.0.100. Assim, erros
C#, Razor, DI e nulidade não podem ser considerados descartados nesta execução.

## 16. Evidências de migrations

O gate estrutural passou com `SQL=195`, `manifest=185`, `baseline=181` e
`governed_orphans=10`. Aplicação limpa, reaplicação, pós-condições e upgrade continuam
**BLOCKED** pela ausência de PostgreSQL 16+, `psql` e
`ConnectionStrings__DefaultConnection`.

## 17. Evidências de login, autorização e navegação

Não verificadas. As sondas de páginas críticas retornaram HTTP 000 e o verificador
classificou corretamente a indisponibilidade como **BLOCKED**, não como PASS ou defeito
funcional. Não há evidência desta execução para login SuperAdmin/admin/operador,
usuário bloqueado, tenant inativo, módulo não contratado, acesso negado, isolamento
A/B, menu, navegação manual ou mobile.

## 18. Riscos restantes

- **Crítico:** segregação tenant/entidade/exercício e decisão persistida de autorização
  ainda sem prova runtime conjunta;
- **Alto:** convergência do baseline e upgrades representativos não executados;
- **Alto:** regras financeiras, estoque, calendário escolar, protocolo e Jurídico não
  exercitadas com rollback e concorrência reais;
- **Alto:** cobertura de auditoria das operações e exportações sensíveis não comprovada;
- **Médio:** rotas e templates passam apenas por verificações estáticas parciais.

## 19. Próximo ciclo recomendado e critério de saída

Provisionar exatamente o SDK 10.0.100, PowerShell, PostgreSQL 16+ descartável,
`psql`, a connection string oficial e navegador. Em seguida: restore/build/test;
clean install/reaplicação/upgrade; startup; matriz autenticada com SuperAdmin, admin e
operador em dois tenants, duas entidades e exercícios aberto/encerrado; navegação
desktop/mobile. Se, e somente se, tudo passar, selecionar uma única jornada vertical
parcial e fechá-la com banco, Dapper, regra, permissão, auditoria, UI e relatório.

O ciclo solicitado **não está concluído**. O estado final correto é **BLOCKED no Gate
A**, sem promoção funcional artificial.

## 20. Revalidação deste pedido e auditoria do bloco “Como funciona”

O Gate A foi reexecutado antes de qualquer implementação. O diagnóstico confirmou
novamente a ausência de `dotnet`, `pwsh`, `psql`, connection string e processos nas
portas 5000/5001. Consequentemente, não foi permitido alterar tela, controller,
serviço, autorização ou banco. Os gates estáticos continuaram aprovados: sintaxe dos
scripts Bash, manifesto JSON, catálogo/paridade das migrations, 630 rotas API sem
conflito direto, colisões do consolidado, artefatos rastreados e a suíte JavaScript.
O gate HTTP permaneceu **BLOCKED**, com HTTP 000 em todas as páginas críticas.

A inspeção específica do Bloco A encontrou um componente reutilizável já existente,
`Views/Shared/_PageIntro.cshtml`, mas ele ainda não atende ao contrato solicitado:
o título visível é “Guia da tela”, há valores genéricos por omissão e não existem
seções explícitas para “Quando usar”, “Campos obrigatórios”, “O que acontece ao
salvar/concluir” e “Permissões necessárias”. A ocorrência do partial, sozinha, não
foi aceita como conformidade. Também foram encontrados textos genéricos repetidos em
Tenants e Módulos, Perfis depende dos defaults genéricos, e as telas auditadas de
Auditoria não usam o componente. Essas lacunas ficam registradas para correção somente
depois do Gate A verde, acompanhadas de compilação Razor e navegação responsiva.

| Área | Funcionalidade | Estado anterior | Estado final | Tela tem “Como funciona”? | Evidência | Pendência |
|---|---|---|---|---|---|---|
| Fundação | restore/build/test/startup | Não verificado | Bloqueado | Não se aplica | `dotnet` ausente; preflight falhou fechado | prover SDK 10.0.100 e executar os comandos normativos |
| Banco | clean install, reaplicação e upgrade | Não verificado | Bloqueado | Não se aplica | catálogo estático PASS: 195 SQL, 185 no manifesto e 181 no baseline | prover PostgreSQL 16+, `psql` e connection string |
| SaaS | Tenants | Parcial | Parcial | Parcial, conteúdo genérico | `Views/Saas/Tenants.cshtml` usa `_PageIntro` | conteúdo específico com os sete tópicos; validar persistência e autorização |
| SaaS | Entidades | Parcial | Parcial | Não comprovado | rota/view estática localizada, sem navegação | localizar fluxo canônico persistido e validar os sete tópicos |
| SaaS | Exercícios | Parcial | Parcial | Não comprovado | rotas dispersas, sem navegação | consolidar fluxo canônico e provar bloqueio de exercício encerrado |
| Segurança | Usuários | Parcial | Parcial | Parcial | `Views/Seguranca/Usuarios.cshtml` usa `_PageIntro` específico, mas incompleto | explicitar obrigatórios, resultado e permissão; eliminar ID manual de pessoa |
| Segurança | Perfis | Parcial | Parcial | Não conforme | `Views/Seguranca/Perfis.cshtml` usa defaults genéricos | conteúdo específico e prova transacional/auditoria |
| Segurança | Permissões | Parcial | Parcial | Parcial | conteúdo específico existe em `Views/Seguranca/Permissoes.cshtml` | explicitar campos, regra ao salvar e perfil/permissão necessários |
| SaaS | Módulos contratados | Parcial | Parcial | Parcial, conteúdo genérico | `Views/Saas/Modulos.cshtml` repete o texto de Tenants | remover modo demonstração e validar entitlement/menu/URL com persistência real |
| Auditoria | consulta administrativa | Parcial | Parcial | Não | views de Auditoria não referenciam `_PageIntro` | orientação específica, estados e prova de segregação |
| Operação | diagnóstico do tenant | Parcial | Parcial | Não comprovado | rota `/Operacao/Diagnostico` localizada | identificar view/contrato canônico e validar por perfil |
| Demais módulos dos Blocos C–F | jornadas solicitadas | Parcial | Parcial | Não verificado integralmente | inventário estático amplo, sem runtime conjunto | selecionar uma jornada vertical após Gate A e fechar ponta a ponta |

### Entrega objetiva desta revalidação

1. **Auditado:** pré-requisitos, manifesto, catálogo/paridade, rotas, artefatos,
   colisões SQL, suíte JavaScript, páginas críticas e amostra dirigida das telas do
   Núcleo SaaS/Segurança.
2. **Quebrado/bloqueado:** ambiente sem toolchain/runtime/banco e contrato incompleto
   do componente explicativo; não há evidência autenticada de login ou isolamento.
3. **Corrigido/implementado:** nenhuma funcionalidade, por imposição do Gate A.
4. **Parcial:** todos os domínios funcionais e o componente explicativo.
5. **Regras consolidadas:** nenhuma regra promovida sem execução real.
6. **Telas com “Como funciona”:** nenhuma tela alterada; ocorrências preexistentes
   foram auditadas e classificadas na tabela.
7. **Migrations/scripts/menus/relatórios:** nenhum criado ou alterado.
8. **Evidência de build/login/navegação:** **BLOCKED**, nunca simulada como PASS.
9. **Risco restante:** crítico para autorização/isolamento e alto para banco e fluxos
   transacionais.
10. **Próximo ciclo:** destravar Gate A e, então, corrigir `_PageIntro` e fechar uma
    única jornada vertical com Dapper, autorização, auditoria, UI e testes existentes.
