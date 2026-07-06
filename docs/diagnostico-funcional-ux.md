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

## 21. Sprint de implantação, suporte, POC e operação contratual

- **Implantação:** rota `/Implantacao` criada com consulta schema-safe para `sigov.implantacao`, `sigov.implantacao_etapa`, `sigov.implantacao_evidencia` e `sigov.aceite_formal`. Funcional quando as tabelas existem; fallback honesto sem simular etapa concluída.
- **Migração:** rota `/Migracao` cobre lotes, logs, validações e importação confirmada; não importa quando schema real não existe.
- **Treinamentos:** rota `/Treinamentos` cobre turmas, participantes, avaliações e certificados; emissão é auditada e não simulada sem tabela.
- **Suporte/SLA:** rotas `/Suporte` e `/Sla` cobrem chamados, satisfação, regras, eventos e monitoramento com dados pessoais mascarados.
- **POC:** rota `/Poc` evoluída para roteiro, requisitos, execuções, evidências e relatório com critério Atende/Não Atende.
- **Aceite formal:** rota `/Aceites` consolida aceite de etapa, POC, treinamento, migração, suporte e implantação.
- **Tabelas avaliadas:** ver `database/diagnostics/schema-report-implantacao-suporte.sql`.
- **Funcional:** navegação MVC/Razor, schema detection, auditoria, modal de confirmação, antiforgery em POST, fallback honesto e layout operacional premium.
- **Parcial:** persistência específica depende das tabelas físicas e colunas reais do tenant.
- **Pendências:** migrations aditivas definitivas, PDFs, webhooks externos e cálculo avançado de SLA por calendário contratual.

## 22. Sprint SIAFIC, Compras, Patrimônio e Gestão Administrativa

