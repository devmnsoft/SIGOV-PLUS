# RC50.64 — plano de polimento UX/UI funcional

Data: 2026-08-19. Escopo: Web existente, sem módulo, migration ou classe de teste nova. Este inventário é estático; ausência de 404/500 só pode ser homologada com runtime autenticado.

## Inventário existente

| Superfície | Rotas/telas localizadas | Perfil e autorização esperados | Diagnóstico | Prioridade/correção |
|---|---|---|---|---|
| Shell | `_Layout`, `_Navbar`, `_Sidebar`, breadcrumb, alertas, toast, confirmação, busca e criação rápida | autenticado; menu deve refletir catálogo/grants | sidebar móvel e skip link existem; catálogo histórico ainda é extenso e parte dos grupos é estática | P1: continuar migração de todos os grupos para `IMenuPermissionService`; P0: backend segue autoridade |
| Autenticação | `/Auth/Login`, recuperação e sessão expirada | público/autenticado conforme jornada | validação unobtrusive e mensagens sanitizadas existentes | P1: homologar teclado, leitores e credenciais reais |
| Minha Central | `/MinhaCentral` | todos os autenticados; cards por papel | dados persistidos, vazios seguros; havia ação local “marcar como visto” que alegava auditoria futura, rota incorreta da matriz e fallback exibido mesmo com sucesso | P0 corrigido: remover ação sem persistência, corrigir rotas e fallback; P1: validar todos os papéis autenticados |
| Governança transversal | `/Pendencias`, `/Alertas`, `/QualidadeDados`, `/IntegracoesInternas`, `/Modulos/StatusFuncional` | grants `governanca.*`/admin/auditor | central compartilhada tem paginação, estado vazio e ação somente com rota | P0: smoke com banco; P1: produtores por módulo |
| Catálogo e acesso | `/Modulos/Catalogo`, `/Modulos/MeuAcesso`, `/Seguranca/MatrizAcesso` | usuário; export/grants administrativos quando aplicável | decisão backend e auditoria introduzidas nas RC50.57–63 | P0: provar 403/exportação por perfil |
| Dashboards arrecadação | Tributário, Financeiro e Saneamento | módulo contratado + grants | dashboards, filtros e serviços persistentes localizados | P0: smoke sem 500; P1: confirmar origem/limite dos indicadores |
| Dashboards sociais | Educação e Saúde, inclusive professor/ACS | escola/turma/unidade/microárea | superfícies extensas; PII exige máscara e escopo | P0: jornada segregada; P1: padronizar vazios remanescentes |
| Documental/institucional | Processos, GED, Assinaturas, Legislativo, Diário, Transparência, Ouvidoria/e-SIC | grants granulares | ações e rotas reais predominantes; integrações oficiais ainda preparatórias | P0: validar cancelamento/publicação/export; P1: e-SIC completo |
| Administrativo | RH/Folha, Compras/Licitações, Contratos, Almoxarifado, Patrimônio, Frotas e Obras | grants granulares, auditor somente leitura | fluxos persistentes convivem com superfícies preparatórias históricas | P0: não expor ações preparatórias como concluídas; P1: concluir transições já documentadas |
| Erros/estados | middleware `/Home/Error/{code}` e `Views/Shared/Errors` | todos | middleware já registra correlation id; páginas compartilhadas tinham textos genéricos idênticos | P0 corrigido: mensagens 403/404/500 distintas, sanitizadas e com rota segura; P1: reutilizar novos partials em módulos |
| Formulários/modais | componentes `Forms`, CRUDs setoriais e modais críticos | grant da action no backend | antiforgery e validação são heterogêneos; formulários centrais usam `required`/ModelState | P0: POST sensível com antiforgery/justificativa; P1: adotar `_ValidationSummaryPro` progressivamente |
| Exportações | administrativas, matriz e módulos | grant de exportação | auditoria existe em superfícies recentes; exportadores legados ainda requerem revisão | P0: negar e auditar sem grant; P1: filtros, máscara, limite e nomes consistentes |

## Achados objetivos

- A varredura `href`/actions/fetch encontrou **499 ocorrências** para reconciliação; o script de conflitos é a autoridade estática para rotas de API.
- **Botão sem ação:** “Marcar como visto” da Minha Central apenas criava toast e alegava auditoria futura. Removido e substituído por rota real de Pendências.
- **Card/rota incorreta:** matriz do SuperAdmin apontava a permissões genéricas; corrigida para `/Seguranca/MatrizAcesso`. Cards ACS foram alinhados às rotas reais `/Acs/Visitas` e `/Acs/Domicilios`.
- **Estado vazio:** Minha Central já possuía vazios de pendências/recentes; faltava vazio de atalhos. Foram consolidados estados compartilhados de acesso negado, estrutura pendente, módulo bloqueado e validação.
- **Mensagens técnicas:** o middleware já mantém exceção no log e apresenta correlation id. A cópia 403/404/500 foi diferenciada sem SQL, stack trace, segredo ou PII.
- **Responsividade:** sidebar já era off-canvas. Foram adicionados fechamento por `Escape`, contenção horizontal de tabelas, ações empilhadas no celular e respeito a `prefers-reduced-motion`.
- **LGPD:** consultas novas da Central projetam metadados, filtram `tenant_id` e não carregam documento pessoal. Validação autenticada das páginas históricas segue P0.
- **501:** a busca estática não encontrou `NotImplemented`, `NotImplementedException` ou `StatusCode(501)` no escopo Web essencial.
- **Risco 404/500:** nenhuma ausência pode ser encerrada por leitura estática. O smoke autenticado e o banco são P0 obrigatório.

## Backlog priorizado

### P0 — bloqueia produção
1. Aplicar banco limpo/parcial, compilar Release e executar smoke production-like.
2. Executar jornadas autenticadas por tenant/perfil e provar 200/302/401/403 sem 404/500.
3. Provar autorização/auditoria de toda exportação e mutação sensível.
4. Revisar os grupos ainda estáticos da sidebar contra módulo contratado e grant, mantendo bloqueio backend.
5. Remover/funcionalizar botões preparatórios encontrados em relatórios e importações antes de torná-los visíveis em produção.

### P1 — necessário para homologação funcional
1. Aplicar os novos partials a dashboards/listagens ainda com vazios em `<td>` ou mensagens ad hoc.
2. Aplicar resumo de validação e mensagens por campo aos formulários heterogêneos.
3. Homologar foco, modal, contraste e leitura de tabelas com tecnologia assistiva.
4. Medir paginação, agregações e limites de exportação com volume real.

### P2 — melhoria contínua
1. Consolidar CSS legado duplicado por domínio sem remover funcionalidade.
2. Evoluir preferências de dashboard somente após confirmar necessidade persistente; nenhuma tabela foi criada nesta RC.
3. Eliminar CSP `unsafe-inline` por migração controlada dos scripts inline.
