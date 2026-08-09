# Checklist de homologação RC49

- [ ] Admin visualiza `/Workflows` e usuário sem permissão recebe acesso negado.
- [ ] Workflow é criado no tenant atual em rascunho.
- [ ] Designer rejeita menos de duas etapas, ausência/duplicidade da inicial e ausência de final.
- [ ] Rascunho salva com antiforgery e grava auditoria.
- [ ] Publicação cria versão imutável e bloqueia o designer.
- [ ] Outro tenant não consulta nem altera a definição.
- [ ] Layout opera em desktop e muda para lista/drawer empilhado em viewport móvel.
- [ ] Migration executa duas vezes sem apagar dados.
- [ ] `script_completop.sql` e manifest permanecem sincronizados.
- [ ] `scripts/check-rc49-platform-flows.ps1` gera JSON, Markdown e log em `artifacts/rc49`.

## Limite desta entrega

A vertical concluída é o núcleo configurável de Workflow. Formulários, portal externo, SLA, aprovação, templates, complementação e relatórios executivos são próximos incrementos e não aparecem como telas ou dados fictícios nesta versão.
