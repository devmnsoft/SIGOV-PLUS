# Auditoria método × schema

- `OperationalEventService`: alinhado a `tenant_id`, `tipo_evento`, `modulo`, `entidade_tipo`, `entidade_id`, `payload`, `status`, `correlation_id`, `created_at` e `processed_at`.
- `OutboxSigovService`: usa `IOperationalEventPublisher`; não encaminha mais à tabela `evento_operacional`.
- Métodos `Try*`: retornam `bool` e registram falha crítica como erro.
