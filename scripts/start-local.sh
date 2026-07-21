#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ -f "$ROOT/.env.local" ] && set -a && . "$ROOT/.env.local" && set +a
: "${SIGOV_WEB_URL:=http://localhost:5000}"; : "${SIGOV_API_URL:=http://localhost:5001}"; : "${SIGOV_RUN_WORKER:=true}"
mkdir -p "$ROOT/.local/run" "$ROOT/.local/logs" "$ROOT/${SIGOV_STORAGE_PATH:-.local/storage}"
dotnet restore "$ROOT/sigov.sln" && dotnet build "$ROOT/sigov.sln" --configuration Release --no-restore
(ASPNETCORE_URLS="$SIGOV_API_URL" dotnet run --project "$ROOT/src/Sigov.Api/Sigov.Api.csproj" --no-launch-profile >"$ROOT/.local/logs/api.log" 2>&1 & echo $! >"$ROOT/.local/run/api.pid")
(ASPNETCORE_URLS="$SIGOV_WEB_URL" dotnet run --project "$ROOT/src/Sigov.Web/Sigov.Web.csproj" --no-launch-profile >"$ROOT/.local/logs/web.log" 2>&1 & echo $! >"$ROOT/.local/run/web.pid")
[ "${SIGOV_RUN_WORKER,,}" = true ] && (dotnet run --project "$ROOT/src/Sigov.Worker/Sigov.Worker.csproj" --no-launch-profile >"$ROOT/.local/logs/worker.log" 2>&1 & echo $! >"$ROOT/.local/run/worker.pid")
