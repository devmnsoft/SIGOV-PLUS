#!/usr/bin/env bash
set -euo pipefail
ENVIRONMENT_NAME="${ASPNETCORE_ENVIRONMENT:-Development}"
OUTPUT_DIR="${SIGOV_BACKUP_DIR:-backups}"
CONNECTION_STRING="${ConnectionStrings__DefaultConnection:?ConnectionStrings__DefaultConnection obrigatório}"
mkdir -p "$OUTPUT_DIR"
FILE="$OUTPUT_DIR/sigov-$ENVIRONMENT_NAME-$(date +%Y%m%d-%H%M%S).dump"
pg_dump --format=custom --no-owner --no-privileges --dbname="$CONNECTION_STRING" --file="$FILE"
sha256sum "$FILE"
