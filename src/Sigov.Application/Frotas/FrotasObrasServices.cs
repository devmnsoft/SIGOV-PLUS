using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Obras;
using Sigov.Application.Saas;
using Sigov.Domain.Common;

namespace Sigov.Application.Frotas;

public sealed class ObrasService : IObrasService
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IObrasRepository _repository;
    private readonly IPermissionService _permissions;
    private readonly IModuloLicenciamentoService _modulos;
    private readonly IAuditService _audit;

    public ObrasService(ICurrentTenant tenant, ICurrentUser user, IObrasRepository repository, IPermissionService permissions, IModuloLicenciamentoService modulos, IAuditService audit)
    { _tenant = tenant; _user = user; _repository = repository; _permissions = permissions; _modulos = modulos; _audit = audit; }

    public async Task<Result<PagedResult<ObraRegistroDto>>> ListarAsync(string recurso, long? obraId, int pagina, int tamanho, CancellationToken ct)
    {
        var guard = await GuardAsync(recurso == "obras" ? "obra" : PermissionResource(recurso), "visualizar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Result<PagedResult<ObraRegistroDto>>.Failure(guard.Error!);
        return Result<PagedResult<ObraRegistroDto>>.Success(await _repository.ListarAsync(_tenant.TenantId!.Value, Recurso(recurso), obraId, Math.Max(1, pagina), Math.Clamp(tamanho, 1, 100), ct).ConfigureAwait(false));
    }

    public async Task<Result<long>> CriarAsync(string recurso, ObraRegistroRequest request, string correlationId, CancellationToken ct)
    {
        var permissionResource = recurso == "obras" ? "obra" : PermissionResource(recurso);
        var guard = await GuardAsync(permissionResource, recurso == "obras" ? "criar" : "registrar", ct).ConfigureAwait(false);
        if (guard.IsFailure) return Result<long>.Failure(guard.Error!);
        var validation = Validate(recurso, request);
        if (validation is not null) return Result<long>.Failure(validation);
        var id = await _repository.CriarAsync(_tenant.TenantId!.Value, _tenant.EntidadeId, _tenant.ExercicioId, _user.UsuarioId, Recurso(recurso), request, correlationId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("obras", "CRIAR", Recurso(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { id, correlationId, request.Justificativa }, ct).ConfigureAwait(false);
        return Result<long>.Success(id);
    }

    public async Task<Result<ObrasDashboardDto>> DashboardAsync(CancellationToken ct)
    {
        var guard = await GuardAsync("dashboard", "visualizar", ct).ConfigureAwait(false);
        return guard.IsFailure ? Result<ObrasDashboardDto>.Failure(guard.Error!) : Result<ObrasDashboardDto>.Success(await _repository.DashboardAsync(_tenant.TenantId!.Value, ct).ConfigureAwait(false));
    }

    private async Task<Result> GuardAsync(string recurso, string acao, CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue || _tenant.TenantId.GetValueOrDefault() <= 0) return Result.Failure("Tenant obrigatório.");
        if (!_user.IsAuthenticated || !_user.UsuarioId.HasValue) return await DenyAsync(recurso, acao, "Usuário autenticado obrigatório.", ct).ConfigureAwait(false);
        if (!await _modulos.IsModuleEnabledAsync(_tenant.TenantId.Value, "obras", ct).ConfigureAwait(false)) return await DenyAsync(recurso, acao, "Módulo obras não contratado ou habilitado.", ct).ConfigureAwait(false);
        if (!await _permissions.HasPermissionAsync(_user.UsuarioId.Value, "obras", recurso, acao, ct).ConfigureAwait(false)) return await DenyAsync(recurso, acao, "403", ct).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> DenyAsync(string recurso, string acao, string motivo, CancellationToken ct)
    {
        await _audit.RegistrarAsync("obras", "ACESSO_NEGADO", "seguranca_evento", recurso, null, new { recurso, acao, motivo, tenantId = _tenant.TenantId, usuarioId = _user.UsuarioId }, ct).ConfigureAwait(false);
        return Result.Failure(motivo);
    }

    private static string? Validate(string recurso, ObraRegistroRequest r)
    {
        if (recurso == "obras" && (string.IsNullOrWhiteSpace(r.Nome) || string.IsNullOrWhiteSpace(r.Descricao))) return "Obra exige nome e objeto/descrição.";
        if (recurso == "diario" && (r.ObraId is null or <= 0 || r.DataReferencia is null || string.IsNullOrWhiteSpace(r.Descricao))) return "Diário exige obra, data e descrição.";
        if (recurso == "medicoes" && (r.ObraId is null or <= 0 || r.DataReferencia is null || r.Valor is null or <= 0)) return "Medição exige obra, competência/data e valor positivo.";
        if ((r.Status.Equals("PARALISADA", StringComparison.OrdinalIgnoreCase) || r.Status.Equals("ENCERRADA", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(r.Justificativa)) return "Paralisação ou encerramento exige justificativa.";
        return null;
    }

    private static string PermissionResource(string recurso) => recurso switch { "etapas" => "obra", "medicoes" => "medicao", "fiscalizacoes" => "ocorrencia", "diario" => "diario", _ => throw new ArgumentException("Recurso de obra inválido.") };
    private static string Recurso(string recurso) => recurso switch { "obras" or "obra" => "obra", "etapas" or "etapa" => "obra_etapa", "medicoes" or "medicao" => "obra_medicao", "fiscalizacoes" or "fiscalizacao" => "obra_fiscalizacao", "diario" => "obra_diario", _ => throw new ArgumentException("Recurso de obra inválido.") };
}
