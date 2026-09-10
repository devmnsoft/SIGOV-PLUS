#!/usr/bin/env bash
set -euo pipefail

fail() {
  echo "Tracked artifact gate: FAIL — $*" >&2
  exit 1
}

if ! command -v git >/dev/null 2>&1; then
  fail "git is not available."
fi

if ! command -v rg >/dev/null 2>&1; then
  fail "rg is not available."
fi

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  fail "not inside a git worktree."
fi

pattern='(^|/)(bin|obj|\.vs)(/|$)|(^|/)(artifacts|TestResults|coverage)(/|$)|\.(trx|pdb|suo|user|cache)$'

tracked_files="$(git ls-files)" || fail "git ls-files failed."

matches="$(printf '%s\n' "$tracked_files" | rg -n "$pattern" || true)"
if [ -n "$matches" ]; then
  echo 'Generated artifacts are tracked by Git:' >&2
  printf '%s\n' "$matches" >&2
  echo 'Remove them from the index with git rm --cached (preserve local files).' >&2
  exit 1
fi

echo 'Tracked artifact gate: PASS'
