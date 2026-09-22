# Auditoria do ciclo funcional — 2026-09-22

## Decisão executiva

O ciclo foi interrompido no Gate A, antes de qualquer evolução funcional. O ambiente
não dispõe do SDK .NET 10, PowerShell, PostgreSQL/`psql`,
`ConnectionStrings__DefaultConnection` nem das aplicações Web/API iniciadas. Como o
critério recebido determina que nenhuma implementação avance antes de build, banco,
login, autorização, isolamento e navegação aprovados, alterar regra, tela, menu ou
schema neste estado produziria uma entrega sem evidência e contrariaria o próprio
gate.

Os gates estáticos disponíveis foram reexecutados. Eles ajudam a detectar regressões
estruturais, mas não demonstram comportamento runtime e não promovem nenhum módulo.

## 1. O que foi auditado

- instruções do repositório, `README.md`, `CHANGELOG.md`, status real dos módulos,
  auditorias vigentes, `global.json` e manifesto de migrations;
- disponibilidade da toolchain e do contrato de conexão oficial;
- integridade estática do catálogo de migrations e dos consolidados;
- conflitos diretos das rotas da API, colisões do SQL consolidado, artefatos
  rastreados e sintaxe dos gates Bash;
- disponibilidade HTTP das páginas críticas da API e da Web.

## 2. O que estava quebrado

Não foi possível atribuir defeito novo ao código sem build e execução. A
infraestrutura desta execução está incompleta: `dotnet`, `pwsh` e `psql` não existem
no `PATH`; a connection string oficial não está configurada; API e Web retornaram
HTTP 000 por estarem indisponíveis. Esses resultados são **BLOCKED**, não sucesso nem
falha funcional do produto.

## 3. O que foi corrigido

Nenhum código funcional foi corrigido após o bloqueio. Esta auditoria registra a
decisão fail-closed e impede que evidência estática seja apresentada como validação
de persistência, autorização ou navegação.

## 4. O que foi implementado

Somente a evidência documental desta execução. Não foram implementadas regras,
telas, endpoints, repositórios, DTOs, permissões, relatórios ou dados.

## 5. O que ficou parcial

Todos os domínios permanecem com a classificação conservadora publicada em
`STATUS_REAL_MODULOS.md`. Núcleo SaaS, Financeiro/Compras, Educação,
Saúde/ACS, Protocolo, Jurídico e template não foram promovidos.

## 6. Regras de negócio consolidadas

Nenhuma regra recebeu nova evidência runtime. Em particular, isolamento de tenant,
entidade e exercício, bloqueio de módulo não contratado, exercício encerrado,
auditoria crítica, limites financeiros, calendário escolar, território ACS,
tramitação de protocolo e histórico jurídico continuam exigindo homologação.

## 7. Telas com “Como funciona”

Nenhuma tela foi alterada. A lacuna já identificada no componente `_PageIntro` — não
garantir explicitamente todos os tópicos obrigatórios — permanece pendente e não foi
mascarada por uma alteração sem validação Razor.

## 8. Migrations criadas

Nenhuma. Não houve alteração de schema.

## 9. Scripts atualizados

Nenhum script SQL consolidado foi alterado. O gate estático confirmou 195 arquivos
SQL, 185 migrations registradas, 181 no baseline e dez órfãos governados, com
paridade estática aprovada. Aplicação, reaplicação e equivalência semântica seguem
bloqueadas sem PostgreSQL 16 e o runner canônico.

## 10. Menus alterados

Nenhum menu foi alterado, pois sua navegação autorizada não pôde ser exercitada.

## 11. Testes executados

| Comando | Resultado | Interpretação |
|---|---|---|
| `./scripts/check-prerequisites.sh` | BLOCKED (exit 1) | faltam .NET, PowerShell, `psql`, connection string e sondagem de portas |
| `./scripts/check-migration-catalog.sh` | PASS | catálogo/paridade estática; execução PostgreSQL explicitamente bloqueada |
| `./scripts/check-api-route-conflicts.sh` | PASS | 630 rotas da API sem conflito direto |
| `./scripts/check-one-shot-object-collisions.sh` | PASS | sem colisão estática no consolidado canônico |
| `./scripts/check-tracked-artifacts.sh` | PASS | nenhum artefato proibido rastreado |
| `bash -n scripts/check-prerequisites.sh scripts/check-migration-catalog.sh scripts/check-api-route-conflicts.sh scripts/check-one-shot-object-collisions.sh scripts/check-critical-pages.sh` | PASS | sintaxe Bash válida nos gates executados |
| `python3 -m json.tool database/postgres/migrations/manifest.json` | PASS | manifesto JSON sintaticamente válido |
| `./scripts/check-critical-pages.sh` | BLOCKED (exit 2) | API e Web indisponíveis; todas as sondas retornaram HTTP 000 |

