# Validação final obrigatória — Release Candidate

| Checagem | Resultado nesta execução | Observação |
|---|---|---|
| dotnet clean/restore/build | Não executado no ambiente | `/bin/bash: dotnet: command not found`. |
| dotnet test | Não executado no ambiente | Depende de SDK .NET. |
| Docker compose | Não executado no ambiente | `/bin/bash: docker: command not found`. |
| Smoke test | Script atualizado; execução depende de Web/API ativas | Rodar em PowerShell no host com containers. |
| Navegador/console/mobile | Pendente | Exige ambiente funcional. |

## Pendências bloqueantes
- Reexecutar build/test/Docker/smoke em ambiente válido.
- Corrigir falhas reais encontradas.

## Pendências não bloqueantes
- Ampliar cobertura e evidências visuais após homologação.

## Tentativa adicional de execução do smoke test

```bash
pwsh -NoProfile -File scripts/smoke-test-sigov.ps1
```

Resultado: não executado por limitação do ambiente (`/bin/bash: pwsh: command not found`).
