using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class TarefaRepository : ITarefaRepository
{
    private const string Projection = @"id, tenant_id as TenantId, titulo, descricao, status, prioridade,
responsavel_id as ResponsavelId, prazo_em as PrazoEm,
case when prazo_em is null then 'SEM_PRAZO'
     when prazo_em < now() then 'VENCIDA'
     when prazo_em <= now() + interval '3 days' then 'PROXIMA_DO_VENCIMENTO'
     else 'NO_PRAZO' end as SituacaoPrazo,
entidade, entidade_id as EntidadeId, created_at as CreatedAt, updated_at as UpdatedAt, version";

    private readonly DapperContext _context;

    public TarefaRepository(DapperContext context) => _context = context;

    public async Task<TarefaDto> CriarAsync(CriarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.tarefa
(tenant_id, titulo, descricao, prioridade, responsavel_id, prazo_em, status, created_by, updated_by, correlation_id)
values (@TenantId, @Titulo, @Descricao, @Prioridade, @ResponsavelId, @PrazoEm,
        case when @ResponsavelId is null then 'ABERTA' else 'ATRIBUIDA' end, @UserId, @UserId, @CorrelationId)
returning " + Projection + ";";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<TarefaDto>(new CommandDefinition(sql,
            new { context.TenantId, request.Titulo, request.Descricao, request.Prioridade, request.ResponsavelId, request.PrazoEm, context.UserId, context.CorrelationId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TarefaDto?> ObterAsync(long tenantId, long tarefaId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TarefaDto>(new CommandDefinition(
            "select " + Projection + " from sigov.tarefa where tenant_id = @TenantId and id = @TarefaId and is_deleted = false;",
            new { TenantId = tenantId, TarefaId = tarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TarefaDto>> ListarAsync(long tenantId, long? responsavelId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var limit = Math.Clamp(pageSize, 1, 200);
        var rows = await connection.QueryAsync<TarefaDto>(new CommandDefinition(
            "select " + Projection + @" from sigov.tarefa
where tenant_id = @TenantId and is_deleted = false
  and (@ResponsavelId is null or responsavel_id = @ResponsavelId)
  and (@Status is null or status = @Status)
order by prazo_em nulls last, id limit @Limit offset @Offset;",
            new { TenantId = tenantId, ResponsavelId = responsavelId, Status = status, Limit = limit, Offset = Math.Max(page - 1, 0) * limit },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public Task<TarefaDto> AlterarStatusAsync(AlterarStatusTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
        => UpdateAsync(@"status = @Status,
concluida_em = case when @Status = 'CONCLUIDA' then now() when @Status = 'REABERTA' then null else concluida_em end,
cancelada_em = case when @Status = 'CANCELADA' then now() when @Status = 'REABERTA' then null else cancelada_em end",
            new { Status = request.NovoStatus }, request.TarefaId, request.Version, context, cancellationToken);

    public Task<TarefaDto> AtualizarAsync(AtualizarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
        => UpdateAsync("titulo = @Titulo, descricao = @Descricao, prioridade = @Prioridade, responsavel_id = @ResponsavelId, prazo_em = @PrazoEm",
            new { request.Titulo, request.Descricao, request.Prioridade, request.ResponsavelId, request.PrazoEm }, request.TarefaId, request.Version, context, cancellationToken);

    public Task<TarefaDto> AtribuirAsync(AtribuirTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
        => UpdateAsync("responsavel_id = @ResponsavelId, status = case when status = 'ABERTA' then 'ATRIBUIDA' else status end",
            new { request.ResponsavelId }, request.TarefaId, request.Version, context, cancellationToken);

    public Task<TarefaDto> DelegarAsync(DelegarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
        => UpdateAsync("responsavel_id = @NovoResponsavelId", new { request.NovoResponsavelId }, request.TarefaId, request.Version, context, cancellationToken);

    public Task<TarefaDto> IniciarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new(tarefaId, "EM_ANDAMENTO", null, version), context, cancellationToken);
    public Task<TarefaDto> PausarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new(tarefaId, "PAUSADA", null, version), context, cancellationToken);
    public Task<TarefaDto> ConcluirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new(tarefaId, "CONCLUIDA", null, version), context, cancellationToken);
    public Task<TarefaDto> ReabrirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new(tarefaId, "REABERTA", null, version), context, cancellationToken);
    public Task<TarefaDto> CancelarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new(tarefaId, "CANCELADA", null, version), context, cancellationToken);

    public async Task AdicionarComentarioAsync(ComentarioTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tarefa_comentario
(tenant_id, tarefa_id, comentario, created_by, correlation_id)
values (@TenantId, @TarefaId, @Comentario, @UserId, @CorrelationId);",
            new { context.TenantId, request.TarefaId, request.Comentario, context.UserId, context.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AdicionarVinculoAsync(VinculoTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tarefa_vinculo
(tenant_id, tarefa_id, entidade, entidade_id, created_by, correlation_id)
values (@TenantId, @TarefaId, @Entidade, @EntidadeId, @UserId, @CorrelationId);",
            new { context.TenantId, request.TarefaId, request.Entidade, request.EntidadeId, context.UserId, context.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TarefaHistoricoDto>> ListarHistoricoAsync(long tenantId, long tarefaId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TarefaHistoricoDto>(new CommandDefinition(@"select id, tarefa_id as TarefaId, acao, created_at as CreatedAt
from sigov.tarefa_historico where tenant_id = @TenantId and tarefa_id = @TarefaId order by created_at, id;",
            new { TenantId = tenantId, TarefaId = tarefaId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    private async Task<TarefaDto> UpdateAsync(string assignments, object values, long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var parameters = new DynamicParameters(values);
        parameters.Add("TenantId", context.TenantId);
        parameters.Add("TarefaId", tarefaId);
        parameters.Add("Version", version);
        parameters.Add("UserId", context.UserId);
        parameters.Add("CorrelationId", context.CorrelationId);
        var sql = "update sigov.tarefa set " + assignments + @", updated_at = now(), updated_by = @UserId,
correlation_id = @CorrelationId, version = version + 1
where tenant_id = @TenantId and id = @TarefaId and version = @Version and is_deleted = false
returning " + Projection + ";";
        var result = await connection.QuerySingleOrDefaultAsync<TarefaDto>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result ?? throw new OperationalConcurrencyException(tarefaId, version);
    }
}