| Módulo | Rotas | Controller | Views | Services | Tabelas previstas | Tabelas encontradas | Status atual | Funcional | Parcial | Demonstrativo / implantação | Salva de verdade | Fallback | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| SIAFIC / Contabilidade | `/Siafic`, `/Siafic/Dashboard`, `/Siafic/PlanoContas`, `/Siafic/Dotacoes`, `/Siafic/Empenhos`, `/Siafic/Liquidacoes`, `/Siafic/Pagamentos`, `/Siafic/Receitas`, `/Siafic/Relatorios` | `SiaficController` | `Views/Operational/Module.cshtml` + partials administrativas | `SiaficService`, `IDatabaseSchemaInspector`, `IAuditTrailService` | `plano_contas`, `dotacao_orcamentaria`, `empenho`, `liquidacao`, `pagamento`, `receita_arrecadada`, `fonte_recurso` | runtime via `information_schema` | Parcial/Em implantação | navegação, KPIs, listagem real se tabela existir | persistência oficial pendente | sem simular empenho/pagamento | Não | Sim | Alta |
| Planejamento | `/Planejamento`, `/Planejamento/Ppa`, `/Planejamento/Ldo`, `/Planejamento/Loa`, `/Planejamento/Programas`, `/Planejamento/Acoes`, `/Planejamento/AlteracoesOrcamentarias` | `PlanejamentoController` | Operacional padrão | `PlanejamentoService` | `ppa`, `ldo`, `loa`, `programa_governo`, `acao_governo`, `alteracao_orcamentaria` | runtime | Parcial | visão navegável integrada ao SIAFIC | atos oficiais pendentes | alteração sancionada não simulada | Não | Sim | Alta |
| Tesouraria | `/Tesouraria`, `/Tesouraria/ContasBancarias`, `/Tesouraria/Movimentos`, `/Tesouraria/Conciliacao`, `/Tesouraria/Arrecadacao`, `/Tesouraria/Pagamentos` | `TesourariaController` | Operacional padrão | `TesourariaService` | `conta_bancaria`, `movimento_bancario`, `conciliacao_bancaria`, `pagamento`, `receita_arrecadada` | runtime | Parcial | contas, movimentos e indicadores quando schema existir | conciliação real pendente | pagamento não simulado | Não | Sim | Alta |
| Compras | `/Compras`, `/Compras/Solicitacoes`, `/Compras/Solicitacoes/Nova`, `/Compras/Fornecedores`, `/Compras/Itens`, `/Compras/Relatorios` | `ComprasController` | Operacional padrão | `ComprasService` | `compra_solicitacao`, `compra_item`, `fornecedor`, `produto_servico` | runtime | Parcial | fluxo inicial e POST auditado | workflow real futuro | solicitação não simulada sem schema | Não | Sim | Alta |
| Licitações | `/Licitacoes`, `/Licitacoes/Processos`, `/Licitacoes/Processos/Novo`, `/Licitacoes/Itens`, `/Licitacoes/Fornecedores`, `/Licitacoes/Relatorios` | `LicitacoesController` | Operacional padrão | `LicitacoesService` | `licitacao`, `licitacao_item`, `fornecedor`, `compra_solicitacao` | runtime | Parcial | visão inicial | regra/número oficial pendente | processo oficial não simulado | Não | Sim | Alta |
| Contratos administrativos | `/Contratos`, `/Contratos/Listar`, `/Contratos/Novo`, `/Contratos/Detalhes/{id}`, `/Contratos/Aditivos`, `/Contratos/Fiscais`, `/Contratos/Vencimentos`, `/Contratos/Medicoes` | `ContratosController` | Operacional padrão | `ContratosOperationalService` | `contrato`, `contrato_aditivo`, `contrato_fiscal`, `contrato_documento`, `contrato_medicao` | runtime | Parcial | gestão visual, vencimentos, auditoria | persistência depende de schema | contrato não simulado | Não | Sim | Alta |
| Almoxarifado | `/Almoxarifado`, `/Almoxarifado/Produtos`, `/Almoxarifado/Entradas`, `/Almoxarifado/Saidas`, `/Almoxarifado/Movimentos`, `/Almoxarifado/Inventario` | `AlmoxarifadoController` | Operacional padrão | `AlmoxarifadoService` | `almoxarifado_produto`, `almoxarifado_movimento`, `almoxarifado_estoque`, `fornecedor` | runtime | Parcial | visão de materiais | estoque real pendente | saldo não simulado | Não | Sim | Alta |
| Patrimônio | `/Patrimonio`, `/Patrimonio/Bens`, `/Patrimonio/Bens/Novo`, `/Patrimonio/Movimentos`, `/Patrimonio/Inventario`, `/Patrimonio/Depreciacao` | `PatrimonioController` | Operacional padrão | `PatrimonioService` | `patrimonio_bem`, `patrimonio_movimento`, `patrimonio_inventario`, `patrimonio_depreciacao` | runtime | Parcial | base navegável | tombamento oficial pendente | tombamento não simulado | Não | Sim | Alta |
| Frotas | `/Frotas`, `/Frotas/Veiculos`, `/Frotas/Abastecimentos`, `/Frotas/Manutencoes`, `/Frotas/Multas`, `/Frotas/Relatorios` | `FrotasController` | Operacional padrão | `FrotasService` | `frota_veiculo`, `frota_abastecimento`, `frota_manutencao`, `frota_multa` | runtime | Parcial | visão de veículos/custos | movimentos reais pendentes | abastecimento não simulado | Não | Sim | Média |
| Obras | `/Obras`, `/Obras/Listar`, `/Obras/Nova`, `/Obras/Medicoes`, `/Obras/Diario`, `/Obras/Fotos`, `/Obras/Fiscalizacao` | `ObrasController` | Operacional padrão | `ObrasService` | `obra`, `obra_medicao`, `obra_diario`, `obra_foto`, `obra_fiscalizacao` | runtime | Parcial | fiscalização inicial | storage/medições pendentes | obra não simulada | Não | Sim | Alta |
| Transparência | `/Transparencia`, `/Transparencia/Receitas`, `/Transparencia/Despesas`, `/Transparencia/Contratos`, `/Transparencia/Licitacoes`, `/Transparencia/Servidores`, `/Transparencia/Obras` | `TransparenciaController` | Operacional padrão | `TransparenciaService` | `receita_arrecadada`, `empenho`, `pagamento`, `contrato`, `licitacao`, `servidor`, `obra` | runtime | Parcial | publicação administrativa inicial | portal público futuro | dados pessoais mascarados | Não | Sim | Alta |
| Integração Tributário → Contabilidade | planejada | services operacionais | N/A | outbox futuro | `receita_arrecadada`, `guia`, `debito` | runtime | Em implantação | contrato de eventos documentado | ativação futura | não simula arrecadação | Não | Sim | Alta |
| Integração RH → Contabilidade | planejada | services operacionais | N/A | outbox futuro | folha/empenho futuro | runtime | Em implantação | contrato de eventos documentado | folha futura | não simula empenho | Não | Sim | Alta |
| Integração Compras → Contratos → Financeiro | planejada | `Compras`, `Licitacoes`, `Contratos`, `Financeiro` | operacional padrão | outbox futuro | compra, licitação, contrato, pagamento | runtime | Em implantação | pontos de evento preparados/documentados | orquestração futura | não simula contrato/pagamento | Não | Sim | Alta |

