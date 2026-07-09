# Diagnóstico Enterprise Pós-RC 08

## Escopo inspecionado
Foram inspecionados a migration `20260709120000_enterprise_funcional_crud.sql`, o seed `enterprise_demo_seed.sql`, contratos e serviços Enterprise, controllers API/Web, template Razor, JavaScript operacional, smoke test, CI e documentação Pós-RC 07.

## Implementado de fato
- Tabelas `sigov.enterprise_*` idempotentes para Comercial, OS, Estoque/Compras, Industrial/Manutenção, Indústria Produção, eventos e auditoria.
- Seed demo idempotente para dados fictícios Enterprise.
- Serviço Dapper para listagem, criação, atualização, inativação/restauração, ações operacionais, dashboard, CSV e busca.
- Controllers com rotas legadas e rotas REST `/api/enterprise/{area}` para CRUD.
- Páginas MVC/Razor reutilizando `ModulePage.cshtml` com KPIs, filtros, tabela, modal de edição, detalhes, exportação e LGPD.
- Smoke Enterprise cobrindo rotas Web/API críticas e documentação de evidência.

## Ainda genérico ou parcial
- O template é propositalmente operacional e genérico por área; campos específicos continuam concentrados em `dados_json`.
- Produção industrial tem MVP CRUD via tabelas Enterprise, mas regras avançadas de chão de fábrica permanecem como próxima evolução.
- Permissões granulares devem ser conectadas ao provedor real de autorização em homologação; o backend mantém tenant e auditoria.

## CRUD completo
Clientes, leads, oportunidades, propostas, pedidos, produtos, almoxarifados, requisições, fornecedores, pedidos de compra, ativos, planos, medidores, paradas, centros de trabalho, recursos, produtos industriais, fichas técnicas, roteiros e ordens de produção possuem listagem, criação, edição, detalhes, inativação e CSV quando operados pelo template Enterprise.

## Telas apenas consolidadoras
Dashboards, saldos, agenda, checklist, apontamentos, programadas, qualidade, custos e chão de fábrica exibem dados reais/empty state e ações compatíveis com o endpoint associado; algumas regras profundas continuam documentadas como pendência honesta.

## Ações funcionais
Aprovar/reprovar proposta, gerar pedido, confirmar/cancelar pedido, gerar OS, agendar/iniciar/pausar/concluir/cancelar OS, checklist/apontamento, consumir peça, entrada/saída/ajuste de estoque, gerar OS preventiva e registrar leitura de medidor.

## Rotas validadas em análise estática
- Web: `/Comercio/*`, `/OrdemServico/*`, `/Estoque/*`, `/ComprasComercial/*`, `/Industrial/*`, `/Industria/*`.
- API legada: `/api/comercial/*`, `/api/comercio/*`, `/api/os/ordens`, `/api/estoque/*`, `/api/compras/*`, `/api/industrial/*`.
- API REST: `/api/enterprise/{area}`, `/api/enterprise/{area}/{id}`, inativar/restaurar e CSV.

## Riscos técnicos
- Ambiente local sem SDK .NET impede build/test reais nesta execução.
- Ambiente local sem Docker/PowerShell Core operacional pode impedir E2E completo.
- Produção industrial ainda é MVP genérico, não APS/MES completo.

## Plano de correção aplicado
1. Corrigir atualização real por `PUT /{id}` e inativação por `DELETE` no JS/template.
2. Adicionar rotas REST genéricas Enterprise na API.
3. Ajustar ciclo de vida para soft delete real e restauração.
4. Garantir `NOT_FOUND` quando update/delete/action não afetar registro do tenant.
5. Ampliar rotas de Indústria para endpoints listáveis.
6. Documentar limites honestos, evidências e roteiro manual Pós-RC 08.
