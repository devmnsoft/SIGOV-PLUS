using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasSolicitacaoClienteService : ISaasSolicitacaoClienteService
{
    private readonly ISaasSolicitacaoClienteRepository _repository;
    private readonly ISaasTenantProvisioningService _provisioningService;
    private readonly SaasSolicitacaoClienteValidator _validator;
    private readonly ILogger<SaasSolicitacaoClienteService> _logger;

    public SaasSolicitacaoClienteService(ISaasSolicitacaoClienteRepository repository, ISaasTenantProvisioningService provisioningService, SaasSolicitacaoClienteValidator validator, ILogger<SaasSolicitacaoClienteService> logger)
    {
        _repository = repository;
        _provisioningService = provisioningService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<SaasSolicitacaoClienteResponse>> CriarAsync(SaasSolicitacaoClienteCreateRequest request, CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(request);
        if (validation.IsFailure) return validation.ValidationErrors.Count > 0 ? Result<SaasSolicitacaoClienteResponse>.ValidationFailure(validation.ValidationErrors) : Result<SaasSolicitacaoClienteResponse>.Failure(validation.Error ?? "Solicitação inválida.");
        var correlationId = Guid.NewGuid();
        var created = await _repository.CreateAsync(request, SaasSolicitacaoClienteMapper.NewProtocol(), correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, "SaasSolicitacaoClienteRecebida", "saas_solicitacao_cliente", created.Id, new { created.Id, created.Protocolo, created.PlanoCodigo }, correlationId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Solicitação SaaS {Protocolo} recebida com dados pessoais mascarados em respostas.", created.Protocolo);
        return Result<SaasSolicitacaoClienteResponse>.Success(created);
    }

    public Task<IReadOnlyCollection<SaasSolicitacaoClienteResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken) => _repository.ListAdminAsync((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100), Math.Clamp(pageSize, 1, 100), cancellationToken);
    public Task<SaasSolicitacaoClienteResponse?> GetAdminAsync(long id, CancellationToken cancellationToken) => _repository.GetAdminAsync(id, cancellationToken);

    public async Task<Result> AprovarAsync(long id, AprovarSolicitacaoClienteRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        await _repository.UpdateStatusAsync(id, "APROVADA", request.Observacao, null, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, "SaasSolicitacaoClienteAprovada", "saas_solicitacao_cliente", id, new { id, usuarioId }, correlationId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<long>> ConverterEmTenantAsync(long id, ConverterSolicitacaoEmTenantRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var tenantId = await _provisioningService.ConverterSolicitacaoAsync(id, request, usuarioId, cancellationToken).ConfigureAwait(false);
        return Result<long>.Success(tenantId);
    }

    public async Task<Result> RecusarAsync(long id, RecusarSolicitacaoClienteRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        await _repository.UpdateStatusAsync(id, "RECUSADA", request.Motivo, null, correlationId, cancellationToken).ConfigureAwait(false);
        await _repository.InsertEventoAsync(null, "SaasSolicitacaoClienteRecusada", "saas_solicitacao_cliente", id, new { id, usuarioId }, correlationId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
