# Relatório Ciclo 1 · Sprint 4 — Educação Avançada (notas e boletim: publicação de histórico escolar versionado + portais)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

Publicação de histórico escolar/boletim (`EducacaoBloco3Repository`/`Service`/Portal), multi-esfera (regra 24):

- **Avaliação com notas por aluno e componente curricular**: `EducacaoDiarioAvaliacaoRequest` ganha
  `ComponenteCurricular` e `Alunos` (`record(AlunoId, Nota)`), persistidos no jsonb `dados` da
  `educacao_diario_avaliacao` (a tabela não tem colunas de nota por aluno; o jsonb é o contrato).
  Validação nomeada no serviço: valor máximo positivo, nota dentro de `[0, valorMáximo]`, aluno válido.
- **Publicação do histórico escolar**: `POST /api/educacao/portal/admin/historico-escolar/{alunoId:long}/publicar`
  com `{matriculaId}` — agrega por componente curricular, sobre as turmas do aluno no ano letivo da
  matrícula-âncora, as notas médias das avaliações do diário (`dados->'Alunos'`) em FULL JOIN com a
  frequência do diário (`100*presenças/total` por componente). O `educacao_historico_escolar` é
  versionado (`dados->versao` incrementado a cada republicação, status `PUBLICADO`, auditoria
  `PUBLICACAO_HISTORICO`) e os itens (`educacao_historico_escolar_item`) são substituídos transacionalmente.
- **Portais consomem o publicado**: boletim do portal (`GET /portal/alunos/{id}/boletim`) e
  vida-escolar (`GET /portal/alunos/{id}/vida-escolar`) reagem à publicação e às chamadas lançadas,
  sem regra de aprovação inventada (regra: o portal apenas expõe o que a secretaria publicou/agregou).
- **Autorização com banco como autoridade** (regras 11/12): nova permissão granular
  `educacao.historico_escolar.publicar` (módulo educacao, recurso historico_escolar, ação publicar)
  semeada idempotentemente na migration, com concessão `PERMITIR` aos perfis administrativos por
  tenant (mesmo padrão RC50.29: ADMINISTRADOR_GERAL / ADMINISTRADOR_TENANT / SUPERADMIN / *ADMINISTRADOR*).
