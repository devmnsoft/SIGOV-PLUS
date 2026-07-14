# matriz-crud-enterprise Pós-RC 15

Documento de consolidação Pós-RC 15.

- Escopo: Minha Central, Dashboard, Enterprise, Protocolo, GED, Busca, Relatórios, Notificações, Tarefas, Outbox, Auditoria, Agenda, SLA, Kanban, CI/CD, Docker, seeds e release.
- Regra: tenant obrigatório, permissão aplicada no front e API, LGPD com mascaramento, CSV seguro, auditoria de ações críticas e fallback honesto sem falso sucesso.
- Evidência: consultar `docs/evidencias-consolidacao-pos-rc-15.md`, smoke MD/JSON e go-live check.
- Pendência honesta: recursos dependentes de storage/GED/provedores externos só são homologáveis após configuração runtime.
