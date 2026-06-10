# Módulo de Ordem de Serviço

## Objetivo

Atendimento técnico, agenda, checklist, apontamento de horas, consumo de peças e anexos.

## Segurança SaaS, auditoria e LGPD

- Todas as tabelas operacionais possuem `tenant_id` obrigatório.
- Listagens retornam documento, e-mail e telefone mascarados.
- Ações de criar, editar, aprovar, reprovar, cancelar, inativar, movimentar estoque e alterar status geram evento de auditoria com `correlationId`.
- O acesso é condicionado a módulo contratado e permissões por prefixo (`comercial.*`, `os.*`, `industrial.*`, `estoque.*`, `compras.*`, `comercio.*`).

## APIs base

- `/api/os/ordens`
- `/api/os/ordens/{id}/iniciar`
- `/api/os/ordens/{id}/consumir-peca`

## Telas base

- `/OrdemServico/Dashboard`
- `/OrdemServico/Ordens`
- `/OrdemServico/Agenda`
- `/OrdemServico/Checklist`
- `/OrdemServico/Apontamentos`

## Integrações

- Proposta aprovada pode gerar pedido.
- Pedido confirmado pode gerar OS quando aplicável.
- OS pode consumir peças do estoque.
- Plano preventivo pode gerar OS preventiva.
- Produto abaixo do mínimo gera alerta operacional.
- OS concluída registra evento para financeiro futuro.
