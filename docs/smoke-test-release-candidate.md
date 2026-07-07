# Smoke test Release Candidate SIGOV PLUS

Documento gerado/atualizado pela sprint Pós-RC 05. O arquivo real deve ser sobrescrito pela execução de `scripts/smoke-test-sigov.ps1` no ambiente de homologação.

## Cobertura obrigatória

- Web: `/`, `/Auth/Login`, `/Dashboard`, `/MinhaCentral`, `/Protocolo`, `/Protocolo/Novo`, `/Ged`, `/Ged/NovoDocumento`, `/Workflow`, `/Tarefas`, `/Notificacoes`, `/Busca?q=protocolo`, `/Relatorios`, `/Poc`, `/Seguranca/ApiKeys`, `/Integracoes/Webhooks`, `/ValidarDocumento`, `/Operacao/Outbox`.
- API: `/api/health/live`, `/api/health/ready`, `/api/health/db`, `/api/v1/health`, `/api/v1/protocolos` sem key `401`, `/api/v1/protocolos`, `/api/v1/documentos` e `/api/v1/tarefas` com key válida `200`.

## Status desta execução no container do agente

Não executado por ausência de `pwsh`, `dotnet` e `docker` no ambiente atual. A execução real deve ocorrer no GitHub Actions e/ou host de homologação.
