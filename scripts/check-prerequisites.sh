#!/usr/bin/env bash
set -u

# Diagnóstico somente leitura: não instala ferramentas nem revela segredos.
status=0
report() { printf '%-16s %-16s %s\n' "$1" "$2" "$3"; }
missing() { report "$1" MISSING_TOOL "$2"; status=1; }

report OS OK "$(uname -srm 2>/dev/null || printf unknown)"
for tool in dotnet pwsh psql; do
  if command -v "$tool" >/dev/null 2>&1; then report "$tool" OK "$(command -v "$tool")"; else missing "$tool" 'não encontrado no PATH'; fi
done

if command -v node >/dev/null 2>&1; then report node OK "$(node --version)"; else missing node 'necessário para as verificações JavaScript'; fi
if command -v psql >/dev/null 2>&1; then
  pg_major="$(psql --version | sed -E 's/.* ([0-9]+).*/\1/')"
  if [[ "$pg_major" == 16 ]]; then report PostgreSQL OK "major $pg_major"; else report PostgreSQL INVALID_VERSION "esperado 16; encontrado $pg_major"; status=1; fi
fi

if [[ -n "${SIGOV_DB_PASSWORD:-}" ]]; then report SIGOV_DB_PASSWORD OK definido; else report SIGOV_DB_PASSWORD MISSING_ENV 'defina sem registrar o valor'; status=1; fi
[[ -f .env.local ]] && report .env.local OK presente || { report .env.local MISSING_ENV 'copie .env.local.example'; status=1; }
[[ -f global.json ]] && report global.json OK presente || { report global.json MISSING_ENV ausente; status=1; }

port_used() {
  if command -v ss >/dev/null 2>&1; then ss -ltnH | awk '{print $4}' | sed 's/.*://' | grep -qx "$1"
  elif command -v lsof >/dev/null 2>&1; then lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
  else return 2; fi
}
for port in 7000 7001 5432; do
  if port_used "$port"; then report "port:$port" PORT_IN_USE 'há um listener'; status=1
  elif [[ $? -eq 2 ]]; then report "port:$port" MISSING_TOOL 'ss/lsof indisponível'; status=1
  else report "port:$port" OK disponível; fi
done

exit "$status"
