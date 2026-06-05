using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Core;

public static class CorePermissoes
{
    public const string Modulo = "core";
    public const string PessoasVisualizar = "core.pessoas.visualizar";
    public const string PessoasCriar = "core.pessoas.criar";
    public const string PessoasEditar = "core.pessoas.editar";
    public const string PessoasExcluir = "core.pessoas.excluir";
    public const string EnderecosGerenciar = "core.enderecos.gerenciar";
    public const string Exportar = "core.exportar";
}

public sealed record PessoaFiltro(int Page = 1, int PageSize = 20, long? EntidadeId = null, string? Termo = null, string? TipoPessoa = null, bool? Ativo = null);
public sealed record PessoaCreateRequest(string TipoPessoa, string Nome, string? NomeSocial, string? Documento, string? Observacao, IReadOnlyCollection<EnderecoCreateRequest>? Enderecos);
public sealed record PessoaUpdateRequest(string TipoPessoa, string Nome, string? NomeSocial, string? Documento, string? Observacao, bool Ativo);
public sealed record PessoaResumoResponse(long Id, string TipoPessoa, string Nome, string? NomeSocial, string? Documento, bool Ativo, IReadOnlyCollection<EnderecoResponse> Enderecos);
public sealed record PessoaDetalheResponse(long Id, string TipoPessoa, string Nome, string? NomeSocial, string? Documento, string ClassificacaoLgpd, string? Observacao, bool Ativo, IReadOnlyCollection<EnderecoResponse> Enderecos);

public sealed record EnderecoCreateRequest(string Logradouro, string? Numero, string? Complemento, string? Bairro, string Municipio, string Uf, string? Cep, string? Observacao);
public sealed record EnderecoUpdateRequest(string Logradouro, string? Numero, string? Complemento, string? Bairro, string Municipio, string Uf, string? Cep, string? Observacao, bool Ativo);
public sealed record EnderecoResponse(long Id, long? PessoaId, string Logradouro, string? Numero, string? Complemento, string? Bairro, string Municipio, string Uf, string? Cep, string? Observacao, bool Ativo);

public interface IPessoaCadastroRepository
{
    Task<PagedResult<PessoaResumoResponse>> ListarAsync(long tenantId, PessoaFiltro filtro, CancellationToken cancellationToken);
    Task<PessoaDetalheResponse?> ObterAsync(long tenantId, long id, CancellationToken cancellationToken);
    Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, PessoaCreateRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task AtualizarAsync(long tenantId, long id, PessoaUpdateRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken cancellationToken);
    Task<long> AdicionarEnderecoAsync(long tenantId, long pessoaId, EnderecoCreateRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task AtualizarEnderecoAsync(long tenantId, long pessoaId, long enderecoId, EnderecoUpdateRequest request, long? usuarioId, CancellationToken cancellationToken);
    Task ExcluirEnderecoAsync(long tenantId, long pessoaId, long enderecoId, long? usuarioId, CancellationToken cancellationToken);
    Task<byte[]> ExportarAsync(long tenantId, string formato, CancellationToken cancellationToken);
}

public interface IPessoaCadastroService
{
    Task<Result<PagedResult<PessoaResumoResponse>>> ListarAsync(PessoaFiltro filtro, CancellationToken cancellationToken);
    Task<Result<PessoaDetalheResponse>> ObterAsync(long id, CancellationToken cancellationToken);
    Task<Result<long>> CriarAsync(PessoaCreateRequest request, CancellationToken cancellationToken);
    Task<Result> AtualizarAsync(long id, PessoaUpdateRequest request, CancellationToken cancellationToken);
    Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken);
    Task<Result<long>> AdicionarEnderecoAsync(long pessoaId, EnderecoCreateRequest request, CancellationToken cancellationToken);
    Task<Result> AtualizarEnderecoAsync(long pessoaId, long enderecoId, EnderecoUpdateRequest request, CancellationToken cancellationToken);
    Task<Result> ExcluirEnderecoAsync(long pessoaId, long enderecoId, CancellationToken cancellationToken);
    Task<Result<byte[]>> ExportarAsync(string formato, CancellationToken cancellationToken);
}
