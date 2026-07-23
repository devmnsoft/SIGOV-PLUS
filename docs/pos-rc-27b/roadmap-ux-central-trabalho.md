# Roadmap Pós-RC 27B — UX, Central de Trabalho e Tarefas

O Pós-RC 27A removeu os gates funcionais falsos `tarefas-postgres`, `tarefas-api-e2e` e `tarefas-ui-e2e` do go-live técnico porque os filtros `TarefasApi` e `TarefasUi` ainda não possuem implementação real confirmada.

Esses gates retornarão somente quando:

- Tarefas Web usar `ITarefaService` real;
- API de Tarefas existir;
- concorrência estiver implementada;
- transação estiver implementada;
- testes PostgreSQL e Playwright existirem;
- não houver filtros vazios nem projetos fictícios.

Também permanecem fora deste PR: redesign completo do template, nova topbar, nova sidebar, White Label no layout, login novo, Dashboard novo, Minha Central nova, Kanban de Tarefas e novo módulo setorial.
