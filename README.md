# sigov

Plataforma de gestão pública municipal com ASP.NET Core, Dapper, PostgreSQL, Docker, Bootstrap e JavaScript puro.

## Execução local

- Web: http://localhost:5000
- API: http://localhost:5001
- Swagger: http://localhost:5001/swagger
- Health: http://localhost:5001/api/health
- DB Health: http://localhost:5001/api/health/db

```powershell
scripts/start-dev.ps1
```

## Banco de dados

O PostgreSQL usa o database `sigov`, usuário `sigov` e schema físico único `sigov`.
Os domínios permanecem organizados no código por bounded contexts, pastas e namespaces.
