# Plano executável — Indústria 360

Este plano evolui o código canônico `industria_producao`. Todas as histórias pressupõem isolamento por `tenant_id`, PostgreSQL 16+, Dapper, migrations idempotentes, auditoria e autorização persistida. Funcionalidade só é considerada pronta com interface operacional, API, autorização, persistência, testes e critério de aceite verificável.

## Sprint 02 — fundação SaaS e acesso seguro

- **Objetivo:** concluir identidade multiempresa, contrato de módulo, contexto e autorização fail-closed.
- **Histórias:** entrar com CPF/CNPJ/e-mail/login; selecionar organização após validar credenciais; operar catálogo e contratação; suspender ou reativar acesso.
- **Regras:** nenhuma organização é inferida em ambiguidade; licença vencida/suspensa/inadimplente bloqueia; perfil nominal não substitui permissão.
- **Tabelas:** evoluir `modulo_saas`, `tenant_modulo_contratado`, histórico de contrato e futura `sessao_usuario`.
- **APIs:** `/api/saas/tenants`, `/api/saas/modulos`, `/api/saas/contratos`, `/api/auth/contexto`.
- **Telas:** login, seletor de organização, dashboard SaaS, tenants, módulos, planos e contratos.
- **Permissões:** `saas.tenant.*`, `saas.modulo.*`, `saas.contrato.*`, `seguranca.contexto.trocar`.
- **Integrações:** auditoria central, cache por tenant e medição de uso.
- **Testes:** identificadores válidos/inválidos, ambiguidade, tenant cruzado, licença e 401/403; migration duas vezes.
- **Aceite/dependências/riscos:** contrato canônico governa menu+rota+ação; depende de autenticação da API; risco principal é coexistência das tabelas legadas.

## Sprint 03 — administração comercial SaaS

- **Objetivo:** tornar o painel SaaS operacional, sem números ou botões estáticos.
- **Histórias:** cadastrar organização, configurar implantação, publicar preço, iniciar trial, contratar, renovar, suspender, cancelar e consultar histórico.
- **Regras:** preços e limites são snapshots no contrato; alterações têm responsável, motivo e correlação; suspensão não apaga dados.
- **Tabelas:** `tenant`, `modulo_saas`, `plano_saas`, `plano_modulo`, `tenant_modulo_contratado`, `tenant_uso_mensal`.
- **APIs:** CRUD e comandos explícitos `/contratar`, `/iniciar-trial`, `/suspender`, `/reativar`, `/cancelar`.
- **Telas:** funil de implantação, detalhe 360 do tenant, editor de módulo/plano, linha do tempo de contrato e consumo.
- **Permissões:** separar visualizar, criar, editar preço, contratar, suspender, cancelar, ver auditoria e exportar.
- **Integrações:** outbox de eventos comerciais, notificações internas e cobrança futura desacoplada.
- **Testes:** concorrência de contratação, idempotência de comandos, vigência, limites e auditoria imutável.
- **Aceite/dependências/riscos:** todas as ações persistem e reaparecem após reinício; depende da Sprint 02; risco de migração de dados duplicados.

## Sprint 04 — cadastros industriais

- **Objetivo:** entregar cadastros reutilizáveis e versionáveis de centros, recursos e produtos.
- **Histórias:** criar/editar/inativar centros de trabalho, máquinas, linhas, ferramentas, mão de obra e produto industrial.
- **Regras:** código único por tenant; inativação bloqueia novas referências e preserva histórico; unidade, lote e validade são parametrizados.
- **Tabelas:** `industria_centro_trabalho`, `industria_recurso`, `industria_produto` e tabelas de domínio parametrizado.
- **APIs:** filtros, paginação, detalhe, criação, edição e mudança de status com ETag/controle otimista quando necessário.
- **Telas:** listas responsivas, formulários validados, detalhe lateral, estados vazios/erro e confirmação de inativação.
- **Permissões:** `industria.centros.*`, `industria.recursos.*`, `industria.produtos.*`.
- **Integrações:** produto e unidade do estoque, anexos GED opcionais e auditoria.
- **Testes:** unicidade por tenant, referências inativas, validações numéricas, paginação e isolamento.
- **Aceite/dependências/riscos:** CRUD completo sem registro fake; depende do estoque canônico; risco de duplicidade de produtos legados.