## 23. Sprint Setorial — Educação, Saúde, Saneamento, Social, Agro, Portal e Mobile

| Módulo | Rota | Controller | Views | Service | Tabelas previstas | Tabelas encontradas | Status atual | Funcional/parcial/demonstrativo/implantação | Salva de verdade | Fallback | Riscos LGPD | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Educação | `/Educacao` | `EducacaoController` | Operational/Sectors | `OperationalDemoService` + base setorial | `educacao_*` | Ver `docs/schema-report-setorial-local.md` | Parcial | Listagens/rotas e POST protegido sem simulação | Só quando schema existir | Sim | Alunos/responsáveis | Alta |
| Saúde | `/Saude` | `SaudeController` | Operational/Sectors | idem | `saude_*` | Ver relatório | Parcial | Pacientes, unidades, agendas, procedimentos | Só com schema | Sim | Saúde/CNS/CPF | Alta |
| ACS | `/Acs` | `AcsController` | `Views/Sectors/Module.cshtml` | `SectorModuleService` | `saude_acs`, família, domicílio, visita | Ver relatório | Em implantação/parcial | Base campo, mapa placeholder, sync planejada | Não simula | Sim | Saúde/família | Alta |
| Saneamento | `/Saneamento` | `SaneamentoController` | Operational/Sectors | idem | `saneamento_*` | Ver relatório | Parcial | Consumidores, ligações, leituras, faturas, OS | Só com schema/motor | Sim | Documento/endereço | Alta |
| Social | `/Social` | `SocialController` | Sectors + views existentes | `SectorModuleService` | `social_*` | Ver relatório | Parcial | Famílias, pessoas, atendimentos, benefícios | Só com schema | Sim | Vulnerabilidade social | Alta |
| Agro | `/Agro` | `AgroController` | Views existentes | Serviços agro existentes | `agro_*` | Ver relatório | Parcial | Produtores, propriedades, programas | Existente conforme módulo | Sim | Documentos | Média |
| Portal Cidadão | `/PortalCidadao` | `PortalCidadaoController` | Sectors | `SectorModuleService` | `portal_*`, ouvidoria | Ver relatório | Em implantação | Catálogo/solicitações | Não simula protocolo | Sim | Dados públicos | Alta |
| Portal Contribuinte | `/PortalContribuinte` | `PortalContribuinteController` | Sectors | `SectorModuleService` | contribuinte/débito/guia/protocolo | Ver relatório | Em implantação | Débitos/guias/certidões futuras | Não simula guia | Sim | Fiscal | Alta |
| Ouvidoria | `/Ouvidoria` | `OuvidoriaController` | Processos existentes | Serviço existente/futuro | ouvidoria/protocolo | Ver relatório | Parcial | Manifestação básica | Não simular | Sim | Denúncias/dados pessoais | Alta |
| Mobile/Campo | `/MobileCampo` | `MobileCampoController` | Sectors | `SectorModuleService` | `campo_*` | Ver relatório | Em implantação | Roteiros/coletas/evidências/sync planejada | Não simula sync | Sim | Evidências | Alta |
| GIS | `/Gis` | `GisController` | Sectors | `SectorModuleService` | `gis_*` | Ver relatório | Em implantação | Camadas/mapa placeholder | Não | Sim | Geolocalização | Média |
| BI Setorial | `/BiSetorial` | `BiSetorialController` | Sectors | `SectorModuleService` | agregadas setoriais | Ver relatório | Em implantação | KPIs com fonte real quando houver | Não aplicável | Sim | Agregação | Média |
| Relatórios Setoriais | `/Relatorios` | `RelatoriosController` | existentes | existentes/futuro | setoriais | Ver relatório | Parcial | Base para CSV seguro | Só com schema | Sim | Exportação | Alta |

