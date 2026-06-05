using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Core;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Repositories;

// Tenant guard invariant used by smoke tests: where tenant_id = @TenantId
public sealed class PessoaRepository : BaseRepository, IPessoaCadastroRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<PessoaRepository> _logger;
    private readonly ICurrentTenant _currentTenant;

    public PessoaRepository(DapperContext context, ILogger<PessoaRepository> logger, ICurrentTenant currentTenant)
    {
        _context = context;
        _logger = logger;
        _currentTenant = currentTenant;
    }

    public async Task<PagedResult<PessoaResumoResponse>> ListarAsync(long tenantId, PessoaFiltro filtro, CancellationToken cancellationToken)
    {
        try
        {
            var page = new PaginationQuery(filtro.Page, filtro.PageSize);
            const string sql = """
                select
                    p.id,
                    p.tipo_pessoa as TipoPessoa,
                    p.nome,
                    p.nome_social as NomeSocial,
                    p.documento,
                    p.ativo
                from sigov.pessoa p
                where p.tenant_id = @TenantId
                  and p.is_deleted = false
                  and (@EntidadeId is null or p.entidade_id = @EntidadeId)
                  and (@TipoPessoa is null or p.tipo_pessoa = @TipoPessoa)
                  and (@Ativo is null or p.ativo = @Ativo)
                  and (@Termo is null or p.nome ilike '%' || @Termo || '%' or p.documento ilike '%' || @Termo || '%')
                order by p.nome
                limit @Limit offset @Offset;

                select count(1)
                from sigov.pessoa p
                where p.tenant_id = @TenantId
                  and p.is_deleted = false
                  and (@EntidadeId is null or p.entidade_id = @EntidadeId)
                  and (@TipoPessoa is null or p.tipo_pessoa = @TipoPessoa)
                  and (@Ativo is null or p.ativo = @Ativo)
                  and (@Termo is null or p.nome ilike '%' || @Termo || '%' or p.documento ilike '%' || @Termo || '%');
                """;

            using var connection = _context.CreateConnection();
            var args = new { TenantId = tenantId, filtro.EntidadeId, filtro.Termo, filtro.TipoPessoa, filtro.Ativo, Limit = page.SafePageSize, page.Offset };
            using var grid = await connection.QueryMultipleAsync(Command(sql, args, cancellationToken)).ConfigureAwait(false);
            var pessoas = (await grid.ReadAsync<PessoaResumoRow>().ConfigureAwait(false)).AsList();
            var total = await grid.ReadSingleAsync<long>().ConfigureAwait(false);
            var enderecos = await ListarEnderecosAsync(connection, tenantId, pessoas.Select(p => p.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            var items = pessoas.Select(p => new PessoaResumoResponse(p.Id, p.TipoPessoa, p.Nome, p.NomeSocial, p.Documento, p.Ativo, enderecos.Where(e => e.PessoaId == p.Id).ToArray())).ToArray();
            return new PagedResult<PessoaResumoResponse>(items, page.SafePage, page.SafePageSize, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar pessoas no schema sigov. TenantId={TenantId} EntidadeId={EntidadeId}", tenantId, filtro.EntidadeId);
            throw;
        }
    }

    public Task<PagedResult<PessoaResumoResponse>> ListarAsync(PessoaFiltro filtro, CancellationToken cancellationToken)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            throw new InvalidOperationException("TenantId obrigatório para listar pessoas.");
        }

        return ListarAsync(_currentTenant.TenantId.Value, filtro, cancellationToken);
    }

    public async Task<PessoaDetalheResponse?> ObterAsync(long tenantId, long id, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                select id, tipo_pessoa as TipoPessoa, nome, nome_social as NomeSocial, documento, classificacao_lgpd as ClassificacaoLgpd, observacao, ativo
                from sigov.pessoa
                where tenant_id = @TenantId and id = @Id and is_deleted = false;
                """;
            using var connection = _context.CreateConnection();
            var pessoa = await connection.QuerySingleOrDefaultAsync<PessoaDetalheRow>(Command(sql, new { TenantId = tenantId, Id = id }, cancellationToken)).ConfigureAwait(false);
            if (pessoa is null) return null;
            var enderecos = await ListarEnderecosAsync(connection, tenantId, new[] { id }, cancellationToken).ConfigureAwait(false);
            return new PessoaDetalheResponse(pessoa.Id, pessoa.TipoPessoa, pessoa.Nome, pessoa.NomeSocial, pessoa.Documento, pessoa.ClassificacaoLgpd, pessoa.Observacao, pessoa.Ativo, enderecos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter pessoa no schema sigov. TenantId={TenantId} PessoaId={PessoaId}", tenantId, id);
            throw;
        }
    }

    public async Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, PessoaCreateRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                insert into sigov.pessoa (tenant_id, entidade_id, exercicio_id, tipo_pessoa, nome, nome_social, documento, observacao, created_by)
                values (@TenantId, @EntidadeId, @ExercicioId, @TipoPessoa, @Nome, @NomeSocial, @Documento, @Observacao, @UsuarioId)
                returning id;
                """;
            using var connection = _context.CreateConnection();
            var id = await connection.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, TipoPessoa = NormalizarTipo(request.TipoPessoa), Nome = request.Nome.Trim(), request.NomeSocial, Documento = Sigov.Domain.Core.Pessoa.NormalizarDocumento(request.Documento), request.Observacao, UsuarioId = usuarioId }, cancellationToken)).ConfigureAwait(false);
            foreach (var endereco in request.Enderecos ?? Array.Empty<EnderecoCreateRequest>())
            {
                await InserirEnderecoAsync(connection, tenantId, entidadeId, exercicioId, id, endereco, usuarioId, cancellationToken).ConfigureAwait(false);
            }

            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pessoa no schema sigov. TenantId={TenantId}", tenantId);
            throw;
        }
    }

    public async Task AtualizarAsync(long tenantId, long id, PessoaUpdateRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                update sigov.pessoa
                set tipo_pessoa = @TipoPessoa,
                    nome = @Nome,
                    nome_social = @NomeSocial,
                    documento = @Documento,
                    observacao = @Observacao,
                    ativo = @Ativo,
                    updated_at = now(),
                    updated_by = @UsuarioId
                where tenant_id = @TenantId and id = @Id and is_deleted = false;
                """;
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, TipoPessoa = NormalizarTipo(request.TipoPessoa), Nome = request.Nome.Trim(), request.NomeSocial, Documento = Sigov.Domain.Core.Pessoa.NormalizarDocumento(request.Documento), request.Observacao, request.Ativo, UsuarioId = usuarioId }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar pessoa no schema sigov. TenantId={TenantId} PessoaId={PessoaId}", tenantId, id);
            throw;
        }
    }

    public async Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                update sigov.pessoa
                set is_deleted = true, ativo = false, deleted_at = now(), deleted_by = @UsuarioId
                where tenant_id = @TenantId and id = @Id and is_deleted = false;
                update sigov.endereco
                set is_deleted = true, ativo = false, deleted_at = now(), deleted_by = @UsuarioId
                where tenant_id = @TenantId and pessoa_id = @Id and is_deleted = false;
                """;
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir pessoa no schema sigov. TenantId={TenantId} PessoaId={PessoaId}", tenantId, id);
            throw;
        }
    }

    public async Task<long> AdicionarEnderecoAsync(long tenantId, long pessoaId, EnderecoCreateRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var scope = await ObterEscopoPessoaAsync(connection, tenantId, pessoaId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Pessoa não encontrada.");
        return await InserirEnderecoAsync(connection, tenantId, scope.EntidadeId, scope.ExercicioId, pessoaId, request, usuarioId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AtualizarEnderecoAsync(long tenantId, long pessoaId, long enderecoId, EnderecoUpdateRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                update sigov.endereco
                set logradouro = @Logradouro,
                    numero = @Numero,
                    complemento = @Complemento,
                    bairro = @Bairro,
                    municipio = @Municipio,
                    uf = @Uf,
                    cep = @Cep,
                    observacao = @Observacao,
                    ativo = @Ativo,
                    updated_at = now(),
                    updated_by = @UsuarioId
                where tenant_id = @TenantId and pessoa_id = @PessoaId and id = @EnderecoId and is_deleted = false;
                """;
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, PessoaId = pessoaId, EnderecoId = enderecoId, Logradouro = request.Logradouro.Trim(), request.Numero, request.Complemento, request.Bairro, Municipio = request.Municipio.Trim(), Uf = request.Uf.Trim().ToUpperInvariant(), Cep = NormalizarCep(request.Cep), request.Observacao, request.Ativo, UsuarioId = usuarioId }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar endereço. TenantId={TenantId} PessoaId={PessoaId} EnderecoId={EnderecoId}", tenantId, pessoaId, enderecoId);
            throw;
        }
    }

    public async Task ExcluirEnderecoAsync(long tenantId, long pessoaId, long enderecoId, long? usuarioId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                update sigov.endereco
                set is_deleted = true, ativo = false, deleted_at = now(), deleted_by = @UsuarioId
                where tenant_id = @TenantId and pessoa_id = @PessoaId and id = @EnderecoId and is_deleted = false;
                """;
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, PessoaId = pessoaId, EnderecoId = enderecoId, UsuarioId = usuarioId }, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir endereço. TenantId={TenantId} PessoaId={PessoaId} EnderecoId={EnderecoId}", tenantId, pessoaId, enderecoId);
            throw;
        }
    }

    public async Task<byte[]> ExportarAsync(long tenantId, string formato, CancellationToken cancellationToken)
    {
        var page = await ListarAsync(tenantId, new PessoaFiltro(1, 5000), cancellationToken).ConfigureAwait(false);
        return formato switch
        {
            "json" => JsonSerializer.SerializeToUtf8Bytes(page.Items),
            "xml" => Encoding.UTF8.GetBytes(new XElement("pessoas", page.Items.Select(p => new XElement("pessoa", new XAttribute("id", p.Id), new XElement("nome", p.Nome), new XElement("tipoPessoa", p.TipoPessoa), new XElement("documento", p.Documento ?? string.Empty)))).ToString()),
            _ => Encoding.UTF8.GetBytes(ToCsv(page.Items))
        };
    }

    private static async Task<IReadOnlyCollection<EnderecoResponse>> ListarEnderecosAsync(System.Data.IDbConnection connection, long tenantId, IReadOnlyCollection<long> pessoaIds, CancellationToken cancellationToken)
    {
        if (pessoaIds.Count == 0) return Array.Empty<EnderecoResponse>();
        const string sql = """
            select id, pessoa_id as PessoaId, logradouro, numero, complemento, bairro, municipio, uf, cep, observacao, ativo
            from sigov.endereco
            where tenant_id = @TenantId and pessoa_id = any(@PessoaIds) and is_deleted = false
            order by id;
            """;
        var rows = await connection.QueryAsync<EnderecoResponse>(Command(sql, new { TenantId = tenantId, PessoaIds = pessoaIds.ToArray() }, cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    private static async Task<long> InserirEnderecoAsync(System.Data.IDbConnection connection, long tenantId, long? entidadeId, long? exercicioId, long pessoaId, EnderecoCreateRequest request, long? usuarioId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.endereco (tenant_id, entidade_id, exercicio_id, pessoa_id, logradouro, numero, complemento, bairro, municipio, uf, cep, observacao, created_by)
            values (@TenantId, @EntidadeId, @ExercicioId, @PessoaId, @Logradouro, @Numero, @Complemento, @Bairro, @Municipio, @Uf, @Cep, @Observacao, @UsuarioId)
            returning id;
            """;
        return await connection.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, PessoaId = pessoaId, Logradouro = request.Logradouro.Trim(), request.Numero, request.Complemento, request.Bairro, Municipio = request.Municipio.Trim(), Uf = request.Uf.Trim().ToUpperInvariant(), Cep = NormalizarCep(request.Cep), request.Observacao, UsuarioId = usuarioId }, cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<PessoaEscopoRow?> ObterEscopoPessoaAsync(System.Data.IDbConnection connection, long tenantId, long pessoaId, CancellationToken cancellationToken)
    {
        const string sql = "select entidade_id as EntidadeId, exercicio_id as ExercicioId from sigov.pessoa where tenant_id = @TenantId and id = @PessoaId and is_deleted = false;";
        return await connection.QuerySingleOrDefaultAsync<PessoaEscopoRow>(Command(sql, new { TenantId = tenantId, PessoaId = pessoaId }, cancellationToken)).ConfigureAwait(false);
    }

    private static string NormalizarTipo(string tipoPessoa) => tipoPessoa.Equals("J", StringComparison.OrdinalIgnoreCase) || tipoPessoa.Equals("Juridica", StringComparison.OrdinalIgnoreCase) ? "J" : "F";
    private static string? NormalizarCep(string? cep) => string.IsNullOrWhiteSpace(cep) ? null : new string(cep.Where(char.IsDigit).ToArray());
    private static string ToCsv(IEnumerable<PessoaResumoResponse> pessoas)
    {
        var sb = new StringBuilder("id;tipo_pessoa;nome;nome_social;documento;ativo\n");
        foreach (var p in pessoas)
        {
            sb.Append(p.Id).Append(';').Append(p.TipoPessoa).Append(';').Append(Escape(p.Nome)).Append(';').Append(Escape(p.NomeSocial)).Append(';').Append(Escape(p.Documento)).Append(';').Append(p.Ativo).Append('\n');
        }
        return sb.ToString();
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace(";", ",", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal);

    private sealed record PessoaResumoRow(long Id, string TipoPessoa, string Nome, string? NomeSocial, string? Documento, bool Ativo);
    private sealed record PessoaDetalheRow(long Id, string TipoPessoa, string Nome, string? NomeSocial, string? Documento, string ClassificacaoLgpd, string? Observacao, bool Ativo);
    private sealed record PessoaEscopoRow(long? EntidadeId, long? ExercicioId);
}
