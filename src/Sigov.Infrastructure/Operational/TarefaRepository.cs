using System.Text.Json;
using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class TarefaRepository : ITarefaRepository
{
    private readonly DapperContext _context;
    public TarefaRepository(DapperContext context) => _context = context;
    public async Task<TarefaDto> CriarAsync(CriarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    { const string sql = @"insert into sigov.tarefa (tenant_id, titulo, descricao, prioridade, responsavel_id, prazo_em, status, created_by, updated_by, correlation_id) values (@TenantId, @Titulo, @Descricao, @Prioridade, @ResponsavelId, @PrazoEm, 'ABERTA', @UserId, @UserId, @CorrelationId) returning id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt;"; using var connection = _context.CreateConnection(); return await connection.QuerySingleAsync<TarefaDto>(new CommandDefinition(sql, new { context.TenantId, request.Titulo, request.Descricao, request.Prioridade, request.ResponsavelId, request.PrazoEm, context.UserId, context.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false); }
    public async Task<TarefaDto?> ObterAsync(long tenantId, long tarefaId, CancellationToken cancellationToken) { using var connection = _context.CreateConnection(); return await connection.QuerySingleOrDefaultAsync<TarefaDto>(new CommandDefinition(@"select id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt from sigov.tarefa where tenant_id = @TenantId and id = @TarefaId and is_deleted = false;", new { TenantId = tenantId, TarefaId = tarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false); }
    public async Task<IReadOnlyList<TarefaDto>> ListarAsync(long tenantId, long? responsavelId, string? status, int page, int pageSize, CancellationToken cancellationToken) { using var connection = _context.CreateConnection(); var rows = await connection.QueryAsync<TarefaDto>(new CommandDefinition(@"select id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt from sigov.tarefa where tenant_id = @TenantId and is_deleted = false and (@ResponsavelId is null or responsavel_id = @ResponsavelId) and (@Status is null or status = @Status) order by prazo_em nulls last, id limit @Limit offset @Offset;", new { TenantId = tenantId, ResponsavelId = responsavelId, Status = status, Limit = Math.Clamp(pageSize, 1, 200), Offset = Math.Max(page - 1, 0) * Math.Clamp(pageSize, 1, 200) }, cancellationToken: cancellationToken)).ConfigureAwait(false); return rows.AsList(); }
    public async Task<TarefaDto> AlterarStatusAsync(AlterarStatusTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken) { using var connection = _context.CreateConnection(); return await connection.QuerySingleAsync<TarefaDto>(new CommandDefinition(@"update sigov.tarefa set status = @Status, updated_at = now(), updated_by = @UserId, correlation_id = @CorrelationId where tenant_id = @TenantId and id = @TarefaId and is_deleted = false returning id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt;", new { Status = request.NovoStatus, context.UserId, context.CorrelationId, context.TenantId, request.TarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false); }

    public async Task<TarefaDto> AtualizarAsync(AtualizarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<TarefaDto>(new CommandDefinition(@"update sigov.tarefa set titulo = @Titulo, descricao = @Descricao, prioridade = @Prioridade, responsavel_id = @ResponsavelId, prazo_em = @PrazoEm, updated_at = now(), updated_by = @UserId, correlation_id = @CorrelationId where tenant_id = @TenantId and id = @TarefaId and is_deleted = false returning id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt;", new { request.Titulo, request.Descricao, request.Prioridade, request.ResponsavelId, request.PrazoEm, context.UserId, context.CorrelationId, context.TenantId, request.TarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TarefaDto> AtribuirAsync(AtribuirTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<TarefaDto>(new CommandDefinition(@"update sigov.tarefa set status = 'ATRIBUIDA', responsavel_id = @ResponsavelId, updated_at = now(), updated_by = @UserId, correlation_id = @CorrelationId where tenant_id = @TenantId and id = @TarefaId and is_deleted = false returning id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt;", new { request.ResponsavelId, context.UserId, context.CorrelationId, context.TenantId, request.TarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TarefaDto> DelegarAsync(DelegarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<TarefaDto>(new CommandDefinition(@"update sigov.tarefa set status = 'ATRIBUIDA', responsavel_id = @NovoResponsavelId, updated_at = now(), updated_by = @UserId, correlation_id = @CorrelationId where tenant_id = @TenantId and id = @TarefaId and is_deleted = false returning id, tenant_id as TenantId, titulo, status, prioridade, responsavel_id as ResponsavelId, prazo_em as PrazoEm, created_at as CreatedAt;", new { request.NovoResponsavelId, context.UserId, context.CorrelationId, context.TenantId, request.TarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public Task<TarefaDto> IniciarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, "EM_ANDAMENTO", null, version), context, cancellationToken);
    public Task<TarefaDto> PausarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, "PAUSADA", null, version), context, cancellationToken);
    public Task<TarefaDto> ConcluirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, "CONCLUIDA", null, version), context, cancellationToken);
    public Task<TarefaDto> ReabrirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, "REABERTA", null, version), context, cancellationToken);
    public Task<TarefaDto> CancelarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, "CANCELADA", null, version), context, cancellationToken);

    public async Task AdicionarComentarioAsync(ComentarioTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tarefa_comentario (tenant_id, tarefa_id, comentario, created_by, correlation_id) values (@TenantId, @TarefaId, @Comentario, @UserId, @CorrelationId);", new { context.TenantId, request.TarefaId, request.Comentario, context.UserId, context.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AdicionarVinculoAsync(VinculoTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tarefa_vinculo (tenant_id, tarefa_id, tipo, entidade_id, created_by, correlation_id) values (@TenantId, @TarefaId, @Tipo, @EntidadeId, @UserId, @CorrelationId);", new { context.TenantId, request.TarefaId, request.Tipo, request.EntidadeId, context.UserId, context.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TarefaHistoricoDto>> ListarHistoricoAsync(long tenantId, long tarefaId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TarefaHistoricoDto>(new CommandDefinition(@"select id, tarefa_id as TarefaId, acao, created_at as CreatedAt from sigov.tarefa_historico where tenant_id = @TenantId and tarefa_id = @TarefaId order by created_at, id;", new { TenantId = tenantId, TarefaId = tarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }
}