- **Matrícula-âncora validada**: vigente (`ATIVA`/`CONFIRMADA`/`CONCLUIDA`), do aluno, vinculada a
  turma e dentro do escopo institucional do contexto autenticado (mesma política da Sprint 2 #6).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011130000_evo_c1s4_educacao_historico_escolar_publicar.sql` | novo: permissão `educacao.historico_escolar.publicar` + concessão PERMITIR aos perfis administrativos (padrão RC50.29) + pós-condição explícita (falha nomeada se faltar permissão ou concessão) |
| `database/postgres/migrations/manifest.json` | entrada `20261011130000` registrada (sha256 `4ef25a86cb151deecb8458d90ea68b27976431a5d1230e86e9c234af55cb327f`); 222 migrations |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1` (+`-IncludeDevelopmentSeed` para os dev) → S4 presente nos 6 consolidados |
| `src/Sigov.Application/Educacao/Bloco3/EducacaoBloco3Contracts.cs` | `EducacaoDiarioAvaliacaoRequest` +`ComponenteCurricular`/`Alunos`; novo `EducacaoDiarioAvaliacaoItemRequest(AlunoId, Nota)`; novos `EducacaoPublicarHistoricoEscolarRequest/Dto`; método no contrato do portal e do repositório; **correção**: `VidaEscolarDto.AtualizadoEm` `DateTimeOffset`→`DateTime` |
| `src/Sigov.Infrastructure/Educacao/Bloco3/EducacaoBloco3Repository.cs` | `PublicarHistoricoEscolarAsync` (transação: lock da matrícula, agregação por componente CTEs av/fr full join, upsert versionado, substituição de itens); **correção**: `BoletimHeader.AtualizadoEm` `DateTimeOffset`→`DateTime` (mesma armadilha Dapper/timestamptz da Sprint 3) |
| `src/Sigov.Application/Educacao/Bloco3/EducacaoBloco3Service.cs` | `PublicarHistoricoEscolarAsync` (guard `portal-boletim` + administrativo + `Pode("educacao.historico_escolar.publicar")` + falhas nomeadas); `Validar` do recurso `avaliacao` (valor máximo positivo, nota no intervalo, aluno válido) |
| `src/Sigov.Api/Controllers/EducacaoBloco3Controllers.cs` | rota `POST admin/historico-escolar/{alunoId:long}/publicar`; avaliação passa `componenteCurricular`/`alunos` para o jsonb |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `EducacaoNotasBoletim_Sprint4_PublicacaoHistoricoEscolar_Versionada` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado 222→223 segmentos para registrar a migration (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E8 (11 checks: vínculo de portal, avaliação com notas no jsonb, 3 chamadas, publicação v1, boletim, vida-escolar, republicação v2, 404 matrícula alheia, 400 sem lançamentos, permissão/banco) + CX ampliado (histórico+itens, vínculos, responsável, pessoa do usuário restaurada) |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e**.
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`POSTCOND|true|true`; permissão id=1848; 1 concessão para o tenant 910001, 18 no total);
  manifest + 6 consolidados sincronizados (regras 6–8).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **52/52 PASS** (ts=2026-10-11T14:03:08Z),
  fixtures fictícios `c1e26-%`/`C1S3-%` + marcadores S4 (tenant 910001) aplicados e removidos ao
  final (resíduo zero); os 41 checks das Sprints 1–3 (E0–E7) revalidados na mesma rodada.
- Suítes (execução limpa após build): Unit **521/521** · Integration **161/161** · Api **158/158**
  (0 ignorados; total 840/840; o delta de +1 na Integration é o fato estático novo da Sprint 4).

## Evidência Tier-2 ao vivo (bloco E8 — notas e boletim)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16),
reaproveitando o fixture S3 (escola/ano letivo 2026/turma A/aluno X/professor MATEMATICA/matricula mX):

| # | Cenário | Resultado |
|---|---|---|
| E8.0 | Fixture S4 (pessoa responsável fictícia vinculada ao `hom.a.admin` + responsável cadastral do aluno X + aluno D enturmado em A sem lançamentos) | exit=0 (marcadores 999999000401/999999000104/C1S3-MAT-D) |
| E8.1 | Vínculo de portal criado pela secretaria (`POST /portal/admin/vinculos`) | 200; banco `ATIVO\|910301\|<alunoX>` |
| E8.2 | Avaliação do diário (período 2, turma A) com `componenteCurricular=MATEMATICA` e nota 9 para o aluno X | 200; jsonb `dados`: `MATEMATICA\|1\|9\|<alunoX>` |
| E8.3 | 3 chamadas de frequência no aluno X (PRESENTE+PRESENTE+FALTA, MATEMATICA, turma A) | 200/200/200; banco total\|presentes = `3\|2` |
| E8.4 | **Primeira publicação** (`POST …/historico-escolar/{X}/publicar`, matricula mX) | 200 versao=1 itens=1; banco `PUBLICADO\|1\|1`; item `MATEMATICA\|9.00\|66.67` (média real e 2/3 de frequência) |
| E8.5 | Boletim do portal | 200 `situacao=PUBLICADO versao=1 item MATEMATICA nota=9.00 frequencia=66.67` |
| E8.6 | Vida-escolar do portal | 200 `aulasLancadas=3 presencas=2 faltas=1 percentual=66.67%` |
| E8.7 | **Republicação** | 200 versao=2; boletim reflete `versao=2` (itens substituídos no mesmo histórico versionado) |
| E8.8 | Publicação com `matriculaId=9999999999` | **404 nomeado "não encontrada"** (não 500; regra 13) |
| E8.9 | Publicação do aluno D (enturmado em A, zero lançamentos) | **400 "Sem avaliações ou frequência registrada…"** (sucesso vazio não simulado; regra 13) |
| E8.10 | Permissão no banco (autoridade) | `1848\|1\|18` — permissão ativa no catálogo + concessão PERMITIR para o tenant 910001 (regra 11) |
| CX | Limpeza do fixture (ampliada com histórico+itens, vínculos, responsável e restauração da pessoa do usuário) | `restantes=0` |

(Blocos E0–E7 das Sprints 1–3 revalidados na mesma rodada: dashboard conjunto completo, CSV integral,
diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de versão, escopo
institucional, unicidade por oferta, transferência com linhagem, correção auditada de frequência,
fechamento/reabertura de diário — todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

1. **Boletim/vida-escolar do portal 500 latente (armadilha Dapper)**: `BoletimHeader.AtualizadoEm`
   (repositório) e `VidaEscolarDto.AtualizadoEm` (contrato) eram `DateTimeOffset` mapeados de coluna
   `timestamptz` — o Npgsql materializa como `DateTime` e o ctor matching do Dapper falha quando há
   linha (mesmo mecanismo já corrigido na Sprint 3 para `FechamentoDto`). Só apareceu agora porque o
   boletim/vida-escolar nunca tinham sido exercidos **com dados publicados**; o E8 os exerceu ao vivo.
   Corrigido alinhando à convenção dos demais DTOs Bloco3 (`DateTime`).
2. **Driver Tier-2 (artefato de teste, declarado)**: chamadas de frequência com data anterior à
   `data_matricula` da matrícula são rejeitadas pelo SQL canônico (`@DataAula between greatest(m.data_matricula,...)`
   e fim do ano letivo) — o fixture usava datas `hoje-1`/`hoje-2` enquanto a matrícula nasce em
   `current_date-1`; datas ajustadas para dentro da janela (hoje/+1/+2), comportamento do banco mantido.

## Resíduos declarados (regra 13)

- `validate-script-completop` falha em seed pré-existente `admin@sigov.local` (2 ocorrências já em
  HEAD; não introduzidas nesta sprint) — declarado, não mascarado.
- `rc50_25` permanece arquivo órfã do manifest (intocada, regra 8).
- `disciplinaId` do diário de classe continua id fictício (sem tabela `sigov.disciplina` com FK).
- A agregação de notas usa a média das avaliações lançadas (`avg`); política de média ponderada por
  `peso` ou reprovação por componente fica para a fatia de boletim definitivo (regras parametrizadas
  por esfera, regra 24) — nesta sprint o portal não inventa regra de aprovação.
