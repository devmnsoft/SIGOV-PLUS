# CORR03 — fechamento LicitaPro IA no FUNC03

## Escopo

A CORR03 fecha a expansão LicitaPro dentro do módulo FUNC03, sem criar módulo paralelo e sem avançar para Fiscaliza360 ou FUNC21–FUNC24. O fluxo usa autorização persistida, contexto obrigatório de tenant/entidade, Dapper/Npgsql parametrizado e dados reais.

## Entregas

- Razor legível e responsivo para dashboard, fontes, oportunidades, detalhe e workspaces de importações, portal, documentos, checklists, análises, agenda, alertas e auditoria.
- Relacionamentos são escolhidos em listas provenientes do banco; nenhum formulário solicita chave técnica livre.
- POSTs mantêm antiforgery, validação server-side, ModelState e recarga das opções persistidas.
- Oportunidades validam datas; agenda bloqueia oportunidade vencida/cancelada; aprovação documental exige validade e referência.
- CSV tem cabeçalhos específicos para oportunidades, respeita filtros e neutraliza fórmulas iniciadas por `=`, `+`, `-` ou `@`.
- Migration corretiva idempotente reforça URL configurada, explicações, referências, índices e isolamento relacional de checklist/critério.

## Operação

As rotas permanecem sob `/Compras/LicitaPro/*`. Ausência de schema, tenant, entidade, permissão ou configuração é erro explícito, nunca sucesso simulado. A auditoria registra criação, vínculo, aprovação, agenda e exportação sem payload documental sensível.
