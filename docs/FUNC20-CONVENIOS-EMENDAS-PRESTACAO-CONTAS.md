# FUNC20 — Convênios, Emendas e Prestação de Contas

## Escopo
O módulo MVC `/Convenios` gerencia órgãos concedentes, programas, emendas, instrumentos, projetos, metas, etapas, contas vinculadas, repasses, despesas, contrapartidas, acompanhamentos, prestações, diligências e documentos. Todos os dados são persistidos no PostgreSQL por Dapper e segregados por `tenant_id` e `entity_id`.

## Banco e auditoria
A migration `20260826100000_func20_convenios_emendas_prestacao_contas.sql` cria as 16 tabelas `sigov.convenio_*`, chaves `bigint identity`, FKs, unicidades, checks financeiros, de datas e percentuais, índices operacionais e as 23 permissões `CONVENIO_*`. Inclusões, alterações e exclusões geram registros em `convenio_auditoria` na mesma transação.

## Rotas e formulários
Há dashboard e telas reais nas rotas `/Convenios/{Orgaos|Programas|Emendas|Instrumentos|Projetos|Metas|Etapas|ContasBancarias|Repasses|Despesas|Contrapartidas|Acompanhamentos|PrestacoesContas|Diligencias|Documentos|Auditoria}`. Relacionamentos são sempre selects carregados do banco e filtrados pelo contexto; IDs nunca são campos de digitação. POSTs usam antiforgery, ModelState e recarregamento das opções.

## Regras e relatórios
As regras de composição do instrumento, datas, conclusão, pagamento, aprovação e resposta são validadas na aplicação e reforçadas por constraints. O dashboard consulta indicadores reais, inclusive vencimentos. `/Convenios/Relatorios` exporta CSV de instrumentos, emendas, projetos, metas/etapas, repasses, despesas, prestações e diligências, com filtros, RBAC, isolamento contextual, cabeçalhos legíveis e neutralização de CSV injection.

## Operação
Configurar somente `ConnectionStrings__DefaultConnection`. Não há dados simulados ou fallback. Ausência de schema ou contexto resulta em falha explícita.
