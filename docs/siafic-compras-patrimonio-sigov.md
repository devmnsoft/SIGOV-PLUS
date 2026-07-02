# Sprint SIAFIC, Compras, Patrimônio e Gestão Administrativa — SIGOV PLUS

## Escopo
Cria a fundação MVC/Razor para SIAFIC/Contabilidade, Planejamento, Tesouraria, Compras, Licitações, Contratos administrativos, Almoxarifado, Patrimônio, Frotas, Obras e Transparência.

## Requisitos atendidos
- Controllers dedicados por módulo, rotas principais e tela operacional padrão.
- Services Dapper schema-safe baseados em `IDatabaseSchemaInspector`.
- Auditoria por `IAuditTrailService` em consultas e POSTs críticos.
- `CancellationToken`, `ILogger`, try/catch e fallback honesto.
- POSTs criados com `[ValidateAntiForgeryToken]` e mensagem via `TempData`.
- Partials administrativas reutilizáveis e ViewModel base.
- Diagnóstico SQL e script PowerShell para mapear schema antes de ativar ações reais.

## Limitações honestas
Nenhum empenho, liquidação, pagamento, licitação, tombamento, contrato ou obra é persistido oficialmente nesta sprint sem schema/regra homologados. Números oficiais não são gerados em fallback.

## Módulos criados
SIAFIC, Planejamento, Tesouraria, Compras, Licitações, Almoxarifado, Patrimônio, Frotas, Obras e Transparência usam a view operacional padrão com cards, filtros, tabela, CSV visual, timeline, auditoria e LGPD.

## Integrações planejadas
Eventos/outbox previstos: compra solicitada, licitação criada/homologada, contrato criado/vencendo, empenho gerado, pagamento realizado, bem incorporado, movimento de almoxarifado e obra medida.

## Tabelas
As tabelas candidatas estão listadas em `database/diagnostics/schema-report-siafic-administrativo.sql` e são verificadas em runtime por `information_schema` antes de exibir dados reais.

## Fluxos
- Compras → Licitações → Contratos → Financeiro/SIAFIC.
- Almoxarifado → Patrimônio.
- Obras → Contratos.
- Tributário → Receita arrecadada.
- RH → Empenho/Folha futura.

## Pontos de auditoria e LGPD
Consultas reais e exportações devem auditar. Documentos pessoais são mascarados nas listagens. Dados sensíveis não aparecem em stacktrace para usuário final.

## Próximos passos
Homologar schema físico, ativar persistência real com regras oficiais, ampliar permissões granulares, criar outbox físico e smoke E2E autenticado.
