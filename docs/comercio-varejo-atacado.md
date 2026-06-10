# Base de Comércio Varejista e Atacadista

## Objetivo

Base navegável para varejo e atacado integrada ao Comercial e Estoque, sem PDV completo e sem fiscal nesta etapa.

## Segurança SaaS, auditoria e LGPD

- Todas as tabelas operacionais possuem `tenant_id` obrigatório.
- Listagens retornam documento, e-mail e telefone mascarados.
- Ações de criar, editar, aprovar, reprovar, cancelar, inativar, movimentar estoque e alterar status geram evento de auditoria com `correlationId`.
- O acesso é condicionado a módulo contratado e permissões por prefixo (`comercial.*`, `os.*`, `industrial.*`, `estoque.*`, `compras.*`, `comercio.*`).

## APIs base

- `/api/comercial/pedidos`
- `/api/estoque/produtos`

## Telas base

- `/Comercio/Varejo`
- `/Comercio/Atacado`
- `/Comercio/Pedidos`
- `/Comercio/Produtos`

## Integrações

- Proposta aprovada pode gerar pedido.
- Pedido confirmado pode gerar OS quando aplicável.
- OS pode consumir peças do estoque.
- Plano preventivo pode gerar OS preventiva.
- Produto abaixo do mínimo gera alerta operacional.
- OS concluída registra evento para financeiro futuro.
