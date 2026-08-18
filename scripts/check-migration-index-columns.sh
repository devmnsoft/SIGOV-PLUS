#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
migrations="${1:-$root/database/postgres/migrations}"
python3 - "$migrations" <<'PY'
from pathlib import Path
import re, sys
directory = Path(sys.argv[1]); warnings = checked = 0
indexes = re.compile(r"create\s+(?:unique\s+)?index\s+(?:if\s+not\s+exists\s+)?(?P<name>[\w\"]+)\s+on\s+(?P<table>[\w\".]+)\s*\((?P<keys>.*?)\)(?:\s+where\s+(?P<where>.*?))?;", re.I | re.S)
identifier = re.compile(r"^[a-z_][a-z0-9_]*$", re.I)
def guaranteed(prefix, table, column):
    table = table.replace('"', '')
    create = re.search(r"create\s+table\s+if\s+not\s+exists\s+" + re.escape(table) + r"\s*\((.*?)\)\s*;", prefix, re.I | re.S)
    if create and re.search(r"(?:^|,)\s*" + re.escape(column) + r"\s+[a-z]", create.group(1), re.I): return True
    alters = re.finditer(r"alter\s+table\s+" + re.escape(table) + r"\s+(.*?);", prefix, re.I | re.S)
    if any(re.search(r"add\s+column\s+if\s+not\s+exists\s+" + re.escape(column) + r"\b", a.group(1), re.I) for a in alters): return True
    return bool(re.search(r"ensure_[a-z0-9_]*columns?\s*\([^;]*" + re.escape(table), prefix, re.I))
paths = [directory] if directory.is_file() else sorted(directory.glob('*.sql'))
for path in paths:
    sql = path.read_text(encoding='utf-8')
    for match in indexes.finditer(sql):
        fragments = [part.strip() for part in match.group('keys').split(',')]
        columns = {part.lower() for part in fragments if identifier.fullmatch(part)}
        columns.update(word.lower() for word in re.findall(r"\b[a-z_][a-z0-9_]*\b", match.group('where') or '', re.I) if word.lower() not in {'and','or','not','null','is','true','false'})
        missing = sorted(c for c in columns if not guaranteed(sql[:match.start()], match.group('table'), c))
        checked += 1
        if missing:
            warnings += 1
            print(f"[WARN] {path.name}: {match.group('name')} em {match.group('table')} não garante antes do índice: {', '.join(missing)}")
print(f"Resumo: {checked} índice(s) simples verificados; {warnings} aviso(s).")
sys.exit(0)
PY
