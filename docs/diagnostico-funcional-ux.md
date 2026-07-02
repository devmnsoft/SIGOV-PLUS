# Diagnóstico funcional e UX — Sprint SaaS comercial

## Antes

Planos eram majoritariamente catálogo visual; assinaturas, marketplace, notificações, busca e portal tinham cobertura parcial ou estática.

## Depois

- Planos usam dados reais quando `sigov.plano_saas` existe e deixam claro quando são demonstrativos.
- Assinaturas passam a ter rota operacional com fallback explícito.
- Marketplace organiza módulos para venda e gestão modular.
- Notificações exibem dados reais ou recomendações úteis derivadas.
- Busca global consulta áreas disponíveis com inspeção de schema.
- Portal do Cliente concentra assinatura, módulos, suporte e faturas com limitações honestas.

## Pendências UX

- Persistir permissões finas por ação quando a matriz definitiva de permissões estiver consolidada.
- Aplicar white label dinâmico por tenant em todas as telas após confirmação das colunas/metadados.

## 17. Sprint operacional de governo

| Módulo | Rota | Controller | Views | Services | Tabelas usadas | Status atual | Funcional | Parcial | Demonstrativo / implantação | Salva de verdade | Fallback | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Protocolo | `/Protocolo`, `/Protocolo/Processos`, `/Protocolo/Novo`, `/Protocolo/Detalhes/{id}`, `/Protocolo/Tramitar/{id}` | `ProtocoloController` | `Views/Operational/Module.cshtml` + partials operacionais | `OperationalDemoService`, `IDatabaseSchemaInspector`, `IAuditTrailService` | `sigov.protocolo`, `processo`, `tramite`, `protocolo_movimento`, `protocolo_anexo`, `arquivo` | Parcial/Em implantação conforme schema | Navegação, KPIs, filtros, detalhes, timeline, ações críticas auditáveis | Persistência depende de schema homologado | Sem schema exibe fallback honesto | Não simula salvamento | Sim | Alta |
| GED/OCR | `/Ged`, `/Ged/Documentos`, `/Ged/Pastas`, `/Ged/NovoDocumento`, `/Ged/Detalhes/{id}` | `GedController` | `Views/Operational/Module.cshtml` | `OperationalDemoService`, `IAuditTrailService` | `sigov.documento`, `ged_documento`, `ged_pasta`, `pasta`, `documento_versao`, `arquivo`, `ocr_fila` | Parcial/Em implantação | Rotas, aviso LGPD, auditoria de acesso/download | Upload real aguarda storage/schema | OCR não é simulado | Não simula upload | Sim | Alta |
| Tributário | `/Tributario`, `/Tributario/Contribuintes`, `/Tributario/Imoveis`, `/Tributario/Debitos`, `/Tributario/Guias`, `/Tributario/DividaAtiva` | `TributarioController` | `Views/Operational/Module.cshtml` | `OperationalDemoService`, `IAuditTrailService` | `sigov.contribuinte`, `imovel`, `debito`, `guia`, `divida_ativa` | Parcial/Em implantação | Visão operacional e CSV mascarado | Motor fiscal/guia real pendente | Guia permanece demonstrativa | Não simula guia fiscal | Sim | Alta |
| Contratos | `/Contratos`, `/Contratos/Listar`, `/Contratos/Novo`, `/Contratos/Detalhes/{id}`, `/Contratos/Vencimentos` | `ContratosController` | `Views/Operational/Module.cshtml` | `OperationalDemoService`, `IAuditTrailService` | `sigov.contrato`, `contrato_aditivo`, `contrato_fiscal`, `contrato_documento` | Parcial/Em implantação | Dashboard, vencimentos, detalhes, arquivar auditável | Persistência depende de schema | Sem schema em implantação | Não simula contrato salvo | Sim | Alta |
| Jurídico | `/Juridico`, `/Juridico/Processos`, `/Juridico/Prazos`, `/Juridico/Pareceres`, `/Juridico/Audiencias` | `JuridicoController` | `Views/Operational/Module.cshtml` | `OperationalDemoService`, `IAuditTrailService` | `sigov.processo_juridico`, `parecer_juridico`, `prazo_juridico`, `audiencia_juridica` | Parcial/Em implantação | Rotas e auditoria de visualização | Cadastro de parecer não simulado | Dados sensíveis com LGPD | Não salva parecer | Sim | Alta |
| Financeiro | `/Financeiro`, `/Financeiro/ContasReceber`, `/Financeiro/ContasPagar`, `/Financeiro/Caixa`, `/Financeiro/Categorias`, `/Financeiro/Relatorios` | `FinanceiroController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` | `sigov.conta_pagar`, `conta_receber`, `caixa_movimento`, `categoria_financeira` | Parcial/Em implantação | Visão operacional mínima | Lançamentos reais pendentes | Não simula lançamento | Não | Sim | Alta |
| Relatórios operacionais | `/Relatorios` | `RelatoriosController` | Views existentes de relatórios | `IDatabaseSchemaInspector`, `IAuditTrailService` | Fontes por módulo quando existirem | Parcial | Catálogo existente + plano operacional | CSVs específicos dependem de tabelas | Fallback honesto | Não exporta segredo | Sim | Média |
| Busca integrada | `/Busca?q=teste` | `BuscaController` | `Views/Busca/Index.cshtml` | Serviços existentes + navegação operacional | Fontes validadas por módulo na camada operacional | Parcial | Rota preservada | Busca real por tabela é próxima etapa | Não quebra sem schema | N/A | Sim | Média |
| Auditoria por módulo | ações POST/visualização | Controllers operacionais | Timeline/audit notice | `IAuditTrailService` | `sigov.auditoria_evento` | Parcial | Registra ou loga fallback | Depende de tabela | Fallback em log | Sim se tabela existir | Sim | Alta |
| LGPD por módulo | listagens/CSV/detalhes | Controllers operacionais | alerta LGPD | Mascaramento visual | N/A | Parcial | Avisos e documentos mascarados | Catálogo campo-a-campo pendente | N/A | N/A | Sim | Alta |

