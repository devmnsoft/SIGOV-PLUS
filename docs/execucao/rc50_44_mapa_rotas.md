# RC50.44 — Mapa de rotas API, Web e menu

## Diagnóstico
- Validador estático: **575 rotas API, nenhum conflito direto**.
- Rotas com controller/action inexistente: não confirmadas automaticamente; links dinâmicos e rotas convencionais exigem smoke test.
- Rotas planejadas: ver blocos e prompts RC50.45–52.

## API por controller

| Controller | Bases declaradas | operações HTTP |
|---|---|---:|
| `AgroRelatoriosController.cs` | `(atributo/local)` | 8 |
| `AgroDashboardController.cs` | `api/agro/dashboard` | 1 |
| `SystemHealthController.cs` | `api/system-health` | 1 |
| `RhController.cs` | `api/rh` | 9 |
| `RhBloco2Controller.cs` | `api/rh` | 50 |
| `OuvidoriaController.cs` | `api/ouvidoria` | 6 |
| `SaasPlanosController.cs` | `(atributo/local)` | 10 |
| `ModuleParametersController.cs` | `api/parametros` | 3 |
| `SaasWhiteLabelController.cs` | `(atributo/local)` | 5 |
| `EducacaoControllers.cs` | `api/educacao/escolas, api/educacao/anos-letivos, api/educacao/cursos, api/educacao/turmas, api/educacao/alunos` | 48 |
| `RhTypedController.cs` | `api/rh` | 23 |
| `AgroPropriedadesController.cs` | `(atributo/local)` | 11 |
| `BusinessRulesController.cs` | `api/regras-negocio, api/business-rules` | 2 |
| `SaasTenantsController.cs` | `api/saas/tenants` | 5 |
| `SaasCadastroClienteController.cs` | `(atributo/local)` | 6 |
| `AgroPatrulhaMecanizadaController.cs` | `(atributo/local)` | 18 |
| `IntegracoesControllers.cs` | `api/integracoes/api-credentials, api/integracoes/sistemas, api/integracoes/webhooks, api/integracoes/outbox, api/integracoes/remessas` | 44 |
| `TenantParametersController.cs` | `api/saas/parametros` | 3 |
| `AtendimentoPublicoController.cs` | `(atributo/local)` | 4 |
| `ModuleCatalogController.cs` | `api/ui/modulos, api/ui/module-catalog` | 3 |
| `OperacaoController.cs` | `api/operacao` | 7 |
| `AgroGeoController.cs` | `api/agro/geo` | 11 |
| `AgroProducaoController.cs` | `(atributo/local)` | 5 |
| `ProtocolosController.cs` | `api/protocolos` | 6 |
| `LegislativoController.cs` | `(atributo/local)` | 7 |
| `IaController.cs` | `api/ia` | 42 |
| `MobileCampoController.cs` | `(atributo/local)` | 47 |
| `AgroPainelComercialController.cs` | `(atributo/local)` | 0 |
| `AssinaturasController.cs` | `(atributo/local)` | 3 |
| `Bloco6Controllers.cs` | `api/bloco6/compras, api/bloco6/contratos, api/bloco6/almoxarifado, api/bloco6/patrimonio` | 15 |
| `AgroProdutoresController.cs` | `(atributo/local)` | 5 |
| `SaasAssinaturasController.cs` | `(atributo/local)` | 9 |
| `AgroBiController.cs` | `(atributo/local)` | 4 |
| `SaudeControllers.cs` | `api/saude/unidades, api/saude/profissionais, api/saude/pacientes, api/saude/prontuarios, api/saude/atendimentos` | 54 |
| `ProcessosControllerBase.cs` | `(atributo/local)` | 0 |
| `IndustriaController.cs` | `api/industria` | 49 |
| `OperationalImportsController.cs` | `(atributo/local)` | 4 |
| `AuditoriaController.cs` | `api/auditoria` | 3 |
| `FrotasController.cs` | `(atributo/local)` | 4 |
| `WorkflowsOperacionaisController.cs` | `api/workflows` | 7 |
| `ExecutiveOperationsController.cs` | `api/governanca-operacional, api/notificacoes, api/integracoes-internas, api/qualidade-dados, api/assistentes-operacionais` | 24 |
| `GedController.cs` | `api/ged` | 16 |
| `AgroFeirasController.cs` | `(atributo/local)` | 12 |
| `DiarioOficialController.cs` | `api/diario-oficial/publicacoes` | 7 |
| `ObrasController.cs` | `(atributo/local)` | 6 |
| `AgroProgramasController.cs` | `(atributo/local)` | 21 |
| `AtendimentoDigitalController.cs` | `(atributo/local)` | 3 |
| `SocialControllers.cs` | `api/social/unidades, api/social/familias, api/social/pessoas, api/social/programas, api/social/beneficios` | 49 |
| `ComercioController.cs` | `api/comercio` | 47 |
| `AgroComprasAgriculturaFamiliarController.cs` | `(atributo/local)` | 5 |
| `AgroAgroindustriasController.cs` | `(atributo/local)` | 8 |
| `ExecutivoDashboardController.cs` | `api/ui/executivo/dashboard, api/executivo/dashboard` | 1 |
| `OnboardingController.cs` | `api/onboarding, api/ui/onboarding` | 2 |
| `OperationalAlertsController.cs` | `api/alertas-operacionais` | 2 |
| `ProtocoloExternoController.cs` | `(atributo/local)` | 3 |
| `TransparenciaController.cs` | `(atributo/local)` | 5 |
| `SaasPerfilTemplatesController.cs` | `(atributo/local)` | 3 |
| `RelatoriosExecutivosController.cs` | `(atributo/local)` | 9 |
| `ProcessosDigitaisController.cs` | `api/processos` | 9 |
| `SaasModulesController.cs` | `api/saas` | 8 |
| `UserPreferencesController.cs` | `api/ui/preferencias, api/ui/preferences` | 4 |
| `AgroInfraestruturaRuralController.cs` | `(atributo/local)` | 19 |
| `HealthController.cs` | `api/health` | 10 |
| `TiposProcessoController.cs` | `api/processos/tipos` | 5 |
| `TributarioController.cs` | `api/tributario` | 37 |
| `LgpdController.cs` | `api/lgpd` | 3 |
| `SaneamentoControllers.cs` | `api/saneamento/consumidores, api/saneamento/ligacoes, api/saneamento/unidades-consumidoras, api/saneamento/hidrometros, api/saneamento/leituras` | 46 |
| `AgroDadosAbertosController.cs` | `(atributo/local)` | 4 |
| `EducacaoBloco3Controllers.cs` | `api/educacao/secretaria, api/educacao/diario-classe, api/educacao/portal` | 41 |
| `SegurancaController.cs` | `api/seguranca` | 3 |
| `TenantContextController.cs` | `api/saas/contexto` | 3 |
| `SaasTenantComercialController.cs` | `(atributo/local)` | 14 |
| `ProcessosDigitaisBloco8Controller.cs` | `(atributo/local)` | 6 |
| `AgroTransparenciaController.cs` | `(atributo/local)` | 4 |
| `IndustriaComercialController.cs` | `api/comercio` | 1 |
| `EnterpriseModulesController.cs` | `(atributo/local)` | 103 |
| `PessoasController.cs` | `api/pessoas` | 9 |
| `OperacaoHealthController.cs` | `api/operacao/health` | 1 |
| `SaasProfilesController.cs` | `api/saas` | 2 |
| `FinanceiroComercialController.cs` | `api/financeiro/contas-receber` | 8 |
| `FinanceiroControllers.cs` | `api/financeiro/plano-contas, api/financeiro/fontes-recurso, api/financeiro/programas, api/financeiro/acoes, api/financeiro/naturezas-despesa` | 90 |

