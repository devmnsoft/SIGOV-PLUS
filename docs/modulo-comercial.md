# Módulo Comercial/CRM

## Objetivo

Clientes, leads, oportunidades, propostas, pedidos e tabelas de preço.

## Segurança SaaS, auditoria e LGPD

- Todas as tabelas operacionais possuem `tenant_id` obrigatório.
- Listagens retornam documento, e-mail e telefone mascarados.
- Ações de criar, editar, aprovar, reprovar, cancelar, inativar, movimentar estoque e alterar status geram evento de auditoria com `correlationId`.
- O acesso é condicionado a módulo contratado e permissões por prefixo (`comercial.*`, `os.*`, `industrial.*`, `estoque.*`, `compras.*`, `comercio.*`).

## APIs base

- `/api/comercial/clientes`
- `/api/comercial/propostas/{id}/aprovar`
- `/api/comercial/propostas/{id}/gerar-pedido`

## Telas base

- `/Comercial/Dashboard`
- `/Comercial/Clientes`
- `/Comercial/Propostas`
- `/Comercial/Pedidos`

## Integrações

- Proposta aprovada pode gerar pedido.
- Pedido confirmado pode gerar OS quando aplicável.
- OS pode consumir peças do estoque.
- Plano preventivo pode gerar OS preventiva.
- Produto abaixo do mínimo gera alerta operacional.
- OS concluída registra evento para financeiro futuro.
