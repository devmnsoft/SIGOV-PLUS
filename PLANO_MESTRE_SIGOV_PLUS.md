# Plano Mestre SIGOV PLUS

## Fundação concluída — RC50.68A
- [x] Fixar .NET 10/C# 14 e contrato único de conexão.
- [x] Remover `.env` do Git e segredos literais dos gates.
- [x] Persistir autorização com recurso+ação e escopos de tenant, entidade, exercício,
  unidade, vigência, alçada e negativa explícita, reutilizando o schema existente.
- [x] Entregar migration/manifest/scripts sincronizados e seed fictícia idempotente dos
  oito perfis institucionais.
- [x] Formalizar Definition of Done e relatório da release.

## Próximas releases (não implementar nesta RC)
- [x] RC50.68B — avaliador de autorização persistente, auditável e precedência de negativas.
- [x] RC50.68C — troca auditada de contexto (tenant/entidade/exercício/unidade), integrada nos
  PRs #267/#268. A implementação está entregue; a promoção conjunta da RC50.68 permanece
  bloqueada pelos gates externos de runtime, banco e CI registrados na RC50.68E.
- [x] RC50.68D — dashboard operacional do SuperAdmin entregue com leitura Dapper, autorização
  persistente, exports protegidos e estados explícitos para áreas opcionais. A promoção permanece
  condicionada aos gates de runtime, PostgreSQL e CI descritos no relatório da RC50.68E.
- [ ] RC50.68E-R4 — execução da esteira tentada no candidato
  `f6da64e3b756640b1322e7d0b8a3e506f7c92311`, mas **BLOCKED** antes do dispatch porque o ambiente
  não possui autenticação GitHub para confirmar o repository secret `SIGOV_CI_DB_PASSWORD` nem
  consultar Actions. Nenhum gate foi marcado PASS e ainda é obrigatória uma run integralmente verde.
- [x] RC50.68E-R5 — entregue esteira local assistida para Windows/Linux, com preflight seguro,
  build runtime, aplicação e reaplicação PostgreSQL 16, autoridade persistida, smoke opcional e
  evidências sanitizadas. A entrega da ferramenta não altera o estado da promoção: **BLOCKED** até
  execução real verde e conclusão do gate oficial.
- [ ] RC50.68E-R6 — decisão técnica local executada no ambiente disponível: checks estáticos PASS,
  porém build, PostgreSQL, smoke e parse PowerShell estão **BLOCKED** por ferramentas/banco/credencial
  ausentes. O CI oficial não foi consultado e a RC50.68 não foi promovida.
- [ ] RC50.69 — ERP Serviços é a próxima macro-release e não foi iniciada. Só pode começar após
  a RC50.68 ficar sem pendência P0/P1.

## Regras permanentes
O banco é a fonte de autoridade para autorização e parametrização. Não são aceitos
mock/demo/fallback, segredo versionado, nova PK UUID ou drift entre migration, manifest
e scripts distribuíveis. Riscos P0/P1 bloqueiam a promoção da release.

## Módulos estratégicos 2026

O roadmap de integração de SST 360, LicitaPro IA, Fiscaliza360, Obras360,
DefesaCivil360, Ativos360, Carbono360, Cidadão360, Jurídico360, Energia360 e
Royalties360 foi consolidado em
[`docs/ROADMAP-MODULOS-ESTRATEGICOS-2026.md`](docs/ROADMAP-MODULOS-ESTRATEGICOS-2026.md).
Os produtos que correspondem a módulos existentes devem evoluir as FUNCs atuais,
sem criar cadastros, permissões, tabelas ou menus paralelos. Os módulos novos são
planejados como FUNC21 SST 360, FUNC22 Carbono360, FUNC23 Energia360 e FUNC24
Royalties360. O registro no roadmap não altera o estado **BLOCKED** da RC50.68 e
não classifica qualquer módulo planejado como funcional ou disponível.

