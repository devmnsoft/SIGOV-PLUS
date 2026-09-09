#!/usr/bin/env bash
set -euo pipefail

pattern='(^|/)(bin|obj|\.vs)(/|$)|(^|/)artifacts(/|$)|\.(trx|pdb|suo|user|cache)$'

if git ls-files | rg -n "$pattern"; then
  echo 'Generated artifacts are tracked by Git. Remove them from the index with git rm --cached.' >&2
  exit 1
fi

echo 'Tracked artifact gate: PASS'
