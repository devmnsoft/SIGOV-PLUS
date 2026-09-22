# Changelog

## 2026-09-22 — auditoria do ciclo funcional condicionada ao Gate A

- reexecutados os pré-requisitos e gates portáveis antes de qualquer implementação;
- confirmado o bloqueio por ausência de .NET 10, PowerShell, PostgreSQL/`psql`,
  connection string e aplicações iniciadas, com páginas críticas em HTTP 000;
- publicado o relatório nas 16 seções solicitadas e a matriz final obrigatória,
  preservando o estado parcial dos módulos e sem alterar código, schema, scripts
  consolidados, menus ou telas antes do Gate A verde.

## 2026-09-22 — revalidação do Gate A e do guia de telas

- reexecutados os gates portáveis antes de qualquer evolução funcional, mantendo o
  ciclo bloqueado sem .NET 10, PowerShell, PostgreSQL/`psql`, connection string e
  aplicação autenticada;
- auditado o componente compartilhado `_PageIntro` e uma amostra dirigida das telas
  obrigatórias do Núcleo SaaS/Segurança, registrando que a presença do partial não
  satisfaz os sete tópicos exigidos nem substitui conteúdo específico;
- acrescentada ao relatório a matriz obrigatória com a coluna “Tela tem Como
  funciona?”, sem alterar telas, migrations, scripts consolidados, menus ou regras de
  negócio antes do Gate A verde.

## 2026-09-22 — reexecução do ciclo de funcionalidades essenciais

- reexecutados os gates portáveis de catálogo/paridade, rotas, colisões SQL,
  artefatos, páginas críticas e a suíte JavaScript disponível;
- ampliado o relatório de estabilização para as 19 seções requeridas, distinguindo
  evidência estática, item parcial, não verificado e bloqueio de infraestrutura;
- mantido o Gate A como **BLOCKED** sem .NET 10, PowerShell, PostgreSQL/`psql`,
  connection string e sessão autenticada, sem promover módulo nem alterar migrations,
  scripts consolidados, telas, menus, relatórios ou dashboards.

## 2026-09-22 — paridade portável dos scripts PostgreSQL consolidados

- O gate Bash do catálogo passou a comparar os quatro aliases de produção e os
  dois consolidados de desenvolvimento, além de conferir ordem, checksum e corpo
  normalizado de cada migration incluída no baseline.
- Migrations excluídas continuam fora do baseline e agora precisam manter sua
  decisão explícita no consolidado; os seeds fictícios de desenvolvimento também
  são comparados deterministicamente.
- O catálogo e os seis scripts estavam sincronizados. A aplicação limpa,
  reaplicação e upgrade permanecem bloqueados sem PostgreSQL 16 e `psql`.

## 2026-09-22 — gate de páginas críticas fail-closed

- O verificador Bash de páginas críticas agora registra `BLOCKED` quando `curl`, API
  ou Web estão indisponíveis, preservando `FAIL` para respostas HTTP inesperadas.
- O artefato do smoke passou a conter resumo final explícito e não expõe o ruído de
  conexão do cliente HTTP.
- A auditoria permanece conservadora: nenhum módulo foi promovido sem .NET 10,
  PostgreSQL 16, credencial e navegação autenticada.

## 2026-09-22 — gate estático portável do catálogo de migrations

- Adicionado `scripts/check-migration-catalog.sh` para validar, sem PowerShell,
  estrutura, ordenação, flags, dependências, checksums normalizados e governança das
  migrations excluídas do manifesto.
- Corrigida a matriz da auditoria prioritária para enumerar explicitamente as dezoito
  áreas obrigatórias, mantendo todas como parciais enquanto o Gate A runtime estiver
  bloqueado.
- Nenhuma migration, DDL, script consolidado, tela, menu ou regra de produto foi
  alterada neste ciclo.

## Auditoria das jornadas prioritárias — 2026-09-22

- reauditadas, de forma conservadora, as jornadas de Educação, Saúde/ACS, Protocolo,
  Jurídico e suas integrações administrativas, além de segurança, relatórios,
  dashboards e template/menu;
- mantidos todos os módulos como parciais, sem promover presença estática a evidência
  funcional e sem iniciar alterações antes do Gate A obrigatório;
