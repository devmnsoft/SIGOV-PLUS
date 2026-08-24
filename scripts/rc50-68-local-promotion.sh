#!/usr/bin/env bash
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS="$ROOT/artifacts/rc50-68-local-promotion"
LOG="$ARTIFACTS/promotion.log"
RESULT_JSON="$ARTIFACTS/result.json"
SUMMARY="$ARTIFACTS/summary.md"
CONFIRM=false
RUN_SMOKE=false
declare -a STEP_NAMES=() STEP_STATUS=() STEP_DETAILS=()

# A evidência é materializada também em saídas antecipadas. O fallback sem Python
# é deliberadamente mínimo, mas continua sendo JSON válido e nunca sugere PASS.
finalize() {
  local incoming="$?" overall=PASS promotion='PROMOVÍVEL LOCALMENTE'
  trap - EXIT
  for s in "${STEP_STATUS[@]}"; do [[ "$s" == FAIL ]] && overall=FAIL; done
  if [[ "$overall" != FAIL ]]; then
    for s in "${STEP_STATUS[@]}"; do [[ "$s" == BLOCKED ]] && overall=BLOCKED; done
  fi
  if ((${#STEP_STATUS[@]} == 0)); then overall=BLOCKED; fi
  [[ "$overall" != PASS ]] && promotion=BLOCKED
  export RESULT_JSON SUMMARY SHA DOTNET_VERSION PSQL_VERSION NODE_VERSION overall promotion
  if command -v python3 >/dev/null 2>&1; then
    python3 - "${STEP_NAMES[@]}" -- "${STEP_STATUS[@]}" -- "${STEP_DETAILS[@]}" <<'PY'
import datetime,json,os,sys
a=sys.argv[1:]; i=a.index('--'); j=a.index('--',i+1); names=a[:i]; statuses=a[i+1:j]; details=a[j+1:]
d={'release':'RC50.68E-R6','status':os.environ['overall'],'promotion':os.environ['promotion'],'validatedSha':os.environ.get('SHA','desconhecido'),'generatedAtLocal':datetime.datetime.now().astimezone().isoformat(),'versions':{'dotnet':os.environ.get('DOTNET_VERSION','indisponível'),'psql':os.environ.get('PSQL_VERSION','indisponível'),'node':os.environ.get('NODE_VERSION','indisponível')},'steps':[{'name':n,'status':s,'detail':detail} for n,s,detail in zip(names,statuses,details)]}
with open(os.environ['RESULT_JSON'],'w',encoding='utf-8') as f: json.dump(d,f,ensure_ascii=False,indent=2); f.write('\n')
with open(os.environ['SUMMARY'],'w',encoding='utf-8') as f:
 f.write('# Evidência local RC50.68\n\n- Resultado da execução: **%s**\n- Decisão local: **%s**\n- CI oficial: **não executado; gate distinto**\n- SHA validado: `%s`\n- Data/hora local: `%s`\n\n| Etapa | Status | Detalhe |\n|---|---|---|\n'%(d['status'],d['promotion'],d['validatedSha'],d['generatedAtLocal']))
 for x in d['steps']: f.write('| %s | **%s** | %s |\n'%(x['name'],x['status'],x['detail'].replace('|','/').replace('\n',' ')))
PY
  else
    printf '{"release":"RC50.68E-R6","status":"BLOCKED","promotion":"BLOCKED","validatedSha":"desconhecido","versions":{"dotnet":"indisponível","psql":"indisponível","node":"indisponível"},"steps":[{"name":"evidence-writer","status":"BLOCKED","detail":"python3 ausente; detalhes disponíveis somente no log sanitizado"}]}\n' >"$RESULT_JSON"
    printf '# Evidência local RC50.68\n\n- Resultado: **BLOCKED**\n- Motivo: `python3` ausente; foi emitido JSON mínimo válido.\n' >"$SUMMARY"
    overall=BLOCKED
  fi
  chmod 600 "$LOG" "$RESULT_JSON" "$SUMMARY" 2>/dev/null || true
  printf '%s %s\n' "$(date '+%Y-%m-%dT%H:%M:%S%z')" "Evidências concluídas (resultado=$overall)" | tee -a "$LOG"
  [[ "$overall" == FAIL ]] && exit 1
  [[ "$overall" == BLOCKED ]] && exit 2
  [[ "$incoming" -ne 0 ]] && exit "$incoming"
  exit 0
}

usage() {
  cat <<'EOF'
Uso: scripts/rc50-68-local-promotion.sh --confirm [--smoke]

Variáveis obrigatórias: SIGOV_DB_HOST, SIGOV_DB_PORT, SIGOV_DB_NAME,
SIGOV_DB_USER e SIGOV_DB_PASSWORD. A confirmação autoriza somente aplicar/reaplicar
script_completop.sql no banco informado; este script nunca remove o banco.
--smoke inicia Web/API temporariamente e consulta health e a rota protegida.
EOF
}
mkdir -p "$ARTIFACTS"
: >"$LOG"
chmod 700 "$ARTIFACTS" 2>/dev/null || true
SHA="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null || printf desconhecido)"
DOTNET_VERSION="indisponível"; PSQL_VERSION="indisponível"; NODE_VERSION="indisponível"
trap finalize EXIT
sanitize() {
  sed -E \
    -e 's#(Password|Pwd|PGPASSWORD|SIGOV_DB_PASSWORD|Authorization|Cookie|Set-Cookie)[=:][^;[:space:]]+#\1=***#gI' \
    -e 's#(Authorization:[[:space:]]*Bearer)[[:space:]]+[^[:space:]]+#\1 ***#gI' \
    -e 's#(Host|Server|Username|User ID|User Id)[=:][^;[:space:]]+#\1=***#gI' \
    -e 's#postgres(ql)?://[^/@[:space:]]+(:[^/@[:space:]]+)?@#postgres://***:***@#gI'
}
log() { printf '%s %s\n' "$(date '+%Y-%m-%dT%H:%M:%S%z')" "$*" | sanitize | tee -a "$LOG"; }
step() { STEP_NAMES+=("$1"); STEP_STATUS+=("$2"); STEP_DETAILS+=("$3"); log "[$2] $1 — $3"; }
have() { command -v "$1" >/dev/null 2>&1; }
run_logged() {
  log "Executando: $1"
  shift
  "$@" > >(sanitize >>"$LOG") 2> >(sanitize >>"$LOG")
}

for arg in "$@"; do
  case "$arg" in
    --confirm) CONFIRM=true ;;
    --smoke) RUN_SMOKE=true ;;
    -h|--help) usage; step invocation BLOCKED 'Ajuda solicitada; homologação não executada'; exit 2 ;;
    *) step invocation BLOCKED "Argumento inválido: $arg"; usage; exit 2 ;;
  esac
