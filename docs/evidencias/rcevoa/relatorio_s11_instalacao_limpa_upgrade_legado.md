# RELATORIO §11 RC-EVO-A — Instalacao limpa + upgrade legado em PostgreSQL 16 oficial

Data: 2026-10-08 (UTC)
Base commit: fc2cd605 (HEAD). Correcao asociada nos 6 consolidados commitada junto deste relatorio.
Stack: PostgreSQL 16.14 (container `sigov-postgres`, compose oficial), role `postgres` (env do volume atual: POSTGRES_USER=postgres).
Bancos descartaveis: `sigov_evo_a_clean` e `sigov_evo_a_legacy` (criados e destruidos ao final; residuo zerado verificado).

## RESULTADO GLOBAL DO §11

- Instalacao limpa: **PASS**
- Reexecucao idempotente do consolidado: **PASS** (no-op explicito)
- Upgrade legado (sidecar `db-migrations`): **PASS**
- Convergecia de schema limpo == legado+upgrade: **PASS** (unica diferenca: ledger proprio do sidecar)
- Bug real capturado e corrigido: **SIM** (corpo da migration 20261007130000 nos 6 consolidados continha `c.consrc`, inexistente no PG16)

## FASE 1 — INSTALACAO LIMPA (`sigov_evo_a_clean`)

Comando: `docker cp database/postgres/script_completo.sql` + `psql -X -v ON_ERROR_STOP=1 -f` (EXIT_CODE=0).

| Medicao | Esperado | Obtido |
|---|---|---|
| Ledger canonico `sigov.schema_migrations` | manifest(218) - excluidas do baseline(4) = 214 | **214** |
| Ultima versao | 20261007130000 | **20261007130000** |
| Coluna `sigov.saas_plano.limite_modulos` | 1 | **1** |
| Check `ck_saas_plano_limites` cobrindo `limite_modulos` (`pg_get_constraintdef like`) | 1 | **1** |
| Reexecucao do consolidado | exit 0, no-op | **exit 0**, mensagem "Baseline canônico já registrado; nenhuma migration foi reaplicada.", ledger permanece 214 |

Excluidas do baseline no manifest@HEAD (4): `011`, `20260902000000`, `20260902010000`, `20260903130000`.

Logs: `Temp\opencode\s11_noop.log` e saida da execucao `s11_fase1.ps1` (NOTICEs finais das migrations RH/S3.3 no corpo do consolidado).

### Defeito P0 capturado pelo gate (e corrigido neste commit)

Os 6 consolidados continham o **corpo antigo** do bloco `20261007130000`, com `c.consrc like '%limite_modulos%'` no check pos-alter. `pg_constraint.consrc` nao existe no PG16: qualquer instalacao limpa a partir do consolidado abortaria no `DO $$ ... $$` da S3.3. Causa: o primeiro sync do consolidado rodou antes da correcao da migration publicada-in-place; a reexecucao anterior do sync propagou apenas o checksum (sha `b98f2b5c...`), nao o corpo.
Correcao: corpo do bloco substituido nos 6 consolidados por igualdade byte-a-byte com o arquivo em disco (`database/postgres/migrations/20261007130000_evo_a_s33_limite_modulos_plano.sql`, sha256 `b98f2b5cbcbc2cc272efdc072498ee3e8cbefcbd086456e388bdf9fcd126e998`). Verificadores: `s11_verify_bodies3.ps1` (todos os 6 conferem), `s11_final_check.ps1` (0 residuos de `c.consrc`, 1 ocorrencia canonica por arquivo). A execucao desta Fase 1 usou o consolidado ja corrigido.

## FASE 2 — UPGRADE LEGADO (`sigov_evo_a_legacy`)

Ponto legado escolhido: commit `7ece53c1` (§8, ultimo baseline antes de §10/§3.3). Baseline extraido com `git show 7ece53c1:database/postgres/script_completo.sql` (3.354.419 bytes) e aplicado com exit 0.

