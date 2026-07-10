# Evidências Enterprise Pós-RC 09

## Status dos checks

| Check | Status | Evidência |
|---|---|---|
| Git main/pull | Parcial | `main` não existe no clone local; branch de trabalho criada a partir do commit `4fe5c08`. |
| Build Release | Bloqueado pelo ambiente | `dotnet` não está instalado no container (`/bin/bash: dotnet: command not found`). |
| Testes Release | Bloqueado pelo ambiente | Não executado porque o SDK .NET não está disponível. |
| Docker compose | Não executado | Ambiente sem validação segura do compose nesta sessão. |
| Seed demo | Não executado | Requer stack Postgres/API funcional. |
| Smoke Enterprise | Não executado | Requer Web/API em execução. |
| Revisão Web/JS | Concluído | Template e JS Enterprise ajustados para UX operacional, ciclo de vida e CSV com tenant. |

## Rotas Web cobertas pelo smoke existente

Comércio, OS, Estoque, Compras, Industrial e Indústria estão listados em `scripts/smoke-test-sigov.ps1`.

## Endpoints API cobertos

O smoke cobre endpoints Enterprise de listagem principais. A controller expõe também POST/PUT/DELETE/CSV e ações operacionais para as jornadas Comercial, OS, Estoque/Compras e Industrial.

## Pendências honestas

- Reexecutar `dotnet clean`, `restore`, `build` e `test` em ambiente com SDK .NET.
- Subir `docker compose` e registrar logs de API, Web, Worker, migrations e Postgres.
- Aplicar seed duas vezes e anexar saída real.
- Executar smoke contra Web/API reais e substituir este documento com resultados runtime.
- Homologar permissões com usuário sem acesso e confirmar auditoria persistida por ação crítica.
