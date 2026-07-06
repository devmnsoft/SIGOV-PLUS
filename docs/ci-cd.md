# CI/CD

Workflow mínimo em `.github/workflows/ci.yml`: checkout, setup .NET, restore, build, test, docker build de API/Web/Worker e publicação de resultados TRX/logs quando aplicável.

Segurança: não expor secrets; usar variáveis do GitHub Actions; banco PostgreSQL efêmero para testes.
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
