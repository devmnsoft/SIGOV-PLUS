# RELATORIO P0.1 / Gate A item 6 — Fluxo oficial do runner de migrations reconciliando ledgers em PostgreSQL 16

Data: 2026-10-09 (UTC)
Base commit: `6c7b269a` (HEAD).
Stack: PostgreSQL 16.14 (container `sigov-postgres`, imagem `postgres:16`, role `postgres`, `PGPASSWORD` local, `SIGOV_PG_HOST=localhost`).
Fluxo oficial executado: `database/docker/apply-migrations.sh` **corrente** (com a reconciliação bidirecional RC-EVO-B §6), copiado para o container via `docker cp "$PWD/database" sigov-postgres:/database` e invocado com `POSTGRES_USER=postgres POSTGRES_DB=<alvo> SIGOV_PG_HOST=localhost bash /database/docker/apply-migrations.sh` — exatamente o entrypoint do serviço `db-migrations` do compose.

## O QUE FECHA ESTE RELATÓRIO

O item 6 do gate RC50.68A (registrado como "BLOCKED: `ERROR: column 'version' does not exist`") apontava duas deficiências no fluxo oficial de boot do runner (`db-migrations`):
1. Incompatibilidade entre o DDL legado do ledger `sigov.docker_schema_migrations` (só `name`) e as colunas `version/file_path/checksum` usadas pelo runner.
2. Após upgrade legado, o sidecar registrava as novas versões **somente** no seu próprio ledger, deixando o ledger canônico do app (`sigov.schema_migrations`) para trás.

A correção vive em `database/docker/apply-migrations.sh`: normaliza o shape do ledger (alter ... add column if not exists version/file_path/checksum; unique index em version), replica as versões do canônico para o sidecar e, na reconciliação por migration, **grava também no ledger canônico** (`source='docker-sidecar'`) com verificação fail-closed de checksum (`DIVERGENCIA_LEDGER_*`, regra 13) e guarda final lado-a-lado (`DIVERGENCIA_LEDGER_LADO_A_LADO`). Este relatório prova ao vivo, em PostgreSQL 16, que o fluxo converge em instalação limpa **e** upgrade legado.

## RESULTADO GLOBAL

| Cena | Exit | Canônico final | Sidecar final | Divergências l↔l | Canônico ⊄ sidecar | Veredito |
|---|---|---|---|---|---|---|
| A — instalação limpa | **0** | 216 / última `20261008130000` | 217 (=216 + marker) | **0** | **0** | **PASS** |
| B — upgrade legado (`7ece53c1`) | **0** | 212→**216** / última `20261008130000` | 217 | **0** | **0** | **PASS** |

Única diferença estrutural entre os dois ledgers: o marcador de baseline `00000000000000_script_completo_baseline` (presente apenas no sidecar, `version NULL`) — por design, representa que o volume novo rodou o script completo; o fluxo final não o julga.

## FASE A — INSTALAÇÃO LIMPA (`evo_c_clean`, banco vazio)

1. `CREATE DATABASE evo_c_clean;`
2. `docker cp "$PWD/database" sigov-postgres:/database`
3. `bash /database/docker/apply-migrations.sh` → **exit 0**; fim: "Migrations aplicadas com sucesso; ledgers do sidecar e do app reconciliados."
4. Fase 1 aplicou `/database/apply_all_required_migrations.sql` (que faz `\i /database/script_completo.sql` + cria `docker_schema_migrations` + insere o marker); fase 2 sincronizou o sidecar a partir do canônico e reconciliou versão a versão. Sem `column "version" does not exist`.

Medições finais: canônico 216 / última `20261008130000`; sidecar 217; divergências lado-a-lado **0**; versões no sidecar sem canônico **apenas o marker**; versões no canônico sem sidecar **0**.

## FASE B — UPGRADE LEGADO (`evo_c_legacy`, ponto `7ece53c1`)

1. Baseline legado extraído de `git show 7ece53c1:database/postgres/script_completo.sql` (~3,4 MB) aplicado com `-v ON_ERROR_STOP=1` → **canônico 212 / última `20261007110000`** (ponto legado idêntico ao usado no §11 RC-EVO-A).
2. Marker de baseline semeado no **shape legado** (`name text not null unique`, sem `version/file_path/checksum`) para forçar o caminho incremental (fase 1 pulada) e exercer a normalização do shape pelo §6.
3. `bash /database/docker/apply-migrations.sh` → **exit 0**; fase 1 pulada ("Baseline ja aplicado neste volume"); aplicadas exatamente as 4 pendentes `20261007120000`, `20261007130000`, `20261008120000`, `20261008130000`.

Asserção central do item 6 (antes: canônico ficava em 212 enquanto o sidecar recebia as novas) provada: após o upgrade, as 4 versões agora existem **no ledger canônico** com `source='docker-sidecar'` e `success=true`.

```
    version     |     source     | success
----------------+----------------+---------
 20261007120000 | docker-sidecar | t
 20261007130000 | docker-sidecar | t
 20261008120000 | docker-sidecar | t
 20261008130000 | docker-sidecar | t
```

Medições finais: canônico 212→**216** / última `20261008130000`; sidecar 217; divergências lado-a-lado **0**; canônico ⊆ sidecar (**0** ausentes).

## ESTADO FINAL DO AMBIENTE

- Bancos descartáveis `evo_c_clean` e `evo_c_legacy` destruídos com `DROP DATABASE ... WITH (FORCE)`; `select datname where datname like 'evo_c_%'` vazio (residuo zerado verificado).
- Nenhum banco dev/homologação foi tocado; operações restritas aos bancos descartáveis.

## ARTEFATOS (Temp\opencode\)

- `evo_c_faseA_clean.log` — saída completa do fluxo oficial na instalação limpa.
- `legacy_script_completo_7ece53c1.sql` — baseline legado extraído de `7ece53c1`.
- `evo_c_faseB_legacy_apply.log` — aplicação do baseline legado (ON_ERROR_STOP).
- `evo_c_faseB_upgrade.log` — saída completa do fluxo oficial no upgrade legado (4 pendentes + reconciliação).
