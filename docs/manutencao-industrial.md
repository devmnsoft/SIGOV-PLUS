# Módulo de Manutenção Industrial

## Objetivo

Ativos, localizações, planos preventivos/corretivos/preditivos, medidores, leituras, paradas e falhas.

## Segurança SaaS, auditoria e LGPD

- Todas as tabelas operacionais possuem `tenant_id` obrigatório.
- Listagens retornam documento, e-mail e telefone mascarados.
- Ações de criar, editar, aprovar, reprovar, cancelar, inativar, movimentar estoque e alterar status geram evento de auditoria com `correlationId`.
- O acesso é condicionado a módulo contratado e permissões por prefixo (`comercial.*`, `os.*`, `industrial.*`, `estoque.*`, `compras.*`, `comercio.*`).

## APIs base

- `/api/industrial/ativos`
- `/api/industrial/planos-manutencao/{id}/gerar-os`
- `/api/industrial/medidores/{id}/leituras`

## Telas base

- `/Industrial/Dashboard`
- `/Industrial/Ativos`
- `/Industrial/PlanosManutencao`
- `/Industrial/Medidores`
- `/Industrial/Paradas`

## Integrações

- Proposta aprovada pode gerar pedido.
- Pedido confirmado pode gerar OS quando aplicável.
- OS pode consumir peças do estoque.
- Plano preventivo pode gerar OS preventiva.
- Produto abaixo do mínimo gera alerta operacional.
- OS concluída registra evento para financeiro futuro.
