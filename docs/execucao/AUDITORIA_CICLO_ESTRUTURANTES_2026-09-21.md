# Auditoria do ciclo de módulos estruturantes — 2026-09-21

## Decisão do gate

**Resultado: BLOQUEADO no Bloco A; Blocos B a I não iniciados.**

Esta auditoria foi feita sobre o branch `work`, sem promover a existência de arquivos a
funcionalidade homologada. O ambiente não dispõe de .NET 10, PowerShell, PostgreSQL
16/`psql`, `ConnectionStrings__DefaultConnection` nem navegador autenticado. Portanto,
não foi possível demonstrar build, DI, compilação Razor, instalação limpa, upgrade,
login, navegação, autorização ou isolamento entre dois tenants. Conforme o gate pedido,
nenhuma evolução funcional ou alteração de schema foi iniciada.

O inventário histórico de julho (`docs/inventario-modulos-sigov.md`) está defasado: ele
classifica várias superfícies atuais como demonstrativas e não pode ser usado isoladamente
como evidência. A fonte conservadora vigente continua sendo
`docs/execucao/STATUS_REAL_MODULOS.md`.

## Evidência reproduzível

| Verificação | Resultado | Evidência/limitação |
|---|---|---|
| Pré-requisitos | **BLOCKED** | `./scripts/check-prerequisites.sh`: `dotnet`, `pwsh` e `psql` ausentes; connection string ausente; sondagem de portas sem `ss`/`lsof` |
| Sintaxe shell | **PASS** | `bash -n scripts/*.sh scripts/db/*.sh scripts/ci/*.sh scripts/homologacao/*.sh` |
| Manifesto | **PASS estático** | JSON válido e `VALIDATE_ONLY=true ./scripts/apply-migrations-manifest.sh` valida entradas, unicidade, arquivos e checksums |
| Rotas API | **PASS estático** | 630 rotas sem conflito direto segundo `./scripts/check-api-route-conflicts.sh` |
| SQL consolidado | **PASS estático** | `./scripts/check-one-shot-object-collisions.sh`; não substitui execução no PostgreSQL |
| Índices | **PASS com alertas** | verificadores retornam zero, mas registram riscos conservadores em migrations legadas |
| Artefatos rastreados | **PASS** | `./scripts/check-tracked-artifacts.sh` |
| Contraste básico | **PASS** | `node scripts/check-ui-contrast.mjs` |
| Restore/build/test | **BLOCKED** | SDK 10.0.100 ausente |
| Banco limpo/reaplicação/upgrade | **BLOCKED** | PostgreSQL 16, `psql`, credencial e PowerShell ausentes |
| Runtime/login/menu/permissões | **BLOCKED** | aplicação não pode ser iniciada; não há navegador/sessão autenticada |

## Mapeamento estático por prioridade

“Existe” abaixo significa somente que os artefatos foram localizados no checkout. Não
significa que o fluxo passou em runtime.

| Módulo | Existe no checkout | Parcial/quebrado ou não demonstrado | Ausente ou sem evidência suficiente |
|---|---|---|---|
| Financeiro / Contábil / SIAFIC | Migrations `018`, `rc50_36`, `func10`, `rc50_86`; domínio `Empenho`; contratos/serviços financeiros; repositório Dapper; controllers API e Web; views de orçamento, receita, empenho, liquidação e pagamento; testes unitários financeiros | Transições, saldo, exercício, transação e auditoria não foram executados; Tesouraria/SIAFIC ainda têm superfícies historicamente demonstrativas; convergência dos vários modelos financeiros não foi homologada | Prova de pagamento somente após liquidação, limites de saldo, exercício encerrado, isolamento tenant/entidade/exercício e fluxo ponta a ponta |
| Compras / Licitações / Contratos / Atas | Migrations `rc50_37`, `func03`, `rc50_85` e corretivas; domínio de processo; contratos e serviços Dapper; API/Web; views de fornecedores, processos, julgamento, recebimento, contratos e atas | Há mais de uma superfície histórica de compras; o validador de contratos de schema existe, mas banco não foi aplicado; recebimento parcial, rollback e idempotência não foram executados | Prova concorrente de saldo, sanção de fornecedor, cancelamento, homologação específica, ata vencida, aditivo e auditoria integrada |
| Almoxarifado | Migrations `func02`, `rc50_89`, distribuição, transferência e reposição; contratos; três serviços Dapper; API/Web; views de materiais, locais, estoque, requisições, transferências e reposição | Locks e transações estão declarados em código, mas sem PostgreSQL não há prova de concorrência, saldo não negativo, rollback ou isolamento | Kardex/inventário/lote devem ser confirmados por jornada; integração de recebimento e consumo de peças precisa de execução transacional |
| Patrimônio | Migrations `func01`, `rc50_89` e jornada de custódia; serviços parciais por incorporação/inventário/movimentação/transferência; API/Web; views de bens, inventários e termos | Tombamento, baixa, transferência, custódia e auditoria não foram exercitados; compatibilidade de upgrade não demonstrada | Prova de unicidade, bloqueio de bem baixado, responsável no escopo e histórico imutável |
| Frotas | Migrations `func04`, `rc50_89`; contratos/repositório/serviço; API/Web; views de veículos, motoristas, uso, abastecimento, manutenção e OS | Regras de hodômetro, veículo inativo e consumo atômico de peças existem documentalmente, mas estão sem execução nesta auditoria | Prova de concorrência, rollback estoque/OS, autorização da justificativa e isolamento institucional |
| RH / Folha / Portal | Migrations `020`, `rc50_24`, `rc50_31`, `rc50_32`, `func12`, `rc50_88`; domínio Servidor; DTOs/serviços/repositório; API/Web; views extensas de cadastro, frequência, folha e portal; testes localizados | Modelos históricos sobrepostos exigem convergência em banco; folha fechada/reabertura, fórmulas parametrizadas, LGPD e escopo do próprio servidor não foram demonstrados | Prova ponta a ponta de cálculo parametrizado, fechamento, reabertura auditada e acesso exclusivo aos próprios dados |
| Saneamento | Migrations `023`, `rc50_38`, `rc50_50`, `func07`, `rc50_92`; entidades e regras; repositórios; APIs/Web; views de consumidores, ligações, hidrômetros, leituras, faturas, arrecadação, OS e laboratório; testes unitários/API/smoke existentes | Leitura, tarifa, competência, faturamento e OS não foram executados; nenhum teste compilou neste ambiente | Prova de tarifa válida, leitura regressiva tratada, unidade inativa, troca de hidrômetro e isolamento |
| Meio Ambiente | Migration `func14` e `rc50_92`; controller Web; views de painel, lista, formulário e relatórios | Cobertura encontrada é menor que saneamento; persistência Dapper/API específica e histórico de parecer/anexo precisam ser confirmados após build e schema report | Evidência de condicionantes, vencimento, infração, anexos, autorização e auditoria ponta a ponta |
| Menu / permissões / SaaS | Catálogo modular, entitlements, avaliador persistido, contexto request-scoped, controllers de segurança/SaaS, menus/layouts e testes de permissões/tenant | Acesso direto, módulo contratado, precedência de negação e troca de contexto não foram exercitados; P0/P1 do avaliador/contexto ficam fora desta correção documental | Matriz autenticada com SuperAdmin, admin tenant, gestor, operador e consulta, em dois tenants e múltiplas entidades/exercícios |