done

# Preflight de arquivos e ferramentas não consulta GitHub Actions nem secrets de CI.
missing_files=()
for file in script_completop.sql database/postgres/migrations/manifest.json sigov.runtime.slnf; do
  [[ -f "$ROOT/$file" ]] || missing_files+=("$file")
done
if ((${#missing_files[@]})); then step preflight-files FAIL "Ausentes: ${missing_files[*]}"; else step preflight-files PASS "Entradas obrigatórias presentes"; fi

if have dotnet; then
  DOTNET_VERSION="$(dotnet --version 2>/dev/null || printf erro)"
  if run_logged 'dotnet --info' dotnet --info; then step preflight-dotnet PASS ".NET $DOTNET_VERSION"; else step preflight-dotnet FAIL 'dotnet --info falhou'; fi
else step preflight-dotnet BLOCKED 'dotnet não encontrado'; fi
if have psql; then
  PSQL_VERSION="$(psql --version 2>/dev/null || printf erro)"
  if run_logged 'psql --version' psql --version; then step preflight-psql PASS "$PSQL_VERSION"; else step preflight-psql FAIL 'psql --version falhou'; fi
else step preflight-psql BLOCKED 'psql não encontrado'; fi
if have node; then
  NODE_VERSION="$(node --version 2>/dev/null || printf erro)"
  if run_logged 'node --version' node --version; then step preflight-node PASS "$NODE_VERSION"; else step preflight-node FAIL 'node --version falhou'; fi
else step preflight-node BLOCKED 'node ausente; validação JavaScript indisponível'; fi

required=(SIGOV_DB_HOST SIGOV_DB_PORT SIGOV_DB_NAME SIGOV_DB_USER SIGOV_DB_PASSWORD)
missing_vars=(); for var in "${required[@]}"; do [[ -n "${!var:-}" ]] || missing_vars+=("$var"); done
if ((${#missing_vars[@]})); then
  step connection-preflight BLOCKED "Variáveis ausentes: ${missing_vars[*]}"
  DB_SAFE=false
elif [[ "${SIGOV_DB_HOST,,}" =~ prod(uction)? ]]; then
  step connection-preflight FAIL 'Host recusado: o destino parece ser de produção'
  DB_SAFE=false
elif [[ ! "${SIGOV_DB_NAME,,}" =~ (rc50|homolog|local|dev|test) ]]; then
  step connection-preflight FAIL 'Nome do banco recusado: deve conter rc50, homolog, local, dev ou test'
  DB_SAFE=false
elif [[ "${ASPNETCORE_ENVIRONMENT:-}" =~ ^[Pp]roduction$ || "${SIGOV_ENVIRONMENT:-}" =~ ^[Pp]roduction$ ]]; then
  step connection-preflight FAIL 'Ambiente Production recusado'
  DB_SAFE=false
elif ! $CONFIRM; then
  step connection-preflight BLOCKED 'Use --confirm após conferir o destino mascarado; nenhuma alteração foi feita'
  DB_SAFE=false
else
  log 'Destino confirmado: host=*** port='"$SIGOV_DB_PORT"' database='"$SIGOV_DB_NAME"' user=*** (senha omitida)'
  step connection-preflight PASS 'Banco local/homologação confirmado; destino sensível mascarado'
  DB_SAFE=true
fi

# Validações estáticas continuam mesmo quando runtime ou banco estão bloqueados.
if run_logged 'git diff --check' git -C "$ROOT" diff --check; then step git-diff-check PASS 'Sem erros de whitespace'; else step git-diff-check FAIL 'git diff --check falhou'; fi
if have python3 && python3 -m json.tool "$ROOT/database/postgres/migrations/manifest.json" >/dev/null 2>>"$LOG"; then
  step manifest-json PASS 'manifest.json é JSON válido'
  if ROOT="$ROOT" python3 - <<'PY' >>"$LOG" 2>&1
import hashlib,json,os,sys
r=os.environ['ROOT']; m=json.load(open(r+'/database/postgres/migrations/manifest.json',encoding='utf-8'))
bad=[]
for x in m['migrations']:
 p=r+'/database/postgres/migrations/'+x['file']
 if not os.path.isfile(p) or hashlib.sha256(open(p,'rb').read()).hexdigest()!=x['checksum']: bad.append(x['version'])
if bad: print('checksums divergentes: '+', '.join(bad)); sys.exit(1)
print('checksums do manifest conferidos')
PY
  then step manifest-checksums PASS 'Arquivos das migrations conferem com o manifest'; else step manifest-checksums FAIL 'Checksum/arquivo divergente no manifest'; fi
else step manifest-json FAIL 'python3 ausente ou manifest inválido'; step manifest-checksums BLOCKED 'Parse do manifest não concluído'; fi
if have node; then
  if run_logged 'node --check src/Sigov.Web/wwwroot/js/saas-authorization-admin.js' node --check "$ROOT/src/Sigov.Web/wwwroot/js/saas-authorization-admin.js"; then step javascript PASS 'JavaScript válido'; else step javascript FAIL 'node --check falhou'; fi
fi
if run_logged 'bash -n scripts/rc50-68-local-promotion.sh' bash -n "$ROOT/scripts/rc50-68-local-promotion.sh"; then step shell-syntax PASS 'Script Linux válido'; else step shell-syntax FAIL 'bash -n falhou'; fi
if have ruby; then
  if run_logged 'Ruby/Psych parse .github/workflows/*.yml' ruby -e 'require "yaml"; ARGV.each { |p| YAML.load_file(p, aliases: true) }' "$ROOT"/.github/workflows/*.yml; then step workflow-yaml PASS 'Workflows parseados com Ruby/Psych'; else step workflow-yaml FAIL 'YAML inválido'; fi
elif have actionlint; then
  if run_logged 'actionlint .github/workflows/*.yml' actionlint "$ROOT"/.github/workflows/*.yml; then step workflow-yaml PASS 'Workflows validados por actionlint'; else step workflow-yaml FAIL 'actionlint falhou'; fi
else step workflow-yaml BLOCKED 'Ruby/actionlint ausentes'; fi

if have dotnet; then
  if run_logged 'dotnet clean sigov.runtime.slnf' dotnet clean "$ROOT/sigov.runtime.slnf" &&
     run_logged 'dotnet restore sigov.runtime.slnf --locked-mode' dotnet restore "$ROOT/sigov.runtime.slnf" --locked-mode &&
     run_logged 'dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror' dotnet build "$ROOT/sigov.runtime.slnf" --configuration Release --no-restore --nologo -warnaserror; then
    step runtime-build PASS 'Clean, restore locked e build Release concluídos'
  else step runtime-build FAIL 'Build runtime falhou; consulte o log sanitizado'; fi
else step runtime-build BLOCKED '.NET indisponível'; fi

psql_sigov() { PGPASSWORD="$SIGOV_DB_PASSWORD" PGHOST="$SIGOV_DB_HOST" PGPORT="$SIGOV_DB_PORT" PGDATABASE="$SIGOV_DB_NAME" PGUSER="$SIGOV_DB_USER" psql -X -v ON_ERROR_STOP=1 "$@"; }
if $DB_SAFE && have psql; then
  server_num="$(psql_sigov -Atqc 'show server_version_num' 2>>"$LOG" || true)"
  if [[ "$server_num" =~ ^[0-9]+$ ]] && ((server_num >= 160000 && server_num < 170000)); then
    step postgres-version PASS "PostgreSQL 16 confirmado (server_version_num=$server_num)"
    if run_logged 'psql ON_ERROR_STOP=1 -f script_completop.sql (1ª aplicação)' psql_sigov -f "$ROOT/script_completop.sql"; then step baseline-apply PASS 'Baseline aplicado'; else step baseline-apply FAIL 'Primeira aplicação falhou'; fi
    if run_logged 'psql ON_ERROR_STOP=1 -f script_completop.sql (reexecução)' psql_sigov -f "$ROOT/script_completop.sql"; then step baseline-reapply PASS 'Reexecução idempotente concluída'; else step baseline-reapply FAIL 'Reexecução falhou'; fi
    read -r -d '' VALIDATE_SQL <<'SQL' || true
do $$
declare n integer; missing text;
begin
 if not exists(select 1 from information_schema.schemata where schema_name='sigov') then raise exception 'schema sigov ausente'; end if;
 if to_regclass('sigov.schema_migrations') is null then raise exception 'schema_migrations ausente'; end if;
 select string_agg(x, ', ') into missing from unnest(array['perfil_acesso','grupo_acesso','permissao','usuario_grupo','grupo_perfil','perfil_permissao','autorizacao_decisao_auditoria','autorizacao_admin_auditoria']) x where to_regclass('sigov.'||x) is null;
 if missing is not null then raise exception 'tabelas ausentes: %',missing; end if;
 select count(*) into n from sigov.permissao where chave='saas.superadmin.autorizacao.administrar' and ativo and not is_deleted; if n<>1 then raise exception 'permissão administrar: esperado 1, atual %',n; end if;
 select count(*) into n from sigov.permissao where chave in ('saas.superadmin.dashboard.visualizar','saas.superadmin.dashboard.exportar') and ativo and not is_deleted; if n<>2 then raise exception 'permissões dashboard: esperado 2, atual %',n; end if;
 select count(*) into n from sigov.permissao where not is_deleted group by chave having count(*)>1 limit 1; if n is not null then raise exception 'permissões duplicadas'; end if;
 select count(*) into n from sigov.perfil_acesso where not is_deleted group by codigo_externo having count(*)>1 limit 1; if n is not null then raise exception 'perfis duplicados'; end if;
 select count(*) into n from sigov.grupo_acesso where not is_deleted group by tenant_id,codigo having count(*)>1 limit 1; if n is not null then raise exception 'grupos duplicados'; end if;
end $$;
SQL
    if printf '%s\n' "$VALIDATE_SQL" | psql_sigov >>"$LOG" 2>&1; then step database-authority PASS 'Schema, ledger, tabelas, permissões e ausência de duplicatas validados'; else step database-authority FAIL 'Asserções persistentes falharam'; fi
    if ROOT="$ROOT" python3 - <<'PY' | psql_sigov >>"$LOG" 2>&1
import json,os
m=json.load(open(os.environ['ROOT']+'/database/postgres/migrations/manifest.json'))
v=[(x['version'],x['checksum']) for x in m['migrations'] if x.get('includeInBaseline')]
vals=','.join("('%s','%s')"%(a.replace("'","''"),b.replace("'","''")) for a,b in v)
print("do $$ declare bad text; begin select string_agg(v.version, ', ') into bad from (values %s) v(version,checksum) left join sigov.schema_migrations sm on sm.version=v.version and sm.success where sm.version is null or sm.checksum<>v.checksum; if bad is not null then raise exception 'ledger/manifest divergente: %%',bad; end if; end $$;"%vals)
PY
    then step ledger-manifest PASS 'Ledger corresponde ao manifest para migrations do baseline'; else step ledger-manifest FAIL 'Ledger/checksum diverge do manifest'; fi
  else step postgres-version FAIL 'É obrigatório servidor PostgreSQL 16.x'; step baseline-apply BLOCKED 'Versão do servidor recusada'; step baseline-reapply BLOCKED 'Versão do servidor recusada'; step database-authority BLOCKED 'Baseline não validado'; step ledger-manifest BLOCKED 'Baseline não validado'; fi
else
  step postgres-version BLOCKED 'Conexão segura não confirmada ou psql ausente'
  step baseline-apply BLOCKED 'Banco não disponível com confirmação explícita'
  step baseline-reapply BLOCKED 'Banco não disponível com confirmação explícita'
  step database-authority BLOCKED 'Banco não disponível'
  step ledger-manifest BLOCKED 'Banco não disponível'
fi

if $RUN_SMOKE && $DB_SAFE && have dotnet && have curl; then
  export ConnectionStrings__DefaultConnection="Host=$SIGOV_DB_HOST;Port=$SIGOV_DB_PORT;Database=$SIGOV_DB_NAME;Username=$SIGOV_DB_USER;Password=$SIGOV_DB_PASSWORD"
  export ASPNETCORE_ENVIRONMENT=Local SIGOV_RUN_MIGRATIONS=false SIGOV_MIGRATION_MODE=Validate
  api_url="${SIGOV_API_URL:-http://localhost:5001}"; web_url="${SIGOV_WEB_URL:-http://localhost:5000}"
  ASPNETCORE_URLS="$api_url" dotnet run --project "$ROOT/src/Sigov.Api/Sigov.Api.csproj" --no-launch-profile --no-build --configuration Release > >(sanitize >>"$LOG") 2> >(sanitize >>"$LOG") & api_pid=$!
  ASPNETCORE_URLS="$web_url" dotnet run --project "$ROOT/src/Sigov.Web/Sigov.Web.csproj" --no-launch-profile --no-build --configuration Release > >(sanitize >>"$LOG") 2> >(sanitize >>"$LOG") & web_pid=$!
  trap 'kill ${api_pid:-} ${web_pid:-} 2>/dev/null || true' EXIT
  ready=false; for _ in {1..30}; do if curl -fsS "$api_url/api/health" >/dev/null 2>>"$LOG" || curl -fsS "$api_url/health" >/dev/null 2>>"$LOG"; then ready=true; break; fi; sleep 2; done
  if $ready; then step smoke-health PASS 'API health respondeu'; else step smoke-health FAIL 'Health não respondeu'; fi
  code="$(curl -sS -o /dev/null -w '%{http_code}' "$web_url/SaasAdmin/Autorizacao" 2>>"$LOG" || printf 000)"
  if [[ "$code" =~ ^(302|401|403)$ ]]; then step smoke-unauthenticated PASS "Rota protegida recusou acesso anônimo ($code)"; else step smoke-unauthenticated FAIL "Resposta anônima inesperada ($code)"; fi
  if [[ -n "${SIGOV_LOCAL_AUTH_COOKIE:-}" ]]; then
    code="$(curl -sS -o /dev/null -w '%{http_code}' -H "Cookie: $SIGOV_LOCAL_AUTH_COOKIE" "$web_url/SaasAdmin/Autorizacao" 2>/dev/null || printf 000)"
    if [[ "$code" != 500 && "$code" =~ ^2 ]]; then step smoke-authenticated PASS "Tela autenticada respondeu $code (cookie omitido)"; else step smoke-authenticated FAIL "Tela autenticada respondeu $code (cookie omitido)"; fi
  else step smoke-authenticated BLOCKED 'SIGOV_LOCAL_AUTH_COOKIE não fornecido; nenhum PASS autenticado foi inferido'; fi
  kill "$api_pid" "$web_pid" 2>/dev/null || true; wait "$api_pid" "$web_pid" 2>/dev/null || true
else
  reason='use --smoke com banco confirmado, dotnet e curl'
  $RUN_SMOKE && reason='pré-requisito de smoke indisponível'
  step smoke-health BLOCKED "$reason"; step smoke-unauthenticated BLOCKED "$reason"; step smoke-authenticated BLOCKED 'Credencial local segura e smoke preparado são obrigatórios'
fi

exit 0
