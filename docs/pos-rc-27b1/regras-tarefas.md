# Regras de Tarefas

Status persistidos planejados: `ABERTA`, `ATRIBUIDA`, `EM_ANDAMENTO`, `AGUARDANDO`, `PAUSADA`, `CONCLUIDA`, `REABERTA` e `CANCELADA`. `VENCIDA` é situação calculada, nunca transição. Alterações exigem tenant, id, versão e registro não excluído; zero linhas atualizadas representa conflito otimista. Jornadas compostas devem compartilhar conexão e transação. Esta etapa não declara tais contratos implementados antes dos Portões A e B.
