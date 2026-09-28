# RELATORIO DE ENTREGA - GATE COMPLETO RC50.68A (jornada de compras empresariais)

Data: 2026-09-28 (UTC)
Base commit: d249dd2222ddc6225fca6bfd8b2711bb293c3751 | Nada commitado/empurrado.
Stack validada: .NET 10 / C# 14 / Dapper / PostgreSQL 16 (docker), camadas preservadas, sem EF Core.
Parede total do gate: 21 cenarios obrigatorios.

## RESULTADO GLOBAL

- PASS: 20 | FAIL: 0 | BLOCKED: 1 (documentado, fora do escopo RC50.68A) | NAO EXECUTADO: 0

## TABELA DOS 21 CENARIOS OBRIGATORIOS

| # | Cenario | Status | Evidencia principal |
|---|---------|--------|---------------------|
| 1 | Compilacao sigov.sln no runtime .NET 10 / C# 14 (SDK do global.json) | PASS | build de todos os projetos na execucao final de hoje (0 erros); `docker compose build api` limpo, imagem `sigov-plus-api:latest` recriada com o defeito corrigido |
| 2 | Suite completa de testes + regresses fixados (fragmento novo do enviar) | PASS | execucao fresca hoje: UnitTests 393 + IntegrationTests 141 + ApiTests 132 = **666 aprovados, 0 falhas**; classe regressiva 26/26 com o fragmento `not i.is_deleted)),@ciclo,@us,@us,@corr)` |
| 3 | Migration: instalacao limpa em PG16 (docker) | PASS | `gate_clean_install.log` + reexecucao idempotente no-op (`gate_clean_noop2.log`) em `sigov_gate_clean` |
| 4 | Migration: caminho de upgrade pre->post (nao destrutivo, dados preservados) | PASS | `gate_upgrade_pre2.log`, `gate_upgrade_assert1.txt`/`assert2.txt`, fixture legado OK, reexecucao no-op (`gate_upgrade_mig_noop.log`) em `sigov_gate_upgrade` |
| 5 | Migration: demonstracao de auto-cura (branch P1 em schema vazio/partial) | PASS | `gate_heal_sim.log`, `gate_heal_bug.log`, `gate_heal_postfix.log`, `gate_heal_assert.txt` em `sigov_gate_a_empty` |
| 6 | Fluxo oficial: runner de migracoes no boot (`sigov-db-migrations`) | BLOCKED | `gate_runtime_offical_flow_fail.log`: `ERROR: column "version" does not exist` — incompatibilidade preexistente entre DDL do ledger e colunas usadas pelo runner; gap de infra anterior a esta RC (fora do escopo RC50.68A). Apps sobem via `up -d --no-deps --force-recreate`; comportamento documentado e nao mascarado |
| 7 | S-1: emissao das sessoes de demonstracao (mesmas colunas do login oficial; sem senha literal) | PASS | 3 sessoes em `sigov.identidade_sessao` (sha256 do bearer = token_hash; regra 18 cumprida); replay idempotente |
| 8 | S0: preflight do estado virgem (fixtures RASCUNHO v1, valores originais, politica=0, etapas=0, chaves jornada=0) | PASS | `jornada_evidence.txt` S0; reset previo verificado por `gate_reset.ps1` (RESET_OK: 80000/40000/60000/70000, POLITICA=0, ETAPAS=0, CHAVES_JORNADA=0, RQ5=0, PP_9001=9, PP_9002=2, PENDENCIAS=0) |
| 9 | S1: envio ANTES da politica => etapa bloqueada (aprovador NULL) + pendencia APROVACAO_SEM_POLITICA | PASS | Rq2 v3 PENDENTE_APROVACAO, etapa PENDENTE aprovador NULL/limite 0, mensagem "aguarda configuracao institucional", pendencia ABERTA no banco |
| 10 | S2: politica institucional por tenant (upsert) + alcadas cumulativas 50000/250000 + replay idempotente | PASS | 1 politica + 2 niveis persistidos; replay com equivalencia de conteudo nao duplica (advisory lock transacional) |
| 11 | S3: Rq1 total 80000 => duas escalas cumulativas + quorum any-of por etapa => APROVADA | PASS | 3 etapas (n1 analista CANCELADO + n1 gestor APROVADO + n2 gestor APROVADO); requisicao APROVADA v4 |
| 12 | S4: Rq3 total EXATO 50000.00 => fronteira de alcada INCLUSIVA => etapa unica decisivel pelo analista | PASS | etapa n1 decidida pelo analista (alcada 50000.00) => APROVADA v4; comparacao >= comprovada no runtime |
| 13 | S5: Rq4 idempotencia de envio, devolucao com motivo, correcao, 409 conteudo, ciclo 2 REJEITAR, decisao dupla | PASS | c1 (3 etapas, DEVOLVIDA), correcao RASCUNHO_ATUALIZADO, reenvio k2 (2 etapas), c2 REJEITADA => REJEITADA v7; 409s de conteudo/versao capturados |
| 14 | S6: Rq5 criada via API (chave de idempotencia, HTTP 201) e enviada => etapas pendentes para render web | PASS | `RC-2026-000002` (guid c581e52c-...), 3 etapas PENDENTE (n1x2 + n2) |
| 15 | S7: provas de falha explicita sem mutacao (versao 409, itens/total 422, estado 422, justificativa 400) | PASS | codigos + textos exatos validados; nada persistido fora do esperado |
| 16 | S8: admin de OUTRO contexto institucional (tenant 5, sem grants de compras) => fail-closed | PASS | fila vazia no contexto admin + decidir etapa municipal => 404 "no contexto autorizado"; isolamento de esfera comprovado comportamentalmente |
| 17 | S9: honestidade fila/paineis/relatorio CSV vs banco vivo | PASS | fila analista api=2 db=2; gestor api=3 db=3; devolvidas=0; concluidas=3; CSV dataRows=14 == etapas=14; CSV contem os 5 numeros |
| 18 | S10: snapshot final do banco vivo | PASS | 5 requisicoes (APROVADA, PENDENTEx2, REJEITADA), 14 etapas, versoes consistentes, 13 chaves de idempotencia, 1 pendencia ABERTA, historico coerente (6 ENVIADA_PARA_APROVACAO etc.) |
| 19 | Tela/navegacao apos jornada (P1-P3 + 10 OBRIG + 3 DADO) | PASS | `nav_evidence.html` (78.664 chars): 13/13 SIM; DADO virou SIM com dados (link Detalhe, "Decisao sua", "Aguarda configuracao"); `aria-current="page"` x2; login oficial 302->Aprovacoes |
| 20 | Modelagem multi-esfera (municipal/estadual/federal; regras parametrizadas por esfera) | PASS | entidades demo 9101 (secretaria municipal), 9102 (secretaria estadual), 9103 (ministerio federal) com esfera_governo/tipo_entidade/hierarquia/abrangencia; isolamento comportamental no S8; nomenclaturas genericas (orgao, unidade gestora/executora) |
| 21 | Autoridade do banco + integridade do repositorio | PASS | catalogo com 42 permissoes `compras_empresariais` completas no banco; seed resolvido por chave (modulo+recurso+acao) com RAISE explicito; grants demo reparados ao vivo; `.env` local/ignorado (regra 16/18); manifest/scripts consolidados sem a string prohibida e com `uq_comp_aprovacao_ciclo` |