## 18. Sprint de operacionalização real dos módulos de governo

Esta sprint reposiciona Protocolo, GED/OCR, Tributário, Contratos, Jurídico e Financeiro para a primeira camada operacional real do SIGOV PLUS. O `OperationalDemoService` passa a ser fallback visual honesto; os novos services em `src/Sigov.Web/Services/Operational` usam Dapper, `IDatabaseSchemaInspector`, `IAuditTrailService`, `CancellationToken`, `ILogger`, mascaramento LGPD e consulta schema-safe.

| Módulo | Rota | Controller | Views | Service atual | Novo service/repository | Tabelas/colunas detectadas | Status atual | Fallback | Consulta real | Salvamento | Ações auditadas | Pendências |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Protocolo | `/Protocolo`, `/Protocolo/Processos`, `/Protocolo/Novo`, `/Protocolo/Detalhes/{id}`, `/Protocolo/Tramitar/{id}` | `ProtocoloController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` fallback | `ProtocoloOperationalService` | `sigov.protocolo`, `sigov.processo`, `sigov.tramite`, `sigov.protocolo_movimento`, `sigov.protocolo_anexo`, `sigov.arquivo`; colunas via `information_schema` em runtime | Parcial/Em implantação conforme schema | Quando nenhuma tabela existe ou em erro | Listagem Dapper na primeira tabela existente | POSTs auditam e não simulam persistência sem schema homologado | abertura, tramitação, anexação, arquivamento, reabertura, consulta | Persistir criação/tramitação quando colunas obrigatórias estiverem homologadas |
| GED/OCR | `/Ged`, `/Ged/Documentos`, `/Ged/Pastas`, `/Ged/NovoDocumento`, `/Ged/Detalhes/{id}` | `GedController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` fallback | `GedOperationalService` | `sigov.documento`, `sigov.ged_documento`, `sigov.ged_pasta`, `sigov.pasta`, `sigov.documento_versao`, `sigov.arquivo`, `sigov.ocr_fila` | Parcial/Em implantação | Sem schema/storage real | Listagem schema-safe de documentos/pastas | Upload não é simulado sem storage | criar documento, nova versão, arquivar, visualizar/download, consulta | Validar provider de storage e OCR real |
| Tributário | `/Tributario`, `/Tributario/Contribuintes`, `/Tributario/Debitos`, `/Tributario/Guias`, `/Tributario/DividaAtiva` | `TributarioController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` fallback | `TributarioOperationalService` | `sigov.contribuinte`, `sigov.imovel`, `sigov.debito`, `sigov.guia`, `sigov.divida_ativa` | Parcial/Em implantação | Sem schema fiscal | Listagem Dapper, documentos mascarados | Cadastro não simulado sem tabela | criação de contribuinte, consulta | Motor fiscal e emissão válida de guias |
| Contratos | `/Contratos`, `/Contratos/Listar`, `/Contratos/Novo`, `/Contratos/Vencimentos` | `ContratosController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` fallback | `ContratosOperationalService` | `sigov.contrato`, `sigov.contrato_aditivo`, `sigov.contrato_fiscal`, `sigov.contrato_documento` | Parcial/Em implantação | Sem schema | Listagem real quando existir tabela | Contrato não simulado | criação, arquivamento, consulta | CRUD completo e alertas por vigência |
| Jurídico | `/Juridico`, `/Juridico/Processos`, `/Juridico/Prazos`, `/Juridico/Pareceres`, `/Juridico/Audiencias` | `JuridicoController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` fallback | `JuridicoOperationalService` | `sigov.processo_juridico`, `sigov.parecer_juridico`, `sigov.prazo_juridico`, `sigov.audiencia_juridica` | Parcial/Em implantação | Sem schema | Listagem real quando existir tabela | Parecer não simulado | visualização, consulta | Cadastro de pareceres/prazos com workflow |
| Financeiro | `/Financeiro`, `/Financeiro/ContasReceber`, `/Financeiro/ContasPagar`, `/Financeiro/Caixa`, `/Financeiro/Categorias` | `FinanceiroController` | `Views/Operational/Module.cshtml` | `OperationalDemoService` fallback | `FinanceiroOperationalService` | `sigov.conta_pagar`, `sigov.conta_receber`, `sigov.caixa_movimento`, `sigov.categoria_financeira` | Parcial/Em implantação | Sem schema | Listagem Dapper real | Lançamento não simulado | consulta | CRUD financeiro e CSV operacional por fonte |
| Busca integrada | `/Busca?q=...` | `BuscaController` | Views existentes | Busca SaaS existente | Próxima etapa deve consumir services operacionais | Fontes operacionais ignoradas quando ausentes | Parcial | Fonte inexistente não quebra | Planejado por fonte real | N/A | consulta | Integrar agregação por área |
| Relatórios operacionais | `/Relatorios` | `RelatoriosController` | Views existentes | CSV SaaS existente | Próxima etapa deve usar services operacionais | Usuários/tenants e fontes operacionais conforme schema | Parcial | CSV honesto quando fonte ausente | Parcial | N/A | exportação | Adicionar CSVs operacionais específicos |
| Minha Central operacional | `/MinhaCentral` | `MinhaCentralController` | Views existentes | `MinhaCentralService` | Próxima etapa deve agregar services operacionais | Depende do schema local | Parcial | Cards sem persistência quando fonte ausente | Parcial | N/A | visualização | Pendências operacionais reais por usuário |
| POC | `/Poc` | `PocController` | Views existentes | POC visual | Próxima etapa deve exibir status dos services | Depende do schema local | Parcial | Indica implantação | Parcial | N/A | validação | Mostrar última validação por módulo |

