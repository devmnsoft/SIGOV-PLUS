using Microsoft.Extensions.Logging;
using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Relatorios;

public sealed class AgroIndicadorService : IAgroIndicadorService
{
    private readonly IAgroAccessChecker _accessChecker; private readonly IAgroIndicadorRepository _repository; private readonly AgroIndicadorValidator _validator; private readonly ILogger<AgroIndicadorService> _logger;
    public AgroIndicadorService(IAgroAccessChecker accessChecker, IAgroIndicadorRepository repository, AgroIndicadorValidator validator, ILogger<AgroIndicadorService> logger) { _accessChecker = accessChecker; _repository = repository; _validator = validator; _logger = logger; }
    public async Task<Result<IReadOnlyCollection<AgroIndicadorResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var context = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.IndicadorVisualizar, "agro.bi"), cancellationToken).ConfigureAwait(false); if (context.IsFailure) return Result<IReadOnlyCollection<AgroIndicadorResponse>>.Failure(context.Error!);
        var value = context.Value!; return Result<IReadOnlyCollection<AgroIndicadorResponse>>.Success(await _repository.ListarIndicadoresAsync(value.TenantId, value.EntidadeId, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), cancellationToken).ConfigureAwait(false));
    }
    public async Task<Result<AgroIndicadorResponse>> CriarAsync(AgroIndicadorCreateRequest request, CancellationToken cancellationToken)
    {
        var context = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.IndicadorGerenciar, "agro.bi", request.EntidadeId), cancellationToken).ConfigureAwait(false); if (context.IsFailure) return Result<AgroIndicadorResponse>.Failure(context.Error!);
        try { var value = context.Value!; _validator.Validate(request, value.TenantId, request.EntidadeId ?? value.EntidadeId); _logger.LogInformation("Criando indicador Agro {Codigo} no tenant {TenantId}.", request.Codigo, value.TenantId); return Result<AgroIndicadorResponse>.Success(await _repository.CriarIndicadorAsync(value.TenantId, request.EntidadeId ?? value.EntidadeId, value.UsuarioId, request, cancellationToken).ConfigureAwait(false)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Result<AgroIndicadorResponse>.Failure(ex.Message); }
    }
    public async Task<Result<IReadOnlyCollection<AgroIndicadorValorResponse>>> ListarValoresAsync(long id, int page, int pageSize, CancellationToken cancellationToken)
    {
        var context = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.IndicadorVisualizar, "agro.bi"), cancellationToken).ConfigureAwait(false); if (context.IsFailure) return Result<IReadOnlyCollection<AgroIndicadorValorResponse>>.Failure(context.Error!);
        var value = context.Value!; return Result<IReadOnlyCollection<AgroIndicadorValorResponse>>.Success(await _repository.ListarValoresAsync(value.TenantId, value.EntidadeId, id, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), cancellationToken).ConfigureAwait(false));
    }
}
