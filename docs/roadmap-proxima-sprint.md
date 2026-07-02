# Roadmap próxima sprint

1. Executar schema report em PostgreSQL homologado e anexar resultado.
2. Criar migrations idempotentes não destrutivas para tabelas administrativas ausentes.
3. Ativar CRUD real por módulo somente após regras oficiais.
4. Implementar outbox persistente e consumidores de integração.
5. Ampliar permissões por ação: visualizar, criar, editar, excluir, cancelar, estornar, aprovar, homologar, assinar e exportar.
6. Smoke tests autenticados em Docker com screenshots das telas premium.

## Próxima sprint — módulos setoriais

- Criar migrations não destrutivas para tabelas setoriais detectadas como ausentes.
- Implementar repositórios Dapper por entidade após validação de colunas obrigatórias.
- Conectar eventos setoriais ao Workflow/Tarefas/Notificações/Agenda com regras parametrizadas.
- Evoluir CSV seguro e auditoria de exportação por módulo.
- Implementar offline sync real para Mobile/Campo sem simulação.