- registrados os gates estáticos aprovados e os gates runtime bloqueados pela ausência
  de SDK .NET 10, PowerShell, PostgreSQL/`psql`, connection string e navegador
  autenticado; nenhuma migration, script consolidado, tela ou menu foi alterado.

## Estabilização dos gates locais — 2026-09-22

- alinhados os diagnósticos de pré-requisitos Bash e PowerShell ao contrato oficial de configuração, portas e PostgreSQL 16 ou superior;
- removida do diagnóstico PowerShell a exigência paralela de senha isolada e arquivo `.env.local`, preservando `ConnectionStrings__DefaultConnection` como contrato único;
- corrigidos os smokes RC50.68 nas duas plataformas para consultar `/api/health/live` e aceitar versões PostgreSQL futuras compatíveis;
- reexecutada a auditoria conservadora dos 18 domínios solicitados, mantendo todos como parciais ou não verificados enquanto build, banco, login, autorização e navegação estiverem bloqueados pelos pré-requisitos ausentes.

## Jornada de transferência escolar — 2026-09-21

- substituída a atualização destrutiva da matrícula por uma transferência transacional que encerra o vínculo de origem e cria uma nova matrícula ativa no destino;
- validados no servidor o estado da matrícula, a compatibilidade do ano letivo, a escola ativa, a turma aberta e a disponibilidade de vaga, sempre no mesmo tenant e entidade;
- movimentadas as vagas de origem e destino na mesma transação e registrados eventos de auditoria correlacionando as duas matrículas e a justificativa obrigatória.

## Estabilização das permissões do Jurídico — 2026-09-21

- incluídas no catálogo canônico as políticas persistidas usadas pelas jornadas de advogados, partes, movimentações, intimações, audiências, consultas e dívida ativa;
- registrada a política administrativa de consulta à auditoria jurídica, mantendo a decisão de autorização no serviço persistido;
- eliminada a falha de política inexistente nas ações de listagem e manutenção do controlador Jurídico, sem criar permissões hardcoded como autoridade.
- restaurada a proteção antifalsificação explícita no formulário de política de reposição do Almoxarifado, detectada pelo gate estático de views.

## Estabilização do smoke local — 2026-09-21

- alinhados os defaults do gate de páginas críticas ao contrato oficial local: Web em `http://localhost:5000` e API em `http://localhost:5001`;
- substituída a sonda inexistente `/health` pelo endpoint público canônico `/api/health/live`;
- preservados os overrides de ambiente usados pelo gate de produção, inclusive a porta Web alternativa configurada explicitamente pelo workflow.

## Estabilização de contexto do Kanban — 2026-09-21

- removido o UUID de tenant de desenvolvimento usado como fallback no Kanban operacional;
- consultas sem claim `tenant_id`/`tenant` válida agora retornam estado vazio explícito em todos os ambientes, enquanto alterações continuam bloqueadas;
- a tela informa que o contexto autorizado deve ser selecionado, sem simular dados ou sucesso.

## FUNC06 Saúde, Atenção Básica e Regulação — 2026-09-21

