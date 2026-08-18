# RC50.48 — Educação avançada

## Entrega
Foram implementados núcleos persistidos, multi-tenant e incrementais de Transporte Escolar, Merenda/Cardápio/Estoque, Biblioteca Digital e FUNDEB/Custos/Indicadores/Educacenso preparatório. As quatro migrations criam 48 tabelas, índices tenant/status e unicidade de aluno por rota/turno e exemplar por escola.

## Aplicação e API
Contratos tipados, repositório Dapper com allowlist, SQL parametrizado, filtros por tenant/escola/ano letivo, regras de transição, CSV sem documentos pessoais e dashboards reais compõem a camada funcional. Controllers API expõem cadastro, consulta, status, painéis e relatórios dos quatro domínios.

## Web, LGPD e auditoria
As rotas Web canônicas usam uma tela institucional responsiva com KPIs, filtros, tabela, empty state, badge LGPD e timeline. Mutações registram usuário, CorrelationId e auditoria JSONB; consultas aplicam tenant e soft delete. FUNDEB e Educacenso são exclusivamente preparatórios, sem alegação de integração oficial MEC/INEP.

## Pendências RC50.49
Homologar permissões granulares por ação, conectar alertas transversais e Portal do Aluno, ampliar formulários específicos e executar build, PostgreSQL, Swagger e login em ambiente com .NET/PostgreSQL disponíveis.