## RC50.68B — avaliador persistente
- [x] Centralizar decisão fail-closed em avaliador Dapper parametrizado.
- [x] Aplicar vigência, escopos, alçada e precedência de `NEGAR`.
- [x] Converter serviços legados e handler Enterprise em adapters sem autoridade paralela.
- [x] Policies Web e diagnóstico da API consomem a mesma decisão persistente, sem autoridade em claims.
- [x] Trilha global de decisões e integridade/estado dos vínculos adicionadas por migration corretiva.
- [x] RC50.68F-R2 estabiliza SQL Dapper, contratos e tela do CRUD, sem alterar a promoção bloqueada.
- [x] RC50.68F fecha o risco residual do CRUD administrativo: SuperAdmin administra perfis, grupos,
  permissões e os três vínculos da matriz com escopos, vigência, efeito, alçada, estado e auditoria.
- [ ] A promoção continua **BLOCKED** pelo gate de CI real da RC50.68E-R4; esta conclusão funcional
  não inicia a RC50.69 nem substitui execução verde da esteira.
- [x] RC50.68C e RC50.68D foram implementadas; sua promoção produtiva integra o gate único da
  RC50.68E e não deve ser confundida com conclusão apenas documental.

## Trilha funcional FUNC01 — Patrimônio e Inventário

- [x] Código ponta a ponta para bens, tombamento, responsabilidade, movimentação, inventário, divergência, baixa, dashboard e CSV LGPD.
- [x] Persistência PostgreSQL bigint, Dapper parametrizado, auditoria transacional e autoridade de permissões no banco.
- [ ] Validação de runtime e banco permanece condicionada às ferramentas/ambiente seguros disponíveis.
- Esta trilha paralela não promove releases: RC50.68 permanece **BLOCKED** por ambiente/CI; RC50.69 permanece não iniciada e não promovida.

## FUNC02 — Almoxarifado (trilha paralela)

- [x] Catálogo e locais por tenant/entidade, estoque transacional sem saldo negativo e ledger imutável.
- [x] Requisições com estados RASCUNHO, ENVIADA, APROVADA, ATENDIDA, CANCELADA e REJEITADA e histórico persistido.
- [x] Integração não destrutiva com FUNC01 por pendência patrimonial única para entradas permanentes.
- [x] MVC/API, dashboard operacional, CSV protegido e RBAC persistente.
- [ ] Gates oficiais de runtime/banco permanecem pendentes; RC50.68 segue BLOCKED e RC50.69 não foi iniciada.

### FUNC03 — Compras, Licitações, Contratos e Atas
Trilha funcional implementada paralelamente, sem alteração do estado da RC50.68 (**BLOCKED**) e sem iniciar RC50.69. Inclui integração rastreável com FUNC01/FUNC02, condicionada à validação PostgreSQL oficial.

## FUNC04 — Frotas, Abastecimento e Manutenção

Trilha funcional paralela entregue em código: autoridade PostgreSQL, Dapper parametrizado, auditoria antes/depois, CPF mascarado, RBAC fail-closed, MVC/API e integrações seguras com FUNC01/FUNC02/FUNC03. A RC50.68 permanece **BLOCKED** e a RC50.69 não foi iniciada.

## FUNC05 — Educação e Gestão Escolar

Trilha funcional paralela entregue com autoridade PostgreSQL, Dapper parametrizado, RBAC fail-closed, auditoria/LGPD, MVC/API, i-Diário, pré-matrícula e portal interno. Não promove a RC50.68 (**BLOCKED**), não inicia a RC50.69 e mantém GED/InovaGED adiado para a etapa final.

## FUNC06 — Saúde, Atenção Básica e Regulação

Trilha funcional paralela entregue com schema corretivo idempotente, Dapper/API, MVC/Razor, RBAC persistente, auditoria e LGPD. Abrange unidades, pacientes, equipes, agenda, acolhimento, prontuário, procedimentos, vacinação, farmácia e regulação. Não promove RC50.68, não inicia RC50.69 e mantém GED/InovaGED para a etapa final.