## Web por controller

| Controller | Rotas explícitas (amostra) | total explícito |
|---|---|---:|
| `SaneamentoController.cs` | `/Saneamento/Hidrometros, /Saneamento/Hidrometros/Novo, /Saneamento/Hidrometros/{id:long}, /Saneamento/Consumidores/Novo` | 4 |
| `AgroRelatoriosController.cs` | `rota convencional` | 0 |
| `SystemHealthController.cs` | `/SystemHealth/ProjectStatus` | 1 |
| `FinanceiroController.cs` | `rota convencional` | 0 |
| `ComercialController.cs` | `rota convencional` | 0 |
| `RhController.cs` | `rota convencional` | 0 |
| `TenantConfiguracaoController.cs` | `rota convencional` | 0 |
| `OuvidoriaController.cs` | `rota convencional` | 0 |
| `InventarioController.cs` | `/Inventario, /Inventario/Campanhas, /Inventario/Campanhas/Nova, /Inventario/Divergencias, /Inventario/Relatorios` | 9 |
| `SelfServiceController.cs` | `rota convencional` | 0 |
| `DeveloperController.cs` | `rota convencional` | 0 |
| `PlaceholderController.cs` | `/Placeholder/{modulo}, /Implantacao/{modulo}` | 2 |
| `OperationalTransversalController.cs` | `/Tarefas, /Tarefas/Minhas, /Tarefas/Abertas, /Tarefas/Vencidas, /Tarefas/Equipe` | 44 |
| `EnterprisePagesControllers.cs` | `rota convencional` | 0 |
| `BiSetorialController.cs` | `rota convencional` | 0 |
| `PlanejamentoController.cs` | `rota convencional` | 0 |
| `ContratosController.cs` | `rota convencional` | 0 |
| `IAController.cs` | `/Ia, /Ia/Assistentes, /Ia/Logs, /Ia/Politicas, /Ia/Assistente/Sugerir` | 12 |
| `ManualController.cs` | `rota convencional` | 0 |
| `AssinaturasDigitaisController.cs` | `/AssinaturasDigitais, /AssinaturasDigitais/Solicitacoes, /AssinaturasDigitais/Nova, /AssinaturasDigitais/{id:long}, /AssinaturasDigitais/Nova` | 8 |
| `PlanosPublicosController.cs` | `rota convencional` | 0 |
| `PortalController.cs` | `/Portal, /Portal/MinhaAssinatura, /Portal/MeusModulos, /Portal/Usuarios, /Portal/Suporte` | 6 |
| `OperacaoController.cs` | `/Operacao/Logs, /Operacao/AuditoriaTecnica, /Operacao/Metricas, /Operacao/ApiLogs, /Operacao/Webhooks` | 14 |
| `RegrasNegocioController.cs` | `rota convencional` | 0 |
| `ProtocolosController.cs` | `rota convencional` | 0 |
| `LegislativoController.cs` | `rota convencional` | 0 |
| `MobileCampoController.cs` | `rota convencional` | 0 |
| `PatrimonioController.cs` | `rota convencional` | 0 |
| `AgroPainelComercialController.cs` | `rota convencional` | 0 |
| `AssinaturasController.cs` | `rota convencional` | 0 |
| `AuthController.cs` | `Auth/EsqueciSenha, Auth/EsqueciMinhaSenha, Auth/EsqueciSenha, Auth/EsqueciMinhaSenha, Auth/SolicitacaoEnviada` | 12 |
| `CadastroClienteController.cs` | `rota convencional` | 0 |
| `AgroBiController.cs` | `rota convencional` | 0 |
| `OperationalModulesController.cs` | `/{module}/BulkAction, /{module}/ExportCsv` | 2 |
| `IndustriaController.cs` | `rota convencional` | 0 |
| `ComprasEmpresariaisController.cs` | `Fornecedores, Fornecedores/Novo, Fornecedores/{id:guid}, Fornecedores/{id:guid}/Editar, Requisicoes` | 23 |
| `AuditoriaController.cs` | `rota convencional` | 0 |
| `WhiteLabelB2BController.cs` | `rota convencional` | 0 |
| `SaudeController.cs` | `/Saude/Pacientes/Novo` | 1 |
| `TesourariaController.cs` | `rota convencional` | 0 |
| `FrotasController.cs` | `rota convencional` | 0 |
| `ExecutiveOperationsController.cs` | `/GovernancaOperacional, /IntegracoesInternas, /QualidadeDados, /BuscaGlobal, /Favoritos` | 15 |
| `GedController.cs` | `/Ged, /Ged/Dashboard, /Ged/Documentos, /Ged/Pendentes, /Ged/Lixeira` | 13 |
| `DiarioOficialController.cs` | `rota convencional` | 0 |
| `ObrasController.cs` | `rota convencional` | 0 |
| `AgroController.cs` | `/Agro/Programas, /Agro/Patrulha` | 2 |
| `AtendimentoDigitalController.cs` | `rota convencional` | 0 |
| `ContratosB2BController.cs` | `rota convencional` | 0 |
| `DashboardController.cs` | `rota convencional` | 0 |
| `MarketplaceController.cs` | `/Marketplace, /Marketplace/{categoria}, /Marketplace/Modulo/{codigo}` | 3 |
| `AjudaController.cs` | `rota convencional` | 0 |
| `JuridicoController.cs` | `rota convencional` | 0 |
| `BuscaController.cs` | `/Busca/Sugestoes, /Busca` | 2 |
| `PlanosController.cs` | `rota convencional` | 0 |
| `OnboardingController.cs` | `rota convencional` | 0 |
| `MinhaAssinaturaController.cs` | `rota convencional` | 0 |
| `ExecutivoController.cs` | `rota convencional` | 0 |
| `SocialController.cs` | `/Social/Familias/Nova` | 1 |
| `ProtocoloExternoController.cs` | `rota convencional` | 0 |
| `LicitacoesController.cs` | `rota convencional` | 0 |
| `ModuloController.cs` | `/Modulo/EmImplantacao, /OrdemServico/{*path}, /Industrial/{*path}, /Estoque/{*path}, /Varejo/{*path}` | 8 |
| `ModulosController.cs` | `rota convencional` | 0 |
| `AdministrativeExportController.cs` | `/{module}/ExportCsv` | 1 |
| `SaasAdminController.cs` | `rota convencional` | 0 |
| `QuickCreateController.cs` | `/QuickCreate/Iniciar` | 1 |
| `ContractOperationControllers.cs` | `/Implantacao, /Implantacao/Projetos, /Implantacao/Projetos/Novo, /Implantacao/Projetos/{id:long}, /Implantacao/Projetos/{id:long}/Etapas` | 54 |
| `NotificacoesController.cs` | `/Notificacoes, /Notificacoes/{id:long}/MarcarLida, /Notificacoes/MarcarTodasLidas` | 3 |
| `IntegracoesController.cs` | `/Integracoes, /Integracoes/Conectores, /Integracoes/Reprocessar, /Integracoes/Logs, /Integracoes/Webhooks` | 10 |
| `TransparenciaController.cs` | `rota convencional` | 0 |
| `AcsController.cs` | `rota convencional` | 0 |
| `AlertasOperacionaisController.cs` | `rota convencional` | 0 |
| `EducacaoController.cs` | `/Educacao, /Educacao/Dashboard, /Educacao/Escolas, /Educacao/Turmas, /Educacao/Alunos` | 17 |
| `RelatoriosExecutivosController.cs` | `rota convencional` | 0 |
| `ProcessosDigitaisController.cs` | `rota convencional` | 0 |
| `SaasController.cs` | `Saas/Tenants/Novo, Saas/Tenants/Novo, Saas/Tenants/{id:long}, Saas/Tenants/{id:long}/Editar, Saas/Tenants/{id:long}/Editar` | 30 |
| `SaasConfiguracaoController.cs` | `rota convencional` | 0 |
| `GisController.cs` | `rota convencional` | 0 |
| `MonitoramentoB2BController.cs` | `rota convencional` | 0 |
| `WorkflowController.cs` | `/Workflow, /Workflow/Definicoes, /Workflow/Definicoes/Nova, /Workflow/Definicoes/Nova, /Workflow/Instancias` | 10 |
| `ProtocoloController.cs` | `/Protocolo, /Protocolo/Processos, /Protocolo/MinhasPendencias, /Protocolo/Meus, /Protocolo/Pendentes` | 17 |
| `GoToMarketController.cs` | `rota convencional` | 0 |
| `SaasComercialController.cs` | `rota convencional` | 0 |
| `ParceirosController.cs` | `rota convencional` | 0 |
| `DevAuthController.cs` | `Status, ResetAdmin, TestLogin` | 3 |
| `PreferenciasController.cs` | `/Perfil/Preferencias, /Preferencias, /Perfil/Preferencias, /Perfil/Preferencias/Restaurar` | 4 |
| `BetaController.cs` | `rota convencional` | 0 |
| `CoreController.cs` | `rota convencional` | 0 |
| `MinhaContaController.cs` | `/Perfil, /MinhaConta` | 2 |
| `ProcessosController.cs` | `rota convencional` | 0 |
| `AtendimentoController.cs` | `rota convencional` | 0 |
| `TributarioController.cs` | `/Tributario/DividaAtiva` | 1 |
| `LgpdController.cs` | `rota convencional` | 0 |
| `MinhaCentralController.cs` | `rota convencional` | 0 |
| `AgroPublicoController.cs` | `rota convencional` | 0 |
| `DesignSystemController.cs` | `/DesignSystem` | 1 |
| `SegurancaController.cs` | `Seguranca/Usuarios/Novo, Seguranca/Usuarios/Novo, Seguranca/Usuarios/{id:long}, Seguranca/Usuarios/{id:long}/Editar, Seguranca/Usuarios/{id:long}/Editar` | 25 |
| `ApiV1Controller.cs` | `/api/v1/health, /api/v1/{resource}` | 2 |
| `AlmoxarifadoController.cs` | `rota convencional` | 0 |
| `ValidacaoDocumentoController.cs` | `/ValidarDocumento, /ValidarDocumento, /ValidarDocumento/{codigo}` | 3 |
| `TecnicoController.cs` | `/Tecnico, /Tecnico/Ordens, /Tecnico/Ordens/{id:guid}` | 3 |
| `AgroTransparenciaController.cs` | `rota convencional` | 0 |
| `RelatoriosController.cs` | `/Relatorios/UsuariosCsv, /Relatorios/TenantsCsv, /Relatorios/ModulosCsv, /Relatorios/ContratualCsv/{table}, /Relatorios/AuditoriasCsv` | 14 |
| `EditaisPocControllers.cs` | `/Editais, /Editais/Novo, /Editais/Novo, /Editais/{id:long}, /Editais/{id:long}/Editar` | 48 |
| `SiaficController.cs` | `rota convencional` | 0 |
| `PortalContribuinteController.cs` | `rota convencional` | 0 |
| `HomeController.cs` | `rota convencional` | 0 |
| `PessoasController.cs` | `rota convencional` | 0 |
| `ComprasController.cs` | `rota convencional` | 0 |
| `JornadaController.cs` | `rota convencional` | 0 |
| `PortalCidadaoController.cs` | `/PortalCidadao/NovaSolicitacao` | 1 |
| `WorkflowsController.cs` | `MinhasTarefas, Detalhe/{id:long}, Novo, Novo, Editar/{id:long}` | 10 |

## Rotas de menu
Os `asp-controller`, `asp-action` e `href` estão distribuídos em `Views/Shared` e views modulares; validar autenticado. O novo status é `/SystemHealth/ProjectStatus`.

## Blocos 1–9
- **Bloco 1:** Educação, RH e Folha; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 2:** Ponto, Férias/Afastamentos e Portal do Servidor; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 3:** Secretaria, Diário e Portal Aluno; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 4:** núcleos transversais/relatórios; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 5:** Financeiro/SIAFIC e Tributário; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 6:** Compras, Contratos, Almoxarifado e Patrimônio; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 7:** Saúde, Social, Saneamento, Frotas e Obras; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 8:** Processos, Protocolo, GED, Assinaturas e governo digital; controllers inventariados acima; pendências executáveis nos prompts.
- **Bloco 9:** CRM, OS, estoque, compras e indústria; controllers inventariados acima; pendências executáveis nos prompts.

## Pendências e riscos
- Não há duplicidade estática confirmada; Swagger/runtime continuam pendentes pela ausência de `dotnet`.
- Links convencionais devem ser percorridos com sessão admin antes de declarar fechamento.
- Rotas planejadas avançadas são as descritas em RC50.45–RC50.52.
