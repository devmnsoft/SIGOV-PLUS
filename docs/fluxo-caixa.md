# fluxo caixa

Documento da evolução Pós-Build 07 do SIGOV PLUS.

- Módulo SaaS: `financeiro_empresarial`.
- Isolamento: todo registro financeiro operacional usa `tenant_id`.
- Auditoria: criação, alteração, baixa, cancelamento, estorno, conciliação e recálculo geram eventos auditáveis.
- Permissões: usuários sem permissão recebem bloqueio e o menu dinâmico oculta a rota.
- Integrações: vendas, PDV, compras, OS, contratos, indústria/produção e SaaS alimentam contas, movimentos e fluxo de caixa.

Consulte `docs/financeiro-integrado.md` para o fluxo geral.
