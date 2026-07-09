# Smoke E2E Pós-RC 06

## Chave demo local/homologação

Token demo local: `sigov_demo_local_only_2026_please_rotate`.

Use apenas em Development/Homologation, rotacione antes de qualquer ambiente real e nunca salve o token claro no banco. O seed grava somente o hash SHA-256 `fc86ee2b04157910a83296966cd5033de0f564cbe8dc64d1f3a54238fb32063a`.

## Execução

```powershell
pwsh -NoProfile -File scripts/apply-demo-seed.ps1
$env:SIGOV_SMOKE_USE_DEMO_KEY="true"
$env:SIGOV_SMOKE_TENANT_ID="1"
$env:SIGOV_SMOKE_API_KEY="sigov_demo_local_only_2026_please_rotate"
pwsh -NoProfile -File scripts/smoke-test-sigov.ps1
```

O log mascara a chave como `sigov_demo_****rotate`. O smoke valida rotas Web com 200/302, API sem chave com 401 e API autenticada com 200 quando a seed demo está aplicada.
