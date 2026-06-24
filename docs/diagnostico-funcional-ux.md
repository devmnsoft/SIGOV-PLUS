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
