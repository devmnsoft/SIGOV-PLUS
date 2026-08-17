#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
migrations="${1:-$root/database/postgres/migrations}"

python3 - "$migrations" <<'PY'
from pathlib import Path
import re
import sys

directory = Path(sys.argv[1])
predicates = re.compile(r"\bwhere\b[^;]*(?:\bis_deleted\b|\bativo\b|\bstatus\b)[^;]*;", re.I | re.S)
columns = re.compile(r"\b(is_deleted|ativo|status)\b", re.I)
warnings = 0

for path in sorted(directory.glob("*.sql")):
    sql = path.read_text(encoding="utf-8")
    used = {column.lower() for predicate in predicates.findall(sql)
            for column in columns.findall(predicate)}
    if not used:
        continue
    missing = []
    for column in sorted(used):
        create_guarantee = re.search(
            r"create\s+table\s+if\s+not\s+exists[\s\S]*?\b" + re.escape(column) + r"\b[\s\S]*?\)",
            sql, re.I)
        alter_guarantee = re.search(
            r"alter\s+table[\s\S]*?add\s+column\s+if\s+not\s+exists\s+" + re.escape(column) + r"\b",
            sql, re.I)
        if not (create_guarantee and alter_guarantee):
            missing.append(column)
    if missing:
        warnings += 1
        print(f"[WARN] migration {path.name}: índice parcial usa {', '.join(missing)}, mas a compatibilidade legado (CREATE + ADD COLUMN IF NOT EXISTS) não está explícita")
    else:
        print(f"[OK] migration {path.name}: colunas usadas em índices parciais estão garantidas")

print(f"Resumo: {warnings} migration(s) com possível risco; revise os avisos antes de publicar.")
PY
