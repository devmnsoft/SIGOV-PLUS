# Workflow operacional SIGOV PLUS

A camada transversal de operação conecta Protocolo, GED/OCR, Tributário, Contratos, Jurídico e Financeiro por meio de workflow/tramitação, tarefas, notificações, agenda, eventos internos/outbox, integrações monitoradas, BI operacional e Mobile/Campo.

- **Workflow**: define etapas, transições, instâncias, responsáveis, prazos e histórico. Quando as tabelas `sigov.workflow*` existem, a tela consulta dados reais; sem schema, exibe fallback honesto sem simular salvamento.
- **Tarefas**: centraliza pendências por módulo, responsável, prioridade e vencimento.
- **Notificações**: lê tabelas de notificação/eventos quando disponíveis; caso contrário, apresenta recomendações claramente marcadas.
- **Prazos e agenda**: consolida contratos, jurídico, financeiro, protocolo e instâncias de workflow.
- **Eventos/outbox**: registra eventos operacionais de forma não bloqueante e prepara worker futuro.
- **Integrações**: monitora conectores, logs, falhas e reprocessamento.
- **BI operacional**: apresenta indicadores com dados reais schema-safe ou fallback explícito.
- **Mobile/Campo**: prepara roteiros, coletas, evidências, fila offline, conflitos e sincronização futura.
- **Auditoria e LGPD**: POSTs críticos chamam `IAuditTrailService`; as telas evitam dados pessoais e mantêm mensagens sem stacktrace.