## DEFETO P0 DESCOBERTO E CORRIGIDO DURANTE O GATE (enviar => HTTP 500 universal)

- Sintoma: todo POST `.../enviar` retornava 500 `Falha inesperada no servidor` (NpgsqlException crua, sem log — mapeamento `Falha(ex)` do controller).
- Causa raiz: literal SQL `insereEtapa` em `src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs` (L~81, `EnviarAsync`) faltava UM parentese fechador apos `not i.is_deleted)`. Consequencia: `@ciclo,@us,@us,@corr` eram engolidos como pares chave/valor dentro de `jsonb_build_object(...)`, sobrando 8 expressoes para 12 colunas => PG `INSERT has more target columns than expressions`.
- Prova: replicacao passo a passo da transacao C# em psql (`probe_enviar_r2.sql`, BEGIN..ROLLBACK, falha exatamente no passo 9 = insert da etapa) + mapeamento programatico de profundidade de parenteses do literal e do arquivo (arquivo estava +1 antes do fix; 0 depois; 12 expressoes = 12 colunas).
- Correcao: um `)` adicionado apos `not i.is_deleted)` => `...is_deleted)),@ciclo,@us,@us,@corr)`. Unica ocorrencia do padrao em src; sem alteracao de schema/migration.
- Regressao pinada: fragmento novo em `tests/Sigov.ApiTests/PostBuild01RegressionTests.cs` (testes 26/26; suica completa 666/666).
- Prova comportamental: imagem `api` reconstruida, container recriado, reset do estado virgem, jornada completa em `JOURNEY_FAILS=0` (todos os envios 201/202 com etapas criadas).