- **FUNC06 — jornada básica servidor-first no padrão FUNC01–05; alertas/toasts/confirm no layout; LGPD em lista/CSV; RC50.68 BLOCKED:**
  - padronização transversal de alertas e mensageria no layout compartilhado (`_Alerts.cshtml`, `sigov-alerts.js`, `sigov-components.css`) com suporte a banners TempData de sucesso/aviso/erro após POST-Redirect-Get, auto-dismiss em 8 segundos, toasts acessíveis empilháveis e diálogos modal `data-confirm` para ações destrutivas ou irreversíveis;
  - reestruturação de todas as views de Saúde em renderização server-first com degradação graciosa em empty states, eliminando placeholders e containers ocos dependentes exclusivamente de JS de módulo;
  - seletores amigáveis por nome em todos os formulários (`Unidades`, `Profissionais`, `Pacientes`, `Agenda`, `Atendimentos`, `Vacinacao`, `Farmacia`, `Regulacao`), sem IDs técnicos digitados;
  - validações de autoridade no serviço:
    - bloqueio estrito de unidades e profissionais inativos em agendamentos, acolhimentos, atendimentos e dispensações;
    - bloqueio de sobreposição de horários da agenda para o mesmo profissional e obrigatoriedade de motivo em cancelamentos;
    - classificação de risco Manchester obrigatória na finalização de acolhimentos; contagem agregada de risco LARANJA/VERMELHO no dashboard sem exposição de queixa ou dado clínico;
    - imutabilidade formal de evolução de atendimento com status `ATENDIDO`, com fluxo auditado de retificação vinculada com justificativa (`saude.prontuario.retificar`);
    - recusa de vacinação com lote vencido ou cancelamento sem motivo, e bloqueio de dose duplicada sem justificativa;
    - dispensação de farmácia com validação de saldo quando vinculada a material de estoque (`material_id`); quando sem vínculo, registro sem baixa automática e exibição de aviso persistente explícito na UI;
    - rastreamento de histórico e obrigatoriedade de justificativa na devolução ou cancelamento de solicitações de regulação;
  - conformidade estrita com a LGPD: mascaramento de CPF e CNS em listas, detalhes e exportações CSV; sanitização anti-fórmula (`=+-@\t\r`), codificação UTF-8 com BOM e limite de registros; supressão de queixa e SOAP de relatórios tabulares;
  - dashboard operacional `/Saude` alimentado com indicadores reais e bloco "Regras vigentes (B3–B7)";
  - sem abertura de GED/InovaGED, sem criação de novas migrações DDL e sem reabrir FUNC01–05; RC50.68 mantida **BLOCKED** e RC50.69 não iniciada.

## FUNC05 Educação e Gestão Escolar — 2026-09-19


- **FUNC05 — templates no padrão FUNC01–04; seletores por nome; pré-matrícula/vaga/frequência/boletim sem inferência; CSV LGPD:**
  - padronização visual das views com breadcrumbs canônicos (`Início > Educação > [Página]`), títulos H1 únicos, blocos informativos "Como usar esta tela", empty states e ícones canônicos SVG (sem classes `bi-`);
  - eliminação completa de campos manuais de ID técnico digitado nos formulários operacionais (`Escolas`, `Turmas`, `Matriculas`, `Frequencias`, `Avaliacoes`, `Boletins`, `PreMatriculas`), substituídos por seletores amigáveis por nome;
  - autoridade no serviço: verificação rigorosa de escola ativa em matrículas, pré-matrículas e enturmações;
  - garantia de unicidade de matrícula ativa por aluno + escola + ano letivo + série; cancelamento de matrícula exige justificativa formal não vazia;
  - fluxo de ingresso/pré-matrícula: inscrição gera protocolo sem ocupar capacidade; oferta válida (`OFERTADA`/`ACEITA`) reserva capacidade; conversão atômica e idempotente consome a oferta de origem; ausência de vaga destina exclusivamente à lista de espera;
  - diário de frequência: estado inicial estritamente `NAO_LANCADO` (sem presença por omissão); lançamento restrito a professor com atribuição na turma/componente (`professor_turma`), data dentro do ano letivo e período aberto; falta justificada exige motivo real;
  - avaliações e boletins: escala e peso explícitos da avaliação; ausência de nota exibida como `NAO_LANCADO`; sem política acadêmica persistida e aprovada em banco, médias, cortes e aprovações permanecem indisponíveis sem fabricação de números;
  - dashboard operacional alimentado com dados reais do PostgreSQL no escopo do tenant e entidade; bloco de "Regras vigentes (B3, B4, B5, B6)";
  - exportação CSV (`/api/educacao/export/alunos.csv`) em conformidade com LGPD: documentos/CPFs mascarados, proteção contra injeção de fórmulas (`=+-@\t\r`), codificação UTF-8 com BOM e limite de registros;
  - preservados 100% dos testes existentes; build limpo sem CS0103, CS0535 e CS0006; sem pontes com FUNC06; RC50.68 mantida **BLOCKED** e RC50.69 não iniciada.

## FUNC04 Frotas, Abastecimento e Manutenção — 2026-09-19

