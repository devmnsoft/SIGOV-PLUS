#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

python3 - "$repo_root" <<'PY'
import hashlib
import json
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])
migration_dir = root / "database/postgres/migrations"
manifest_path = migration_dir / "manifest.json"
governance_path = root / "database/postgres/migration-governance.json"
failures: list[str] = []
baseline_entries: list[dict] = []


def fail(message: str) -> None:
    failures.append(message)


def load_json(path: pathlib.Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        fail(f"JSON_INVALID {path.relative_to(root)}: {error}")
        return {}
    if not isinstance(value, dict):
        fail(f"JSON_ROOT_INVALID {path.relative_to(root)}")
        return {}
    return value


def digest(path: pathlib.Path) -> str:
    text = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def safe_name(value: object) -> bool:
    return isinstance(value, str) and bool(value) and pathlib.PurePath(value).name == value and "/" not in value and "\\" not in value


manifest = load_json(manifest_path)
governance = load_json(governance_path)
migrations = manifest.get("migrations", [])
orphans = governance.get("orphans", [])
exceptions = governance.get("prefixExceptions", [])
collision_decisions = governance.get("prefixCollisions", [])

if not isinstance(migrations, list):
    fail("MANIFEST_MIGRATIONS_INVALID")
    migrations = []
if not isinstance(orphans, list):
    fail("GOVERNANCE_ORPHANS_INVALID")
    orphans = []

orphan_by_file: dict[str, dict] = {}
allowed_classifications = {"substituida", "baseline_only", "auxiliar", "obsoleta", "incompativel", "necessaria", "duplicada"}
for entry in orphans:
    if not isinstance(entry, dict) or not safe_name(entry.get("file")):
        fail("ORPHAN_ENTRY_INVALID")
        continue
    name = entry["file"]
    if name in orphan_by_file:
        fail(f"ORPHAN_DUPLICATE {name}")
    orphan_by_file[name] = entry
    if entry.get("classification") not in allowed_classifications or not str(entry.get("reason", "")).strip():
        fail(f"ORPHAN_UNCLASSIFIED {name}")
    if entry.get("disposition") != "excluded_pending_runtime_reconciliation":
        fail(f"ORPHAN_DISPOSITION_INVALID {name}")

exception_by_file = {
    entry.get("file"): entry
    for entry in exceptions
    if isinstance(entry, dict) and safe_name(entry.get("file"))
}
if len(exception_by_file) != len(exceptions):
    fail("PREFIX_EXCEPTION_INVALID_OR_DUPLICATE")

seen_versions: set[str] = set()
seen_files: set[str] = set()
previous_version = ""
for index, entry in enumerate(migrations, start=1):
    if not isinstance(entry, dict):
        fail(f"ENTRY_INVALID #{index}")
        continue
    version = entry.get("version")
    name = entry.get("file")
    if not all(isinstance(entry.get(field), str) and entry[field].strip() for field in ("version", "file", "description", "category")):
        fail(f"ENTRY_INVALID {name or index}")
        continue
    if not safe_name(name):
        fail(f"FILE_PATH {name}")
        continue
    if version in seen_versions:
        fail(f"VERSION_DUPLICATE {version}")
    if name in seen_files:
        fail(f"FILE_DUPLICATE {name}")
    if previous_version and previous_version >= version:
        fail(f"ORDER {previous_version} >= {version}")
    seen_versions.add(version)
    seen_files.add(name)
    previous_version = version
    if not name.startswith(f"{version}_"):
        exception = exception_by_file.get(name, {})
        if exception.get("version") != version or not str(exception.get("reason", "")).strip():
            fail(f"PREFIX_UNJUSTIFIED {version}/{name}")
    if not isinstance(entry.get("applyAutomatically"), bool) or not isinstance(entry.get("includeInBaseline"), bool):
        fail(f"ENTRY_FLAGS {name}")
    if entry.get("includeInBaseline") is True:
        baseline_entries.append(entry)
    for dependency in entry.get("dependencies", []) or []:
        if dependency not in seen_versions or dependency == version:
            fail(f"DEPENDENCY_NOT_PRIOR {name}/{dependency}")
    known = entry.get("knownChecksums", []) or []
    for checksum in known:
        if not isinstance(checksum, str) or not re.fullmatch(r"[0-9a-fA-F]{64}", checksum):
            fail(f"KNOWN_CHECKSUM_INVALID {name}")
        postcondition = str(entry.get("postConditionSql", "")).strip()
        if not postcondition or re.fullmatch(r"(?is)select\s+(true|1)\s*;?", postcondition):
            fail(f"KNOWN_CHECKSUM_WITHOUT_SPECIFIC_POSTCONDITION {name}")
    path = migration_dir / name
    if not path.is_file():
        fail(f"FILE_MISSING {name}")
    elif digest(path) != str(entry.get("checksum", "")).lower():
        fail(f"CHECKSUM_CURRENT {name}")

sql_files = sorted(migration_dir.glob("*.sql"), key=lambda path: path.name)
for path in sql_files:
    if path.name in seen_files:
        if path.name in orphan_by_file:
            fail(f"ORPHAN_ALREADY_REGISTERED {path.name}")
        continue
    decision = orphan_by_file.get(path.name)
    if decision is None:
        fail(f"UNCLASSIFIED {path.name}")
    elif digest(path) != str(decision.get("checksum", "")).lower():
        fail(f"ORPHAN_CHECKSUM {path.name}")

for name in orphan_by_file:
    if not (migration_dir / name).is_file():
        fail(f"ORPHAN_FILE_MISSING {name}")
for name in exception_by_file:
    if name not in seen_files:
        fail(f"STALE_PREFIX_EXCEPTION {name}")

prefix_groups: dict[str, list[str]] = {}
for path in sql_files:
    prefix_groups.setdefault(path.name.split("_", 1)[0], []).append(path.name)
decisions_by_prefix = {
    entry.get("prefix"): entry
    for entry in collision_decisions
    if isinstance(entry, dict) and isinstance(entry.get("prefix"), str)
}
for prefix, names in prefix_groups.items():
    if len(names) < 2:
        continue
    decision = decisions_by_prefix.get(prefix, {})
    if sorted(decision.get("files", [])) != sorted(names) or not str(decision.get("reason", "")).strip():
        fail(f"PREFIX_COLLISION_UNRESOLVED {prefix}")

# The four production aliases are part of the published database contract.  Keep
# this check portable: the canonical PowerShell generator remains authoritative,
# but CI/minimal Linux hosts must still fail when an alias or concatenated
# migration drifts.
production_scripts = [
    root / "database/postgres/script_completo.sql",
    root / "database/script_completo.sql",
    root / "script_completo.sql",
    root / "script_completop.sql",
]
production_texts: dict[pathlib.Path, str] = {}
for path in production_scripts:
    try:
        production_texts[path] = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
    except OSError as error:
        fail(f"CONSOLIDATED_MISSING {path.relative_to(root)}: {error}")

canonical_path = production_scripts[0]
canonical = production_texts.get(canonical_path, "")
for path, content in production_texts.items():
    if canonical and content != canonical:
        fail(f"CONSOLIDATED_ALIAS_DRIFT {path.relative_to(root)}")

cursor = -1
if canonical:
    for entry in baseline_entries:
        marker = f"-- MIGRATION: {entry['file']}"
        position = canonical.find(marker)
        if position < 0:
            fail(f"CONSOLIDATED_MIGRATION_MISSING {entry['file']}")
            continue
        if position <= cursor:
            fail(f"CONSOLIDATED_ORDER {entry['file']}")
        cursor = position
        checksum_marker = f"-- CHECKSUM_SHA256: {str(entry.get('checksum', '')).lower()}"
        body = (migration_dir / entry["file"]).read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n").strip()
        if checksum_marker not in canonical[position:position + len(marker) + 256]:
            fail(f"CONSOLIDATED_CHECKSUM {entry['file']}")
        if body not in canonical[position:]:
            fail(f"CONSOLIDATED_BODY_DRIFT {entry['file']}")
    for entry in migrations:
        if entry.get("includeInBaseline") is False:
            if f"-- MIGRATION: {entry['file']}" in canonical:
                fail(f"CONSOLIDATED_EXCLUDED_INCLUDED {entry['file']}")
            if f"-- EXCLUDED_FROM_BASELINE: {entry['file']} [{entry['category']}]" not in canonical:
                fail(f"CONSOLIDATED_EXCLUSION_MISSING {entry['file']}")

dev_paths = [root / "database/postgres/script_completo_dev.sql", root / "script_completo_dev.sql"]
seed_paths = [
    root / "database/postgres/seeds/development/999_super_admin_access_guard.sql",
    root / "database/postgres/seeds/rc50_68a_perfis_autorizacao.sql",
]
if canonical:
    expected_dev = canonical + "\n-- DEVELOPMENT ONLY: seeds fictícias idempotentes\n"
    for seed_path in seed_paths:
        try:
            expected_dev += seed_path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n").strip() + "\n"
        except OSError as error:
            fail(f"DEVELOPMENT_SEED_MISSING {seed_path.relative_to(root)}: {error}")
    for path in dev_paths:
        try:
            content = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
            if content != expected_dev:
                fail(f"DEVELOPMENT_CONSOLIDATED_DRIFT {path.relative_to(root)}")
        except OSError as error:
            fail(f"DEVELOPMENT_CONSOLIDATED_MISSING {path.relative_to(root)}: {error}")

status = "PASS" if not failures else "FAIL"
print(f"Migration catalog: SQL={len(sql_files)} manifest={len(migrations)} baseline={len(baseline_entries)} governed_orphans={len(orphan_by_file)} static={status}")
for failure in failures:
    print(f"FAIL: {failure}")
if failures:
    sys.exit(1)
print("BLOCKED: a validação semântica e a execução/reexecução exigem PostgreSQL 16 e o runner canônico.")
PY
