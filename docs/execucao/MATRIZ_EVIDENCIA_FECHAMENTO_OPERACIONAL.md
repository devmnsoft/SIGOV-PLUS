# Matriz de evidência — Fechamento operacional (BR-11.3)

Data: 2026-10-14 · Ciclo 2 Fechamento Operacional, Sprint 11. Incremento coberto: **Educação**
(Ciclo 1 S1–S10 + Ciclo 2 S1–S6) + **frente administrativa** (Ciclo 2 S7–S10) + **homologação
integrada** (Ciclo 2 S11).

## Níveis e fonte da evidência

Níveis: **IMPLEMENTADO** (código commitado, camadas existentes — regra 3) / **VALIDADO POR TESTES**
(suítes existentes — regra 14) / **EXECUTADO EM INTEGRAÇÃO** (Tier-2 E2E HTTP sobre apps reais no PG16) /
**HOMOLOGADO PELA INTERFACE** (login web real form+antiforgery+cookie e/ou HTML renderizado pós-JS) /
**BLOQUEADO** (impedimento nomeado, regra 18 — nunca simulado, regra 13).

Fontes: suítes **851/851** (Unit 522 / Integration 171 / Api 158, 0 ignorados) sobre o binário final;
Tier-2 E2E oficial **rodada 7 = 436/436 PASS** (driver `scripts/c1_tier2_e2e.ps1`, ROUND_EXIT=0,
auto-limpeza `restantes=0`; apps reais Web :5099 / Api :5100 + :5001; PostgreSQL 16.14 `p1_dev`);
relatório da corrida em `docs/evidencias/rcevo/relatorio_ciclo2_fechamento_operacional_sprint11.md`.
**Screenshots: BLOCKED herdado (regra 18)** — `browser.screenshot` falha consistentemente no harness
(desde a Sprint 6); o nível HOMOLOGADO PELA INTERFACE assenta-se sobre E2E HTTP real + HTML renderizado
+ estados de rede + toasts, prática declarada nos relatórios S4–S10.

Leitura honesta (regra 13): existência de controller, view ou tabela isolada não promove uma
funcionalidade; cada célula aponta a evidência que a sustenta, e o que não tem evidência fechável fica
BLOQUEADO com o motivo.

## A. Frente administrativa (Ciclo 2 S7–S10)

| Funcionalidade | IMPLEMENTADO | VALIDADO POR TESTES | EXECUTADO EM INTEGRAÇÃO | HOMOLOGADO PELA INTERFACE | BLOQUEADO |
|---|---|---|---|---|---|
| Catálogo de permissões da frente (25 chaves por ato, S7) | ✅ seed idempotente `sigov.permissao` + vínculo SUPERADMIN | ✅ 851/851 (fact manifest 237) | ✅ r7 — base 420 (checks S7/S8 autorizados) | ⚠ login web real → ApiToken (mesma cadeia) | screenshots (r18) |
| Demanda tipada → procedimento único (S8) | ✅ `ComprasIntegradas*` (cmdos tipados) | ✅ 851/851 | ✅ r7 — `jornada-a.municipal-cadeia-completa-cancelamento-com-efeito` + base 420 | ⚠ mesma cadeia de autenticação | screenshots (r18); tela dedicada (API-first) |
| Licitação: habilitação → lances → zona ME/EPP → julgamento → adjudicação (S8) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (família `licitacao.*`) | ⚠ idem | idem |
| Homologação + contrato + execução + prorrogação art. 106 + rescisão + dispensa art. 75 (S8) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (famílias `contrato.*`/`homologacao.*`) | ⚠ idem | idem |
| Fornecimento → recebimento (rejeição + prazo paramétrico + espelho legado) → desfecho único (S9) | ✅ idem | ✅ 851/851 | ✅ r7 — `jornada-a.recebimento-rejeicao-prazo-parametrico-desfecho-unico` + `fornecimento.concorrencia-*` | ⚠ idem | idem |
| Fatura conferida → obrigação única → liquidação → estorno por compensação (S9) | ✅ idem | ✅ 851/851 | ✅ r7 — `perfil.tesoureiro-*` + `obrigacao.estorno-repetido-compensacao-unico-terminal` | ⚠ idem | idem |
| Destinação estoque/patrimônio referenciada (S9) | ✅ idem | ✅ 851/851 | ✅ r7 — `jornada-a.destino-estoque-movimento-unico-efetivo` | ⚠ idem | idem |
| Jurídico: consulta tipada + parecer CONDICIONADO gateando homologação + contencioso bloqueando prorrogação (S10) | ✅ idem (+13 operações) | ✅ 851/851 | ✅ r7 — `perfil.juridico-moda-juridica-sem-acesso-a-compra` + base (famílias `contencioso.*`, `juridico.*`) | ⚠ idem | idem |
| Convênios: contrapartida/vínculo/prestação de contas/encerramento em cascata (S10) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (`encerramento.cascata-*`, `contrapartida.*`) | ⚠ idem | idem |
| Obras: projeto APROVADO exigido + medição sequenciada limitada pela física/contratado (S10) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (`medicao.homologacao-acima-fisico-ou-contratado-insuficiente`) | ⚠ idem | idem |

