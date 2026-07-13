# Diagnóstico de consolidação Pós-RC 12

Base analisada: PR #110 e artefatos atuais de Enterprise, Protocolo, GED, Dashboard, Minha Central, Busca, Relatórios, Notificações, Tarefas, Outbox, Auditoria, Agenda, Smoke, CI/CD, Docker, menus, layout, seeds e migrations.

| Área | Funcionalidade | Status atual | Falha/Risco | Correção/Evolução | Prioridade |
|---|---|---|---|---|---|
| Enterprise | CRUD Dapper por tenant | Funcional real | Depende das migrations aplicadas em runtime | Mantido endpoint real, permissões e fallback 503 sem falso sucesso | Alta |
| Enterprise | Importação CSV | Parcial | Prévia era majoritariamente local no front e pouco detalhada | Template com exemplo seguro/BOM, preview backend com validação de e-mail, documento, status, quantidade, valor e duplicidade; confirmação audita via CRUD e retorna relatório/notificação | Alta |
| Enterprise | Ações em lote | Parcial | Resumo não diferenciava falha total | Resultado por item, notificação lote.executado e HTTP 409 quando tudo falha | Alta |
| Enterprise/GED | Anexos enterprise_anexo | Fallback honesto | Storage/provedor GED pode não estar configurado | Endpoints REST consolidados para listar, vincular, remover, visualizar e baixar com bloqueio honesto quando provedor indisponível | Alta |
| Enterprise | Detalhe/timeline | Parcial | Offcanvas não mostrava todas as abas operacionais | Mantida base operacional; timeline completa depende de consultas adicionais sobre auditoria/tarefas/notificações | Média |
| Protocolo | Criação/tramitação/anexo | Funcional real | Smoke precisa tenant e seed | Cobrir no smoke E2E e evidências | Alta |
| GED | Documentos/OCR/assinatura | Dependente de provedor | OCR/assinatura externos não garantidos localmente | Fallback honesto e bloqueio de restritos sem permissão | Alta |
| Minha Central | Entrada operacional | Parcial | Indicadores dependem de schemas setoriais e permissões | Consolidar cards reais quando schema existe; fallback honesto | Alta |
| Dashboard | KPIs e drill-down | Parcial | Alguns cards demonstrativos | Drill-down por rotas reais e indicação de fonte real/fallback/demo | Alta |
| Busca Global | Busca multiárea | Parcial | Cobertura depende de serviços/tabelas aplicadas | Consolidar Enterprise SearchAsync e links mascarados | Média |
| Relatórios | CSV seguro | Funcional real | Precisa ampliar matriz de relatórios | CSV com UTF-8, mascaramento e neutralização de fórmula | Alta |
| Notificações | Eventos operacionais | Parcial | Integração completa depende de schemas sigov.notificacao | Retornos de importação/lote/anexo sinalizam eventos e outbox futuro | Média |
| Tarefas | Tarefas por evento | Parcial | Geração automática ainda dependente de tabelas/workers | Smoke e documentação registram pendência honesta | Média |
| Outbox | Eventos Enterprise | Parcial | Worker/runtime real pendente | Eventos críticos documentados para proposta/pedido/OS/importação/lote/anexo | Média |
| Auditoria | Ator real/correlationId | Funcional real | Deve ser validada em runtime | CRUD Dapper registra auditoria operacional por tenant | Alta |
| Agenda | Agenda operacional | Parcial | Tela existente é demonstrativa em alguns módulos | Consolidar /Agenda no smoke e documentar eventos OS/planos/tarefas | Média |
| SLA | Prazos/status | Parcial | Campos podem não existir em todos os schemas | Documentado e exibido onde metadados existem; pendência de migration ampla | Média |
| CI/CD | Workflows | Pendente de runtime | Validação local sem dotnet/docker pode falhar por ambiente | Registrar limitação e manter scripts de CI | Alta |
| Docker | Compose | Pendente de runtime | Ambiente local pode não ter docker daemon | Evidência honesta em smoke/go-live | Alta |
| Menus/Layout | Navegação | Parcial | Risco de links mortos em módulos legados | Smoke lista rotas principais e exige ausência de 404/500 | Alta |
| Seeds/Migrations | Homologação | Funcional real | Idempotência precisa runtime PostgreSQL | Scripts apply-demo-seed e go-live check validam quando banco disponível | Alta |