## 24. Sprint Patrimônio, Inventário e Obras

### Estado atual
- **Patrimônio:** módulo operacional parcial com Dapper e inspeção de schema; rotas de dashboard, bens, localizações, responsáveis, movimentos, depreciação, relatórios e CSV seguro.
- **Inventário:** fluxo inicial rastreável para campanhas, itens, conclusão, divergências e relatórios; sem simular conclusão quando não há tabelas.
- **Obras:** módulo operacional parcial para obras, medições, diário, fotos, fiscalização, relatórios e CSV.
- **Contratos:** integração preparada por tabelas `contrato`, `contrato_aditivo`, `contrato_fiscal` e `contrato_documento`.
- **Almoxarifado:** integração preparada por `almoxarifado_produto` e `almoxarifado_movimento`.
- **RH integrado:** responsáveis/fiscais previstos por `rh_servidor` e `rh_lotacao`, com mascaramento e minimização LGPD.
- **Financeiro/SIAFIC integrado:** ponte prevista por `conta_pagar`, `empenho`, `liquidacao`, `pagamento`, `patrimonio_bem` e `obra_medicao`.

### Tabelas monitoradas
`pessoa`, `endereco`, `rh_servidor`, `rh_lotacao`, `contrato`, `contrato_aditivo`, `contrato_fiscal`, `contrato_documento`, `almoxarifado_produto`, `almoxarifado_movimento`, tabelas `patrimonio_*`, tabelas `obra_*`, `conta_pagar`, `empenho`, `liquidacao`, `pagamento` e `auditoria_evento`.

### Rotas e componentes
Controllers existentes/evoluídos: `PatrimonioController`, `ObrasController`, `ContratosController`, `AlmoxarifadoController`, `RhController`, `FinanceiroController`, `MobileCampoController`, `BuscaController`, `RelatoriosController`. Criado `InventarioController`.
Services existentes/evoluídos: `PatrimonioService`, `ObrasService`; criado `InventarioService`; todos usam `IDatabaseSchemaInspector` via base operacional.

### Funcional/parcial/fallback
Funcional quando as tabelas reais existem e colunas são detectadas por `information_schema`. Parcial quando uma parte do schema existe. Fallback honesto quando nenhuma tabela física é encontrada, com status “Em implantação neste ambiente”.

### Riscos LGPD
Responsáveis, fiscais, CPF, matrícula e e-mail devem permanecer mascarados em listagens e exportações. Evidências fotográficas futuras exigem finalidade, retenção e controle de acesso.

### Pendências
DDL não destrutiva, regras oficiais de tombamento, cálculo de depreciação, armazenamento validado de fotos, integração SIAFIC oficial, permissões finas por ação e testes em ambiente com Docker/.NET.

## 26. Sprint Matriz de Aderência, Editais e POC Automatizada

### Estado atual da POC

O SIGOV PLUS já possuía rotas de POC operacional, implantação, suporte e aceite formal. Nesta sprint foi adicionada uma camada específica por edital, com roteiro, execução binária e bloqueio conceitual para não aprovar POC com requisito crítico não atendido.

### Estado atual da documentação

Foram criados documentos específicos para matriz de aderência, relatório de schema, smoke tests e checklist manual. A documentação orienta o uso seguro e reforça que fallback não representa funcionalidade persistida.

### Estado atual dos módulos

O catálogo estratégico cobre SaaS/Admin, Segurança, Pessoa/Endereço, RH, Protocolo, GED, Tributário, Contratos, Jurídico, Financeiro, Patrimônio, Obras, Portal, Transparência, Ouvidoria, Saúde, Educação, Saneamento, Social, Agro, BI, API, Integrações e Mobile/Campo.