## Sprint 05 — ficha técnica/BOM e roteiros

- **Objetivo:** modelar composição e processo produtivo versionados.
- **Histórias:** montar BOM, perdas e rendimento; montar roteiro ordenado com setup, execução, centro e recurso; ativar versões.
- **Regras:** uma versão ativa por produto/data; componente pertence ao tenant; ciclos de BOM são proibidos; versão usada por OP é imutável.
- **Tabelas:** `industria_ficha_tecnica`, `_item`, `industria_roteiro`, `_operacao` e histórico de versões.
- **APIs:** validar composição, clonar versão, ativar/inativar, reordenar operações e simular consumo/capacidade.
- **Telas:** editor de BOM, editor de roteiro, comparação de versões e validação antes de publicar.
- **Permissões:** `industria.fichas.*`, `industria.roteiros.*`, incluindo publicar e clonar.
- **Integrações:** estoque para componentes, custos padrão e GED para instruções de trabalho.
- **Testes:** ciclo, soma/rendimento, versão concorrente, referência cross-tenant e imutabilidade após uso.
- **Aceite/dependências/riscos:** OP reproduz exatamente a versão selecionada; depende da Sprint 04; risco de BOM profunda e desempenho.

## Sprint 06 — ordens de produção e PCP

- **Objetivo:** planejar, liberar e acompanhar OP com máquina de estados explícita.
- **Histórias:** criar OP manual ou a partir de pedido, reservar materiais, sequenciar, liberar, pausar, cancelar e concluir.
- **Regras:** transições inválidas retornam 422; conclusão exige apontamento e qualidade quando configurada; alterações geram histórico.
- **Tabelas:** `industria_ordem_producao`, `_material`, `_operacao`, `_historico` e reservas de estoque.
- **APIs:** comandos idempotentes de estado, consulta por período/status/recurso e visão de carga.
- **Telas:** quadro PCP, detalhe completo, timeline, materiais, operações, alertas de atraso e ações condicionais.
- **Permissões:** visualizar, criar, liberar, iniciar, pausar, cancelar, concluir e reprogramar.
- **Integrações:** Comercial gera OP; estoque reserva; agenda de recurso e notificações recebem eventos via outbox.
- **Testes:** matriz de transições, repetição de comando, concorrência, falta de material, tenant cruzado e auditoria.
- **Aceite/dependências/riscos:** nenhuma transição ocorre só no frontend; depende das Sprints 04–05; risco de deadlock em reserva concorrente.

## Sprint 07 — chão de fábrica e rastreabilidade

- **Objetivo:** executar operações com UX mobile-first e rastreio de materiais/produto.
- **Histórias:** operador inicia/pausa/retoma operação, aponta tempo, consome material, produz, registra refugo e encerra.
- **Regras:** ação exige operador, OP e recurso válidos; quantidades não podem ser negativas; lote/validade são obrigatórios conforme produto.
- **Tabelas:** `industria_apontamento`, `industria_consumo_material`, `industria_producao_acabada`, `industria_refugo` e genealogia de lotes.
- **APIs:** comandos curtos e idempotentes, consulta do posto, pendências de sync e rastreabilidade para frente/para trás.
- **Telas:** posto de trabalho touch, contraste alto, confirmação crítica, modo degradado explícito e fila offline futura.
- **Permissões:** chão de fábrica acessar, apontar, consumir, produzir, refugar e corrigir com alçada.
- **Integrações:** estoque transacional, impressão/QR futuro, mobile PWA e outbox.
- **Testes:** dupla submissão, saldo insuficiente, lote, retomada, correção auditada e responsividade.
- **Aceite/dependências/riscos:** estoque e OP fecham na mesma transação ou compensação documentada; depende da Sprint 06; risco de conectividade industrial.

