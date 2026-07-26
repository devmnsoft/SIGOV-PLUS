# Evidências Pós-RC 27A.2

Evidências locais foram limitadas pelo ambiente sem `dotnet`, `pwsh`, `psql`, `docker`, `gh` e remote `origin`. Os gates completos devem ser consumidos pelo GitHub Actions do PR draft.

## Checks locais executados

| Comando | Resultado | Observação |
|---|---:|---|
| `bash scripts/test-sslmode-contract.sh` | PASS | Contrato de `SIGOV_DB_SSLMODE` ausente, vazio, `prefer`, `require` e log sem segredo. |
| `bash -n scripts/apply-migrations-manifest.sh` | PASS | Parser Bash do aplicador do manifest. |
| `python3` checksum manifest | PASS | Todos os checksums conferem usando SHA-256 de conteúdo UTF-8 normalizado com LF. |
| `bash -n scripts/*.sh` | PASS | Parser Bash dos scripts versionados. |
| `dotnet --info` | WARNING | `dotnet` indisponível neste container. |
| `pwsh` parser/PSScriptAnalyzer | WARNING | `pwsh` indisponível neste container. |
| `psql` migrations/idempotência/equivalência | WARNING | `psql` indisponível neste container. |
| `docker` build/compose | WARNING | `docker` indisponível neste container. |
| `gh` workflow/PR real | WARNING | `gh` e remote `origin` indisponíveis neste container. |