## 12. Evidências de build

`dotnet --info`, restore, build e testes .NET não puderam ser executados porque
`dotnet` não está instalado. Portanto, não há evidência de build verde neste ciclo.

## 13. Evidências de login e autorização

Não há evidência runtime. Login SuperAdmin, admin do tenant e operador, acesso
negado, módulo não contratado, tenant inativo, usuário bloqueado e isolamento A/B
permanecem **NÃO VERIFICADOS / BLOCKED**.

## 14. Evidências de navegação

O gate de páginas críticas registrou HTTP 000 para health, Swagger, login, Minha
Central e dashboards. Não houve navegação manual nem validação responsiva porque a
aplicação não iniciou. Nenhuma captura de tela seria evidência válida nesse estado.

## 15. Riscos restantes

- crítico: compilação e DI não verificadas;
- crítico: instalação limpa, upgrade, reaplicação e transações PostgreSQL não
  verificadas;
- crítico: autenticação, autorização persistida e segregação multi-tenant não
  verificadas;
- alto: regras financeiras, escolares, clínicas, processuais e jurídicas sem prova
  integrada;
- alto: menus, telas Razor, estados de erro/vazio e responsividade sem navegação;
- médio: cobertura incompleta e não uniforme do conteúdo “Como funciona”.

## 16. Próximo ciclo recomendado

1. Disponibilizar exatamente o SDK `10.0.100`, PowerShell e PostgreSQL 16+ com
   `psql`.
2. Configurar `ConnectionStrings__DefaultConnection` por ambiente, sem registrar o
   segredo.
3. Executar restore/build/test normativos e instalar o banco limpo pelo manifesto;
   depois reaplicar e validar upgrade representativo e paridade dos consolidados.
4. Iniciar API/Web e homologar login com SuperAdmin, admin e operador, dois tenants,
   duas entidades, dois exercícios e um módulo não contratado.
5. Somente com Gate A verde selecionar uma fatia vertical pequena, fechá-la de ponta
   a ponta e adicionar o “Como funciona” específico em cada tela tocada.

## Matriz final obrigatória

| Área | Funcionalidade | Estado anterior | Estado final | Tela tem “Como funciona”? | Evidência | Pendência |
|------|----------------|----------------|--------------|----------------------------|-----------|-----------|
| Fundação | restore/build/test/DI | Bloqueado | Bloqueado | N/A | pré-requisitos: `dotnet` e `pwsh` ausentes | instalar toolchain e executar gates .NET |
| Banco | clean install/upgrade/reaplicação | Bloqueado | Bloqueado | N/A | catálogo estático PASS; `psql` ausente | PostgreSQL 16, runner canônico e equivalência |
| Núcleo SaaS | tenants, contexto, IAM, entitlements e auditoria | Parcial | Parcial, não promovido | Não verificado | aplicação indisponível | homologar perfis, bloqueios e isolamento A/B |
| Financeiro e Compras | operações, transações e relatórios | Parcial | Parcial, não promovido | Não verificado | sem runtime/banco | homologar exercício, saldos, contratos e auditoria |
| Educação | matrícula, transferência, frequência e notas | Parcial | Parcial, não promovido | Não verificado | sem runtime/banco | homologar concorrência, calendário e histórico |
| Saúde/ACS | território, cidadãos, visitas e exportação | Parcial | Parcial, não promovido | Não verificado | sem runtime/banco | homologar LGPD, escopo e auditoria |
| Protocolo | tramitação, prazos e histórico | Parcial | Parcial, não promovido | Não verificado | sem runtime/banco | homologar escopo, reabertura e histórico |
| Jurídico | parecer, movimentações, anexos e relatórios | Parcial | Parcial, não promovido | Não verificado | sem runtime/banco | homologar alçadas, histórico e auditoria |
| Template/UX | login, menus, formulários e responsividade | Parcial | Parcial, não promovido | Lacuna conhecida | páginas críticas HTTP 000 | executar navegação autenticada e padronizar ajuda |

