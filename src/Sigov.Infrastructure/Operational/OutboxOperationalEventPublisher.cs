using System.Text.Json;
using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class OutboxOperationalEventPublisher : IOperationalEventPublisher { private readonly DapperContext _context; public OutboxOperationalEventPublisher(DapperContext context)=>_context=context; public async Task PublishAsync(OperationalEvent operationalEvent,CancellationToken cancellationToken){using var connection=_context.CreateConnection(); await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.outbox_evento (tenant_id, event_id, event_type, event_version, aggregate_type, aggregate_id, user_id, correlation_id, occurred_at, payload, status, attempts, next_attempt_at, idempotency_key) values (@TenantId, gen_random_uuid(), @EventType, 1, @AggregateType, @AggregateId, @UserId, @CorrelationId, now(), @Payload::jsonb, 'PENDING', 0, now(), @IdempotencyKey) on conflict (idempotency_key) do nothing;", new {operationalEvent.TenantId, operationalEvent.EventType, operationalEvent.AggregateType, operationalEvent.AggregateId, operationalEvent.UserId, operationalEvent.CorrelationId, Payload=JsonSerializer.Serialize(operationalEvent.Payload), operationalEvent.IdempotencyKey}, cancellationToken:cancellationToken)).ConfigureAwait(false);} }
