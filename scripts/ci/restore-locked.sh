#!/usr/bin/env bash
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
output="${1:-$root/artifacts/build}"
mkdir -p "$output"

log="$output/restore.log"
binlog="$output/restore.binlog"
summary="$output/restore-summary.json"

set +e
dotnet restore "$root/sigov.sln" --locked-mode --verbosity normal "-bl:$binlog" 2>&1 | tee "$log"
exit_code=${PIPESTATUS[0]}
set -e

python3 - "$log" "$summary" "$exit_code" <<'PY'
import json
import pathlib
import re
import sys

log_path, summary_path, raw_exit_code = sys.argv[1:]
text = pathlib.Path(log_path).read_text(encoding="utf-8", errors="replace")
projects = sorted(set(re.findall(r"(?:Restored|Restoring)\s+([^\r\n]+?\.csproj)", text)))
errors = [line.strip() for line in text.splitlines() if re.search(r"\berror\s+(?:NU|MSB)\d+", line, re.I)]
warnings = [line.strip() for line in text.splitlines() if re.search(r"\bwarning\s+(?:NU|MSB)\d+", line, re.I)]
result = {
    "command": "dotnet restore sigov.sln --locked-mode --verbosity normal -bl:artifacts/build/restore.binlog",
    "exitCode": int(raw_exit_code),
    "status": "passed" if int(raw_exit_code) == 0 else "failed",
    "projectsObserved": projects,
    "errors": errors,
    "warnings": warnings,
}
pathlib.Path(summary_path).write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
PY

exit "$exit_code"