## B. Homologação integrada e cenários transversais (Ciclo 2 S11)

| Funcionalidade | IMPLEMENTADO | VALIDADO POR TESTES | EXECUTADO EM INTEGRAÇÃO | HOMOLOGADO PELA INTERFACE | BLOQUEADO |
|---|---|---|---|---|---|
| Fixture multi-esfera idempotente (2 tenants, esferas municipal/estadual, override paramétrico) | ✅ driver (SQL do bloco S11) | — (harness, não produto) | ✅ r7 — `fixture.dois-tenants-duas-esferas-quatro-perfis-parametro-override` (pós-condições explícitas) | ⚠ 6 logins web reais na mesma rodada | screenshots (r18) |
| Autenticação multi-tenant (BR-11.1) | ✅ `AuthController`/`AuthenticationRepository` (banco autoridade) | ✅ 851/851 | ✅ r7 — `auth.seis-logins-duas-esferas-token-mint` | ✅ 6× form+antiforgery+cookie → 302 MinhaCentral → ApiToken len=64 (a interface real autentica nas duas esferas) | screenshots (r18) |
| Jornada completa municipal (admin+gestor) | ✅ superfície S8–S10 | ✅ 851/851 | ✅ r7 — `jornada-a.*` (3 checks, ponta a ponta até o almoxarifado) | ⚠ autenticação real + API do próprio app | screenshots (r18); tela dedicada (API-first) |
| Jornada completa estadual (administração própria do tenant) | ✅ idem | ✅ 851/851 | ✅ r7 — `jornada-b.estadual-mesma-cadeia-outro-tenant-outro-contexto` | ⚠ idem | idem |
| Jornada tesoureiro/financeiro (modal própria) | ✅ autorização por chave pontuada | ✅ 851/851 | ✅ r7 — `perfil.tesoureiro-fatura-obrigacao-liquidacao-chave-propria` (200 na moda + 403 fora dela) | ⚠ login real do perfil | idem |
| Jornada jurídico | ✅ idem | ✅ 851/851 | ✅ r7 — `perfil.juridico-moda-juridica-sem-acesso-a-compra` | ⚠ login real do perfil | idem |
| **Cidadão quando exposto** | — | — | — **N/A registrado**: `tipo_usuario` ativo ∈ {OPERADOR, ADMINISTRADOR_GERAL}; não existe usuário cidadão no banco — sem simulacro (regras 11–13) | — | item não-executável no schema atual (promoveria catálogos mock) |
| Concorrência em regra de unicidade (BR-11.2) | ✅ fix de race `FornecimentoCriarAsync` (S9) + backstop `ux_compras_fornecimento_chave` | ✅ 851/851 | ✅ r7 — `fornecimento.concorrencia-efeto-unico-reenvio-idempotente` (2 clientes simultâneos; banco count\|id = 1\|54) | ⚠ mesma sessão real | screenshots (r18) |
| Reenvio idempotente (BR-11.2) | ✅ chaves de idempotência dos comandos | ✅ 851/851 | ✅ r7 — asserts de replay em `fornecimento.concorrencia-*` + `jornada-b.*` (mesmo id, nada duplica, nos 2 tenants) | ⚠ idem | idem |
| Estorno repetido (BR-11.2) | ✅ estorno por compensação única (fix 400 desta sprint) | ✅ 851/851 | ✅ r7 — `obrigacao.estorno-repetido-compensacao-unico-terminal` (400→200→409 sobre terminal) | ⚠ idem | idem |
| Cancelamento com efeito (BR-11.2) | ✅ imutabilidade após aprovação | ✅ 851/851 | ✅ r7 — assert em `jornada-a.municipal-cadeia-completa-cancelamento-com-efeito` (409 `DEMANDA_APOS_APROVACAO_IMUTAVEL`) | ⚠ idem | idem |
| Schema ausente (BR-11.2) | ✅ guard de schema por tabela + falha nomeada | ✅ 851/851 | ✅ r7 — `schema.ausente-nomeada-nos-caminhos-e-recuperacao` (tabela renomeada → 404 nomeada GET+POST; restaurada → 200) | ⚠ idem | idem |
| Permissão ausente (BR-11.2) | ✅ avaliador fail-closed (banco) | ✅ 851/851 | ✅ r7 — `permissao.ausente-fail-closed-em-ambas-as-esferas` (403×3 antes de qualquer acesso a dado) | ⚠ logins reais dos SemFrente | idem |
| Regra 21 nos dados (mesmo ato → derivações diferentes por esfera) | ✅ parametrização lida do banco | ✅ 851/851 | ✅ r7 — `regra21.mesmo-ato-duas-esferas-parametrizacao-observavel-no-banco` (prazos 2026-11-19 × 2026-11-26; sonda fresca no ponto da asserção) | ⚠ idem | idem |
| Isolamento cross-tenant (visão/escopo/dados) | ✅ escopo tenant antes da busca | ✅ 851/851 | ✅ r7 — `isolamento.cross-tenant-visao-escopo-e-dados` (404 leitura cruzada; contagem cruzada = 0) | ⚠ listagem web real com marcador | idem |
| Auto-limpeza (resíduo zero após a homologação) | ✅ cascata nos 2 tenants | — (harness) | ✅ r7 — `fixture.removido-auto-limpeza-restantes-zero` + cleanup global `restantes=0` | — | — |

