# Smoke test Release Candidate SIGOV PLUS

Arquivo gerado/atualizado para Pós-RC 02. Execute:

```powershell
pwsh -NoProfile -File scripts/smoke-test-sigov.ps1
```

Cobertura: rotas web principais, health checks, API v1 sem chave (401 esperado) e API v1 com chave válida quando `SIGOV_SMOKE_API_KEY` e `SIGOV_SMOKE_TENANT_ID` estiverem definidos.
