# Relatório Ciclo 0 — Auditoria do runner de migrations (`apply-migrations.sh`) e da sonda EVO‑C

Data: 2026‑10‑10 · Worktree `swift-circuit` · HEAD `f6e4aa4c` (merge de `origin/main`=`aaec9515` "novos" + linha local P0/Gate B) · Auditoria estática completa + sondas de banco em `p1_dev`.

Escopo do Ciclo 0 (plano mestre): auditar `database/docker/apply-migrations.sh` (leitura estruturada do manifest, locks sidecar↔runner, escopo/duração dos locks, recuperação, pré/pós‑condições, consistência de ledgers, atomicidade por migration, reexecução/concorrência); investigar a lógica de encerramento de sessões antigas; e dar disposição à sonda `99999999999999_evo_c_probe_aplicada.sql`. Sem alterar migrations publicadas, checksums ou registros de sucesso.

## 1. Reescrita RC‑EVO‑C §3.1 (entregue em `aaec9515`) — mecanismos verificados

### 1.1 Bloqueio dual (fase + DDL)
- **Trava de fase (gate de sessão):** chave própria do sidecar `GATE_LOCK = x'5349474F564D4947'` ("SIGOVMIG"), distinta da chave do runner para não deadlocks contra si mesma (linhas 28–31). Uma **sessão psql persistente** toma `pg_advisory_lock(GATE_LOCK)` e segura `pg_sleep(36000)`; um segundo sidecar/compose‑restart espera no loop de porta (linhas 69+) e, ao assumir, **reconverge pelos ledgers sem reaplicar nada**.
- **Guarda de DDL (transacional):** chave estável `MIGRATION_LOCK = x'5349474F56504C55'` ("SIGOVPLUS") tomada como `pg_advisory_xact_lock` **dentro da transação de cada entrada** (linha 534): sidecar e ManifestRunner de startup nunca executam DDL ao mesmo tempo; o último espera o commit do outro e reconverge pelos ledgers.
- O nome da sessão vai por `set_config` dentro da sessão (psql moderno envia `application_name` no startup packet com prioridade sobre `PGOPTIONS`) — linha 51–53.

### 1.2 Sessão do gate e limpeza
- `application_name` exclusivo por execução: `sigov-docker-migrations-gate-$(date +%s)-$$-${RANDOM}` (linha 43).
- Encerramento pelo **servidor** (`pg_terminate_backend` por `application_name`), não por PID de SO: comentário nas linhas 40–42 registra que matar por PID já causou crash recovery por colisão/reaproveitamento de PID.
- `cleanup_gate` em `trap EXIT` com **dupla passada** (linhas 56–67): termina o portal estabelecido, recolhe o cliente, e termina de novo um portal que tenha completado a toma de lock entre as etapas (backend em `pg_sleep` não percebe a morte do cliente e seguraria a trava até o fim do sono). Se o container morre, a sessão cai e a trava vai junto (sem lock órfão).

### 1.3 Porteiro (janitor) — achado C0‑R1 (risco latente, mudança adiado)
Linha 49, executado antes de cada espera por lock:

```sql
select pg_terminate_backend(pid) from pg_stat_activity
where datname = current_database()
  and (application_name like 'sigov-docker-migrations-gate-%'
       or (query like 'select pg_advisory_lock%' and query like '%pg_sleep(36000)%'))
  and pid <> pg_backend_pid()
  and coalesce(xact_start, query_start) < now() - interval '45 minutes';
```

