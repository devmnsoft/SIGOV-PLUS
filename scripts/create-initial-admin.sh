#!/usr/bin/env bash
set -euo pipefail
read -rp "Tenant ID: " SIGOV_BOOTSTRAP_TENANT_ID
read -rp "Nome: " SIGOV_BOOTSTRAP_NAME
read -rp "Login: " SIGOV_BOOTSTRAP_LOGIN
read -rp "E-mail: " SIGOV_BOOTSTRAP_EMAIL
read -rsp "Senha: " SIGOV_BOOTSTRAP_PASSWORD; echo
if [ ${#SIGOV_BOOTSTRAP_PASSWORD} -lt 12 ] || ! [[ "$SIGOV_BOOTSTRAP_PASSWORD" =~ [A-Z] ]] || ! [[ "$SIGOV_BOOTSTRAP_PASSWORD" =~ [a-z] ]] || ! [[ "$SIGOV_BOOTSTRAP_PASSWORD" =~ [0-9] ]]; then
  echo "Senha fraca: use ao menos 12 caracteres com maiúsculas, minúsculas e números." >&2
  exit 1
fi
: "${SIGOV_DB_CONNECTION:?Defina SIGOV_DB_CONNECTION sem expor senha no histórico.}"
: "${SIGOV_ADMIN_BOOTSTRAP_HASH_COMMAND:=dotnet run --project src/Sigov.Tools.AdminBootstrap --}"
echo "Bootstrap seguro deve ser executado pelo comando .NET configurado em SIGOV_ADMIN_BOOTSTRAP_HASH_COMMAND; senha não será gravada em log."