## C. Educação — verticais (Ciclo 1 + Ciclo 2)

| Funcionalidade | IMPLEMENTADO | VALIDADO POR TESTES | EXECUTADO EM INTEGRAÇÃO | HOMOLOGADO PELA INTERFACE | BLOQUEADO |
|---|---|---|---|---|---|
| Máquina de estados/efeitos/versão/relógio/schema ausente uniforme (C1 S2) | ✅ repositório avançado | ✅ 851/851 | ✅ r7 — base 420 (bloco E16) | ⚠ via telas reais (S6) | screenshots (r18) |
| Merenda: integridade transacional estoque×movimento×estorno + operação (C1 S1/S5/S6, C2 S1) | ✅ comandos tipados | ✅ 851/851 | ✅ r7 — base 420 (`estoque.abertura-tipada-inicial` e família E15/merenda) | ✅ jornada J1 no browser real (artefatos `s6_jornadas`: estorno pela tela c/ justificativa + 200 + linha ESTORNO visível; 404 nomeada verbatim) | screenshots (r18) |
| Biblioteca: circulação real/inventário/ocorrências/histórico (C1 S7, C2 S3) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (família E17/biblioteca) | ✅ jornada J2 (acervo real na tela) | screenshots (r18) |
| Transporte escolar: rota→viagem→presença→ocorrências→custo (C1 S8, C2 S4) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (família E18/transporte) | ✅ jornada J3 (KPIs com origem real) | screenshots (r18) |
| FUNDEB/custos/rateio + avaliação magistério + Educacenso (C1 S9/S10, C2 S5) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (famílias E13–E14/E19/fundeb) | ✅ jornadas J4 (painel conformância LC 114) + Educacenso + rateios (telas reais) | screenshots (r18) |
| Matrícula/enturmamento/frequência/transferência (C2 S2, base educacional) | ✅ idem | ✅ 851/851 | ✅ r7 — base 420 (`enturmamento.*`, `transferencia.*`, `frequencia.*`, `correcao.*`) | ✅ jornada J6 (dropdown escola real + estado vazio nomeado) | screenshots (r18) |
| Telas reais substituindo a fachada estática (C2 S6) | ✅ views+motor `educacao-area.js` | ✅ 851/851 | ✅ r7 — base 420 (checks das áreas) | ✅ HTMLs renderizados pós-JS + responsive/teclado (artefatos `s6_jornadas/0{1..9}_*`) | screenshots (r18) |
| Dashboard conjunto completo + CSV integral (C1 S1) | ✅ DTO dedicado | ✅ 851/851 | ✅ r7 — base 420 (`dashboard.conjunto-completo`, `csv.export-integral-bom-escape`) | ⚠ consumo pelas telas reais | screenshots (r18) |

## D. Plataforma transversal (base do incremento)

| Funcionalidade | IMPLEMENTADO | VALIDADO POR TESTES | EXECUTADO EM INTEGRAÇÃO | HOMOLOGADO PELA INTERFACE | BLOQUEADO |
|---|---|---|---|---|---|
| Auth/isolamento multi-tenant (P0.4 + Gate B) | ✅ avaliador canônico fail-closed | ✅ 851/851 | ✅ jornadas P0.4/Gate B ao vivo + r7 (S11: 6 logins × 2 tenants/esferas) | ✅ jornadas com CPF/CNPJ/e-mail reais (relatórios P0.4/Gate B) | screenshots (r18) |
| Migrations/baseline idempotentes (P0.1; manifest 237 invariante neste incremento final) | ✅ runner auditado | ✅ 851/851 (fact 237) | ✅ aplicação 2–12× por migration no PG16 (relatórios S1–S10) | — | `check-migration-governance.ps1` no host (PS 5.1, pré-existente) — porta oficial = gerador `-Verify` |

## Resumo de células BLOQUEADO (nomeadas, regra 18)

1. **Screenshots do browser** — ferramenta do harness falha consistentemente (`browser.screenshot`);
   evidência substituta nomeada desde a Sprint 6. Afeta todas as células de "HOMOLOGADO PELA INTERFACE"
   que dependem de captura (mantém-se o nível por HTML renderizado + estados de rede).
2. **Tela dedicada (browser) da frente administrativa S8–S10** — superfície API-first; as telas de
   operação não são escopo deste incremento (interface real provada até autenticação + telas de
   Educação da S6).
3. **Usuário cidadão** — não existe no schema (`tipo_usuario` OPERADOR/ADMINISTRADOR_GERAL); item N/A
   da BR-11.1 registrado, sem simulacro.
4. **GED** — último módulo por construção (Sprint 18), salvo adiamento formal.