- Varre gates com **idade > 45 min** antes de esperar, para o lock não ficar sequestrado por gate órfão; o padrão secundário pega orfãos antigos que nasceram antes do `application_name` dedicado.
- **Risco:** a idade é o único critério além do fingerprint. O comentário (linhas 45–46) assume que "uma execução completa não passa de poucos minutos". Com manifest de **220 entradas** (`statement_timeout` local de **30 min por entrada**, linha 543), uma execução legítima após ausência longa (cenário clean‑install de 220 entradas ainda não aplicadas) pode ultrapassar 45 min **em total**. Nesse caso, um sidecar que iniciar em paralelo varre o gate da execução viva → a garantia de *fase* é enfraquecida (a execução continua entre transações enquanto o segundo sidecar já aplica entradas).
- **Mitigação existente:** o DDL de cada entrada continua serializado pela `pg_advisory_xact_lock` (1.1) e a convergência final é pelos ledgers com prova de pós‑condições — ou seja, o efeito real provável é *duas execuções intercalando entradas*, não DDL duplicado. A entrada em andamento quando o gate é varrido não sofre efeito na própria transação (o janitor mata a **sessão do portal**, não backends de entrada); o lock de fase só é o que se perde.
- **Severidade:** latente / baixa probabilidade (exige > 45 min de execução + novo sidecar no mesmo banco nesse intervalo). **Disposição nesta entrega:** registrado como achado C0‑R1; a mudança de política do janitor (ex.: excluir gates com progresso ativo, ou elevar limiar, ou marcarear o gate com "entrada em curso") fica **adiada** e exigirá RC própria com validação em banco descartável antes de tocar o script publicado (regra 8: não alterar artefato publicado; acrescentar correção validada).

## 2. Atomicidade por entrada e decisões nomeadas (linhas 530–590)
Cada migration vira um único script psql com exatamente **uma transação**:
```
\set ON_ERROR_STOP on
begin;
select pg_advisory_xact_lock(MIGRATION_LOCK);
select sigov.mig_conferir_checksums(:'v', string_to_array(:'p',' '));  -- checksum atual + knownChecksums (base64→lista)
set local statement_timeout = '30min';
<decisão: aplicar ou reconciliar/replicar>
-- DDL da migration entra CRU dentro da transação (sem meta‑comandos psql, sem \i — o acervo não os usa)
select sigov.mig_provar_poscondicoes(:'v');                            -- prova obrigatória ANTES de registrar sucesso
insert into sigov.docker_schema_migrations ... on conflict (version) do nothing;   -- ledger do sidecar
insert into sigov.schema_migrations ... on conflict (version) do nothing;          -- ledger canônico do app
commit;
select :'resultado' || ' ' || :'v';
```
- **Decisões (linhas 642–659):** `APLICADA` (nova; ledgers escritos na mesma transação pós‑prova), `RECONCILIADA` (consta só no ledger do sidecar → registro canônico escrito após prova de pós‑condições; "linha no ledger do sidecar por si só não basta"), `REPLICADA` (consta só no canônico → sidecar replica; startup‑first), `PULADA` (em ambos → nunca reaplica). Resultado inesperado → `FALHA_DECISAO_MIGRATION`.
- **Pré‑condições:** versão fora do vocabulário `[0‑9A‑zA._-]` → `MANIFEST_INVALIDO`; arquivo ilegível → `FALHA_CHECKSUM_ARQUIVO`; checksum divergente do publicado → `DIVERGENCIA_CHECKSUM_ARQUIVO` (nenhuma prossegue sem erro nomeado, `exit 1`).
- **Pós‑condições:** `sigov.mig_provar_poscondicoes(:'v')` é chamado tanto no caminho de aplicação quanto no de reconciliação — sucesso novo no ledger canônico **exige** a prova.
- **Reexecução:** idempotente por construção (`on conflict (version) do nothing` nos dois ledgers + decisão prévia por existência em qualquer ledger). Concorrência: serializada nas duas camadas (gate de fase + xact‑lock de DDL).

