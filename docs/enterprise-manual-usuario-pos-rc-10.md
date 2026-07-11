# Manual do Usuário Enterprise Pós-RC 10

## Operação segura

1. Faça login antes de acessar qualquer tela Enterprise.
2. Selecione ou informe um tenant válido; produção não usa tenant demo silencioso.
3. Abra a tela Enterprise desejada pelo menu existente.
4. Use filtros de busca/status e paginação.
5. Clique em **Novo** para abrir o formulário específico da entidade.
6. Use **Editar**, **Inativar**, **Restaurar** e os botões operacionais somente quando exibidos na tela.
7. Exporte CSV apenas com permissão `enterprise.relatorios.exportar`.

## Formulários por entidade

- Clientes: nome, documento, e-mail, telefone e status.
- Propostas: cliente, valor, status e observação.
- Pedidos: cliente, valor, status e observação.
- Produtos: nome, código interno, quantidade inicial, valor e status.
- OS: descrição, status e horas previstas.
- Ativos/planos/medidores/paradas: campos específicos de manutenção industrial.

## LGPD

Listagens, detalhes e CSV apresentam documento, e-mail e telefone mascarados. Dados completos, tokens, secrets, API keys e paths completos não são renderizados nas telas Enterprise operacionais.
