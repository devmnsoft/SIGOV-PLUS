using Dapper;
using Sigov.Application.Agro.Dashboard;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro;

public sealed class AgroDashboardRepository : IAgroDashboardRepository
{
    private readonly DapperContext _context;

    public AgroDashboardRepository(DapperContext context) => _context = context;

    public async Task<AgroDashboardResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken)
    {
        const string sql = """
            select tenant_id as TenantId,
                   entidade_id as EntidadeId,
                   total_camadas as TotalCamadas,
                   total_feicoes as TotalFeicoes,
                   total_eventos as TotalEventos,
                   total_produtores as TotalProdutores,
                   total_propriedades as TotalPropriedades,
                   total_visitas as TotalVisitas,
                   total_servicos_maquina as TotalServicosMaquina,
                   total_pontos_criticos as TotalPontosCriticos
              from sigov.vw_agro_dashboard
             where tenant_id = @TenantId
               and ((@EntidadeId is null and entidade_id is null) or entidade_id = @EntidadeId)
             limit 1;
            """;
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AgroDashboardResponse>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId }, cancellationToken: cancellationToken)).ConfigureAwait(false)
            ?? new AgroDashboardResponse(tenantId, entidadeId, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