## 19. Sprint de workflow, automação e BI operacional

- **Rotas novas/atualizadas**: `/Workflow`, `/Workflow/Definicoes`, `/Workflow/Instancias`, `/Tarefas`, `/Notificacoes`, `/Agenda`, `/Integracoes`, `/Bi`, `/MobileCampo`.
- **Controllers envolvidos**: `WorkflowController`, `TarefasController`, `NotificacoesController`, `AgendaController`, `IntegracoesController`, `BiController`, `MobileCampoController`, além de Dashboard, Minha Central, Busca e Relatórios como pontos de evolução.
- **Services criados**: `OperationalStatusService`, `WorkflowService`, `WorkflowDefinitionService`, `WorkflowInstanceService`, `TarefaService`, `NotificacaoService`, `AgendaOperacionalService`, `OperationalEventService`, `OutboxSigovService`, `IntegracaoMonitorService`, `BiOperacionalService`, `MobileCampoService`.
- **Tabelas avaliadas**: `workflow`, `workflow_etapa`, `workflow_transicao`, `workflow_instancia`, `workflow_historico`, `tarefa`, `notificacao`, `notificacao_usuario`, `agenda_prazo`, `evento_operacional`, `outbox_evento`, `integracao_sistema`, `integracao_log` e tabelas operacionais dos módulos.
- **Módulos aptos a workflow/notificações/prazos**: Protocolo, GED/OCR, Tributário, Contratos, Jurídico e Financeiro.
- **Fallbacks**: quando tabelas transversais não existem, as telas mostram “em implantação”, não simulam persistência e informam o schema necessário.
- **Prioridades**: consolidar migrations não destrutivas, implementar worker outbox, aprofundar integração com criação real de protocolo/documento/contrato/prazo/conta.

## 20. Sprint de IA, integrações e produção

- **IA:** área `/Ia` criada com governança, assistentes por módulo, logs, política LGPD, mascaramento e fallback honesto quando `sigov.ai_configuracao`/provider não existir.
- **Integrações:** `/Integracoes` mantém monitoramento operacional; documentação orienta conectores oficiais sem simular conexão real.
- **API:** `/api/v1/health` e rotas versionadas preparadas com resposta padronizada/fallback; contrato documentado em `docs/api-publica-sigov.md`.
- **Observabilidade:** `/Operacao/Logs`, `/Operacao/AuditoriaTecnica`, `/Operacao/Metricas`, `/Operacao/Erros` e `/Operacao/Backup` criadas como telas seguras sem stacktrace.
- **Segurança:** `/Seguranca/Politicas`, `/Seguranca/Sessoes`, `/Seguranca/ApiKeys` e `/Seguranca/TentativasLogin` iniciadas com fallback honesto.
- **Backup:** scripts e documentação criados; restore destrutivo não é exposto pela UI.
- **Health:** health visual existente preservado.
- **Riscos LGPD:** prompts e documentos podem conter CPF/CNPJ/e-mail/telefone; camada de IA mascara dados e exige aviso/justificativa.
- **Dependências externas:** IA, OCR, assinatura oficial, SMTP/storage e conectores oficiais dependem de provider, segredo seguro e schemas.
- **Funcionalidades reais:** auditoria via `IAuditTrailService`, inspeção por `IDatabaseSchemaInspector`, rotas MVC/Razor e API health v1.
- **Fallback honesto:** IA, OCR, assinatura e conectores não configurados exibem estado indisponível/em implantação sem simular sucesso oficial.
