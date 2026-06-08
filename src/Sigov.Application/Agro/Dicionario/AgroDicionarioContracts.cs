using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Dicionario;

public sealed record AgroDicionarioDadosResponse(long Id, long? TenantId, string TabelaNome, string? CampoNome, string? NomeAmigavel, string? Descricao, string? Categoria, bool DadoPessoal, bool DadoSensivel, bool Publico, string? MascaraPadrao);
public interface IAgroDicionarioDadosRepository { Task<IReadOnlyCollection<AgroDicionarioDadosResponse>> ListarAsync(long? tenantId, int page, int pageSize, CancellationToken cancellationToken); }
public interface IAgroDicionarioDadosService { Task<Result<IReadOnlyCollection<AgroDicionarioDadosResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken); }
