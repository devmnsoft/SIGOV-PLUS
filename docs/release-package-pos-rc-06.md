# Release package Pós-RC 06

## Gerar pacote

```powershell
pwsh -NoProfile -File scripts/package-release.ps1 -Version 1.0.0-rc-final
```

O pacote é criado em `artifacts/release/sigov-plus-1.0.0-rc-final` com `release-manifest.json` e checksums SHA-256.

## Segurança do pacote

- `.env.example` é permitido, mas sanitizado com `POSTGRES_PASSWORD=change_me_local_only` e `Sigov__Jwt__Secret=change_me_local_only` quando existir.
- `.env` real, `.pfx`, `.pem`, `.key`, dumps, `storage/`, `.bak` e padrões de segredo real são bloqueados.
- Documentos sobre API key/webhook são permitidos desde que não contenham segredo real de produção.
