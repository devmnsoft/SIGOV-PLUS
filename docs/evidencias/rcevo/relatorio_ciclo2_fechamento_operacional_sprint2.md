# Relatório Ciclo 2 · Sprint 2 — Fechamento Operacional de Educação: máquina de estados por recurso com efeitos declarados, fim dos bypasses genéricos e tempo único

Data: 2026-10-12 · Trilha: commit `f1fd1399` (Ciclo 2 S1) + `9887e8a4` (código/tests desta sprint) + commit desta sprint (driver + evidências) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **máquina de estados genérica** dos recursos avançados de educação (`EducacaoStatusMaquina` + `EducacaoAvancadaRepository.AlterarStatusAsync` + controller base) passa de matriz com wildcard de origem e criação livre a **comportamento declarativo por família de status com efeitos por recurso** (BR-2.1 a BR-2.6 do roadmap `docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md`). Base normativa da fatia: LGPD/LAI (trilha de transição auditável), IN 65/2020 (TSP — idempotência e trilha em comandos tipados) e a regra 13 do AGENTS.md (falha nomeada explícita, nunca sucesso simulado). Sem migration nesta sprint: as regras vivem em C# sobre o schema existente (manifest permanece 230, consolidados intocados — regra 7 satisfeita sem alteração).

- **BR-2.1 — Matriz de transição declarativa com efeitos + origem legada tratada:**
  - Wildcard de origem `_ => true` eliminado da máquina. Origem fora das famílias canônicas cai no
    estado nomeado `LEGADO_NAO_REGULARIZADO`: só regulariza para família canônica **com
    justificativa** (409 `REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA` sem ela), não salta direto a
    terminal nem para destino não-canônico (409 `TRANSICAO_INVALIDA`).
  - **Efeitos declarados por recurso × estado** (`EducacaoEfeitosTransicao.Matriz`, aplicada antes
    de gravar): o único efeito vigente na fatia é a distribuição que já consumiu estoque exigir o
    estorno tipado antes de fechar (409 `DISTRIBUICAO_ESTORNO_OBRIGATORIO`, herdado da S1) — a
    matriz é o ponto único onde novos efeitos entram (ex.: exemplar bloqueado ao encerrar
    empréstimo com perda — S3).
- **BR-2.2 — Criação genérica restrita aos comandos tipados:** `CriarAsync` rejeita POST genérico
  nos 6 recursos que têm comando tipado — `educacao_merenda_movimento`,
  `educacao_merenda_distribuicao`, `educacao_merenda_estoque`, `educacao_biblioteca_emprestimo`,
  `educacao_biblioteca_reserva`, `educacao_fundeb_aplicacao` — com **409
  `COMANDO_TIPADO_OBRIGATORIO`** e a rota tipada na mensagem. Recursos livres (produtos, acervo,
  cardápios…) seguem genéricos. A porta legado `POST /merenda/estoques` fica restrita por
  constante `PortaLegadaEstoqueAberta = false` (reabre só por código, nunca por flag ambiente).
- **BR-2.3 — Guard de versão quando já houve atualização:** a cláusula de escrita
  `(@VersaoAtual::timestamptz is null or coalesce(updated_at,created_at)=@VersaoAtual)` já existia;
  agora é **exigida**: se `updated_at is not null` e o cliente não enviou `updatedAt` → 409
  `CONFLITO_VERSAO` (a própria gravação anterior atualizou o carimbo — o reenvio cego perdeu a
  corrida). Primeira escrita de um registro recém-criado segue sem versão.
- **BR-2.4 — Terminais irreversíveis:** família TERMINAL não reabre via máquina genérica;
  reabertura só existe quando a família declara porta própria (nenhuma declara nesta RC).
