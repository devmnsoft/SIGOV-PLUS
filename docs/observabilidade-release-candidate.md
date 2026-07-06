# Observabilidade, health e operação

Endpoints alvo: `/api/health/live`, `/api/health/ready`, `/api/health/db`, `/Operacao/Health`, `/Operacao/Logs`, `/Operacao/Metricas`, `/Operacao/Erros`, `/Operacao/Backup`.

Validar banco, storage, worker, migrations, outbox, OCR/IA habilitados ou desabilitados, SMTP, integrações, memória e tempo de resposta.
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
