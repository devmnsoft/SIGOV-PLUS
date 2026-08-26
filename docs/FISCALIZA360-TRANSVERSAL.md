# Fiscaliza360 transversal

## Escopo entregue

O núcleo persistente usa Dapper/Npgsql e contexto obrigatório de tenant, entidade e exercício. Entrega dashboard, ordens com transições auditadas, modelos e itens de checklist, vistorias, equipes/membros, roteiros, autos/notificações, evidências transversais, sincronização idempotente, auditoria e CSV protegido contra injection.

## Banco e integrações

A migration `20260826150000_exp_fiscaliza360_transversal.sql` cria as onze tabelas `fiscalizacao_*`, constraints, FKs e índices. Ordens aceitam somente as origens `OBRAS`, `MEIO_AMBIENTE`, `TRANSITO` e `DEFESA_CIVIL`; o registro fiscalizado é resolvido nos cadastros FUNC13, FUNC14, FUNC18 e FUNC19 e revalidado no servidor. Evidências reutilizam `evidencia_transversal`. Pendências referenciam `sincronizacao_outbox` por chave idempotente.

## Telas e permissões

Rotas em `/Fiscalizacao`: painel, Ordens (criar/editar/detalhar), Vistorias (criar/checklist/detalhar), Checklists, Equipes, Roteiros, Autos, Evidências, Sincronização e Relatórios. As policies persistidas são `FISCALIZACAO_DASHBOARD_VIEW`, `FISCALIZACAO_ORDEM_VIEW`, `FISCALIZACAO_ORDEM_MANAGE`, `FISCALIZACAO_VISTORIA_VIEW`, `FISCALIZACAO_VISTORIA_MANAGE`, `FISCALIZACAO_CHECKLIST_MANAGE`, `FISCALIZACAO_AUTO_MANAGE`, `FISCALIZACAO_RELATORIO_EXPORT` e `FISCALIZACAO_SINCRONIZACAO_VIEW`.

## Limites reais

**BLOCKED técnico:** não existe adaptador/worker externo oficial para concluir a sincronização offline. A entrega persiste fila, idempotência, estado e erro sanitizado, mas não simula envio. Evidências registram metadados no contrato transversal; nenhum upload foi inventado. As superfícies de cadastro avançado de checklists, equipes, roteiros e vistorias ficam apoiadas no schema e nas rotas protegidas, sem catálogo fake; sua operação exige os cadastros oficiais do banco.

## Validação

Comandos executados e resultados estão registrados em `docs/entregas/EXP-FISCALIZA360-TRANSVERSAL.md`.