### Módulos com evidências

Módulos com rotas operacionais e health checks podem ser evidenciados por URL, rota, relatório ou documento, desde que a evidência seja validada no fluxo de editais.

### Módulos apenas em fallback

Sem o schema `sigov.edital*`, os cadastros de edital, requisito, evidência e POC ficam em fallback honesto. O sistema não simula gravação nem atendimento.

### Requisitos já cobertos

Estão cobertos como organização operacional: sistema web, SaaS, multi-tenant, LGPD/auditoria, relatórios, POC, suporte, SLA, implantação, migração e módulos setoriais. A marcação como atendimento depende de evidência.

### Requisitos que precisam evolução

Persistência completa das tabelas de edital/POC, permissões granulares, storage de anexos, exportação PDF/DOCX e integração profunda com cada módulo devem evoluir na próxima sprint.

### Riscos de demonstrar sem persistência

Demonstrar cadastro em fallback pode induzir falsa aderência. Por isso as telas exibem aviso e ações críticas são auditadas/logadas.

### Riscos LGPD

Evidências não devem conter CPF, prontuários, dados de saúde, dados educacionais identificáveis ou documentos pessoais sem base legal, mascaramento e controle de acesso.

### Próximos passos

Criar migrations não destrutivas, habilitar permissões por perfil, ampliar relatórios em `/Relatorios`, integrar `/Busca`, adicionar dashboard operacional e validar em ambiente com .NET/Docker.

## 27. Sprint de consolidação funcional e integração real dos módulos existentes

### Síntese

Esta sprint muda a estratégia de criação de módulos para consolidação do que já existe. A aplicação contém controllers navegáveis e serviços operacionais com fallback honesto; a evolução segura exige confirmar schema, habilitar persistência real apenas onde houver tabela/colunas, preservar Dapper, auditoria, LGPD e multi-tenancy.

### Classificação inicial

- **Módulos já existentes:** ver `docs/inventario-modulos-sigov.md`.
- **Persistência real:** SaaS/Admin, usuários/tenants, relatórios administrativos e partes de cadastros setoriais onde migrations já existem.
- **Apenas navegáveis/fallback:** fluxos críticos de Protocolo, GED, Workflow, Tarefas, Notificações e módulos operacionais ainda dependem de confirmação do schema.
- **POST sem persistência:** ações críticas mantêm antiforgery/auditoria e mensagem honesta quando o schema não está homologado.
- **Sem auditoria/LGPD/relatório/busca:** marcados como prioridade de endurecimento no inventário e no checklist LGPD.

| Módulo | Controller | Service | Views | Tabelas | Status | Salva? | Fallback? | Auditoria? | LGPD? | Busca? | Relatório? | Prioridade |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Protocolo | ProtocoloController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P1 |
| GED | GEDController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P2 |
| Workflow | WorkflowController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P3 |
| Tarefas | TarefasController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P4 |
| Notificações | NotificaçõesController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P5 |
| Compras | ComprasController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P6 |
| Licitações | LicitaçõesController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P7 |
| Contratos | ContratosController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P8 |
| Financeiro/SIAFIC | FinanceiroController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P9 |
| Patrimônio | PatrimônioController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Almoxarifado | AlmoxarifadoController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Obras | ObrasController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Portal/Ouvidoria | PortalController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Dashboard/Minha Central | DashboardController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Busca | BuscaController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Relatórios | RelatóriosController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| Outbox/Worker | OutboxController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |
| LGPD/Auditoria | LGPDController quando existente | ver inventário | Operational/Module, Hub ou view própria | ver gaps-schema | Parcial | Somente onde schema existe | Sim, honesto | Parcial | Parcial | Parcial | Parcial | P10 |

### Prioridades

1. Validar migrations aditivas transversais.
2. Ativar persistência real em Protocolo + GED + Workflow somente com schema confirmado.
3. Ampliar busca/relatórios com validação de tabela e máscara LGPD.
4. Aplicar permissões finas nas ações críticas antes de expor botões.
