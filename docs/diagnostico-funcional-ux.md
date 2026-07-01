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
