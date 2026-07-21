#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
for f in "$ROOT"/.local/run/*.pid; do [ -e "$f" ] || continue; kill "$(cat "$f")" 2>/dev/null || true; rm -f "$f"; done
