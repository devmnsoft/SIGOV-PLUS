# Regras canônicas de Tarefas

Status persistidos: `ABERTA`, `ATRIBUIDA`, `EM_ANDAMENTO`, `AGUARDANDO`, `PAUSADA`, `CONCLUIDA`, `REABERTA` e `CANCELADA`. `VENCIDA` é situação calculada. Atualizações exigem tenant, id, version e registro não excluído, incrementando `version`. Delegação exige motivo, preserva status e é bloqueada para concluídas/canceladas. Vínculos usam `entidade` e `entidade_id` textuais.