## Sprint 08 — qualidade, paradas e manutenção

- **Objetivo:** controlar inspeções, bloqueios, não conformidades e paradas produtivas.
- **Histórias:** criar plano/amostra, aprovar/reprovar, bloquear lote/OP, abrir não conformidade, registrar parada e gerar OS corretiva.
- **Regras:** inspeção obrigatória pendente bloqueia conclusão; geração de OS exige módulo contratado; evidências são preservadas.
- **Tabelas:** `industria_inspecao_qualidade`, não conformidade/ação corretiva, `industria_parada_producao` e vínculo de OS.
- **APIs:** fila de inspeção, decisão, bloqueio/liberação, abertura de NC e geração idempotente de OS.
- **Telas:** cockpit de qualidade, detalhe com evidências, Pareto de causas, paradas abertas e vínculo com manutenção.
- **Permissões:** qualidade visualizar/inspecionar/liberar; paradas visualizar/criar/encerrar/gerar OS.
- **Integrações:** Manutenção Industrial, Ordem de Serviço, GED e notificações.
- **Testes:** bloqueio de conclusão, dupla decisão, módulo dependente ausente, anexos e tenant cruzado.
- **Aceite/dependências/riscos:** decisão deixa trilha completa e afeta OP/lote; depende das Sprints 06–07; risco de taxonomia inconsistente de causas.

## Sprint 09 — custos e indicadores

- **Objetivo:** calcular custos industriais reproduzíveis e indicadores explicáveis.
- **Histórias:** calcular material, máquina, mão de obra, indireto e refugo; comparar padrão x real; analisar OEE simplificado.
- **Regras:** cálculo guarda versão, fontes e data; recálculo cria nova versão; dados ausentes são exibidos como pendência, não zero silencioso.
- **Tabelas:** `industria_custo_ordem`, rateios, custo padrão, snapshots e agregados diários.
- **APIs:** calcular/recalcular, memória de cálculo, séries por período, produto, centro e causa.
- **Telas:** detalhe de custo por OP, variações, produção diária, refugo, paradas, atrasos e OEE simplificado.
- **Permissões:** custos visualizar/calcular/recalcular/exportar; dashboard visualizar.
- **Integrações:** estoque, apontamentos, recursos, financeiro empresarial e BI.
- **Testes:** precisão decimal, snapshot, dados faltantes, timezone, agregação e autorização de exportação.
- **Aceite/dependências/riscos:** todo valor é rastreável à origem; depende das Sprints 04–08; risco de interpretação contábil além do escopo básico.

## Sprint 10 — hardening, go-live e observabilidade

- **Objetivo:** fechar segurança, desempenho, migração, suporte e operação produtiva.
- **Histórias:** administrador monitora saúde/uso; suporte investiga por correlação; cliente exporta dados; operação revoga sessão e recupera desastre.
- **Regras:** logs estruturados sem segredo/PII; rate limit por tenant; backup/restauração testados; erros de schema são explícitos.
- **Tabelas:** sessão revogável, outbox/inbox, métricas de uso, retenção e jobs operacionais.
- **APIs:** health/readiness, uso/limites, sessões, exportação assíncrona, reprocessamento seguro de outbox.
- **Telas:** saúde SaaS, sessões, consumo, jobs, falhas de integração e runbooks vinculados.
- **Permissões:** operação, suporte, observabilidade, sessão revogar, exportação e reprocessamento segregados.
- **Integrações:** OpenTelemetry, storage, e-mail e provedores reais somente por adaptadores configurados.
- **Testes:** carga, caos controlado, backup/restore, pentest, acessibilidade, matriz de navegadores e rollback de deployment.
- **Aceite/dependências/riscos:** gates verdes, migration reaplicável, zero bypass de autorização, RPO/RTO aprovados; depende de todas as sprints; riscos externos permanecem `BLOCKED` até credenciais/infra aprovadas.