## OUTROS DEFETOS CORRIGIDOS NESTA ENTREGA (historico da RC)

1. P1 (migration ainda nao publicada, editada in-place): `resultado_codigo varchar(32)` + branch de auto-cura (prova: `gate_heal_*`).
2. P2 (semantica): constraint `ck_comp_recb_div_codigo_encerrado` apertada — `ENCERRADA` exige codigo de catalogo valido.
3. Seed: ids de permissao hardcoded variavam entre ambientes => resolucao por chave (modulo+recurso+acao) com RAISE; banco vivo reparado (DELETE 11 grants obsoletos + reaplicacao, exit 0).
4. PermissionCatalog: 10 politicas de compras ausentes adicionadas via helper `Purchasing(...)`; completude provada no banco (42 linhas).
5. Ferramental (PS 5.1): splat de array com 4+ elementos parte argumentos `<prefixo>C:\...` em dois argumentos (prova argv via `echoargs.bat`); transporte JApi estavel via `cmd.exe /c` com string unica.

## ESTADO FINAL DO AMBIENTE AO FECHAR O GATE

- Containers: `sigov-api` Up (healthy, NOVA imagem com o fix), `sigov-web` Up (healthy), `sigov-worker` Up, `sigov-postgres` Up (healthy); `sigov-db-migrations` Exited(1) — esperado (item 6).
- Banco vivo: pos-jornada (com dados para demonstracao web): RC-2026-000002/Rq5 pendente com 3 etapas; RC-DEMO-0001 APROVADA; RC-DEMO-0002 PENDENTE bloqueada + pendencia; RC-DEMO-0003 APROVADA; RC-DEMO-0004 REJEITADA (2 ciclos).
- Para repetir a jornada do zero: `gate_reset.ps1` (deletes cirurgicos + reseed idempotente, RESET_OK) e entao `gate_jornada.ps1`.

## ARTIFATOS DE EVIDENCIA (Temp\opencode\)

- `jornada_evidence.txt` — traca completa S-1..S10 (71 asserts PASS, marcador final condicional em $JFail=0)
- `nav_evidence.html` + headers/cookies do login — render web pos-jornada
- `gate_reset.ps1` / saida de execucao — reset+verificacao do estado virgem
- `probe_enviar_r2.sql`, `gate_probe_enviar.ps1`, `gate_paren_*.ps1`, `gate_file_paren.ps1` — prova do defeito P0
- `gate_clean_install*.log`, `gate_upgrade_*.log/txt`, `gate_heal_*.log/txt` — gates de migration
- `gate_runtime_official_flow_fail.log` — fallback do fluxo oficial (item 6)
- `gate_build_api_web.log`, `gate_up_api_web.log` — imagens recriadas
- Base `d249dd2222ddc6225fca6bfd8b2711bb293c3751`; alteracoes somente no trabalho atual (nao commitadas)
