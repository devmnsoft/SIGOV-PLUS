# Relatório Ciclo 1 · Sprint 8 — Educação Avançada (transporte escolar: rota + ponto + vínculo de aluno)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **vertical de transporte escolar** multi-esfera (regra 24) sobre as 11 tabelas `sigov.educacao_transporte_*` criadas na RC50.48: três comandos tipados novos que ligam rota → ponto de embarque/desembarque → vínculo de aluno, e um dashboard dedicado com métricas reais:

- **Comando tipado `POST /api/educacao/transporte/rotas/lancar`** com `{nome, turno, escolaId?, regiao?, codigo?}`:
  - **Exatamente uma de `escolaId` ou `regiao`** (nenhuma ou ambas → **400 `ROTA_ESCOLA_OU_REGIAO_INVALIDA`**) — a rota por região atende órgãos, superintendências e regionais sem unidade escolar (regra 24; a região é persistida em `dados->>'regiao'`).
  - Com `escolaId`: reuso da validação de escopo institucional (S2) → **404 `ESCOLA_INEXISTENTE`** / **409 `ESCOLA_FORA_DO_ESCOPO`**.
  - Persistência: insert em `educacao_transporte_rota` (`ATIVA`, `tipo='ESCOLAR'`, `turno` normalizado para maiúsculas, auditoria `LANCAR_ROTA`). Simplificação declarada: a rota entra direto em `ATIVA` (a máquina S2 continua disponível nas rotas genéricas da família). A rota usa sub-path `/lancar` porque a família avançada já expõe POST genérico em `/rotas` (conflito ASP0023 sob `-warnaserror`).
- **Comando tipado `POST /api/educacao/transporte/rotas/{id}/pontos/lancar`** com `{nome, ordem, descricao?}`:
  - Rota inexistente/inativa → **404 `TRANSPORTE_ROTA_INEXISTENTE`**; rota não ativa → **409 `TRANSPORTE_ROTA_NAO_VIGENTE`** (máquina S2: terminal bloqueia operação).
  - `ordem > 0` → senão **400 `PONTO_ORDEM_INVALIDA`**.
  - **Uma ordem vigente por rota × tenant**: repetição → **409 `PONTO_ORDENACAO_EXISTENTE`**, com índice único parcial `ux_transporte_ponto_tenant_rota_ordem` como backstop transacional.
  - Persistência: insert em `educacao_transporte_rota_ponto` (`ATIVA`, `numero = ordem`, `turno` herdado da rota, `entidade_id`/`escola_id` espelhados da rota, auditoria `LANCAR_PONTO`) na mesma transação da checagem.
- **Comando tipado `POST /api/educacao/transporte/rotas/{id}/alunos/vincular`** com `{alunoId, pontoId?, turno?}`:
  - Mesmas validações de rota (404/409). Aluno validado em `sigov.aluno` (tenant, ativo → **404 `TRANSPORTE_ALUNO_INEXISTENTE`**, divergência de entidade → 409 `CONTEXTO_INSTITUCIONAL_DIVERGENTE`).
  - **Turno herdado da rota**: se informado e divergente do turno da rota → **400 `TURNO_VINCULO_DIVERGENTE`**.
  - Com `pontoId`: ponto inexistente/inativo → **404 `TRANSPORTE_PONTO_INEXISTENTE`**; ponto de outra rota → **409 `PONTO_ROTA_DIVERGENTE`** (o ponto é gravado em `referencia_id`).
  - **Um vínculo vigente por aluno × rota × turno** → repetição **409 `ALUNO_JA_VINCULADO_A_ROTA`**, com o índice pré-existente do rc50.48 `ux_educacao_transporte_aluno_rota_turno` como backstop transacional.
- **Dashboard dedicado `GET /api/educacao/transporte/dashboard`**: migrou do genérico para o DTO pré-existente `EducacaoTransporteDashboardDto` (scaffolding órfão da RC50) com métricas reais do tenant — `rotasAtivas`, `pontosAtivos`, `alunosVinculados` e `escolasAtendidas` (distinct de `escola_id` nas rotas ativas).

