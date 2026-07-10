# Matriz CRUD Enterprise Pós-RC 08

| Área | Entidades | Listar | Criar | Editar | Detalhar | Inativar | CSV | Observação |
|---|---|---:|---:|---:|---:|---:|---:|---|
| Comercial | clientes, leads, oportunidades, propostas, pedidos, tabelas, comissões | Sim | Sim | Sim | Sim | Sim | Sim | Ações comerciais específicas mantidas |
| OS | ordens, checklist, apontamentos, agenda | Sim | Sim | Sim | Sim | Sim | Sim | Status e consumo de peça disponíveis |
| Estoque | produtos, almoxarifados, movimentos, saldos, requisições | Sim | Sim | Sim | Sim | Sim | Sim | Saldo negativo bloqueado por padrão |
| Compras | fornecedores, pedidos | Sim | Sim | Sim | Sim | Sim | Sim | Recebimento detalhado é evolução |
| Industrial | ativos, planos, medidores, paradas | Sim | Sim | Sim | Sim | Sim | Sim | OS preventiva e leituras disponíveis |
| Indústria | centros, recursos, produtos, fichas, roteiros, OP, qualidade, custos | Sim | Sim | Sim | Sim | Sim | Sim | MVP produtivo via CRUD genérico |


## Pós-RC 09 — QA funcional Enterprise

- Diagnóstico criado em `docs/diagnostico-enterprise-pos-rc-09.md`.
- Evidências de homologação registradas em `docs/evidencias-enterprise-pos-rc-09.md` e `docs/evidencias-enterprise-pos-rc-09.json`.
- Manual de usuário e checklist QA criados para a jornada Enterprise navegável.
- UX Enterprise refinada com filtros, paginação, loading, detalhes, edição, inativação, restauração, CSV com tenant, toasts e fallback honesto.
