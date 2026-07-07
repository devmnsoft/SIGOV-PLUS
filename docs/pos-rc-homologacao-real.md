# Homologação real Pós-RC 05

Esta sprint consolida CI/CD, smoke, Docker, segurança, LGPD, performance básica e pacote de release. A homologação real deve usar ambiente com .NET 6, Docker Compose, PowerShell 7 e PostgreSQL.

## Status honesto

- CI/CD: workflow criado e rodável no GitHub Actions.
- Build/test local neste container: não validado por ausência de `dotnet`.
- Docker local neste container: não validado por ausência de `docker`.
- Scripts PowerShell neste container: não executados por ausência de `pwsh`.
- Smoke E2E: script atualizado para Web/API, geração Markdown/JSON e falha bloqueante.

## Evidências obrigatórias

Anexar ao processo de Go-Live: execução do workflow CI, logs do Docker Compose, relatório do smoke, relatório de schema, evidências manuais e checklist LGPD assinado.
