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

Atualização: 2026-06-26. Este plano prioriza consolidar o núcleo funcional real antes de expandir telas mockadas. Quando a tabela/coluna não existir, a UI deve exibir limitação honesta e não simular sucesso.

| Área | Rota | Controller | View | Service/repository | Tabela usada | Status atual | O que salva de verdade | O que ainda é fallback | Ação necessária | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|
|Auth/Login/Logout|/Auth/Login; /Auth/Logout|AuthController|Views/Auth/Login.cshtml|NpgsqlConnectionFactory; IPasswordHashService|sigov.usuario; sigov.auditoria_evento|Funcional|Cookie de autenticação e trilha de login/logout quando auditoria existe|Bloqueio progressivo e métricas de tentativas inválidas|Manter compatibilidade e ampliar política anti-brute-force|Alta|
|Dashboard|/Dashboard|DashboardController|Views/Dashboard/Index.cshtml|PostBuildSaasService|sigov.tenant; sigov.usuario; sigov.plano_saas; sigov.parametro_sistema|Parcial|Indicadores consultados por Dapper quando tabelas existem|Cards permanecem visuais quando consultas falham|Trocar cada card por consulta real validada por tabela/coluna|Alta|
|MinhaCentral|/MinhaCentral|MinhaCentralController|Views/MinhaCentral/Index.cshtml|Serviços atuais de contexto/demo|sigov.usuario; sigov.tenant; auditoria; módulos|Parcial|Dados básicos do usuário autenticado quando disponíveis|Pendências, onboarding e alertas podem ser visuais|Criar MinhaCentralService operacional com fallback honesto|Alta|
|Usuários|/Seguranca/Usuarios|SegurancaController|Views/Seguranca/Usuarios.cshtml; UsuarioDetalhe.cshtml|SegurancaAdminService|sigov.usuario; sigov.tenant; sigov.auditoria_evento|Parcial avançado|Lista, busca, criação, edição, status e reset persistem quando colunas existem|Tenant/perfil dependem do schema instalado; auditoria é best-effort|Validar em Docker e ajustar SQL às colunas reais|Crítica|
|Perfis|/Seguranca/Perfis|SegurancaController|Views/Seguranca/Perfis.cshtml|SegurancaAdminService|sigov.perfil; sigov.permissao; sigov.perfil_permissao|Parcial|CRUD de perfil persiste quando sigov.perfil existe|Vínculo de permissões depende das tabelas definitivas|Completar matriz e persistência transacional de permissões|Alta|
|Permissões|/Seguranca/Permissoes|SegurancaController|Views/Seguranca/Permissoes.cshtml|SegurancaAdminService|sigov.permissao; sigov.perfil_permissao|Parcial/Demonstrativo|Nenhuma alteração deve informar sucesso sem persistir|Matriz genérica quando estrutura não existe|Adicionar POST real por perfil e mensagem de limitação honesta|Alta|
|Tenants|/Saas/Tenants|SaasController|Views/Saas/Tenants.cshtml|PostBuildSaasService|sigov.tenant; sigov.auditoria_evento|Parcial avançado|Lista e criação/edição persistem quando colunas existem|Alguns metadados ficam em JSON; edição ainda precisa tela dedicada|Adicionar rotas REST sugeridas e validar schema Docker|Crítica|
|Planos|/Saas/Planos|SaasController|Views/Saas/Planos.cshtml|PostBuildSaasService/catálogo local|sigov.plano_saas quando existir|Demonstrativo|Nada deve salvar até tabela definitiva estar validada|Catálogo comparativo comercial|Ocultar botões de salvar falso e implementar CRUD quando tabela existir|Média|
|Módulos|/Saas/Modulos|SaasController|Views/Saas/Modulos.cshtml|PostBuildSaasService|sigov.tenant_modulo_contratado|Parcial|Ativa/desativa por tenant quando tabela/chave existem|Catálogo local DefaultModules|Padronizar status Funcional/Parcial/Demo/Implantação|Alta|
|Parâmetros|/Saas/Parametros|SaasController|Views/Saas/Parametros.cshtml|API SaaS; PostBuildSaasService|sigov.parametro_sistema|Parcial|Auditoria visual/best-effort|Edição granular ainda via API/fallback|Criar editor Dapper por categoria/escopo|Alta|
|Auditoria|/Auditoria/Trilhas|AuditoriaController|Views/Auditoria/*|Serviços/repositories existentes|sigov.auditoria_evento; sigov.auditoria|Parcial|Login e ações críticas tentam gravar evento|Consulta pode cair para visual|Consolidar AuditTrailService com IP, user-agent e correlation_id|Alta|
|LGPD|/Lgpd/Dashboard|LgpdController|Views/Lgpd/*|Serviços existentes|Tabelas de consentimento/solicitações quando existirem|Parcial|Máscaras em listagens principais|Fluxos formais de solicitação do titular|Criar trilhas reais e controles de permissão para detalhe|Alta|
|Health|/Operacao/Health; /Health|OperacaoController; Health/API|Views/Operacao/Health.cshtml|PostBuildSaasService; Health checks API|Web/API/PostgreSQL/migrations/storage|Parcial/Funcional|Health visual e API live|Alguns itens são estimados no MVC|Adicionar probes reais e ocultar stacktrace fora de Development|Alta|
|Manual|/Manual|ManualController|Views/Manual/Index.cshtml|Conteúdo estático/guiado|Não aplicável|Parcial|Manual renderiza por perfil como conteúdo|Sem persistência/telemetria de ajuda|Expandir ajuda de tela em modais contextualizados|Média|
|POC|/Poc|PocController|Views/Poc/Index.cshtml|Catálogos/health/demo|Não aplicável|Parcial|Roteiro comercial renderiza|Pode misturar demo com funcional se não sinalizado|Separar listas funcional/parcial/demo/implantação|Média|
|Protocolo|/Protocolo|ProtocoloController|Views/Protocolo/*|OperationalDemoService/repositories|Tabelas de protocolo quando existirem|Demonstrativo/Parcial|Consultas se serviço real existir|Fluxos transacionais incompletos|Mapear tabelas reais e CRUD mínimo|Média|
|GED|/Ged|GedController|Views/Ged/*|OperationalDemoService/repositories|Tabelas GED/documentos|Demonstrativo/Parcial|Nada crítico garantido|Uploads/armazenamento podem ser demo|Validar storage, metadados e auditoria|Média|
|Tributário|/Tributario|TributarioController|Views/Tributario/*|Repositories Dapper operacionais|Tabelas tributárias|Parcial|Indicadores/consultas quando tabelas existem|Cadastros avançados|Completar CRUDs prioritários e relatórios|Média|
|Contratos|/ContratosB2B; /Modulo/EmImplantacao?codigo=contratos|ContratosB2BController|Views/ContratosB2B/*|OperationalDemoService|Tabelas de contratos futuras|Em implantação|Nada transacional garantido|Cards e roteiro|Criar modelo real de contrato/aditivo/vencimento|Baixa|
|Jurídico|/Modulo/EmImplantacao?codigo=juridico|Placeholder/ModuloController|Views/Modulo/EmImplantacao.cshtml|OperationalDemoService|Tabelas jurídicas futuras|Em implantação|Nada|Roadmap visual|Implementar controller e CRUD de processos/pareceres|Baixa|
|Financeiro|/Financeiro|FinanceiroController|Views/Financeiro/*|Repositories Dapper/demo|Tabelas financeiras|Parcial|Consultas quando schema existe|Lançamentos e conciliação podem ser demo|Definir CRUD financeiro mínimo auditado|Média|
|Saúde|/Saude|SaudeController|Views/Saude/*|Repositories Dapper/demo|Tabelas de saúde|Parcial|Indicadores/cadastros existentes conforme schema|Parte do painel operacional|Completar cadastros e sinalizar demo|Média|
|Educação|/Educacao|EducacaoController|Views/Educacao/*|Repositories Dapper/demo|Tabelas de educação|Parcial|Indicadores/cadastros conforme schema|Fluxos escolares completos|Completar CRUDs prioritários|Média|
|Saneamento|/Saneamento|SaneamentoController|Views/Saneamento/*|Repositories Dapper/demo|Tabelas saneamento|Parcial|Indicadores conforme schema|Ordens/leituras avançadas|Completar operações reais|Média|
|RH|/Rh|RhController|Views/Rh/*|Repositories Dapper/demo|Tabelas RH|Parcial|Consultas/cadastros conforme schema|Folha e workflows completos|Completar CRUD básico e permissões|Média|
|Agro|/Agro|AgroController|Views/Agro/*|Repositories Dapper/demo|Tabelas agro|Parcial|Consultas/cadastros conforme schema|Parte BI/demo|Completar CRUD produtor/propriedade/programas|Média|
|Integrações|/Integracoes|IntegracoesController|Views/Integracoes/*|Repositories/API/demo|Tabelas integração/webhook|Parcial/Em implantação|Catálogo/status quando disponível|Execução de conectores|Criar CRUD conectores e logs auditáveis|Média|

## 14. Próxima consolidação funcional

Atualização: 2026-06-26. Esta seção consolida o inventário executivo exigido para a próxima evolução. A classificação usa apenas: **Funcional**, **Parcial**, **Demonstrativo**, **Em implantação** e **Quebrado**. Sempre que a estrutura física não for confirmada no ambiente, a recomendação é fallback visual honesto, sem simular gravação.

| # | Área | Rota | Controller | View | Service/repository | Tabelas usadas | Status atual | O que é real | Fallback/mock | Salva de verdade | Falta corrigir | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
|1|Auth/Login/Logout|`/Auth/Login`, `/Auth/Logout`|`AuthController`|`Views/Auth/Login.cshtml`|`NpgsqlConnectionFactory`, `IPasswordHashService`|`sigov.usuario`, `sigov.auditoria_evento`|Funcional|Cookie de autenticação, validação de senha e tentativa de auditoria|Bloqueio progressivo e métricas de tentativas|Login/logout e auditoria best-effort|Endurecer anti-brute-force e trilha unificada|Alta|
|2|Dashboard|`/Dashboard`|`DashboardController`|`Views/Dashboard/Index.cshtml`|`PostBuildSaasService`|`sigov.tenant`, `sigov.usuario`, `sigov.plano_saas`, `sigov.parametro_sistema`|Parcial|KPIs via Dapper quando tabelas existem|Cards executivos e auditorias recentes visuais|Consultas de contagem reais quando schema existe|Substituir blocos fixos por consultas auditáveis|Alta|
|3|MinhaCentral|`/MinhaCentral`|`MinhaCentralController`|`Views/MinhaCentral/Index.cshtml`|Serviço dedicado ainda pendente|`sigov.usuario`, `sigov.tenant`, auditoria e módulos|Parcial|Jornada pós-login e atalhos úteis|Pendências, atividades e alertas parcialmente visuais|Nada transacional principal|Criar `MinhaCentralService` operacional|Alta|
|4|Manual|`/Manual`|`ManualController`|`Views/Manual/Index.cshtml`|Conteúdo Razor guiado|N/A|Parcial|Manual renderiza por perfil/rotina|Telemetria e conteúdo dinâmico|Não se aplica|Persistir ajuda contextual e feedback|Média|
|5|POC|`/Poc`|`PocController`|`Views/Poc/Index.cshtml`|Catálogos locais/health|N/A|Parcial|Roteiro comercial navegável|Evidências e status podem ser manuais|Não se aplica|Separar funcional/parcial/demo/implantação por fonte real|Alta|
|6|Health|`/Operacao/Health`, `/Health`, `/api/health/live`|`OperacaoController`, API Health|`Views/Operacao/Health.cshtml`|Health checks e `PostBuildSaasService`|Web, API, PostgreSQL, migrations, storage, worker|Parcial|API live e painel visual|Alguns itens estimados no MVC|Não se aplica|Probes reais para worker/storage/migrations|Alta|
|7|Usuários|`/Seguranca/Usuarios`|`SegurancaController`|`Views/Seguranca/Usuarios*.cshtml`|`SegurancaAdminService`|`sigov.usuario`, `sigov.tenant`, `sigov.auditoria_evento`|Parcial|Listar, buscar, criar, editar, ativar/inativar e resetar quando schema existe|Tenant/perfil dependem de colunas/vínculos instalados|Usuário e status via Dapper|Validar duplicidade completa e hash definitivo|Crítica|
|8|Perfis|`/Seguranca/Perfis`|`SegurancaController`|`Views/Seguranca/Perfis.cshtml`|`SegurancaAdminService`|`sigov.perfil`, vínculos|Parcial|CRUD quando tabela existe|Contagens/vínculos podem cair para zero|Perfil via Dapper quando schema existe|Detalhe dedicado e contagens confiáveis|Alta|
|9|Permissões|`/Seguranca/Permissoes`, `/Seguranca/Perfis/{id}/Permissoes`|`SegurancaController`|`Views/Seguranca/Permissoes.cshtml`|`SegurancaAdminService`|`sigov.permissao`, `sigov.perfil_permissao`|Parcial|Listagem/matriz básica|Checkboxes genéricos se schema indisponível|Somente quando vínculo existir|POST real transacional por perfil|Alta|
|10|Tenants|`/Saas/Tenants`|`SaasController`|`Views/Saas/Tenants.cshtml`|`PostBuildSaasService`|`sigov.tenant`, `sigov.auditoria_evento`|Parcial|Listar, buscar e salvar tenant quando colunas existem|Edição visual simplificada|Tenant via Dapper|Rotas dedicadas de detalhe/editar/ativar/inativar|Crítica|
|11|Planos|`/Saas/Planos`|`SaasController`|`Views/Saas/Planos.cshtml`|Catálogo/`PostBuildSaasService`|`sigov.plano_saas` quando existir|Demonstrativo|Catálogo visual|CRUD de planos sem schema confirmado|Não deve salvar sem tabela validada|Implementar CRUD ou remover botões de persistência|Média|
|12|Módulos|`/Saas/Modulos`|`SaasController`|`Views/Saas/Modulos.cshtml`|`PostBuildSaasService`|`sigov.tenant_modulo_contratado`|Parcial|Ativar/desativar por tenant quando tabela existe|Catálogo local e roadmap|Status por tenant via upsert|Padronizar status e esconder ações sem tenant/schema|Alta|
|13|Parâmetros|`/Saas/Parametros`|`SaasController`|`Views/Saas/Parametros.cshtml`|API/configuração SaaS|`sigov.parametro_sistema`|Parcial|Registro/auditoria visual best-effort|Editor granular ainda limitado|Somente auditoria quando disponível|Editor por escopo com restauração padrão|Alta|
|14|Auditoria|`/Auditoria/Trilhas`|`AuditoriaController`|`Views/Auditoria/*`|Serviços existentes|`sigov.auditoria_evento`, `sigov.auditoria`|Parcial|Login e ações críticas tentam auditar|Consulta pode exibir roteiro|Eventos best-effort|Consolidar `AuditTrailService` central|Alta|
|15|LGPD|`/Lgpd/Dashboard`|`LgpdController`|`Views/Lgpd/*`|Serviços existentes|Tabelas LGPD quando existirem|Parcial|Máscaras em listagens sensíveis|Direitos/incidentes/retencão podem ser conteúdo|Pouco transacional confirmado|Fluxos formais do titular e exportação|Alta|
|16|Implantação/Onboarding|`/Saas/Implantacao`, `/Onboarding`|`SaasController`, `OnboardingController`|Views respectivas|`PostBuildSaasService`/visual|Tenant, módulos, parâmetros quando existem|Em implantação|Etapas visuais e auditoria best-effort|Rascunho visual|Auditoria/rascunho quando tabela existe|Persistir etapas e validar campos|Alta|
|17|Busca|`/Busca`|`BuscaController`|`Views/Busca/*`|Serviços/repositories futuros|Usuários, tenants, módulos, parâmetros, auditoria|Em implantação|Rota/entrada se existir|Resultados demonstrativos possíveis|Não confirmado|Busca Dapper por domínio|Média|
|18|Notificações|`/Notificacoes`|`NotificacoesController`|`Views/Notificacoes/*`|Serviço futuro|Auditoria, health, onboarding, parâmetros|Demonstrativo|Página visual|Badges e eventos simulados|Não confirmado|Persistir notificações e lidas|Média|
|19|Protocolo|`/Protocolo`|`ProtocoloController`|`Views/Protocolo/*`|`OperationalDemoService`/repositories|Tabelas de protocolo|Demonstrativo|Consultas apenas se backend existir|Fluxos operacionais demo|Não garantido|CRUD protocolo real auditado|Média|
|20|GED/OCR|`/Ged`|`GedController`|`Views/Ged/*`|`OperationalDemoService`/storage|Tabelas GED/documentos/storage|Demonstrativo|Telas de upload/pesquisa quando disponíveis|OCR/workflow demo|Não garantido|Metadados, storage e OCR reais|Média|
|21|Tributário|`/Tributario`|`TributarioController`|`Views/Tributario/*`|Repositories Dapper/demo|Tabelas tributárias|Parcial|Consultas/cadastros quando schema existe|BI/rotinas avançadas|Parcial conforme tabela|CRUDs fiscais prioritários|Média|
|22|Contratos|`/ContratosB2B`|`ContratosB2BController`|`Views/ContratosB2B/*`|`OperationalDemoService`|Tabelas futuras|Em implantação|Conteúdo comercial|Cards/roteiro|Não|Modelo contrato/aditivo/vencimento|Baixa|
|23|Jurídico|`/Modulo/EmImplantacao?codigo=juridico`|Placeholder/Modulo|`Views/Modulo/EmImplantacao.cshtml`|Catálogo visual|Tabelas futuras|Em implantação|Roadmap|Tudo operacional|Não|Controller jurídico e CRUD processos|Baixa|
|24|Financeiro|`/Financeiro`|`FinanceiroController`|`Views/Financeiro/*`|Repositories/demo|Tabelas financeiras|Parcial|Consultas conforme schema|Conciliação/lançamentos demo|Parcial|Definir CRUD mínimo auditado|Média|
|25|Saúde|`/Saude`|`SaudeController`|`Views/Saude/*`|Repositories/demo|Tabelas saúde|Parcial|Cadastros/agenda conforme schema|Painéis sensíveis demo|Parcial|LGPD reforçada e CRUDs essenciais|Média|
|26|Educação|`/Educacao`|`EducacaoController`|`Views/Educacao/*`|Repositories Dapper|Tabelas educação|Parcial|Muitos cadastros escolares reais quando schema existe|Fluxos completos/integrações|Parcial|Completar matrículas, avaliações e auditoria|Média|
|27|Saneamento|`/Saneamento`|`SaneamentoController`|`Views/Saneamento/*`|Repositories/demo|Tabelas saneamento|Parcial|Indicadores conforme schema|Ordens/leituras avançadas|Parcial|CRUD leitura/fatura/OS|Média|
|28|RH|`/Rh`|`RhController`|`Views/Rh/*`|Repositories/demo|Tabelas RH|Parcial|Consultas/cadastros conforme schema|Folha/workflows|Parcial|LGPD e permissões por dado sensível|Média|
|29|Agro|`/Agro`|`AgroController`|`Views/Agro/*`|Repositories/demo|Tabelas agro|Parcial|Consultas conforme schema|BI/programas|Parcial|CRUD produtor/propriedade/programas|Média|
|30|Integrações|`/Integracoes`|`IntegracoesController`|`Views/Integracoes/*`|Repositories/API|Webhooks/outbox/logs|Parcial|Catálogo/status se tabelas existem|Execução de conectores|Parcial|CRUD conectores e retentativas auditáveis|Média|
|31|IA|`/Ia`/assistente|Controllers de IA/placeholder|Views IA/assistente|Serviços futuros|Logs/prompt/auditoria futuros|Demonstrativo|Assistente visual quando habilitado|Predições/automações|Não confirmado|Governança, logs e consentimento LGPD|Baixa|
|32|Mobile/Campo|`/Mobile`, `/Campo`, `/Offline`|Controllers offline/campo|Views offline/campo|Serviços futuros|Sincronização/dispositivos futuros|Demonstrativo|Roteiro/offline visual|App e sync reais|Não confirmado|Fila offline, conflitos e auditoria|Baixa|

### Diretriz de status padronizado

O catálogo de módulos passa a usar `SigovFeatureStatus` como vocabulário único: **Funcional**, **Parcial**, **Demonstrativo**, **Em implantação** e **Indisponível**. Telas que não conseguem provar persistência completa devem exibir aviso explícito de demonstração e não devem renderizar botão de salvamento transacional.

## 14. Sprint de consolidação do núcleo real

Atualização final da sprint: 2026-06-26. A validação foi executada neste ambiente de agente, porém o container não possui `dotnet` nem `docker`; portanto build, compose e smoke tests HTTP ficaram bloqueados por limitação ambiental, não por erro de aplicação confirmado.

### Arquivos alterados

- `src/Sigov.Web/Services/DatabaseSchemaInspector.cs` — novo serviço seguro de introspecção de schema via `information_schema` com cache curto e fallback por `ILogger`.
- `src/Sigov.Web/Services/AuditTrailService.cs` — auditoria central passa a validar `sigov.auditoria_evento` antes de gravar e registra fallback em log quando a tabela não existe.
- `src/Sigov.Web/Services/SegurancaAdminService.cs` — segurança passa a consultar o inspector antes de operações em `sigov.usuario` e `sigov.perfil`, mantendo retorno honesto quando a estrutura não existe.
- `src/Sigov.Web/Services/PostBuildSaasService.cs` — SaaS passa a validar tabelas de tenant e módulos antes de listar ou persistir, mantendo catálogo visual sem simular gravação.
- `src/Sigov.Web/Services/MinhaCentralService.cs` — nova central pós-login com resumo de tenant, módulos, pendências, alertas LGPD, atividades recentes e health quando tabelas existem.
- `src/Sigov.Web/Helpers/LgpdMaskingHelper.cs` — helper global para mascarar e-mail, documento, telefone e nome.
- `src/Sigov.Web/Controllers/MinhaCentralController.cs` — passa a usar `MinhaCentralService` com `CancellationToken` e fallback seguro.
- `src/Sigov.Web/Controllers/SaasController.cs` — adicionadas rotas REST explícitas de tenant e módulo, mantendo anti-forgery nos POSTs.
- `src/Sigov.Web/Controllers/RelatoriosController.cs` — adicionados CSVs mínimos para usuários, tenants, módulos, auditorias e parâmetros quando tabelas existem.
- `src/Sigov.Web/Models/PostBuild/PostBuildViewModels.cs` — adicionados viewmodels da Minha Central.
- `src/Sigov.Web/Views/MinhaCentral/Index.cshtml` — blocos agora consomem modelo real/fallback honesto.
- `src/Sigov.Web/Views/Relatorios/Index.cshtml` — links CSV e aviso de PDF/Excel em implantação.
- `src/Sigov.Web/Program.cs` — registro DI de `IDatabaseSchemaInspector` e `MinhaCentralService`.
- `docs/diagnostico-funcional-ux.md` — seção de sprint consolidada com resultado real.

### Rotas revisadas

- `/Seguranca/Usuarios`, `/Seguranca/Usuarios/Novo`, `/Seguranca/Usuarios/{id}`, `/Seguranca/Usuarios/{id}/Editar`, `/Seguranca/Usuarios/{id}/Ativar`, `/Seguranca/Usuarios/{id}/Inativar`, `/Seguranca/Usuarios/{id}/ResetSenha`.
- `/Seguranca/Perfis`, `/Seguranca/Perfis/Novo`, `/Seguranca/Perfis/{id}/Editar`, `/Seguranca/Perfis/{id}/Ativar`, `/Seguranca/Perfis/{id}/Inativar`, `/Seguranca/Perfis/{id}/Permissoes`, `/Seguranca/Permissoes`.
- `/Saas/Tenants`, `/Saas/Tenants/Novo`, `/Saas/Tenants/{id}`, `/Saas/Tenants/{id}/Editar`, `/Saas/Tenants/{id}/Ativar`, `/Saas/Tenants/{id}/Inativar`.
- `/Saas/Modulos`, `/Saas/Modulos/{codigo}`, `/Saas/Modulos/{codigo}/Ativar`, `/Saas/Modulos/{codigo}/Inativar`.
- `/Saas/Parametros`, `/Saas/Planos`, `/MinhaCentral`, `/Poc`, `/Relatorios`, `/Relatorios/UsuariosCsv`, `/Relatorios/TenantsCsv`, `/Relatorios/ModulosCsv`, `/Relatorios/AuditoriasCsv`, `/Relatorios/ParametrosCsv`.

### Tabelas detectadas por código e introspecção segura

O ambiente local do agente não permitiu subir PostgreSQL/Docker para detecção física. A aplicação agora detecta em tempo de execução, sem quebrar tela, as tabelas:

- `sigov.usuario`
- `sigov.tenant`
- `sigov.perfil`
- `sigov.permissao`
- `sigov.perfil_permissao`
- `sigov.tenant_modulo_contratado`
- `sigov.parametro_sistema`
- `sigov.plano_saas`
- `sigov.auditoria_evento`

### Colunas usadas

- `sigov.usuario`: `id`, `nome`, `login`, `email`, `pessoa_id`, `ativo`, `bloqueado`, `deve_alterar_senha`, `mfa_habilitado`, `senha_hash`, `tipo_usuario`, `tenant_id`, `is_deleted`, `created_at`, `updated_at`.
- `sigov.tenant`: `id`, `nome`, `slug`, `codigo`, `documento`, `email`, `telefone`, `plano`, `cor_primaria`, `logo_url`, `status`, `ambiente`, `ativo`, `metadados`, `is_deleted`, `created_at`, `updated_at`.
- `sigov.perfil`: `id`, `codigo`, `nome`, `descricao`, `ativo`, `is_deleted`, `created_at`, `updated_at`.
- `sigov.permissao`: `id`, `chave`, `codigo`, `nome`.
- `sigov.perfil_permissao`: `perfil_id`, `permissao_id`.
- `sigov.tenant_modulo_contratado`: `tenant_id`, `modulo_codigo`, `status`, `contratado_em`, `vigencia_inicio`, `ativo`, `updated_at`.
- `sigov.auditoria_evento`: `tenant_id`, `usuario_id`, `acao`, `entidade`, `entidade_id`, `antes`, `depois`, `ip`, `user_agent`, `correlation_id`, `created_at`.
- `sigov.parametro_sistema`: `chave`, `escopo`, `categoria`, `valor` para exportação CSV segura quando existir.

### Ações que persistem de verdade quando o schema existe

- Usuários: criar, editar, ativar, inativar e resetar senha com hash e troca obrigatória.
- Perfis: criar, editar, ativar e inativar quando `sigov.perfil` existe.
- Tenants: criar, editar, ativar e inativar quando `sigov.tenant` existe.
- Módulos por tenant: ativar/desativar com upsert quando `sigov.tenant_modulo_contratado` existe.
- Auditoria: grava em `sigov.auditoria_evento` quando a tabela existe; caso contrário, gera warning estruturado.
- Relatórios CSV: usuários ativos, tenants ativos, módulos por tenant, auditorias recentes e parâmetros por escopo quando as respectivas tabelas existem.

### Ações em fallback honesto

- Planos SaaS permanecem catálogo demonstrativo quando `sigov.plano_saas` não estiver disponível; sem CRUD falso.
- Permissões exibem limitação quando a matriz `sigov.permissao`/`sigov.perfil_permissao` não existe ou não há perfil selecionado.
- Minha Central usa dados reais de tenant, módulos e auditorias quando possível; pendências e ações recomendadas ficam como orientação explícita quando faltam tabelas.
- Dashboard e SaaS mantêm cards/catálogos operacionais quando consultas falham, sem stacktrace e sem afirmar persistência.

### Resultado do build, Docker e smoke tests

- `dotnet restore`: bloqueado — `/bin/bash: dotnet: command not found`.
- `dotnet build`: bloqueado — `/bin/bash: dotnet: command not found`.
- `docker compose up -d --build`: bloqueado — `/bin/bash: docker: command not found`.
- `docker compose ps`: bloqueado — `/bin/bash: docker: command not found`.
- Smoke tests HTTP em `localhost:8080` e `localhost:5001`: bloqueados porque Docker não está disponível e nenhum serviço ficou ouvindo nessas portas.

### Pendências para próxima sprint

1. Validar build e Docker em ambiente com .NET 6 SDK e Docker disponíveis.
2. Ajustar SQL dinâmico por coluna opcional em `sigov.usuario` e `sigov.tenant` após inspecionar o schema real do cliente.
3. Implementar POST transacional completo para `Seguranca/Perfis/{id}/Permissoes` com seleção de permissões por checkbox.
4. Implementar edição granular de `sigov.parametro_sistema` com validação por tipo e restauração de padrão.
5. Evoluir Planos SaaS para CRUD somente se `sigov.plano_saas` e vínculos de módulos/limites estiverem instalados.
6. Executar validação de navegador/console e screenshots em ambiente web disponível.

## 15. Sprint de endurecimento do núcleo real

Atualização executada em 2026-06-26. A trava inicial e a validação final foram tentadas antes e depois das alterações, mas este ambiente não possui `dotnet` nem `docker`, impedindo build/compose/smoke tests locais. O resultado abaixo separa o que foi endurecido no código do que ainda precisa ser validado em ambiente com SDK .NET 6, Docker e PostgreSQL.

### Status inicial do build e Docker

- `dotnet restore`: bloqueado por ambiente (`dotnet: command not found`).
- `dotnet build`: bloqueado por ambiente (`dotnet: command not found`).
- `docker compose up -d --build`: bloqueado por ambiente (`docker: command not found`).
- `docker compose ps`: bloqueado por ambiente (`docker: command not found`).
- Rotas HTTP iniciais não puderam ser testadas porque o Docker não subiu neste ambiente.

### Correções realizadas nesta sprint

- `SegurancaAdminService` deixou de montar SQL fixo para `sigov.usuario` e passou a obter as colunas reais com `IDatabaseSchemaInspector.GetColumnsAsync("sigov", "usuario", ct)` antes de selecionar, inserir ou atualizar colunas opcionais.
- Auditoria inline de Segurança foi substituída por `IAuditTrailService`, usando `IHttpContextAccessor` para IP, user-agent, usuário e correlation id em ações críticas.
- Permissões por perfil ganharam ViewModels dedicados e rotas reais `GET/POST /Seguranca/Perfis/{id}/Permissoes`.
- O salvamento de permissões por perfil agora usa transação: remove vínculos atuais em `sigov.perfil_permissao`, insere a seleção recebida e audita `PERMISSOES_SALVAR` quando a estrutura existe.
- A tela `/Seguranca/Permissoes` passou a ser fallback honesto quando nenhum perfil é selecionado, sem matriz genérica que simule persistência.
- A view de permissões passou a exibir aviso LGPD/auditoria, breadcrumb, filtro, empty state e botão de salvar apenas quando a matriz real foi carregada.

### Rotas analisadas e/ou ajustadas

- Segurança/Usuários: `/Seguranca/Usuarios`, `/Seguranca/Usuarios/Novo`, `/Seguranca/Usuarios/{id}/Editar`, `/Seguranca/Usuarios/{id}/Ativar`, `/Seguranca/Usuarios/{id}/Inativar`, `/Seguranca/Usuarios/{id}/ResetSenha`.
- Segurança/Perfis: `/Seguranca/Perfis`, `/Seguranca/Perfis/Novo`, `/Seguranca/Perfis/{id}/Editar`, `/Seguranca/Perfis/{id}/Ativar`, `/Seguranca/Perfis/{id}/Inativar`.
- Segurança/Permissões: `/Seguranca/Permissoes`, `/Seguranca/Perfis/{id}/Permissoes`.
- SaaS/Tenants, SaaS/Módulos, SaaS/Parâmetros, Minha Central, POC, Health e Relatórios/CSV permanecem com endurecimentos anteriores registrados na seção 14 e precisam de smoke test real em ambiente Docker.

### Tabelas detectadas por código

Sem Docker/PostgreSQL local, não houve detecção física nesta execução. O código consulta em tempo de execução, via `IDatabaseSchemaInspector`, as tabelas críticas:

- `sigov.usuario`
- `sigov.perfil`
- `sigov.permissao`
- `sigov.perfil_permissao`
- `sigov.auditoria_evento`
- `sigov.tenant`
- `sigov.tenant_modulo_contratado`
- `sigov.parametro_sistema`

### Colunas críticas consideradas

- `sigov.usuario`: obrigatórias esperadas `id`, `login`, `senha_hash`, `ativo`; opcionais tratadas dinamicamente `tenant_id`, `nome`, `email`, `pessoa_id`, `tipo_usuario`, `bloqueado`, `deve_alterar_senha`, `mfa_habilitado`, `is_deleted`, `created_at`, `updated_at`.
- `sigov.permissao`: `id`, `modulo`, `recurso`, `acao`, `chave`, `codigo`, `nome` usadas para compor a matriz.
- `sigov.perfil_permissao`: `perfil_id`, `permissao_id` usadas no vínculo transacional.
- `sigov.auditoria_evento`: escrita centralizada por `IAuditTrailService` quando instalada.

### Áreas que persistem de verdade quando o schema existe

- Segurança/Usuários: criar, editar, ativar, inativar e resetar senha com SQL schema-safe para colunas opcionais.
- Segurança/Perfis: CRUD e status permanecem reais quando `sigov.perfil` existe.
- Segurança/Permissões: seleção por perfil passa a persistir transacionalmente quando `sigov.permissao` e `sigov.perfil_permissao` existem.
- Auditoria: ações críticas de Segurança passam pelo `IAuditTrailService`.
- Relatórios/CSV: exportações mínimas permanecem condicionadas à existência das tabelas, com mascaramento já registrado na seção anterior.

### Áreas em fallback honesto ou parcial

- SaaS/Tenants: parcial avançado; precisa smoke test real para confirmar todas as rotas dedicadas no Docker do cliente.
- SaaS/Módulos: parcial; ativação/desativação depende de `sigov.tenant_modulo_contratado`.
- SaaS/Parâmetros: parcial; editor granular completo ainda é pendência.
- Minha Central: parcial; diferencia dados reais/fallback, mas deve ser validada com tenant, módulos e auditoria reais.
- POC: parcial/comercial; deve refletir continuamente os estados Funcional, Parcial, Demonstrativo e Em implantação.
- Health: parcial; painel precisa validação de probes reais de worker/storage/migrations.
- Protocolo, GED, Tributário, Financeiro, Saúde, Educação, Saneamento, RH, Agro e Integrações: permanecem conforme inventário da seção 14, sem expansão nesta sprint.

### Riscos encontrados

- Ambiente de execução sem .NET SDK e Docker impede prova local de build e runtime.
- Schemas legados podem ter variações em `sigov.permissao` além das colunas previstas; próxima validação deve comparar `information_schema.columns` real.
- `sigov.perfil` ainda tem SQL menos dinâmico que `sigov.usuario`; se ambientes antigos não tiverem `is_deleted`, `created_at` ou `updated_at`, perfis devem receber o mesmo nível de schema-safe na próxima etapa.
- SaaS/Parâmetros ainda precisa edição real com validação de tipo e restauração de padrão.

### Resultado final de validação técnica

- `dotnet restore`: bloqueado por ambiente.
- `dotnet build`: bloqueado por ambiente.
- `docker compose up -d --build`: bloqueado por ambiente.
- `docker compose ps`: bloqueado por ambiente.
- Smoke tests HTTP finais: bloqueados por ambiente, pois os serviços não puderam ser iniciados.

### Pendências recomendadas para próxima sprint

1. Rodar a suíte completa em ambiente com .NET 6 SDK e Docker.
2. Capturar colunas reais com `information_schema` no PostgreSQL Docker/local e ajustar `sigov.perfil`, `sigov.tenant` e `sigov.parametro_sistema` com o mesmo rigor aplicado a `sigov.usuario`.
3. Criar testes automatizados para matriz de permissões por perfil e CSV sem dados sensíveis.
4. Completar editor real de parâmetros sensíveis com máscara, tipo e restauração de padrão.
5. Validar console/assets no navegador e registrar screenshots das telas administrativas alteradas.
