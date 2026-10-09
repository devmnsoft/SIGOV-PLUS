# RELATORIO §6 RC-EVO-B — Contexto financeiro da sessão na integração ponto → folha

Data: 2026-10-09 (UTC)
Base commit: `6c7b269a` (HEAD) sobre a jornada RC-EVO-B (`cd5fd580`, evidência journey8). Stack: .NET SDK 10.0.400 (`global.json` 10.0.100 + `latestFeature`), PostgreSQL 16.14 (Docker disponível neste host; container `sigov_evo_c_pg` com instalação limpa no estado atual do repo).
Escopo: regida como incremento da jornada EVO-B (regra fail-closed) sobre a integração financeira de ponto na folha (RC-EVO-RH §9 / EVO-B §5/§6). Sem alteração de schema, migration, scripts consolidados, permissão ou interface.

## RESULTADO GLOBAL DO §6

- Regra fail-closed implementada e falha nomeada antes de materializar: **PASS**
- Build Release `-warnaserror`: **PASS** (0w/0e)
- Suítes existentes preservadas e verdes: **PASS** (Unit 508 / Integration 158 / Api 158)
- Manifest JSON válido: **PASS** (220 entradas, última `20261008130000`, 4 excluídas do baseline)
- Bug novo introduzido: **NÃO** (incremento aditivo de guarda, sem alterar comportamento já homologado)

## O PROBLEMA QUE A REGRA FECHA

Com `HABILITAR_INTEGRACAO_FINANCEIRA=true` (parâmetro do módulo FOLHA, banco é a autoridade), a sessão precisa trazer **entidade** e **exercício** resolvidos para que o custo da folha chegue ao Financeiro com contexto. Antes do §6, uma sessão sem esses dois valores ainda materializava a integração e publicava na fila: o custo da folha saía da RH com **resposta de sucesso**, mas ficava órfão no Financeiro (sem entidade/exercício). A fila nunca pode ser pulada ou preenchida em silêncio com sucesso falso (regra 13: ausência/falha deve ser explícita; nunca simular sucesso).

§6 promove a recusa para **antes** de materializar: se a integração financeira está habilitada mas a sessão não resolve entidade e/ou exercício, o serviço retorna falha nomeada listando as ausências, sem gravar integração nem publicar fila. Desabilitada, não é recusa (não há promessa ao Financeiro; a integração responde `habilitada=false`).

## IMPLEMENTAÇÃO

| Arquivo | Alteração |
|---|---|
| `src/Sigov.Domain/Rh/FolhaPontoFinanceiraRegras.cs` | Nova constante `FalhaContextoAusente = "CONTEXTO_FINANCEIRO_AUSENTE"` e método puro `ValidarContextoFinanceiro(habilitarIntegracaoFinanceira, entidadeId, exercicioId)` → `null` quando desabilitada ou contexto completo; senão mensagem nomeada listando as ausências ("entidade" e/ou "exercício"). |
| `src/Sigov.Application/Rh/RhServices.cs` | No fluxo de integrar apuração na folha, após ler `HABILITAR_INTEGRACAO_FINANCEIRA` e **antes** de materializar/publicar, invoca `ValidarContextoFinanceiro(..., _tenant.EntidadeId ?? 0, _tenant.ExercicioId ?? 0)`; falha retorna antes de qualquer gravação. Os mesmos IDs já fluem para `IntegrarApuracaoNaFolhaAsync` (publicação), então a guarda valida exatamente o que será persistido. |
| `tests/Sigov.UnitTests/Rh/RhRulesTests.cs` | Novo fato `S6_IntegracaoHabilitada_Sem_Entidade_Ou_Exercicio_Na_Sessao_Falha_Nomeada` na classe existente (regra 14: nenhuma classe nova). |

Contrato da regra (puro, determinístico):
- `ValidarContextoFinanceiro(false, x, y)` → `null` (desabilitada não é recusa).
- `ValidarContextoFinanceiro(true, e>0, x>0)` → `null` (contexto suficiente; integra e publica).
- `entidade <= 0` e/ou `exercício <= 0` com habilitada → mensagem contendo `CONTEXTO_FINANCEIRO_AUSENTE` e o(s) nome(s) ausente(s), sempre "nenhuma integração foi registrada".

## GATES EXECUTADAS (host NCELL-DEV-020, 2026-10-09)

| Medição | Esperado | Obtido |
|---|---|---|
| `dotnet restore sigov.sln` | ok | ok (restore não-locked por RID do lockfile `linux-x64` ser incompatível com este host — artefato de ambiente, não do código) |
| `dotnet build sigov.sln -c Release --no-restore --nologo -warnaserror` | 0w/0e | **0w / 0e** |
| `dotnet test Sigov.UnitTests -c Release --no-build` | todos verdes | **Aprovado: 508 / Total: 508 / Falha: 0** (inclui o novo fato §6) |
| `dotnet test Sigov.IntegrationTests -c Release --no-build` | todos verdes | **Aprovado: 158 / Total: 158 / Falha: 0** |
| `dotnet test Sigov.ApiTests -c Release --no-build` | todos verdes | **Aprovado: 158 / Total: 158 / Falha: 0** |
| `python -m json.tool database/postgres/migrations/manifest.json` | JSON válido | **OK** — 220 entradas, última `20261008130000`, 4 excluídas do baseline |

Nota de RID: o `packages.lock.json` foi avaliado para `linux-x64`; neste host (win, `--locked-mode`) o restore reportou `NU1004` por divergência de runtime identifier. A validação do código foi feita com restore padrão + build `-warnaserror`, que é a evidência de compilação relevante. O modo lock do CI permanece inalterado.

## COBERTURA DE TESTE

O §6 é uma **regra pura de domínio** decidida antes de qualquer acesso a banco. O gate adequado é o teste de unidade na classe existente `RhRulesTests`, mesmo padrão do S3.2 (limites comerciais também cobertos por fatos estáticos em classes existentes). O caminho de integração real (publicação na fila outbox, consumo, checksum/hash do payload, parâmetros FOLHA fail-closed) já foi homologado na jornada EVO-B (evidência journey8) e no RC-EVO-RH §9; o §6 apenas antecede aquela materialização com a recusa de contexto, sem alterar a publicação já provada.

## ESCOPO E LIMITES

- Sem migration: nenhum schema novo; reutiliza o contexto já resolvido na sessão (`_tenant.EntidadeId`/`ExercicioId`).
- Sem alteração de permissão, migratão, consolidados, menu ou dashboard (fora das restrições P0/P1 da regra 20).
- Multi-esfera: a guarda é genérica por `entidade`/`exercício` resolvidos na sessão — não introduz comportamento municipal hardcoded (regras 21/22).
- Não promove estado de fluxo vertical na matriz; é guarda fail-closed sobre fluxo já existente.

## ARTEFATOS

- Este relatório: `docs/evidencias/rcevo/relatorio_s6_contexto_financeiro.md`.
- Saídas das suítes: logs de console desta execução (build 0w/0e; Unit 508; Integration 158; Api 158).