| Medicao | Esperado | Obtido |
|---|---|---|
| Ledger legado | manifest@7ece53c1(216) - excluidas(4) = 212 | **212** |
| Ultima versao legado | 20261007110000 | **20261007110000** |
| `limite_modulos` antes do upgrade | 0 | **0** |
| Migrations aplicadas pelo sidecar | exatamente `20261007120000` e `20261007130000` (diff real `git diff --name-status 7ece53c1..HEAD` confirma apenas essas 2 + manifest) | **exatamente essas 2**, demais 212 reportadas "ja aplicada" |
| Exit do sidecar `docker compose run --rm db-migrations` (POSTGRES_DB=sigov_evo_a_legacy) | 0 | **0** ("Migrations aplicadas com sucesso.") |
| `limite_modulos` depois | 1 | **1** |
| Check `ck_saas_plano_limites` com cobertura | 1 | **1** |
| NOTICEs das 2 migrations | presentes no log | presentes (RH 120000 seed ficticio tenant 5; S3.3 limite null=ilimitado) |

Log completo: `Temp\opencode\s11_upgrade.log`.

Nota de ledger (documentada, sem mascaramento): apos o upgrade o ledger **canonico** do app permaneceu 212/ultima=20261007110000; quem registrou as 2 novas versoes foi o ledger do sidecar `sigov.docker_schema_migrations` (215 linhas = 212 sincronizadas + 2 aplicadas + marker de baseline). Reconciliacao `docker_schema_migrations -> sigov.schema_migrations` no fluxo oficial do runner e uma lacuna de infra preexistente (ja registrada como item 6 BLOCKED no gate RC50.68A), anterior e fora do escopo desta RC. O caminho C# (MigrationRunner no startup) cobri essa reconciliacao no banco dev `postgres` (ledger canonico 214, ultima 20261007130000, `limite_modulos=1`).

## CONVERGECIA DE SCHEMA (limpo vs legado+upgrade)

`pg_dump --schema=sigov --schema-only` nos dois bancos (exit 0 nos dois) + `Compare-Object`:
- Diffs fora de `docker_schema_migrations` e ruido de dump (\restrict/linhas vazias): **apenas as 13 linhas do DDL da propria tabela do ledger do sidecar** (`name`, `applied_at`, `id`, sequencia), que por design so existe no ambiente do sidecar.
- **Zero divergencias** em tabelas, colunas, constraints, indices ou grants do dominio. Veredito: **SCHEMA_CONVERGED** — upgrade incremental do legado converge para a mesma autoridade do consolidado limpo.

Dumps: `Temp\opencode\s11_clean_schema.sql`, `Temp\opencode\s11_legacy_schema.sql`. Comparadores: `s11_diff.ps1`, `s11_close.ps1`.

## ESTADO FINAL DO AMBIENTE

- Bancos descartaveis destruidos com `drop database ... with (force)`; `select datname ... like 'sigov_evo_a%'` vazio (residuo zerado).
- Banco dev canonico (`postgres`, usado pela api/web/worker) intacto durante todo o gate: ledger=214, ultima=20261007130000, `limite_modulos=1`; containers `sigov-api`/`sigov-web` healthy, `sigov-worker`/`sigov-postgres` Up.

## ARTEFATOS (Temp\opencode\)

- `s11_fase1.ps1` / `s11_fase2.ps1` — scripts executaveis das duas fases
- `s11_legacy_baseline.sql` — baseline legado extraido de `7ece53c1`
- `s11_upgrade.log` — saida completa do sidecar
- `s11_clean_schema.sql` / `s11_legacy_schema.sql` — dumps schema-only
- `s11_diff.ps1`, `s11_close.ps1` — comparacao e teardown
- `s11_fix_block.ps1`, `s11_verify_bodies3.ps1`, `s11_final_check.ps1` — correcao e verificacao byte-a-byte dos 6 consolidados
- `s11_manifest_check.ps1` — contagens do manifest HEAD vs legado (218-4 / 216-4)
