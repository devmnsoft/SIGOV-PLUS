#!/usr/bin/env bash
set -euo pipefail

script="${1:-database/postgres/script_completo.sql}"
[[ -f "$script" ]] || { echo "Script consolidado ausente: $script" >&2; exit 2; }

python3 - "$script" <<'PY'
import re
import sys
from pathlib import Path

path = Path(sys.argv[1])
sql = path.read_text(encoding="utf-8")
# Remove comentários para que exemplos/documentação não sejam interpretados como DDL.
clean = re.sub(r"/\*.*?\*/", " ", sql, flags=re.S)
clean = re.sub(r"--[^\n]*", " ", clean)

patterns = {
    "table": r"\bcreate\s+table\s+(?!if\s+not\s+exists)([\w.\"]+)",
    "index": r"\bcreate\s+(?:unique\s+)?index\s+(?!if\s+not\s+exists)([\w.\"]+)",
    "view": r"\bcreate\s+(?!or\s+replace\s+)(?:materialized\s+)?view\s+([\w.\"]+)",
    "function": r"\bcreate\s+(?!or\s+replace\s+)(?:function|procedure)\s+([\w.\"]+)",
}
errors = []
for kind, pattern in patterns.items():
    seen = {}
    for match in re.finditer(pattern, clean, flags=re.I):
        name = match.group(1).lower().replace('"', '')
        if name in seen:
            errors.append(f"CREATE {kind} duplicado e não idempotente: {name}")
        seen[name] = match.start()

# Nomes de constraints compartilham namespace por relação no PostgreSQL.
#
# O identificador exige segmentos completos e espaço depois do nome. Isso evita
# interpretar o prefixo de SQL dinâmico (por exemplo, ``ALTER TABLE sigov.%I``)
# como se fosse a relação estática ``sigov.`` e consumir texto até o próximo
# ADD CONSTRAINT. O gate analisa apenas DDL estático; SQL dinâmico é validado
# pelos checks de migration e pela execução no PostgreSQL.
constraints = {}
identifier = r'(?:"[^"]+"|\w+)(?:\.(?:"[^"]+"|\w+))*'
constraint_pattern = rf"\balter\s+table\s+(?:only\s+)?({identifier})\s+[^;]*?\badd\s+constraint\s+([\w\"]+)[^;]*;"
for match in re.finditer(constraint_pattern, clean, flags=re.I | re.S):
    key = (match.group(1).lower().replace('"', ''), match.group(2).lower().replace('"', ''))
    fragment = re.sub(r"\s+", " ", match.group(0)).strip().lower()
    if key in constraints and constraints[key] != fragment:
        errors.append(f"Constraint duplicada incompatível: {key[0]}.{key[1]}")
    constraints[key] = fragment

if errors:
    print("\n".join(sorted(set(errors))), file=sys.stderr)
    sys.exit(1)
print(f"Validação estática de colisões concluída: {path}")
PY
