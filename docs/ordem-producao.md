# Ordem de Produção

A OP suporta status PLANEJADA, LIBERADA, EM_PRODUCAO, PAUSADA, CONCLUIDA e CANCELADA. Toda mudança registra `industria_ordem_historico` e auditoria.

Regras mínimas implementadas: não criar OP para produto inativo; não liberar sem ficha quando exigida; não iniciar OP não liberada; não concluir sem apontamento; não concluir com inspeção obrigatória pendente.
