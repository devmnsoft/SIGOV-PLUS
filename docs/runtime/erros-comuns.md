# Erros comuns de runtime

| Sintoma | Causa | Correção |
|---|---|---|
| `28P01` | senha do papel runtime diverge do `.env.local` | `./scripts/provision-sigov-db-user.ps1 -Database sigov -AdminUser postgres -AppDbUser sigov` |
| `3D000` | banco não existe | `./scripts/install-sigov-database.ps1 -Database sigov` |
| `42P01` ou schema ausente | instalação incompleta | execute `diagnose-sigov-database.ps1`, depois reinstale/repare |
| `42501` | grants insuficientes | execute novamente `provision-sigov-db-user.ps1` |
| migration pendente | modo incompatível | use `ValidateOnly`; aplique somente em ambiente controlado |
| worker/OCR parado | flags desabilitadas | revise `SIGOV_RUN_WORKER`, `OcrWorker__Enabled` e `PreviewWorker__Enabled` |

Nunca cole `.env.local`, connection strings, hashes ou tokens em chamados. Envie os relatórios sanitizados de `artifacts/local-setup` e o correlation id.
