#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$ROOT/scripts/apply-migrations-manifest.sh"
for mode in absent empty prefer require; do
  case "$mode" in
    absent) unset SIGOV_DB_SSLMODE || true ;;
    empty) export SIGOV_DB_SSLMODE="" ;;
    prefer) export SIGOV_DB_SSLMODE="prefer" ;;
    require) export SIGOV_DB_SSLMODE="require" ;;
  esac
  VALIDATE_ONLY=true bash "$SCRIPT" >/tmp/sigov-sslmode-$mode.log 2>&1
  if grep -Eiq 'password|pwd=|postgresql://|postgres://' /tmp/sigov-sslmode-$mode.log; then
    echo "Sensitive data leaked for SSLMode=$mode" >&2
    exit 1
  fi
done
echo "sslmode contract: ok"
