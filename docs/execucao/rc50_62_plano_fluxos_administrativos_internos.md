# RC50.62 — plano dos fluxos administrativos internos

Data do inventário: 2026-08-19.

## Inventário técnico

| Eixo | Controllers | Serviços/repositórios persistentes | Superfícies e tabelas principais |
|---|---|---|---|
| RH/Folha | `RhController`, `RhTypedController`, `RhBloco2Controller`, Web `RhController` | `RhService`, `RhTypedService`, `RhRepository` | dashboard, CRUD JSONB/typed, férias, afastamentos, folhas, eventos, lançamentos e integração financeira; `servidores`, `vinculos`, `lotacoes`, `ferias`, `afastamentos`, `folhas`, `folha_eventos`, `folha_lancamentos` |
| Compras/Licitações | `ComprasBloco6Controller`, Web `ComprasController`, `LicitacoesController`, controllers de Compras Empresariais | `ComprasService`, `ComprasRepository` e stack empresarial | dashboard, solicitação e ordem persistentes; fornecedor, cotação e processo possuem tabelas/superfícies, mas várias actions genéricas do Bloco 6 ainda são preparatórias |
| Contratos | `ContratosBloco6Controller`, Web `ContratosController` | `ContratosService`, `ContratosRepository` | criação e medição persistentes; `contrato`, `contrato_medicao`, aditivos e integração financeira |
| Almoxarifado | `AlmoxarifadoBloco6Controller`, Web `AlmoxarifadoController` | `AlmoxarifadoService`, `AlmoxarifadoRepository` | movimento transacional e saldo; `almoxarifado_item`, `almoxarifado_estoque`, `almoxarifado_movimento` |
| Patrimônio | `PatrimonioBloco6Controller`, Web `PatrimonioController` | `PatrimonioService`, `PatrimonioRepository` | tombamento persistente; bens, localizações, movimentos e inventário |
| Frotas | API/Web `FrotasController` | `FrotasService`, `FrotasObrasRepository` | dashboard, listagem e criação de veículo, motorista, abastecimento, manutenção e viagem/ocorrência |
| Obras | API/Web `ObrasController` | `ObrasService`, `FrotasObrasRepository` | dashboard, obra, etapa/cronograma, diário, medição e fiscalização/ocorrência |

## Diagnóstico por capacidade

- **Dashboard/listagem/cadastro:** presentes nos núcleos RH, Frotas e Obras; dashboards e mutações principais do Bloco 6 persistem via Dapper.
- **Detalhe/edição/status/cancelamento/aprovação:** RH possui CRUD genérico. Parte das actions agregadas de Compras, Contratos, Almoxarifado e Patrimônio ainda responde payload preparatório sem mutação e deve ser substituída, não homologada.
- **Medição:** Contratos e Obras persistem; fiscal/competência e transições completas ainda precisam de fechamento E2E.
- **Exportação:** RH exporta persistência e audita. O exportador administrativo genérico ainda usa `OperationalDemoService`, portanto não é considerado fluxo principal homologado.
- **Financeiro/GED/Processos:** há pontes persistentes de Folha e Compras/Contratos e estruturas GED; idempotência existe em pontos específicos, mas a jornada completa deve ser provada em runtime.
- **LGPD:** RH mascara/exporta dados em sua stack; documentos pessoais de servidor e fornecedor exigem revisão de todas as superfícies legadas.
- **Auditoria:** RH, Frotas e Obras auditam mutações; esta entrega adiciona auditoria de negativas em Frotas/Obras. Bloco 6 ainda requer trilha uniforme por action.
- **501:** a busca estática não apontou `NotImplemented`/501 essencial no escopo.
- **Menus/botões:** as rotas MVC principais existem; o modal genérico Excel/PDF/JSON e actions preparatórias do Bloco 6 são pendências reais, não foram ocultados.

## Regras e acessos planejados

A migration RC50.62 cataloga as permissões granulares solicitadas para `rh`, `folha`, `compras`, `licitacao`, `contrato`, `almoxarifado`, `patrimonio`, `frotas` e `obras`, e cria templates dos perfis administrativos. SuperAdmin continua com bypass existente; AdminTenant depende de módulo contratado; coordenadores, operadores, fiscais, Financeiro e Auditor recebem somente grants explícitos do tenant.

Prioridade de implementação: (1) autorização/auditoria backend; (2) invariantes transacionais e idempotência financeira; (3) substituir actions preparatórias por repositories reais; (4) exportação persistente e mascarada; (5) smoke autenticado de menu, botão, rota e segregação por tenant/entidade.
