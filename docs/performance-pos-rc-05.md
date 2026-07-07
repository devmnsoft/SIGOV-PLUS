# Performance básica — Pós-RC 05

## Consultas críticas

| Área | Requisito mínimo | Verificação |
|---|---|---|
| Dashboard | Filtro por tenant, agregações limitadas, sem `SELECT *` crítico | Revisar SQL do serviço operacional |
| Busca | Termo sanitizado, tenant obrigatório, limite de resultados | Validar Busca Global |
| Relatórios CSV | Filtros por tenant/período e paginação/lotes quando aplicável | Validar exportação |
| Protocolo listagem | Ordenação e limite/paginação | Teste web/API |
| GED listagem | Não expor storage path; limitar resultados | Teste web/API |
| Tarefas | Filtrar por tenant/usuário/status | Teste Minha Central/Tarefas |
| Notificações | Limitar recentes e marcar lida sem varrer tabela | Teste Minha Central |
| Outbox | Índices por status/tentativas/data | Teste Worker/Operação |

## Critérios Pós-RC 05

- Não implementar otimização pesada nesta etapa.
- Bloquear lentidão óbvia: evitar `SELECT *` em consultas críticas, exigir `tenant_id`, usar `LIMIT/OFFSET` ou equivalente e registrar tempo no smoke.
- Qualquer rota acima de 2s no smoke em ambiente limpo deve virar pendência de homologação.
