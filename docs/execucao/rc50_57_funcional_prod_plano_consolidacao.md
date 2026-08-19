# RC50.57-FUNCIONAL-PROD — plano de consolidação

Data: 2026-08-19. Este plano reconcilia os mapas RC50.44/RC50.51 e os gates RC50.54/55. Estado conservador: **não apto para produção até prova runtime**.

## Inventário real

O catálogo comercial contém 50 módulos. Núcleos com persistência e fluxos reais já presentes: Segurança, LGPD, Auditoria, Processos/GED, Tributário, Financeiro, Educação, Saúde, Saneamento, RH/Folha, Compras/Contratos, Almoxarifado/Patrimônio, Frotas/Obras, Agro e integrações. São funcionais/parciais conforme o mapa RC50.44; nenhum item é promovido a “homologado” sem banco e runtime. IA, OCR, NFS-e, DES-IF, e-SUS, georreferenciamento/offline e integrações governamentais permanecem beta, simulados honestamente ou preparatórios. Itens `EmImplantacao`, `Beta` e `Bloqueado` são identificados no catálogo.

Perfis-alvo consolidados: SUPERADMIN, ADMIN_TENANT, GESTOR_MUNICIPAL, COORDENADOR_AREA, OPERACIONAL, FINANCEIRO, AUDITOR, ATENDIMENTO, GESTOR_MODULO, LEITURA e CIDADAO. Permissões existentes usam claims `permission`, `permissao` e `scope`, com chave `modulo.recurso.acao`; habilitação modular aceita claims `module`/`modulo` e SuperAdmin possui bypass explícito.

## Menus, rotas e falhas conhecidas

A sidebar histórica é extensa e parcialmente estática. Nesta RC, Catálogo, Meu Acesso, Segurança, Matriz, Auditoria e LGPD passam a refletir o serviço modular. A autoridade permanece no controller/backend. Rotas canônicas novas: `/Modulos/Catalogo`, `/Modulos/MeuAcesso` e `/Seguranca/MatrizAcesso`. A análise RC50.55 encontrou 605 rotas sem conflito, nenhum endpoint essencial 501 e nenhuma ocorrência executável de `SELECT *`. Não existe prova runtime local de menus 404 ou dashboards 500 por falta histórica de SDK/PostgreSQL; portanto esses itens não são declarados resolvidos.

## Integrações e regras frágeis

Existem outbox, workflow/GED, auditoria transversal e pontes Financeiro↔Tributário/Compras/Saneamento. Folha→Financeiro, medição→Financeiro e recebimento permanente→Patrimônio permanecem preparatórios até homologação transacional. Riscos prioritários: cobertura heterogênea de autorização nos controllers legados; segregação de baixa/estorno/aprovação; máscaras de CPF/CNS nos fluxos de Saúde/Educação; validação de estoque não negativo; e justificativas obrigatórias de cancelamento/reabertura.

## Prioridades

1. P0: executar apply limpo/parcial, build e smoke autenticado; corrigir qualquer 404/500 real.
2. P0: estender enforcement backend e auditoria de negativa/exportação a cada controller sensível legado.
3. P1: substituir os grupos restantes da sidebar por descritores do catálogo.
4. P1: homologar CRUD/status/cancelamento por módulo e segregação financeira.
5. P2: concluir integrações preparatórias sem acoplamento impeditivo.

Nenhuma classe, fixture, mock ou projeto de teste será criado nesta RC.