- **FUNC04 Regras de Negócio e Serviços Autorizados:**
  - vínculo opcional de veículo a bem patrimonial ativo de FUNC01, com bloqueio estrito de bens com status `BAIXADO` e impedimento de vínculo duplicado em veículos ativos;
  - bloqueio de abertura de utilização para veículos com status `EM_MANUTENCAO`, `INATIVO` ou `BAIXADO`, e validação estrita de unique parcial de uso em aberto;
  - obrigatoriedade de condutor ativo com CNH válida (`validade >= hoje`) para abertura de saída;
  - controle estrito de hodômetro progressivo em utilizações e abastecimentos (recusa de km regressivo);
  - abastecimento com valor total calculado no banco, sem baixa implícita de combustível em estoque;
  - abertura de manutenção altera veículo para `EM_MANUTENCAO` atomicamente; conclusão da manutenção só reativa se não restarem outras OS ou manutenções pendentes;
- **Ponte Atômica OS &rarr; Almoxarifado (FUNC02):**
  - itens de peças e materiais em OS restritos a materiais do tipo `CONSUMO` ativos no Almoxarifado;
  - conclusão de OS (`concluir`) executa lock pessimista (`SELECT FOR UPDATE`) nas linhas de `almoxarifado_estoque`, verificando disponibilidade de saldo;
  - falta de saldo suficiente recusa a conclusão com rollback total da transação;
  - registro atômico de débito em `almoxarifado_movimentacao` (`SAIDA`) e vinculação do ID na linha da OS (`movimentacao_almoxarifado_id`);
  - cancelamento e recusa exigem justificativa formal gravada no histórico auditável (`frotas_ordem_servico_historico`);
- **Segurança, LGPD e Templates (Padrão FUNC01–03):**
  - mascaramento obrigatório de CPF de condutores em listagens, detalhes e exportações CSV;
  - exportação CSV (`/Frotas/Exportar`) com proteção anti-fórmula (`=+-@\t\r`), codificação UTF-8 BOM e limite de 5.000 registros;
  - 17 views elevadas ao padrão de design do SIGOV com `frotas.css`, barra `_Nav`, blocos "Como usar esta tela", seletores por nome amigável (sem IDs digitados), badges de situação e diálogos `data-confirm`;
  - interface de detalhe da OS exibe disponibilidade de saldo em estoque e desabilita o botão de conclusão com `title` explicativo caso haja peças sem saldo;
  - preservados os gates, RC50.68 mantida **BLOCKED** e RC50.69 não iniciada.

## FUNC03 Compras, Licitações, Contratos e Ponte Reposição→Estoque→Patrimônio — 2026-09-18

- **FUNC03 Regras de Negócio e Serviços Autorizados:**
  - bloqueio fail-closed de fornecedores suspensos e inativos em novos processos e cotações;
  - exportação CSV de fornecedores com proteção LGPD (documento mascarado), sanitização anti-fórmula (`=+-@\t\r`), codificação UTF-8 BOM e limite de 5.000 registros;
  - consulta de modalidades e critérios vinculada estritamente ao catálogo oficial parametrizado no PostgreSQL (`sigov.compras_parametro_modalidade` e `sigov.compras_parametro_criterio`);
  - ciclo de vida do processo (`PLANEJAMENTO` → `PUBLICADO` → `DISPUTA` → `JULGAMENTO` → `HOMOLOGADO`) com tramitação progressiva e desfechos terminativos (`ANULADO`, `DESERTO`, `FRACASSADO`, `CANCELADO`) com justificativa formal obrigatória e auditada;
- **Ponte Operacional de Suprimentos:**
  - em `/Almoxarifado/Reposicao`, adicionada ação "Gerar demanda de compra" para itens abaixo do mínimo, gerando processo em `PLANEJAMENTO` de forma idempotente (segunda geração recusada) sem duplicidade, sem criar tabelas e sem gerar empenho contábil (FUNC10);
  - tela `/Compras/Processos/{id}/Recebimento` para recebimento físico total ou parcial, validando estritamente `quantidade <= saldo pendente a receber`;
  - integração automática: materiais de `CONSUMO` debitam no Almoxarifado (`sigov.almoxarifado_movimentacao` e `estoque`), enquanto materiais `PERMANENTE` geram entrada física e pendência de conciliação patrimonial (`sigov.almoxarifado_pendencia_patrimonial` com status `PENDENTE`) sem tombar o bem no compras;
