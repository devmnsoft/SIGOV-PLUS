# Cobertura de auditoria por módulos

Ações críticas que precisam de trilha: login, logout, falha de login, usuário/permissão, consulta de dado pessoal, exportação, download, upload, protocolo, tramitação, documento, contrato, aceite formal, parâmetro, API key, IA, assinatura e validação documental.

| Área | Cobertura RC | Observação |
|---|---|---|
| Autenticação e permissões | Obrigatória | Falhas devem ir para log técnico com correlationId. |
| Dados pessoais/LGPD | Obrigatória | Consultas/exportações auditadas. |
| Operações documentais | Parcial | Fallback técnico quando schema/provedor ausente. |
| IA/Assinatura/Integrações | Em implantação | Uso somente com configuração explícita. |

## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
