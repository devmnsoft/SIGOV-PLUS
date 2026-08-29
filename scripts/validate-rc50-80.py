#!/usr/bin/env python3
"""Gate estático e determinístico do fechamento RC50.80.

Não substitui build, PostgreSQL ou smoke HTTP. O objetivo é impedir que a RC
seja promovida com artefatos SQL divergentes ou regressões Razor elementares.
"""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MIGRATIONS = ROOT / "database/postgres/migrations"


def normalized(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")


def sha256(path: Path) -> str:
    return hashlib.sha256(normalized(path).encode()).hexdigest()


def fail(message: str, errors: list[str]) -> None:
    errors.append(message)
    print(f"FAIL: {message}")


def main() -> int:
    errors: list[str] = []
    manifest_path = MIGRATIONS / "manifest.json"
    try:
        manifest = json.loads(normalized(manifest_path))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"FAIL: manifest inválido: {exc}")
        return 1

    entries = manifest.get("migrations", [])
    versions: set[str] = set()
    files: set[str] = set()
    for entry in entries:
        version, filename = entry.get("version", ""), entry.get("file", "")
        if not version or version in versions:
            fail(f"versão ausente ou duplicada: {version!r}", errors)
        if not filename or filename in files or Path(filename).name != filename:
            fail(f"arquivo ausente, duplicado ou inseguro: {filename!r}", errors)
        versions.add(version)
        files.add(filename)
        path = MIGRATIONS / filename
        if not path.is_file():
            fail(f"migration ausente: {filename}", errors)
        elif sha256(path) != str(entry.get("checksum", "")).lower():
            fail(f"checksum divergente: {filename}", errors)

    for stage in ("compatibilityBeforeAll", "compatibilityAfterAll"):
        for entry in manifest.get(stage, []) or []:
            path = ROOT / "database/postgres/bootstrap" / entry.get("file", "")
            if not path.is_file() or sha256(path) != str(entry.get("checksum", "")).lower():
                fail(f"compatibilidade ausente ou divergente: {stage}/{path.name}", errors)

    baselines = [
        ROOT / "script_completo.sql",
        ROOT / "script_completop.sql",
        ROOT / "database/script_completo.sql",
        ROOT / "database/postgres/script_completo.sql",
    ]
    baseline_texts = [normalized(path) for path in baselines if path.is_file()]
    if len(baseline_texts) != len(baselines) or len(set(baseline_texts)) != 1:
        fail("scripts completos de produção não estão sincronizados", errors)
    elif re.search(r"(?m)^\s*\\i\b", baseline_texts[0]):
        fail("baseline contém include psql e não é autônomo", errors)

    views = list((ROOT / "src/Sigov.Web/Views").rglob("*.cshtml"))
    for view in views:
        content = normalized(view)
        if re.search(r"<form\b[^>]*\bmethod\s*=\s*[\"']post[\"']", content, re.I) and "AntiForgeryToken" not in content:
            fail(f"view com POST sem AntiForgeryToken: {view.relative_to(ROOT)}", errors)

    import subprocess
    forbidden = re.compile(r"\b(?:TODO|mock|fake)\b", re.I)
    diff = subprocess.run(
        ["git", "diff", "--unified=0", "HEAD", "--", "*.cs", "*.cshtml", "*.js"],
        cwd=ROOT, text=True, capture_output=True, check=False
    ).stdout
    if any(forbidden.search(line) for line in diff.splitlines() if line.startswith("+") and not line.startswith("+++")):
        fail("marcador provisório adicionado em código alterado", errors)

    if errors:
        print(f"RC50.80 FAIL ({len(errors)} violação(ões)).")
        return 1
    print(f"RC50.80 PASS: {len(entries)} migrations, 4 baselines e {len(views)} views verificados.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
