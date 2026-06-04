#!/usr/bin/env bash
set -euo pipefail
BACKUP_FILE="${1:?Informe o arquivo de backup}"
ENVIRONMENT_NAME="${ASPNETCORE_ENVIRONMENT:-Development}"
CONNECTION_STRING="${ConnectionStrings__DefaultConnection:?ConnectionStrings__DefaultConnection obrigatório}"
if [[ "$ENVIRONMENT_NAME" == "Production" && "${CONFIRM_RESTORE:-}" != "RESTORE_PRODUCTION_SIGOV" ]]; then
  echo "Restore em Production exige CONFIRM_RESTORE=RESTORE_PRODUCTION_SIGOV" >&2
  exit 2
fi
pg_restore --clean --if-exists --no-owner --no-privileges --dbname="$CONNECTION_STRING" "$BACKUP_FILE"
