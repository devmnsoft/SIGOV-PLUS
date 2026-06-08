using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Dicionario;

public sealed class AgroDicionarioDadosService : IAgroDicionarioDadosService
{
    private readonly IAgroAccessChecker _accessChecker; private readonly IAgroDicionarioDadosRepository _repository;
    public AgroDicionarioDadosService(IAgroAccessChecker accessChecker, IAgroDicionarioDadosRepository repository) { _accessChecker = accessChecker; _repository = repository; }
    public async Task<Result<IReadOnlyCollection<AgroDicionarioDadosResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken) { var c = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.DadosAbertosVisualizar, "agro.dados_abertos"), cancellationToken).ConfigureAwait(false); if (c.IsFailure) return Result<IReadOnlyCollection<AgroDicionarioDadosResponse>>.Failure(c.Error!); return Result<IReadOnlyCollection<AgroDicionarioDadosResponse>>.Success(await _repository.ListarAsync(c.Value!.TenantId, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), cancellationToken).ConfigureAwait(false)); }
}
