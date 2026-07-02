# Performance SIGOV PLUS

Diretrizes: paginação obrigatória em telas pesadas, `pageSize` padrão 20/50, limite máximo 100, filtros por período em auditoria/logs/BI, evitar N+1, não carregar anexos em memória, `CancellationToken` em consultas, timeout por comando e cache curto para catálogos.

Índices recomendados: `tenant_id`, `created_at`, `status`, `modulo`, `correlation_id`, chaves externas de workflow/tarefas/documentos/protocolos e índices compostos por `(tenant_id, created_at desc)` em logs.

Endpoints críticos: Dashboard, Minha Central, Busca, Relatórios, Protocolo, GED, Auditoria, Notificações e BI.
