# QA funcional do release candidate v1.0.0-rc.1

Este checklist registra validação funcional mínima por módulo existente no repositório. Fluxos parciais foram marcados como pendência real para não inventar sucesso.

| Módulo | Fluxo mínimo | Evidência automatizada/semi-executável | Status RC |
|---|---|---|---|
| Core | Criar, editar, excluir pessoa e consultar auditoria | `tests/Sigov.UnitTests/Core/PessoaRulesTests.cs`, `tests/Sigov.IntegrationTests/TenantIsolationFullRegressionTests.cs` | Parcial automatizado; E2E com banco pendente. |
| Segurança | Login, usuários, perfis, permissões e bloqueio | `tests/Sigov.ApiTests/AuthPermissionRegressionTests.cs`, `tests/Sigov.ApiTests/ModuleAccessRegressionTests.cs` | Regressão estática/serviço; HTTP autenticado completo pendente. |
| SaaS | Tenant, contexto, suspensão/cancelamento | `tests/Sigov.ApiTests/ModuleAccessRegressionTests.cs` | Guard validado; fluxo E2E pendente. |
| Auditoria/LGPD | Máscara, acesso pessoal, trilha antes/depois | `tests/Sigov.UnitTests/Lgpd/LgpdMaskingRegressionTests.cs` | Máscara validada; auditoria E2E pendente. |
| Processos | Criar, movimentar, parecer, encerrar | `tests/Sigov.UnitTests/Processos/ProcessoDigitalRulesTests.cs` | Parcial. |
| Financeiro | Orçamento, empenho, liquidação, pagamento, saldo | `tests/Sigov.UnitTests/Financeiro/FinanceiroRulesTests.cs` | Parcial. |
| Tributário | Contribuinte, lançamento, parcela, DAM dev, pagamento dev | `tests/Sigov.IntegrationTests/DatabaseMigrationRegressionTests.cs` | Estrutural/parcial. |
| Compras | Fornecedor, solicitação, aprovação, contrato, medição | `tests/Sigov.IntegrationTests/DatabaseMigrationRegressionTests.cs` | Estrutural/parcial. |
| RH | Servidor, folha, lançamento, integração outbox | `tests/Sigov.IntegrationTests/RhModuleSmokeTests.cs`, `tests/Sigov.UnitTests/Rh/RhTypedServiceTests.cs` | Parcial automatizado. |
| Educação | Escola, aluno, matrícula, frequência, nota | `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs`, `tests/Sigov.ApiTests/EducacaoApiTests.cs` | Parcial automatizado. |
| Saúde | Paciente, atendimento, prontuário, ACS | `tests/Sigov.IntegrationTests/SaudeModuleSmokeTests.cs`, `tests/Sigov.ApiTests/SaudeApiTests.cs` | Parcial automatizado. |
| Saneamento | Consumidor, leitura, fatura, pagamento dev | `tests/Sigov.IntegrationTests/SaneamentoModuleSmokeTests.cs`, `tests/Sigov.ApiTests/SaneamentoApiTests.cs` | Parcial automatizado. |
| Social | Família, atendimento, benefício, visita | `tests/Sigov.IntegrationTests/SocialModuleSmokeTests.cs`, `tests/Sigov.ApiTests/SocialApiTests.cs` | Parcial automatizado. |
| Relatórios/BI | Fonte, modelo, execução, exportação | `tests/Sigov.IntegrationTests/DatabaseMigrationRegressionTests.cs` | Estrutural/parcial. |
| Integrações | API credential, webhook, outbox | `tests/Sigov.IntegrationTests/IntegracoesModuleSmokeTests.cs`, `tests/Sigov.IntegrationTests/WorkerRegressionTests.cs` | Parcial automatizado. |
| Suporte | Chamado, interação, resolver, satisfação | `tests/Sigov.IntegrationTests/DatabaseMigrationRegressionTests.cs` | Estrutural/parcial. |
| Operação | Health, backup, runbook | `tests/Sigov.ApiTests/ApiContractRegressionTests.cs`, `scripts/go-live-check.ps1` | Health/version automatizado; Docker local pendente neste ambiente. |

## Scripts para execução do QA

1. `dotnet restore sigov.sln`
2. `dotnet build sigov.sln`
3. `dotnet test sigov.sln`
4. `pwsh scripts/check-module-map.ps1`
5. `pwsh scripts/check-web-assets.ps1`
6. `pwsh scripts/check-residues.ps1`
7. `pwsh scripts/security-check.ps1`
8. `pwsh scripts/go-live-check.ps1`
9. `docker compose config`
10. `docker compose build`