## Banco, migrations e scripts

- O manifesto referencia o catálogo de migrations e seus checksums; a validação estática
  passou. Isso não comprova que todas as migrations convergem em PostgreSQL.
- Os scripts canônicos reconhecidos são
  `database/postgres/script_completo.sql`,
  `database/postgres/script_completo_dev.sql`,
  `database/script_completo.sql`, `script_completo.sql` e
  `script_completop.sql`.
- A geração/verificação determinística depende de PowerShell
  (`scripts/generate-script-completop.ps1 -Verify`), ausente neste ambiente. Logo, a
  sincronização byte a byte não foi novamente certificada neste ciclo.
- Nenhuma migration foi criada ou alterada, pois o gate de banco falhou e não houve
  mudança de schema.
- Os alertas dos verificadores de índices são dívida de compatibilidade a revisar em
  migrations corretivas; migrations publicadas não devem ser editadas.

## Matriz de aceite deste ciclo

| Módulo | Funcionalidade | Estado anterior | O que foi implementado | Arquivos alterados | Migration criada | Teste/validação | Resultado | Pendências | Risco residual |
|---|---|---|---|---|---|---|---|---|---|
| Fundação | Auditoria e Gate A | Bloqueado sem runtime | Inventário e evidência estática atualizados; bloqueio preservado sem falso positivo | Este relatório e status real | Não | Pré-requisitos e gates estáticos | **BLOQUEADO** | SDK, PowerShell, PostgreSQL, credencial e navegador | Crítico |
| Financeiro/SIAFIC | Fluxo público completo | Parcial | Não iniciado por gate | Nenhum código | Não | Inventário estático | **NÃO EXECUTADO** | Regras, transações, UI e autorização em runtime | Alto |
| Compras/contratos/atas | Contratação pública | Parcial | Não iniciado por gate | Nenhum código | Não | Inventário estático | **NÃO EXECUTADO** | Jornada e concorrência em banco | Alto |
| Almoxarifado/patrimônio/frotas | Movimentação física | Parcial | Não iniciado por gate | Nenhum código | Não | Inventário estático | **NÃO EXECUTADO** | Saldo, locks, histórico e integrações | Alto |
| RH/folha/portal | Pessoal e autosserviço | Parcial | Não iniciado por gate | Nenhum código | Não | Inventário estático | **NÃO EXECUTADO** | Parametrização, LGPD e segregação | Crítico |
| Saneamento/meio ambiente | Operação setorial | Parcial | Não iniciado por gate | Nenhum código | Não | Inventário estático | **NÃO EXECUTADO** | Tarifas, licenças e auditoria | Alto |
| Menu/permissões/SaaS | Entitlement e acesso | Parcial | Não iniciado por gate | Nenhum código | Não | 630 rotas sem colisão direta | **PARCIAL estático** | Navegação e negação autenticadas | Crítico |

## Próximo ciclo obrigatório

1. Prover SDK .NET 10.0.100, PowerShell, PostgreSQL 16 e uma connection string
   descartável em `ConnectionStrings__DefaultConnection`.
2. Executar restore/build/test normativos sem relaxar warnings ou testes.
3. Executar `generate-script-completop.ps1 -Verify`, instalação limpa, reaplicação e
   upgrade representativo; arquivar ledger e pós-condições.
4. Iniciar Web/API, testar login e percorrer menus com módulo contratado/não contratado.
5. Executar matriz A/B com dois tenants, duas entidades e exercícios aberto/encerrado.
6. Somente depois selecionar uma jornada vertical de maior risco, começando por
   empenho → liquidação → pagamento, e entregar banco, regra, transação, auditoria,
   API, UI, autorização e testes como uma unidade.