## 3. Disposição da sonda `99999999999999_evo_c_probe_aplicada.sql`
- **O que é:** cria `sigov.evo_c_probe_aplicada` (`id int PK`, `criado_em timestamptz`) e faz `insert … id=1 … on conflict do nothing` — um **sentinela de validação inerte** do ciclo RC‑EVO‑C (prova de que uma aplicação manual do script EVO‑C correu num banco específico).
- **Membro do manifest?** Não. `manifest.json` tem **220 entradas** `"version"` (222 402 bytes / 2945 linhas); há **230 `.sql`** em `database/postgres/migrations/` — os 10 arquivos fora do manifest são ignorados pelo sidecar, que itera sobre o TSV estagiado derivado do manifest (não sobre a listagem do diretório). O runner pula explicitamente o que não consta.
- **Ordenação:** a maior versão real do manifest é `20261008130000`; o nome da sonda `99999999999999…` ordenaria acima dela **se** estivesse no manifest — não está, portanto nunca entra no staging nem no ledger. O sidecar ignora também `includeInBaseline` e `dependencies` e ordena pura e simplesmente por versão.
- **Limpeza:** **não existe DROP/remoção em lugar nenhum** do repositório.
- **Consequência:** numa instalação/upgrade limpa pelo caminho oficial (manifest/sidecar + `script_completo*.sql`), a tabela `sigov.evo_c_probe_aplicada` **não** aparece — a sonda não vaza para o schema corrente. Resíduo apenas em bancos onde a validação EVO‑C foi aplicada manualmente (órfã inerte, sem FK/dependentes).
- **Recomendação (não aplicada nesta entrega):** (a) migration corretiva de drop + entrada de manifest (regra 8), ou (b) remoção do arquivo órfão do acervo com nota de que o sentinela vive apenas nos bancos de validação EVO‑C. Decisão fica com o dono da RC‑EVO‑C; registrada aqui como follow‑up C0‑F1.

## 4. Fatos do manifest (inventário desta auditoria)
- 220 entradas `version`; raiz com `compatibilityAfterAll` → `850_post_migration_compatibility.sql`.
- Campos opcionais por entrada presentes no formato: `knownChecksums[]`, `postConditionSql`, `postConditionProbes[]`, `applyAutomatically`, `includeInBaseline`, `dependencies[]` (estes dois últimos **ignorados** pelo sidecar docker — ordem apenas por versão).
- Caminhos sincronizados (regra 7) conferidos estáticamente: `manifest.json` ↔ `script_completo.sql` ↔ `script_completo_dev.sql` ↔ `database/script_completo.sql` ↔ `script_completop.sql` presentes e coerentes no HEAD `f6e4aa4c` (drift funcional será revalidado na jornada de instalação limpa — ver limite L1).

## 5. Limites declarados desta auditoria (regra 13/18 — nada mascarado)
- **L1 — ciclos de vida completos ainda não executados por mim neste turno:** install limpo, upgrade, re‑aplicação e recuperação foram **auditados estáticamente** (mecanismos acima) mas o Ciclo 0 prevê executá‑los num banco descartável; isso é item de próximo sprint, não evidência já produzida.
- **L2 — janitor (C0‑R1):** comportamento lido e compreendido; nenhuma mudança feita.
- **L3 — sonda (C0‑F1):** disposição recomendada, ainda sem decisão/execução.

## 6. Notas operacionais de ambiente (para as próximas validações)
- Host sem `psql`; acesso via `docker exec sigov-postgres psql`.
- **Quirk descoberto neste turno:** o `psql` exposto pelo container **ignora argumento SQL posicional** (`psql … "select 1"` → "extra command-line argument ignored", saída vazia). Formas que funcionam: `-c "…"` (sentença única), stdin (`Get-Content f | docker exec -i … psql …`), ou `docker cp` + `-f /tmp/x.sql` (padrão Gate B). Usar `-Atq` para saída tabular sem cabeçalho.
- Porta 5432 do host sombreada por Postgres nativo do Windows → forwarder `socat` `p1fwd` 5433→`sigov-postgres:5432` (verificado ativo neste turno: PIDs 13996/51164).