# Diagnóstico funcional e UX — SIGOV PLUS

Data: 2026-06-24. Revisão focada em MVC/Razor, Dapper, Docker, menu, UX premium, rotas e fallback seguro.

## 1. Rotas existentes

Classificação geral: **Parcial**. O projeto possui controllers MVC para autenticação, dashboard, SaaS, segurança, módulos operacionais, ajuda, manual, POC e health. Principais rotas validadas por inspeção:

- `/Auth/Login`, `/Auth/Logout` — **Funcional**: login com cookie, Dapper e auditoria.
- `/Dashboard` — **Parcial**: usa indicadores de banco quando disponível e fallback visual.
- `/MinhaCentral` — **Parcial**: central pós-login já existe, mas ainda usa parte de contexto visual.
- `/Manual` — **Parcial**: manual por perfil existe como experiência guiada.
- `/Poc` — **Parcial**: roteiro de demonstração existe.
- `/Saas/Tenants`, `/Saas/Planos`, `/Saas/Modulos`, `/Saas/Parametros`, `/Saas/Implantacao` — **Parcial**: listagens e telas funcionam com fallback; POSTs foram adicionados para ações SaaS críticas com auditoria/fallback.
- `/Seguranca/Usuarios`, `/Seguranca/Perfis`, `/Seguranca/Permissoes` — **Parcial**: jornada visual existe; precisa persistência total por perfil/permissão.
- `/Auditoria/Trilhas`, `/Lgpd/Dashboard` — **Parcial**: experiência de governança existe.
- `/Operacao/Health` e `/Health` — **Funcional/Parcial**: health visual sem stacktrace, com status amigável.
- `/Modulo/EmImplantacao?codigo=...` — **Funcional** como fallback anti-404 para módulos sem tela real.

Controllers MVC encontrados: 69. Views Razor encontradas: 655.

## 2. Rotas do menu que não existem

Classificação: **Parcial**. O menu foi estruturado para apontar telas reais quando existem e enviar módulos pendentes para `/Modulo/EmImplantacao?codigo=...`. Itens como Varejo/PDV, Atacado, Estoque, Manutenção, Ordem de Serviço, Jurídico, Contratos, White Label, Assinaturas e Checklist estão **Em implantação** em vez de 404.

## 3. Telas com apenas fallback/mock

Classificação: **Demonstrativo** quando não há tabela ou API disponível.

- Planos SaaS: catálogo comparativo visual; persistência completa depende de estrutura comercial definitiva.
- Assinaturas/White Label/Checklist: redirecionadas para página de implantação/roadmap.
- Alguns dashboards operacionais usam `OperationalDemoService` ou services com arrays vazios quando a base não está disponível.
- POC e Manual são telas de demonstração/apoio, não módulos transacionais.

## 4. Telas com cadastro incompleto

Classificação: **Parcial**.

- Tenants: lista com Dapper e dados mascarados; formulário agora possui POST seguro/fallback, mas CRUD completo precisa mapear todas as colunas reais.
- Módulos SaaS: ativar/desativar agora possui POST auditável/fallback; persistência específica em `tenant_modulo_contratado` deve ser consolidada.
- Parâmetros: consulta API visual; POST de fallback criado; edição granular por escopo ainda pendente.
- Segurança/Usuários/Perfis: experiência visual disponível; revisar persistência ponta a ponta, reset de senha e vínculo de permissões.

## 5. Controllers apenas com GET e sem POST

Classificação: **Parcial**. Controllers com ausência de `[HttpPost]` identificada por inspeção estática:

- `AgroBiController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AgroController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AgroPainelComercialController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AgroPublicoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AgroRelatoriosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AgroTransparenciaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AjudaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `AuditoriaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `BetaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `BuscaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `CadastroClienteController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ComercialController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ContratosB2BController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `CoreController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `DashboardController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `DesignSystemController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `DeveloperController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `DiarioOficialController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `EducacaoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `EnterprisePagesControllers.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ExecutivoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `FinanceiroController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `GedController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `GoToMarketController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `HomeController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `IAController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `IndustriaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `IntegracoesController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `JornadaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `LgpdController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ManualController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `MinhaAssinaturaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `MinhaCentralController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `MobileCampoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ModuloController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ModulosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `MonitoramentoB2BController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `NotificacoesController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `OnboardingController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `OperacaoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `OuvidoriaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ParceirosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `PessoasController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `PlaceholderController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `PlanosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `PlanosPublicosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `PocController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `PreferenciasController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ProcessosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ProtocoloController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `ProtocolosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `RegrasNegocioController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `RelatoriosController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `RhController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SaasAdminController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SaasComercialController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SaasConfiguracaoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SaneamentoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SaudeController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SegurancaController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SocialController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `SuporteController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `TenantConfiguracaoController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.
- `TributarioController.cs` — **Parcial/Demonstrativo**: sem actions POST explícitas.


O `SaasController` deixou de ser apenas GET para ações centrais: salvar tenant, implantação, parâmetros e alteração de módulo.

## 6. Services que retornam dados fixos

Classificação: **Demonstrativo/Parcial**.

- `OperationalDemoService` — deve permanecer somente como fallback visual.
- Catálogos locais em `PostBuildSaasService` e serviços de dashboard — úteis para apresentação, mas devem consultar banco quando houver tabela real.
- Alguns repositories retornam `Array.Empty` para listas auxiliares quando a consulta principal existe.

## 7. Views sem formulário funcional

Classificação: **Parcial**.

- `Saas/Tenants` recebeu formulário POST com anti-forgery e confirmação.
- `Saas/Modulos` recebeu POST para ativar/desativar módulo.
- `Saas/Planos` ainda é comparativo visual.
- `Saas/Parametros` ainda prioriza consulta API visual; precisa editor por categoria/escopo.

## 8. JS duplicado ou conflitante

Classificação: **Parcial corrigido**.

Arquivos JS atuais:

- `sigov-assistant.js`
- `sigov-ui.js`
- `sigov.ajax.js`
- `sigov.api.js`
- `sigov.core.js`
- `sigov.errors.js`
- `sigov.forms.js`
- `sigov.grid.js`
- `sigov.masks.js`
- `sigov.mobile.js`
- `sigov.modal.js`
- `sigov.modules.js`
- `sigov.money.js`
- `sigov.onboarding.js`
- `sigov.permissions.js`
- `sigov.preferences.js`
- `sigov.saved-filters.js`
- `sigov.shortcuts.js`
- `sigov.theme.js`
- `sigov.toast.js`
- `sigov.tour.js`
- `sigov.validation.js`


Correções aplicadas:

- Removida duplicidade real `sigov.ui.js`, mantendo `sigov-ui.js` como padrão.
- `service-worker.js` não é carregado como script normal; registro passa por `sigov-ui.js`.
- Em `localhost`, `127.0.0.1` e `[::1]`, service workers do SIGOV são desregistrados.
- Proteção global para `unhandledrejection` mantém log de erros reais e ignora ruído conhecido de extensões.

## 9. CSS duplicado ou desorganizado

Classificação: **Parcial**. CSS está segmentado em tema, layout, componentes, forms, grids e dashboard. Arquivos atuais:

- `sigov-brand.css`
- `sigov-components.css`
- `sigov-dashboard.css`
- `sigov-forms.css`
- `sigov-grids.css`
- `sigov-layout.css`
- `sigov-theme-dark.css`
- `sigov-theme-light.css`
- `site.css`


Recomendação: consolidar tokens visuais e remover regras antigas após inventário de classes em views.

## 10. Módulos realmente funcionais

Classificação: **Funcional/Parcial**.

- Autenticação e auditoria de login.
- Dashboard com consultas Dapper e fallback.
- Catálogo SaaS de módulos e tenants com máscara LGPD.
- Saúde visual do ambiente.
- Módulos com repositórios Dapper reais: Saúde, Educação, Saneamento, Agro, Financeiro, Integrações e RH têm base técnica mais avançada.

## 11. Módulos apenas demonstrativos

Classificação: **Demonstrativo/Em implantação**.

- Jurídico, Contratos, Varejo/PDV, Atacado, Estoque, Manutenção, Ordem de Serviço, White Label e parte do Comercial/CRM.
- POC e roteiro comercial.
- Manual/ajuda contextual são apoio, não transacionais.

## 12. Prioridades de correção

1. **Funcional**: manter build, Docker, login e API saudáveis.
2. **Parcial**: completar CRUD real de usuários, perfis, permissões, tenants, parâmetros, módulos e planos.
3. **Parcial**: trocar fallback visual por consultas reais quando tabela existir.
4. **Parcial**: validar browser sem 404 de asset e sem erro JS próprio em `/Dashboard`.
5. **Demonstrativo**: evoluir módulos de roadmap para telas reais usando Dapper e auditoria.
6. **Parcial**: padronizar todas as ações críticas com modal, toast e TempData.
7. **Parcial**: mascarar dados pessoais em todas as listagens, não apenas tenants.
8. **Parcial**: reforçar LGPD/auditoria em cada jornada principal.

## 13. Plano de correção funcional real

| Área | Rota | Controller | View | Service/repository | Tabela usada | Status atual | O que salva de verdade | O que ainda é fallback | Ação necessária | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|
| Auth/Login/Logout | `/Auth/Login`, `/Auth/Logout` | `AuthController` | `Views/Auth/Login.cshtml` | `NpgsqlConnectionFactory`, `IPasswordHashService` | `sigov.usuario`, `sigov.auditoria_evento` | Funcional | Autenticação cookie e auditoria de login/logout quando tabela existe | Mensagens amigáveis se banco indisponível | Manter e auditar tentativas inválidas com política de bloqueio | Alta |
| Dashboard | `/Dashboard` | `DashboardController` | `Views/Dashboard/Index.cshtml` | `PostBuildSaasService` | `sigov.tenant`, `sigov.usuario`, `sigov.plano_saas`, `sigov.auditoria`, `sigov.parametro_sistema`, `sigov.tenant_modulo_contratado` | Parcial | Indicadores reais quando tabelas existem | Cards e catálogo local quando consulta falha | Consolidar status funcional/parcial/demo por módulo | Alta |
| MinhaCentral | `/MinhaCentral` | `MinhaCentralController` | `Views/MinhaCentral/Index.cshtml` | Serviço atual visual; requer `MinhaCentralService` | Usuário, tenant, auditoria e módulos | Parcial | Contexto visual pós-login | Pendências, recomendações e atividades | Criar service operacional com fallback honesto | Alta |
| Usuários | `/Seguranca/Usuarios` | `SegurancaController` | `Views/Seguranca/Usuarios.cshtml` | `SegurancaAdminService` | `sigov.usuario`, `sigov.tenant`, `sigov.auditoria_evento` | Parcial evoluído | Lista, cria, edita, ativa/inativa e reseta senha com Dapper quando tabela existe | Tenant/perfil exibidos se colunas/tabelas existirem | Ampliar vínculos de perfil e regra de senha temporária por ambiente | Alta |
| Perfis | `/Seguranca/Perfis` | `SegurancaController` | `Views/Seguranca/Perfis.cshtml` | `SegurancaAdminService` | `sigov.perfil`, `sigov.auditoria_evento` | Parcial | Lista/cria perfil quando tabela existe | Permissões associadas se estrutura variar | Mapear tabela definitiva de vínculo perfil-permissão | Alta |
| Permissões | `/Seguranca/Permissoes` | `SegurancaController` | `Views/Seguranca/Permissoes.cshtml` | `SegurancaAdminService` | `sigov.permissao` ou matriz futura | Parcial | Consulta permissões se tabela existir | Matriz visual quando tabela não existe | Implementar persistência de vínculo e auditoria antes/depois | Alta |
| Tenants | `/Saas/Tenants` | `SaasController` | `Views/Saas/Tenants.cshtml` | `PostBuildSaasService` | `sigov.tenant`, `sigov.auditoria_evento` | Parcial | Lista real com máscara; POST atual audita/fallback | Campos comerciais completos e edição granular | Implementar CRUD completo por id e validação slug | Alta |
| Planos | `/Saas/Planos` | `SaasController` | `Views/Saas/Planos.cshtml` | `PostBuildSaasService` | `sigov.plano_saas` | Demonstrativo/parcial | Indicador no dashboard quando tabela existe | Catálogo visual sem persistência | Não exibir salvar falso; criar CRUD após tabela definitiva | Média |
| Módulos | `/Saas/Modulos` | `SaasController` | `Views/Saas/Modulos.cshtml` | `PostBuildSaasService` | `sigov.tenant_modulo_contratado` | Parcial | Lista status contratado quando tabela existe; auditoria visual | Ativar/desativar não garante persistência se tabela faltar | Implementar upsert real por tenant e status | Alta |
| Parâmetros | `/Saas/Parametros` | `SaasController` | `Views/Saas/Parametros.cshtml` | `TenantParameterRepository`/`PostBuildSaasService` | `sigov.parametro_sistema`, valores por escopo | Parcial | Consulta em repositório dedicado quando disponível | Edição visual em tela SaaS | Criar editor por tipo/escopo com auditoria | Alta |
| Auditoria | `/Auditoria/Trilhas` | `AuditoriaController` | `Views/Auditoria/*` | `AuditRepository`/serviços existentes | `sigov.trilha_auditoria`, `sigov.auditoria_evento` | Parcial | Eventos técnicos em ações principais | Detalhes/modal e filtros avançados | Consolidar `AuditTrailService` único | Alta |
| LGPD | `/Lgpd/Dashboard` | `LgpdController` | `Views/Lgpd/*` | Serviços LGPD | Tabelas LGPD e auditoria | Parcial | Avisos e algumas consultas | Workflows de incidente/consentimento | Integrar máscaras e autorização por perfil | Alta |
| Health | `/Operacao/Health`, `/Health` | `OperacaoController`, health API | `Views/Operacao/Health.cshtml` | `PostBuildSaasService` | PostgreSQL, API, storage | Funcional/parcial | Status visual do ambiente | Versão/migrations detalhadas | Incluir worker/storage/migrations reais | Média |
| Manual | `/Manual` | `ManualController` | `Views/Manual/*` | Conteúdo MVC | Não transacional | Parcial | Conteúdo orientativo | Ajuda por tela ainda incompleta | Expandir manual por perfil e botões de ajuda | Média |
| POC | `/Poc` | `PocController` | `Views/Poc/*` | Catálogos/demo | Não transacional | Parcial | Roteiro comercial | Parte dos módulos demo | Separar funcional/parcial/demo em checklist | Média |
| Protocolo | `/Protocolo`, `/Protocolos` | Controllers específicos | Views do módulo | Repositórios de processos/protocolo | `sigov.processo_digital` e correlatas | Parcial | Cadastros técnicos existentes em Dapper | Telas ainda podem estar demo | Revisar fluxo ponta a ponta | Média |
| GED | `/Ged` | `GedController` | Views GED | Services/storage | Tabelas GED/storage | Demonstrativo/parcial | Storage em partes do sistema | Jornada documental | Implementar CRUD documental completo | Média |
| Tributário | `/Tributario` | `TributarioController` | Views tributário | Repositórios do módulo | Tabelas tributárias | Parcial/demo | Consultas quando existentes | Cadastros completos | Priorizar fluxo arrecadação/dívida | Média |
| Contratos | `/Contratos` | Controllers contratos | Views contratos | Services futuros | Tabelas contratos | Em implantação | Nada crítico confirmado | Cards/rotas roadmap | Criar modelo e CRUD mínimo | Baixa |
| Jurídico | `/Juridico` | Controllers jurídico | Views jurídico | Services futuros | Tabelas jurídico | Em implantação | Nada crítico confirmado | Cards/rotas roadmap | Criar fluxo de processos/pareceres | Baixa |
| Financeiro | `/Financeiro` | `FinanceiroController` | Views financeiro | Repositórios financeiros | Tabelas financeiras | Parcial | Alguns repositórios Dapper | Dashboards demo | Validar cadastros principais | Média |
| Saúde | `/Saude` | `SaudeController` | Views saúde | Repositórios saúde | Tabelas saúde | Parcial | Base técnica Dapper | Dashboards/BI demo | Completar cadastros assistenciais | Média |
| Educação | `/Educacao` | `EducacaoController` | Views educação | Repositórios educação | Tabelas educação | Parcial | Base técnica Dapper | Dashboards demo | Completar matrícula/frequência | Média |
| Saneamento | `/Saneamento` | `SaneamentoController` | Views saneamento | Repositórios saneamento | Tabelas saneamento | Parcial | Base técnica Dapper | BI e alguns cadastros | Completar ordens/leituras | Média |
| RH | `/Rh` | `RhController` | Views RH | `RhServices` | Tabelas RH | Parcial | Services Dapper | Fluxos de folha avançados | Completar vínculo/lotação | Média |
| Agro | `/Agro` | Controllers Agro | Views Agro | Repositórios Agro | Tabelas Agro | Parcial | Cadastros/geo/relatórios em Dapper | BI/comercial demo | Validar jornadas principais | Média |
| Integrações | `/Integracoes` | `IntegracoesController` | Views integrações | `IntegracoesApplication` | APIs, webhooks, outbox | Parcial | Credenciais/webhooks/outbox em Dapper | Adaptadores dev | Endurecer produção e permissões | Alta |
