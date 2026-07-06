# Pós-RC — Homologação real

Esta evolução concentra a base homologável em segurança real da API v1 e schema persistente para Protocolo + GED + Workflow + Tarefas + Notificações + Outbox.

## Funcional real
- Migration idempotente cria/completa as tabelas transversais com `tenant_id`, auditoria, soft delete, correlação e índices.
- Middleware da API v1 exige API key, tenant e escopo por endpoint.

## Parcial
- Endpoints existentes continuam com fallback honesto quando o serviço de persistência de fluxo ainda não estiver conectado à action.

## Em implantação/fallback
- Assinatura oficial, OCR e integrações externas dependem de provedores reais configurados.