- **Templates e Interface:**
  - padronização visual com `compras.css`, stepper de fases, badges de ciclo de vida, blocos "Como usar esta tela", conformidade estrita com o sistema canônico de ícones SVG do SIGOV (sem classes externas) e confirmações `data-confirm`;
  - preservados os gates, RC50.68 mantida **BLOCKED** e RC50.69 não iniciada.


- **FUNC01 Fechamento:**
  - conferidos PRs #427 e #428 e exibidos os nomes da unidade e do responsável no cabeçalho do inventário;
  - implementado bloqueio formal e fail-closed na edição de bens com status `BAIXADO` no serviço e no controlador;
  - desabilitado botão de solicitação de transferência/movimentação quando houver OS ativa nos status `APROVADA`, `EM_EXECUCAO` ou `AGUARDANDO_PECA`, com aviso explicativo ao usuário;
  - mantidos redirecionamentos explícitos de Depreciação, Imóveis e Relatórios para o catálogo de Ativos.

- **FUNC02 Almoxarifado, Estoque e Requisições:**
  - validação estrita de materiais como `CONSUMO` ou `PERMANENTE` com código único por tenant/entidade e checagem de estoque mínimo/máximo (`estoque_minimo >= 0` e `estoque_maximo >= estoque_minimo`);
  - geração atômica de `almoxarifado_pendencia_patrimonial` para toda entrada de material permanente (sem tombamento incompleto no almoxarifado);
  - conciliação da pendência patrimonial integrada ao fluxo de cadastro de bens (`/Patrimonio/Bens/Novo`), registrando `patrimonio_bem_id` e data de resolução;
  - validação estrita de saldo em saídas e atendimentos de requisição, garantindo atomicidade com lock de linhas e impedindo saldos negativos (`quantidade >= 0`);
  - ciclo de vida completo de requisições (`RASCUNHO` → `ENVIADA` → `APROVADA`|`REJEITADA` → `ATENDIDA`|`CANCELADA`) com histórico auditável e justificativas obrigatórias para cancelamento e rejeição;
  - exportação CSV com UTF-8 BOM, limite de 5.000 registros, proteção contra injeção de fórmulas (`=+-@\t\r`) e sem exposição de dados de usuários/responsáveis (LGPD);
  - alinhamento visual de todos os templates com o design system do FUNC01 (`almoxarifado.css`), seletores por nome amigável de unidade/almoxarifado, empty states informativos, blocos "Como usar esta tela" e diálogos `data-confirm` em ações críticas;
  - preservados os gates, RC50.68 mantida **BLOCKED** e RC50.69 não iniciada.

## RC50.68E-R6 — 2026-08-24

- executada a homologação local disponível e registrada decisão **BLOCKED**, sem inventar PASS para
  .NET, PostgreSQL, PowerShell ou smoke ausentes;
- endurecida a geração de evidência Bash em saídas antecipadas, os códigos 0/1/2, a sanitização de
  cookie/Bearer e a recusa de destinos com marcador de produção nas duas plataformas;
- separada a decisão local do CI oficial, mantida a RC50.68 bloqueada e a RC50.69 não iniciada.

## RC50.68E-R5 — 2026-08-24

- criada homologação local assistida equivalente em Bash e PowerShell para .NET 10/PostgreSQL 16;
- adicionados aplicação/reaplicação do baseline, checks de autoridade persistida, build, smoke
  opcional e evidências JSON/Markdown/log sanitizadas;
- mantida a promoção **BLOCKED** até execução real verde e CI oficial, sem disparar Actions, validar
  secret de CI ou iniciar a RC50.69.
- reforçada a recusa de hosts com marcador de produção e a sanitização dos logs de startup do
  smoke antes de persistir qualquer saída nas evidências.

## RC50.68F-R2 — 2026-08-24

- corrigido o contrato Dapper para usar somente colunas reais de usuário e contexto persistido;
- estabilizados DTOs, limites de schema, modais, antiforgery, erros HTTP e estados vazios do CRUD;
- preservados migration publicada, manifest e scripts consolidados sem drift; promoção segue **BLOCKED** e RC50.69 não foi iniciada.

## RC50.68F — 2026-08-21

