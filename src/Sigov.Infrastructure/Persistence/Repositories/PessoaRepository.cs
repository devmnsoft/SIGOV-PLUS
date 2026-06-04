using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Common;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Repositories;

public sealed record PessoaFiltro(long? EntidadeId, string? Termo, int Page = 1, int PageSize = 20)
{
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > 100 ? 20 : PageSize;
    public int Offset => (SafePage - 1) * SafePageSize;
}
public sealed record PessoaResumoDto(long Id, string Nome, string? Documento, string TipoPessoa, bool Ativo);

public sealed class PessoaRepository : BaseRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<PessoaRepository> _logger;

    public PessoaRepository(DapperContext context, ILogger<PessoaRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<PessoaResumoDto>> ListarAsync(PessoaFiltro filtro, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                select
                    id,
                    nome,
                    documento,
                    tipo_pessoa as TipoPessoa,
                    ativo
                from sigov.pessoa
                where is_deleted = false
                  and (@EntidadeId is null or entidade_id = @EntidadeId)
                  and (@Termo is null or nome ilike '%' || @Termo || '%' or documento ilike '%' || @Termo || '%')
                order by nome
                limit @PageSize offset @Offset;

                select count(1)
                from sigov.pessoa
                where is_deleted = false
                  and (@EntidadeId is null or entidade_id = @EntidadeId)
                  and (@Termo is null or nome ilike '%' || @Termo || '%' or documento ilike '%' || @Termo || '%');
                """;

            using var connection = _context.CreateConnection();
            var args = new { filtro.EntidadeId, filtro.Termo, PageSize = filtro.SafePageSize, filtro.Offset };
            using var grid = await connection.QueryMultipleAsync(Command(sql, args, cancellationToken)).ConfigureAwait(false);
            var items = (await grid.ReadAsync<PessoaResumoDto>().ConfigureAwait(false)).AsList();
            var total = await grid.ReadSingleAsync<long>().ConfigureAwait(false);
            return new PagedResult<PessoaResumoDto>(items, filtro.SafePage, filtro.SafePageSize, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar pessoas no schema sigov. EntidadeId={EntidadeId}", filtro.EntidadeId);
            throw;
        }
    }
}