Sem novos estados na máquina de estados (S2): rota/ponto/vínculo entram `ATIVA` e saem pela família terminal já existente; terminal libera ordem/vínculo e bloqueia novas operações.

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011170000_evo_c1s8_transporte_ponto_ordem.sql` | novo: índice único parcial `ux_transporte_ponto_tenant_rota_ordem` sobre `(tenant_id, coalesce(rota_id,0), coalesce(numero,0))` com `is_deleted=false and ativo=true and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` + pós-condição explícita (falha nomeada `S8-POSTCOND` se faltar ele ou o `ux_educacao_transporte_aluno_rota_turno` do rc50.48) |
| `database/postgres/migrations/manifest.json` | entrada `20261011170000` registrada (sha256 `b1c9c5f0d62d45dc00037b9ff310a397bf4db286b2dab6db51d550983496eda2`, dependência `20261011160000`); 226 entradas |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1` (+`-Verify`) → S8 presente nos 6 consolidados |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | records novos `EducacaoTransporteLancarRotaRequest`/`EducacaoTransporteLancarPontoRequest`; shapes legados órfãos `EducacaoTransporteDashboardDto`/`EducacaoTransporteVincularAlunoRequest` atualizados para o contrato S8; 4 métodos em `IEducacaoTransporteEscolarService` (`LancarRotaAsync`, `LancarPontoAsync`, `VincularAlunoAsync`, `TransporteDashboardAsync`) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | 10 falhas nomeadas novas: `TRANSPORTE_ROTA_INEXISTENTE` (404), `TRANSPORTE_PONTO_INEXISTENTE` (404), `TRANSPORTE_ALUNO_INEXISTENTE` (404), `TRANSPORTE_ROTA_NAO_VIGENTE` (409), `PONTO_ORDENACAO_EXISTENTE` (409), `PONTO_ROTA_DIVERGENTE` (409), `ALUNO_JA_VINCULADO_A_ROTA` (409), `ROTA_ESCOLA_OU_REGIAO_INVALIDA` (400), `PONTO_ORDEM_INVALIDA` (400), `TURNO_VINCULO_DIVERGENTE` (400) |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | `LancarRotaAsync` (exatamente uma de escola/região + escopo institucional), `LancarPontoAsync` e `VincularAlunoAsync` (transações com checagem de vigência + backstop 23505), `TransporteDashboardAsync` (métricas dedicadas); helper privado `CarregarRotaNoEscopoAsync`; DTOs privados de leitura com aliases SQL explícitos (padrão Dapper das Sprints 4–7) |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | mapeamento HTTP das 10 novas exceções nos catches existentes de criação |
| `src/Sigov.Api/Controllers/EducacaoTransporteEscolarController.cs` | rotas tipadas `POST …/rotas/lancar`, `POST …/rotas/{id}/pontos/lancar`, `POST …/rotas/{id}/alunos/vincular`; `GET dashboard` agora retorna o DTO dedicado |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `TransporteEscolar_Sprint8_RotaPontoAluno_UnicidadeOrdemEVinculo` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado para 227 (registro da migration S8) (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E12 (20 checks: fixture, rota com escola+turno, sem escola nem região, escola+região juntos, regional sem escola (multi-esfera), escola inexistente, ponto com ordem, ordem duplicada, backstop de índice, ordem inválida, rota inexistente, vínculo aluno→rota, vínculo duplicado, backstop aluno×rota×turno, turno divergente, aluno inexistente, ponto de outra rota, vínculo com ponto da própria rota, dashboard dedicado, rota cancelada bloqueia operações); CX ampliado (vinculos/pontos/rotas C1S8-%, alunos C1S8-%, pessoas 999999000402/403, escola c1e26-s8-esc); scan case-insensitive das variáveis novas `$e12*` sem colisões |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e**.
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`S8-POSTCOND|t`); manifest + 6 consolidados sincronizados (regras 6–8).
- Suítes (execução limpa após build): Unit **521/521** · Integration **165/165** · Api **158/158**
  (0 ignorados; total 844; o delta de +1 na Integration é o fato estático novo da Sprint 8).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **⟨FILL: N/109⟩ PASS** (ts=⟨FILL: ts⟩),
  fixtures fictícios `C1S8-%`/`c1e26-s8-esc` (tenant 910001) aplicados e removidos ao final
  (resíduo zero); os 89 checks das Sprints 1–7 (E0–E11) revalidados na mesma rodada.

