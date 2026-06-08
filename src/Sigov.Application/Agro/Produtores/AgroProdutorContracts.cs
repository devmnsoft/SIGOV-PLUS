using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Produtores;

public sealed record AgroProdutorFiltro(int Page = 1, int PageSize = 20, string? Busca = null, string? Situacao = null);
public sealed record AgroProdutorCreateRequest(long PessoaId, string? CodigoProdutor, string TipoProdutor, string? InscricaoEstadual, string? InscricaoMunicipal, string? NumeroProdutorRural, string? AssociacaoCooperativa, string? PrincipalAtividade, string Situacao, string? Observacao);
public sealed record AgroProdutorUpdateRequest(string CodigoProdutor, string TipoProdutor, string? InscricaoEstadual, string? InscricaoMunicipal, string? NumeroProdutorRural, string? AssociacaoCooperativa, string? PrincipalAtividade, string Situacao, string? Observacao, bool Ativo = true);
public sealed record AgroProdutorResponse(long Id, long TenantId, long EntidadeId, long PessoaId, string CodigoProdutor, string TipoProdutor, string NomePessoa, string? DocumentoMascarado, string Situacao, DateOnly DataCadastro, bool Ativo);
public sealed record AgroProdutorDetalheResponse(long Id, long TenantId, long EntidadeId, long PessoaId, string CodigoProdutor, string TipoProdutor, string NomePessoa, string? DocumentoMascarado, string? DocumentoCompleto, string? InscricaoEstadual, string? InscricaoMunicipal, string? NumeroProdutorRural, string? AssociacaoCooperativa, string? PrincipalAtividade, string Situacao, DateOnly DataCadastro, string? Observacao, bool Ativo);

public interface IAgroProdutorRepository
{
    Task<PagedResult<AgroProdutorResponse>> ListarAsync(long tenantId, long entidadeId, AgroProdutorFiltro filtro, CancellationToken cancellationToken);
    Task<AgroProdutorDetalheResponse?> ObterAsync(long tenantId, long entidadeId, long id, bool dadosCompletos, CancellationToken cancellationToken);
    Task<long> CriarAsync(long tenantId, long entidadeId, long? usuarioId, AgroProdutorCreateRequest request, string codigo, CancellationToken cancellationToken);
    Task AtualizarAsync(long tenantId, long entidadeId, long id, long? usuarioId, AgroProdutorUpdateRequest request, CancellationToken cancellationToken);
    Task ExcluirAsync(long tenantId, long entidadeId, long id, long? usuarioId, CancellationToken cancellationToken);
    Task RegistrarAcessoDadoPessoalAsync(long tenantId, long entidadeId, long pessoaId, long? usuarioId, string finalidade, CancellationToken cancellationToken);
}

public interface IAgroProdutorService
{
    Task<Result<PagedResult<AgroProdutorResponse>>> ListarAsync(AgroProdutorFiltro filtro, CancellationToken cancellationToken);
    Task<Result<AgroProdutorDetalheResponse>> ObterAsync(long id, bool dadosCompletos, CancellationToken cancellationToken);
    Task<Result<long>> CriarAsync(AgroProdutorCreateRequest request, CancellationToken cancellationToken);
    Task<Result> AtualizarAsync(long id, AgroProdutorUpdateRequest request, CancellationToken cancellationToken);
    Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken);
}
