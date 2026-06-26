using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Web.Services;

public interface IAuditTrailService
{
    Task RegistrarAsync(
        long? tenantId,
        long? usuarioId,
        string acao,
        string entidade,
        string? entidadeId,
        object? antes,
        object? depois,
        string? ip,
        string? userAgent,
        string correlationId,
        CancellationToken cancellationToken);
}

public sealed class AuditTrailService : IAuditTrailService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<AuditTrailService> _logger;
    private readonly IDatabaseSchemaInspector _schemaInspector;

    public AuditTrailService(NpgsqlConnectionFactory connectionFactory, ILogger<AuditTrailService> logger, IDatabaseSchemaInspector schemaInspector)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _schemaInspector = schemaInspector;
    }

    public async Task RegistrarAsync(long? tenantId, long? usuarioId, string acao, string entidade, string? entidadeId, object? antes, object? depois, string? ip, string? userAgent, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "auditoria_evento", cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("Auditoria em fallback: tabela sigov.auditoria_evento não existe. Acao={Acao} Entidade={Entidade} EntidadeId={EntidadeId} CorrelationId={CorrelationId}", acao, entidade, entidadeId, correlationId);
                return;
            }
            using var connection = _connectionFactory.CreateConnection();
            var antesJson = antes is null ? null : System.Text.Json.JsonSerializer.Serialize(antes);
            var depoisJson = depois is null ? null : System.Text.Json.JsonSerializer.Serialize(depois);
            var correlation = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid();
            const string sql = @"insert into sigov.auditoria_evento
(tenant_id, usuario_id, acao, entidade, entidade_id, antes, depois, ip, user_agent, correlation_id, created_at)
values (@TenantId, @UsuarioId, @Acao, @Entidade, @EntidadeId, cast(@Antes as jsonb), cast(@Depois as jsonb), @Ip, @UserAgent, @CorrelationId, now());";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UsuarioId = usuarioId, Acao = acao, Entidade = entidade, EntidadeId = entidadeId, Antes = antesJson, Depois = depoisJson, Ip = ip, UserAgent = userAgent, CorrelationId = correlation }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auditoria não persistida; evento mantido em log. Acao={Acao} Entidade={Entidade} EntidadeId={EntidadeId} CorrelationId={CorrelationId}", acao, entidade, entidadeId, correlationId);
        }
    }
}
