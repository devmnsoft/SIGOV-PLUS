#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
controllers="${1:-$repo_root/src/Sigov.Api/Controllers}"

python3 - "$controllers" <<'PY'
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1])
routes = {}

for source in sorted(root.rglob("*.cs")):
    controller_route = ""
    pending_route = None
    pending_http = []
    for number, raw in enumerate(source.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = raw.strip()
        route = re.fullmatch(r'\[Route\("([^"]*)"\)\]', line)
        if route:
            pending_route = route.group(1)
            continue
        http = re.fullmatch(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', line)
        if http:
            pending_http.append((http.group(1).upper(), http.group(2) or "", number))
            continue
        if re.search(r'\bclass\s+\w+', line):
            controller_route = pending_route or ""
            pending_route = None
            pending_http.clear()
            continue
        if pending_http and re.search(r'\b(public|internal|protected|private)\b.*\(', line):
            for method, action_route, attribute_line in pending_http:
                # An absolute action template (api/...) does not inherit the controller prefix.
                pieces = [action_route] if action_route.lower().startswith("api/") else [controller_route, action_route]
                path = "/".join(piece.strip("/") for piece in pieces if piece.strip("/"))
                path = re.sub(r"//+", "/", path).lower()
                key = f"{method} {path}"
                routes.setdefault(key, []).append(f"{source.relative_to(root)}:{attribute_line}")
            pending_http.clear()
            pending_route = None
        elif line and not line.startswith("[") and not line.startswith("//"):
            pending_route = None

conflicts = {key: locations for key, locations in routes.items() if len(locations) > 1}
if conflicts:
    print("Conflitos de rota API encontrados:", file=sys.stderr)
    for key, locations in sorted(conflicts.items()):
        print(f"  {key}", file=sys.stderr)
        for location in locations:
            print(f"    - {location}", file=sys.stderr)
    sys.exit(1)

print(f"Nenhum conflito direto em {len(routes)} rotas API ({root}).")
PY
