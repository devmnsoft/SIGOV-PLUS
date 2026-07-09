# Diagnóstico Enterprise Pós-RC 07

| Módulo | Tela | Rota Web | API | CRUD | Banco | Status atual | O que corrigir |
|---|---|---|---|---|---|---|---|
| Comercial | Clientes/Leads/Oportunidades/Propostas/Pedidos/Tabelas | `/Comercio/*` e `/Comercial/*` | `/api/comercial/*`, `/api/comercio/*` | Listar/criar/ações e CSV | `sigov.enterprise_cliente`, `lead`, `oportunidade`, `proposta`, `pedido_venda`, `tabela_preco`, `comissao` | Serviço Dapper com fallback honesto | Evoluir PUT por id e permissões granulares por papel |
| OS | Dashboard/Ordens/Agenda/Checklist/Apontamentos | `/OrdemServico/*` | `/api/os/ordens` | Criar, status, apontar, consumir peça, CSV | `enterprise_ordem_servico`, `os_*` | Operável com auditoria | Especializar telas de agenda/checklist |
| Estoque/Compras | Produtos/Almoxarifados/Movimentos/Saldos/Requisições/Fornecedores/Pedidos | `/Estoque/*`, `/ComprasComercial/*` | `/api/estoque/*`, `/api/compras/*` | Criar, saldos, movimentos, bloqueio saldo negativo | `enterprise_produto`, `almoxarifado`, `estoque_saldo`, `estoque_movimento`, `fornecedor`, `pedido_compra` | Operável | Recebimento de compra detalhado por item |
| Industrial | Ativos/Planos/Medidores/Paradas | `/Industrial/*` | `/api/industrial/*` | Criar, leitura, gerar OS preventiva, CSV | `enterprise_ativo_industrial`, `plano_manutencao`, `medidor`, `leitura_medidor`, `parada_falha` | Operável | Regras avançadas de periodicidade |
| Indústria | Produção | `/Industria/*` | `/api/industria/*` | MVP mapeado em tabelas | `enterprise_*producao*` | Base de dados criada | Completar API específica de produção |

## Achados
- `EnterpriseModuleService` era memória; permanece apenas como fallback honesto sem `ConcurrentDictionary` para testes e ambientes sem migration.
- A implementação real está em Dapper/PostgreSQL, com tenant obrigatório, `is_deleted=false`, parâmetros Dapper, auditoria e LGPD mascarada.
- `EnterprisePageControllerBase` não usa tenant fixo em produção; usa contexto/claim/header e fallback apenas em Development.
- `ModulePage` deixou de ser informativa e passou a listar, criar, filtrar visualmente, detalhar, editar, exportar e orientar inativação.
