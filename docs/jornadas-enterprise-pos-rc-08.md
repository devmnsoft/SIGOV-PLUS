# Jornadas Enterprise Pós-RC 08

## Comercial
Lead/Oportunidade/Proposta/Pedido operam por CRUD Enterprise. Proposta aprovada pode gerar pedido; proposta reprovada bloqueia geração; pedido confirmado pode gerar OS; pedido cancelado bloqueia OS.

## Ordem de Serviço
OS pode ser criada, editada, agendada, iniciada, pausada, concluída ou cancelada. Checklist/apontamento geram filhos operacionais e consumo de peça baixa estoque com bloqueio de saldo negativo.

## Estoque e Compras
Produto gera saldo inicial, entrada/saída/ajuste atualizam saldo, fornecedor e pedido de compra têm CRUD. Produto abaixo do mínimo alimenta alerta de dashboard.

## Industrial/Manutenção
Ativo, plano, medidor e parada possuem CRUD. Plano gera OS preventiva, leitura entra em histórico operacional e parada registra evento/alerta.

## Indústria Produção
Centros, recursos, produtos industriais, fichas, roteiros e OP usam CRUD MVP em tabelas `enterprise_*`; apontamentos, qualidade, paradas e custos têm rotas operáveis para homologação inicial.


## Pós-RC 09 — QA funcional Enterprise

- Diagnóstico criado em `docs/diagnostico-enterprise-pos-rc-09.md`.
- Evidências de homologação registradas em `docs/evidencias-enterprise-pos-rc-09.md` e `docs/evidencias-enterprise-pos-rc-09.json`.
- Manual de usuário e checklist QA criados para a jornada Enterprise navegável.
- UX Enterprise refinada com filtros, paginação, loading, detalhes, edição, inativação, restauração, CSV com tenant, toasts e fallback honesto.