- **BR-2.5 — Clock único via DI:** repositório avançado consome `IDateTimeProvider.DataAtual`
  (registro existente no DI; implementação lê `SIGOV_CLOCK_DATAFIXA` para homologação, senão data
  local do host). Zero `DateTime.Today` restante no repositório — biblioteca (C#) e elegibilidades
  SQL compartilham a mesma fonte de tempo operacional por comando.
- **BR-2.6 — Schema ausente = falha nomeada uniforme:** listar, criar, status, dashboard (e o CSV,
  via caminho genérico) respondem **404 `RECURSO_INDISPONIVEL`** idêntico quando a tabela do
  recurso não existe (era 200 + diagnóstico vazio). Coberto também nas leituras sem try/catch pelo
  filtro de exceção `EducacaoFalhasNomeadasFiltro` (TypeFilter do controller base avançado).

## Suítes (estendidas, sem novas classes — regra 14)

- `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs`: fact estático novo
  `EducacaoAvancada_Ciclo2Sprint2_MaquinaEfeitosBypassesEVersionamento` (asserts de fonte: matriz
  sem wildcard de origem, estado legado, mapa dos 6 recursos com comando tipado,
  `PortaLegadaEstoqueAberta=false`, zero `DateTime.Today`, catches nomeados no controller base) —
  inclusive `NotContain("_ => true")` sobre a máquina (foi exatamente esse fact que pegou um
  comentário-fonte com o literal do wildcard, corrigido no mesmo ciclo).
- `tests/Sigov.UnitTests/PagedResultTests.cs`: extenso para o estado de indisponibilidade.
- Executado: **849 verdes (Unit 522 / Api 158 / Integration 169, 0 ignorados)**; build Release
  `-warnaserror` 0w/0e (uma iteração de build: typo do ctor `IDataTimeProvider` corrigido).

## Matriz verificável da corrida oficial (bloco E16 no driver Tier-2)

| Check | Cenário | Esperado |
|---|---|---|
| E16.1–E16.6 | POST genérico nos 6 recursos com comando tipado (movimento, distribuição, estoque legado, empréstimo, reserva, aplicação FUNDEB) | **409 `COMANDO_TIPADO_OBRIGATORIO`** com a rota tipada na mensagem |
| E16.7 | POST genérico em recurso livre (produto de merenda) | 200 (caminho genérico intacto) |
| E16.8 | Registro legado (`LEGADO_HISTORICO`) regularizado para ATIVA com justificativa | 200; banco ATIVA (estado `LEGADO_NAO_REGULARIZADO` tratável) |
| E16.9 | Origem legada sem justificativa | **409 `REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA`** |
| E16.10 | Origem legada → destino não-canônico (ZULU) | **409 `TRANSICAO_INVALIDA`** (wildcard de origem eliminado) |
| E16.11 | Origem legada → terminal direto (ENCERRADA) | **409** (sem salto direto a terminal) |
| E16.12 | Segunda transição sem `updatedAt` após registro já atualizado | **409 `CONFLITO_VERSAO`** |
| E16.13 | Mesma transição com `updatedAt` correto (fetch do banco) | 200 |
| E16.14 | Tabela renomeada: listar/criar/status/dashboard | **404 idêntico nos 4 caminhos** (mensagem padrão `RECURSO_INDISPONIVEL`) |
| E16.15 | Tabela restaurada íntegra | linhas antes = depois |
| E16.16 | Empréstimo tipado: `data_inicio`/`competencia` = data operacional do host (fonte única C#; SQL UTC não governa) | igualdade com data local |

(Blocos E0–E15 das Sprints 1–10 do Ciclo 1 + S1 Ciclo 2 revalidados na mesma rodada: todos os 173
checks do baseline devem seguir PASS.)

## Corrida oficial — rodada 1: 187 PASS / 2 FAIL (triagem sem máscara)

Primeira execução viva desta sprint (`ts=2026-10-12T13:09Z`): 187 aprovados / 2 falhas. Triagem:

**Classe B — defeito do driver (harness), detectado na prova e corrigido antes da rodada 2:**

1. **SQL inválida no fetch de versão:** os fetches `select coalesce(updated_at,'null')::text`
   (E4 s1/s2 e E9 t92) são inválidos no PostgreSQL: dentro de `COALESCE(timestamptz, literal)`, o
   literal `'null'` é resolvido como `timestamptz` e falha na parsing da constante
   (`invalid input syntax for type timestamp with time zone: "null"`). O texto de erro do psql
   vazava para o campo `text` e era embutido no parâmetro `updatedAt` → 400 de validação de modelo
   MVC (`The value 'docker : psql:...ERROR...'`) — nada gravava, coerente com a amostra de auditoria
   intacta (`ATIVA|||{}`) e com o t93 do cardápio voltando 409 `CONFLITO_VERSAO` (registro ainda
   ATIVA com `updated_at` já preenchido). Corrigido para `coalesce(updated_at::text,'null')`
   (cast da coluna primeiro, depois o coalesce de strings — 3 ocorrências); formato de saída
   `YYYY-MM-DD HH24:MI:SS.FF±hh` confirmado parseável pelo binding `DateTime?` (prova local:
   `+00` e `-03` OK).

**Classe A/C — produto:** nenhum. Os dois FAILs eram a Classe B acima (E4
`status.auditoria-de-para-motivo-append` e E9 `cardapio.maquina-ativa-terminal-e-final`).

**Ambiente (declarado):** a API seguiu firme nos ~129 checks; o host tem RAM crônica baixa
(FREE_MB < 1 GB durante a sessão) e o mesmo binário completou a corrida inteira na Sprint 1.

## Corrida oficial — rodada 2: 127 PASS / 62 FAIL (colapso ambiental do processo da API)

Após a correção do driver (`ts=2026-10-12T13:24Z`): os dois checks da rodada 1 voltaram
**PASS** — E4 auditoria append-only com as duas entradas de/para/motivo
(`de:ATIVA→ATIVA→CANCELADA`, motivo preservado) e E9 cardápio RASCUNHO→ATIVA 200 →
ENCERRADA 200 → ATIVA 409 `TRANSICAO_INVALIDA` (terminal não reabre). A partir do check ~129
(S9/FUNDEB, `aplicacao.centro-rascunho`) o processo da API morreu: todos os calls HTTP seguintes
responderam `code 0` (connection refused instantânea) enquanto os psqls continuavam vivos; os
erros de SQL posteriores (`... and id= limit 1`) são cascateados dos ids extraídos de respostas
mortas (Classe C pura). Log do Windows: processos dotnet órfãos de launcher disputando bind
(`Failed to bind to address http:` às 10:28/10:32 locais) e 6 processos dotnet residuais
consumindo RAM. Mesmos binários da rodada 1 (que completou 100%) → **ambiente, não produto**.

Remediação: kill de todos os processos dotnet (RAM livre 729 → 1187 MB), relançamento limpo dos
apps com portas 5099/5100 confirmadas livres antes do start, e disparo da rodada 3 atrás da sonda
de readiness.

## Corrida oficial — rodada 3: readiness timeout (relançamento não persistiu)

Após o kill total + relançamento, o app nunca ficou pronto em nenhum ciclo de sonda
(`web=0 api=0` em todo o loop de readiness; zero entradas novas no `curl_diag.log` desde o fim
da rodada 2) — mesmo sintoma ambiental de launcher/RAM do host; nenhuma chamada do driver ocorreu
nesta rodada (evidência: `artifacts/c1_tier2_e2e/s2_e2e_round3_readiness_timeout.txt`). Remediado
no runner da rodada 4 (kill → relanço → readiness com retry automático → driver num único fluxo).

## Corrida oficial — rodada 4: 189 PASS / 0 FAIL (corrida oficial)

Com a pilha de apps limpa e as portas confirmadas livres antes do start
(`ts=2026-10-12T15:26Z`, `DRIVER_EXIT=0`), a corrida oficial aprovou **todos os 189 checks**: os
173 do baseline das Sprints 1–10 do Ciclo 1 + S1 do Ciclo 2 revalidados integralmente — incluindo
o E4 de auditoria append-only com as duas entradas de/para/motivo preservadas
(`de:ATIVA→ATIVA motivo c1-t2-ativacao; de:ATIVA→CANCELADA motivo c1-t2-cancelamento-e2e`) e o E9
do cardápio (RASCUNHO→ATIVA 200 → ENCERRADA 200 → ATIVA 409 `TRANSICAO_INVALIDA`, terminal não
reabre) — e o bloco E16 **16/16** desta sprint: 6 bypasses genéricos rejeitados com
`COMANDO_TIPADO_OBRIGATORIO` (rota tipada na mensagem), recurso livre seguindo genérico (200),
origem legada só regularizando para canônico com justificativa (sem ela → 409
`REGULARIZACAO_LEGADA_EXIGE_JUSTIFICATIVA`), destino não-canônico e salto direto a terminal ambos
409 (wildcard de origem eliminado), segunda transição sem `updatedAt` → 409 `CONFLITO_VERSAO`
(com a versão correta do banco → 200), schema renomeado → 404 `RECURSO_INDISPONIVEL` **idêntico
nos 4 caminhos** (listar/criar/status/dashboard) com a tabela restaurada íntegra depois (linhas
antes = depois), e data de empréstimo igual à data operacional do host (fonte única C#; SQL UTC
não governa). Race de 8 ABERTURAS paralelas manteve exatamente 1 linha (perdedores 409, nunca
duplicata); conciliação movimento×saldo fechada (`saldo=7.0000 = soma dos movimentos`); cleanup
final `restantes=0`. Evidências arquivadas:
`artifacts/c1_tier2_e2e/s2_e2e_round1_187p_2f.log`, `s2_e2e_round2_127p_62f.log`,
`s2_e2e_round3_readiness_timeout.txt` e `s2_e2e_round4_final_189p_0f.log`.

## Defeitos descobertos na prova e corrigidos (rodadas prévias, sem máscara)

1. **Typo do ctor `IDataTimeProvider` (build CS0246):** primeira compilação da sprint apontou
   `IDataTimeProvider` inexistente no repositório avançado; corrigido para `IDateTimeProvider`.
   Build Release `-warnaserror` reexecutado: 0w/0e.
2. **Comentário-fonte com o literal do wildcard eliminado:** o novo fact de integração asserciona
   `NotContain("_ => true")` sobre a fonte da máquina e pegou o próprio comentário que citava o
   coringa como referência histórica; reescrito ("wildcard de origem eliminado"). Doc comments
   continuam seguros porque escopados em XML (`&gt;`).
3. **SQL inválida no fetch de versão do driver** (detalhado na rodada 1 acima) — harness,
   corrigido antes da rodada 2.

## Resíduos declarados (regra 13)

- **Sem reabertura de cardápio na máquina genérica**: o baseline E9 (RASCUNHO→ATIVA→ENCERRADA com
  encerramento aceito direto de ATIVA) prevalece; a restrição de reabertura termina em
  `TRANSICAO_INVALIDA` (terminal irreversível), não bloqueia o encerramento.
- `educacao_fundeb_receita` e `educacao_custo_lancamento` permanecem **genéricos nesta RC**
  (não entraram no mapa de comando tipado da fatia; evolução futura com RC própria).
- Competência de movimento usa `current_date` no SQL: o banco exerce a regra de competência; a
  fonte única do clock governa as datas escritas pelo C# por comando.
- `PortaLegadaEstoqueAberta` é constante `false` no código: a porta legado de estoque só reabre
  por mudança de código, nunca por configuração externa.
- `PagedResult.Indisponivel` existe e é coberto por teste unitário, mas ainda sem uso em `src`
  (o caminho 404 nomeado é o da Sprint 2 — BR-2.6); o estado servirá a leituras paginadas futuras.
- Herdadas das sprints anteriores (não reintroduzidas): `validate-script-completop` falha em seed
  pré-existente `admin@sigov.local` (já em HEAD); generator reporta as 4 migrations com
  `includeInBaseline:false` como excluídas; `rc50_25` órfã permanece intocada (regra 8); RAM local
  cronicamente esgotada neste host — apps mantidos em shells destacados + corrida oficial atrás
  de sonda de readiness (mesma política operacional das Sprints 8–10).

## Gates da sprint (RC própria — regra 20)

- Build: `dotnet build Sigov.sln -c Release -warnaserror` → 0 avisos / 0 erros.
- Suítes (estendidas, sem novas classes — regra 14): Unit / Api / Integration verdes com o novo
  fact estático `EducacaoAvancada_Ciclo2Sprint2_MaquinaEfeitosBypassesEVersionamento` e a extensão
  de `PagedResultTests`; **executado: 849 verdes (Unit 522 / Api 158 / Integration 169,
  0 ignorados)**.
- Sem migration nesta fatia: manifest 230 e consolidados intocados e íntegros (regras 6–7).
- Execução viva Tier-2: driver `scripts/c1_tier2_e2e.ps1` com o bloco E16 (checks acima) sobre os
  apps reais (Web :5099 / Api :5100), tenant 910001, login `hom.a.admin` + antiforgery + bearer
  `/Auth/ApiToken`; **executado: 189/189 PASS na corrida oficial (rodada 4 — 173 baseline + 16
  E16), limpeza `restantes=0`, `DRIVER_EXIT=0`** (rodadas 1–3 triadas acima).
- Docs: `ULTIMA_EXECUCAO.md`, `BACKLOG_EXECUTAVEL.md`, `MATRIZ_FLUXOS_VERTICAIS.md`,
  `STATUS_REAL_MODULOS.md` atualizados com o nível real da jornada:
  **IMPLEMENTADO + VALIDADO POR TESTES + EXECUTADO EM INTEGRAÇÃO + HOMOLOGADO PELA INTERFACE**
  para o escopo da fatia (máquina com efeitos, bypasses tipados, versão, relógio, schema uniforme)
  — o módulo como um todo não é promovido por endpoint isolado.