## Evidência Tier-2 ao vivo (bloco E12 — transporte escolar)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E12.0 | Fixture S8 (escola `c1e26-s8-esc` + pessoas/alunos fictícios `C1S8-ALA`/`C1S8-ALB`, tenant 910001) | ⟨FILL: exit=0⟩ |
| E12.1 | **Rota lançada** com escola + turno MANHA | ⟨FILL: 200; banco ATIVA\|escola\|MANHA\|ESCOLAR⟩ |
| E12.2 | Rota sem escolaId nem regiao | **400 `ROTA_ESCOLA_OU_REGIAO_INVALIDA`** |
| E12.3 | Rota com escolaId **e** regiao juntos | **400 `ROTA_ESCOLA_OU_REGIAO_INVALIDA`** |
| E12.4 | **Rota regional sem escola** (multi-esfera, regra 24) | ⟨FILL: 200; banco escola=-1, regiao em dados⟩ |
| E12.5 | Escola inexistente (9999999999) | **404 `ESCOLA_INEXISTENTE`** |
| E12.6 | **Ponto lançado** na ordem 1 | ⟨FILL: 200; banco 1\|rota\|ATIVA\|MANHA (turno herdado)⟩ |
| E12.7 | Segundo ponto na mesma ordem | **409 `PONTO_ORDENACAO_EXISTENTE`** |
| E12.8 | Insert SQL bruto duplicando a ordem | ⟨FILL: exit=3 citando ux_transporte_ponto_tenant_rota_ordem⟩ (backstop transacional) |
| E12.9 | Ordem 0 | **400 `PONTO_ORDEM_INVALIDA`** |
| E12.10 | Ponto em rota inexistente | **404 `TRANSPORTE_ROTA_INEXISTENTE`** |
| E12.11 | **Vínculo aluno → rota** (turno herdado) | ⟨FILL: 200; banco aluno\|rota\|MANHA\|-1\|ATIVA⟩ |
| E12.12 | Vínculo duplicado (mesmo aluno/rota/turno) | **409 `ALUNO_JA_VINCULADO_A_ROTA`** |
| E12.13 | Insert SQL bruto duplicando o vínculo | ⟨FILL: exit=3 citando ux_educacao_transporte_aluno_rota_turno⟩ (backstop rc50.48) |
| E12.14 | Vínculo com turno divergente da rota | **400 `TURNO_VINCULO_DIVERGENTE`** |
| E12.15 | Aluno inexistente (9999999999) | **404 `TRANSPORTE_ALUNO_INEXISTENTE`** |
| E12.16 | Vínculo com ponto de outra rota | **409 `PONTO_ROTA_DIVERGENTE`** |
| E12.17 | Vínculo com ponto da própria rota | ⟨FILL: 200; referencia_id = id do ponto⟩ |
| E12.18 | **Dashboard dedicado** | ⟨FILL: 200; rotasAtivas/pontosAtivos/alunosVinculados/escolasAtendidas⟩ |
| E12.19 | Rota `CANCELADA` via máquina de estados + novo ponto nela | ⟨FILL: 200/409 `TRANSPORTE_ROTA_NAO_VIGENTE`⟩ (terminal bloqueia operação) |
| CX | Limpeza do fixture (ampliada com vínculos, pontos, rotas C1S8-%, alunos, pessoas e escola S8) | ⟨FILL: restantes=0⟩ |

(Blocos E0–E11 das Sprints 1–7 revalidados na mesma rodada: dashboard conjunto (contrato S7),
CSV integral, diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de
versão, escopo institucional, unicidade de matrícula, transferência com linhagem, correção auditada
de frequência, fechamento/reabertura de diário, publicação de histórico escolar versionado, cadastros
base, merenda, biblioteca circulação — todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

⟨FILL: lista dos defeitos encontrados nas corridas ao vivo e como foram corrigidos⟩

## Resíduos declarados (regra 13)

- Rotas entram direto em `ATIVA` no comando tipado (simplificação declarada); `RASCUNHO → ATIVA`
  segue disponível via máquina de estados nas rotas genéricas da família.
- Ponto não tem código/nome únicos — a unicidade é por **ordem** por rota × tenant (modelo
  operacional declarado); pontos com `numero NULL` (inserts fora do comando tipado) compartilham a
  chave 0 no índice e não colidem entre si (comportamento PostgreSQL de coalesce documentado).
- Vínculo guarda o ponto em `referencia_id` (slot genérico das tabelas largas rc50.48) — sem coluna
  dedicada por ponto (evitar migration de schema além do necessário nesta fatia).
- `sigov.aluno` validado com checagem de entidade (mesmo padrão do S7); o turno do vínculo é
  normalizado para maiúsculas (comparação case-insensitive declarada).
- `validate-script-completop` falha em seed pré-existente `admin@sigov.local` (já em HEAD; não
  introduzido nesta sprint) — declarado, não mascarado.
- `rc50_25` permanece arquivo órfã do manifest (intocada, regra 8).
- RAM local cronicamente baixa (~1 GB livre) durante as corridas — ambiente de dev local, sem impacto
  no artifact; apps reiniciados com kill dirigido imediatamente antes da run validada.