- entregue o CRUD SuperAdmin AJAX da autorização contextual persistida para perfis, grupos,
  permissões e vínculos usuário-grupo, grupo-perfil e perfil-permissão;
- preservados a autoridade no PostgreSQL, o avaliador fail-closed e a precedência de `NEGAR`, com
  validação server-side, estados, exclusão lógica e auditoria antes/depois;
- mantida a promoção RC50.68 como **BLOCKED** pelo CI real, sem consultar Actions e sem iniciar RC50.69.

## RC50.68E-R4 — 2026-08-20

- confirmada a tentativa sobre o candidato `f6da64e3b756640b1322e7d0b8a3e506f7c92311`;
- registrado **BLOCKED** porque o ambiente não possui autenticação GitHub para consultar o nome do
  repository secret, confirmar o workflow em Actions ou disparar e acompanhar uma run;
- preservados os gates sem PASS inferido, sem alteração especulativa de código ou migration e sem
  início da RC50.69.

## RC50.68E-R3 — 2026-08-20

- criada a esteira manual `rc50-68-promotion.yml` para executar, em runner equipado, build .NET
  10.0.100, validações estáticas, aplicação e reaplicação do baseline no PostgreSQL 16 e asserções
  das migrations e permissões persistidas das RC50.68A–D;
- adicionados preflight do secret sem impressão do valor, validação explícita de ferramentas,
  verificação de drift e scans preventivos da superfície de promoção;
- mantida a RC50.68 como **BLOCKED** até execução verde real do workflow; RC50.69 não foi iniciada.

## RC50.68E-R2 — 2026-08-20

- repetidos os gates obrigatórios sobre o merge do PR #270, registrando exit 127 para .NET,
  PostgreSQL/psql, PowerShell e actionlint ausentes;
- confirmados Ruby/YAML, integridade do workflow principal, JSON, shell, rotas e igualdade byte a
  byte dos scripts consolidados como verificações estáticas auxiliares;
- mantida a promoção **BLOCKED** porque o banco real, o build e os workflows com
  `SIGOV_CI_DB_PASSWORD` não puderam ser executados; RC50.69 permanece não iniciada.

## RC50.68E — 2026-08-20

- auditada a base do control plane SuperAdmin 360 sobre o merge do PR #269;
- consolidados os scripts PostgreSQL a partir do manifest e ampliada a verificação do gerador para
  todas as cópias distribuíveis de produção e desenvolvimento;
- corrigido o Plano Mestre para separar implementação entregue de promoção produtiva;
- registrados como BLOCKED os gates dependentes de .NET 10, PostgreSQL/psql, PowerShell,
  actionlint e execução autenticada dos workflows; RC50.69 não foi iniciada.

## v1.0.0 - 2026-06-07

### Adicionado
- Versionamento final `v1.0.0` com `VERSION`, release notes final, checklist de release e documentação operacional mínima.
- Metadados de build em `/api/health/version` por `SIGOV_VERSION`, `SIGOV_COMMIT_SHA`, `SIGOV_BUILD_DATE` e `SIGOV_RELEASE_CHANNEL`, mantendo `application`, `database` e `schema` como `sigov`.
- Scripts executáveis de validação final, smoke test, homologação, go-live, rollback e empacotamento de release.
- Pacote local de release em `artifacts/release/v1.0.0/` gerado por script com manifest, migrations, projetos e checksums.
- Workflow manual `.github/workflows/release.yml` para preparar artefato de release sem publicar imagens sem secrets configurados.
- Testes de release para metadados, checklist de go-live, homologação segura, pacote e health version.

### Corrigido
- Endpoint de versão passou a retornar contrato final com `releaseChannel`, `commitSha`, `buildDate`, `environment`, `database` e `schema`.
- Checklist de go-live deixou de ser apenas informativo e passou a retornar PASS/WARN/FAIL com exit code em falhas.
- Rollback passou a validar backup, checksum, versões, restore protegido e plano documentado sem executar restore.

### Segurança operacional
- Homologação bloqueada em `Production` por script e validador de aplicação.
- Swagger Production permanece desabilitado por padrão e, quando habilitado, exige proteção explícita já validada por options.
- CORS wildcard, seed demo, admin default e adapters dev são bloqueados/validados para Production.
- Pacote de release não inclui dumps reais, certificados, tokens ou secrets.

