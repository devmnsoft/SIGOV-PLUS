#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
migrations="${1:-$root/database/postgres/migrations}"

python3 - "$migrations" <<'PY'
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])
files = sorted(root.glob("*.sql")) if root.is_dir() else [root]
dangerous = re.compile(r"(?i)(?:\bnow\s*\(|\bcurrent_date\b|\bdate_trunc\s*\(|\bto_char\s*\(|\btimezone\s*\(|\bextract\s*\(|\bunaccent\s*\(|::\s*(?:date|timestamp)\b)")
conservative = re.compile(r"(?i)\bcoalesce\s*\(")
create_index = re.compile(r"(?is)\bcreate\s+(?:unique\s+)?index\b.*?;")
failures = []
warnings = []

for path in files:
    text = path.read_text(encoding="utf-8-sig")
    for statement in create_index.finditer(text):
        value = statement.group(0)
        match = dangerous.search(value)
        target = failures
        if match is None:
            match = conservative.search(value)
            target = warnings
        if match is None:
            continue
        offset = statement.start() + match.start()
        line = text.count("\n", 0, offset) + 1
        excerpt = " ".join(statement.group(0).split())
        target.append((path, line, excerpt))

for path, line, excerpt in failures:
    print(f"{path}:{line}: expressão potencialmente não IMMUTABLE em CREATE INDEX: {excerpt}", file=sys.stderr)
    print("  recomendação: materialize o valor em data_referencia, competencia ou search_text e indexe somente a coluna simples.", file=sys.stderr)

for path, line, excerpt in warnings:
    print(f"{path}:{line}: aviso conservador para COALESCE em CREATE INDEX: {excerpt}", file=sys.stderr)
    print("  recomendação: avalie uma coluna materializada para simplificar a chave do índice.", file=sys.stderr)

if failures:
    raise SystemExit(f"Falha: {len(failures)} índice(s) com expressão potencialmente não IMMUTABLE.")
print(f"OK: {len(files)} migration(s) sem expressões de índice não IMMUTABLE; {len(warnings)} aviso(s) conservador(es).")
PY
