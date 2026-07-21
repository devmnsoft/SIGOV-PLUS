# execucao-api-web-worker-local

Pós-RC 19 registrado em 2026-07-21.

- SHA inicial: d922dfa.
- Branch solicitada: codex/pos-rc-19-postgresql-standalone-sem-docker.
- Limitação local: este contêiner não possui remote origin, dotnet, pwsh ou psql instalados; os comandos foram preservados no CI e documentados como pendentes de execução em ambiente completo.
- Correção aplicada: serviços operacionais separados, repositórios operacionais separados, notificação com persistência, migration B2B com ALTER TABLE ADD COLUMN IF NOT EXISTS antes dos índices, script_completop.sql autônomo, scripts locais sem Docker e jobs de CI standalone.

## Validações esperadas

- dotnet build sigov.sln --configuration Release --no-restore
- dotnet test sigov.sln --configuration Release --no-build
- pwsh -NoProfile -File scripts/generate-script-completop.ps1 -Verify
- pwsh -NoProfile -File scripts/validate-script-completop.ps1
- psql -v ON_ERROR_STOP=1 -f script_completop.sql