### Pendências pós-release
- Executar homologação assistida com banco real e evidências assinadas pelo cliente.
- Ampliar testes E2E autenticados completos por perfil funcional em releases futuras.
- Evoluir módulos estruturais/parciais em backlog próprio, sem bloquear o pacote v1.0.0 homologável.

## v1.0.0-rc.1 - 2026-06-07

### Adicionado
- Endpoint `/api/health/version` consolidado com metadados opcionais `SIGOV_VERSION`, `SIGOV_COMMIT_SHA` e `SIGOV_BUILD_DATE`.
- Matriz técnica de módulos, rotas, views, JavaScript, migrations, permissões, dashboards, exportações, testes e pendências.
- Scripts de validação `scripts/check-module-map.ps1` e `scripts/check-web-assets.ps1` para QA de matriz e assets MVC/Razor.
- Testes de regressão para contrato de API, versão, permissões por serviço, módulos/feature flags, migrations, tenant isolation, LGPD e worker/outbox.

### Corrigido
- Resposta de versão agora expõe `application = sigov`, versão de release candidate e metadados de build sem depender de secrets versionados.
- Cobertura estática de migrations reforçada para impedir schemas físicos fora de `sigov` e tipos SQL Server.
- Cobertura de worker/outbox reforçada para retry, dead-letter, tenant_id e logs correlacionáveis.

### Observações
- Docker e .NET devem ser validados em ambiente com SDK .NET 6 e Docker instalados.
- Módulos estruturais/parciais foram registrados como pendência real, sem criação de módulos novos neste RC.

## FUNC01 — 2026-08-24

- Entregue módulo funcional de Patrimônio e Inventário com bens, tombamento, movimentação, responsabilidade, inventário físico, divergências, baixa, dashboard, API/MVC e CSV protegido.
- Adicionada migration idempotente com preservação do legado UUID, PKs bigint canônicas, índices, constraints, categorias e dez permissões persistidas.
- Adicionada auditoria antes/depois na transação e interface responsiva Bootstrap.
- RC50.68 continua BLOCKED por ambiente/CI; FUNC01 não promove RC50.68 nem inicia/promove RC50.69.

## FUNC02 — 2026-08-24

- Entregue Almoxarifado com catálogo, locais, entradas, saídas, estoque, requisições, dashboard, API/MVC e três exportações CSV auditadas.
- Adicionada migration idempotente com PK bigint, constraints, índices, doze permissões e pendência única de tombamento para material permanente.
- RC50.68 continua BLOCKED; FUNC02 não promove RC50.68 nem inicia ou marca RC50.69.

## FUNC03 — 2026-08-24
- Módulo operacional de compras públicas, fornecedores, licitações, cotações, julgamento, contratos, atas e recebimentos integrados.
- Migration idempotente, permissões persistentes, Dapper/Npgsql, auditoria e mascaramento LGPD.

## FUNC04 — 2026-08-24
- Gestão operacional de frotas com veículos, motoristas, utilizações, abastecimentos, manutenções, OS e documentos.
- Migration idempotente, scripts consolidados, RBAC, auditoria, LGPD e integrações FUNC01/FUNC02/FUNC03; sem promoção de release.

## FUNC05 — 2026-08-24

- Educação/Gestão Escolar: schema PostgreSQL idempotente, permissões, escolas, alunos/responsáveis, professores, calendário, turmas, matrículas, frequência, avaliações, ocorrências, pré-matrícula, portal, dashboard, CSV e rotas MVC/API.
- RC50.68 permanece BLOCKED; RC50.69 não iniciada; GED/InovaGED adiado.

## FUNC06 — 2026-08-25

- Saúde Pública Municipal fechada com cadastros, equipes, agenda, acolhimento, prontuário SOAP, procedimentos, vacinação, dispensação, regulação, dashboard, CSV e rotas MVC/API.
- Migration corretiva idempotente, RBAC persistente, auditoria/LGPD e validações transacionais com Almoxarifado quando há vínculo seguro.
- RC50.68 permanece BLOCKED; RC50.69 não foi iniciada; GED/InovaGED segue adiado.
