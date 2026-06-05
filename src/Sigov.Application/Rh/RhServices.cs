using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Rh;

public sealed class RhService : IRhService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.OrdinalIgnoreCase)
    {
        "servidores", "cargos", "lotacoes", "vinculos", "folhas", "folha-eventos", "folha-lancamentos",
        "pontos", "ferias", "afastamentos", "saude-ocupacional", "esocial", "portal-usuarios", "portal-acessos", "eventos"
    };

    private readonly IRhRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly ILogger<RhService> _logger;

    public RhService(IRhRepository repo, ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IAuditService audit, ILogger<RhService> logger)
    {
        _repo = repo; _tenant = tenant; _user = user; _permissions = permissions; _audit = audit; _logger = logger;
    }

    private long TenantId => _tenant.TenantId ?? 0;
    private bool EscopoValido => TenantId > 0;
    private static string Tabela(string recurso) => $"sigov.{Normalizar(recurso).Replace('-', '_')}";
    private static string Normalizar(string recurso) => recurso.Trim().ToLowerInvariant();
    private static Result<T> EscopoFailure<T>() => Result<T>.Failure("Tenant obrigatório para operações de RH.");
    private static Result EscopoFailure() => Result.Failure("Tenant obrigatório para operações de RH.");

    private static bool RecursoValido(string recurso) => Recursos.Contains(Normalizar(recurso));

    private async Task<bool> CanAsync(string chave, CancellationToken ct)
    {
        if (!_user.UsuarioId.HasValue) return false;
        var partes = chave.Split('.');
        var recurso = partes.Length >= 3 ? $"{partes[0]}.{partes[1]}" : chave;
        var acao = partes.Length >= 3 ? partes[2] : "visualizar";
        return await _permissions.HasPermissionAsync(_user.UsuarioId.Value, RhPermissoes.Modulo, recurso, acao, ct).ConfigureAwait(false);
    }

    public async Task<Result<PagedResult<RhRegistroResponse>>> ListarAsync(string recurso, RhFiltro filtro, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<PagedResult<RhRegistroResponse>>();
        if (!RecursoValido(recurso)) return Result<PagedResult<RhRegistroResponse>>.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Visualizar, ct).ConfigureAwait(false)) return Result<PagedResult<RhRegistroResponse>>.Failure("403");
        try
        {
            var result = await _repo.ListarAsync(TenantId, Normalizar(recurso), filtro, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("rh", "CONSULTAR", Tabela(recurso), "LIST", null, new { filtro.Page, filtro.PageSize, filtro.Termo }, ct).ConfigureAwait(false);
            return Result<PagedResult<RhRegistroResponse>>.Success(result);
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar RH {Recurso}.", recurso); return Result<PagedResult<RhRegistroResponse>>.Failure("Erro ao listar registros de RH."); }
    }

    public async Task<Result<RhRegistroResponse>> ObterAsync(string recurso, long id, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhRegistroResponse>();
        if (!RecursoValido(recurso)) return Result<RhRegistroResponse>.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Visualizar, ct).ConfigureAwait(false)) return Result<RhRegistroResponse>.Failure("403");
        var item = await _repo.ObterAsync(TenantId, Normalizar(recurso), id, ct).ConfigureAwait(false);
        return item is null ? Result<RhRegistroResponse>.Failure("Registro não encontrado.") : Result<RhRegistroResponse>.Success(item);
    }

    public async Task<Result<long>> CriarAsync(string recurso, RhRegistroCreateRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<long>();
        if (!RecursoValido(recurso)) return Result<long>.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Criar, ct).ConfigureAwait(false)) return Result<long>.Failure("403");
        try
        {
            var id = await _repo.CriarAsync(TenantId, Normalizar(recurso), request, _user.UsuarioId, ct).ConfigureAwait(false);
            await _audit.RegistrarAsync("rh", "CRIAR", Tabela(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request.Dados, ct).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar RH {Recurso}.", recurso); return Result<long>.Failure("Erro ao criar registro de RH."); }
    }

    public async Task<Result> AtualizarAsync(string recurso, long id, RhRegistroUpdateRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure();
        if (!RecursoValido(recurso)) return Result.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Editar, ct).ConfigureAwait(false)) return Result.Failure("403");
        var anterior = await _repo.ObterAsync(TenantId, Normalizar(recurso), id, ct).ConfigureAwait(false);
        await _repo.AtualizarAsync(TenantId, Normalizar(recurso), id, request, _user.UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("rh", "EDITAR", Tabela(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), anterior, request.Dados, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ExcluirAsync(string recurso, long id, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure();
        if (!RecursoValido(recurso)) return Result.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Excluir, ct).ConfigureAwait(false)) return Result.Failure("403");
        await _repo.ExcluirAsync(TenantId, Normalizar(recurso), id, _user.UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("rh", "EXCLUIR", Tabela(recurso), id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { softDelete = true }, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<RhDashboardResponse>> DashboardAsync(CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhDashboardResponse>();
        if (!await CanAsync(RhPermissoes.Dashboard, ct).ConfigureAwait(false)) return Result<RhDashboardResponse>.Failure("403");
        return Result<RhDashboardResponse>.Success(await _repo.DashboardAsync(TenantId, ct).ConfigureAwait(false));
    }

    public async Task<Result<RhPortalResumoResponse>> PortalServidorAsync(long servidorId, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<RhPortalResumoResponse>();
        if (!await CanAsync(RhPermissoes.Portal, ct).ConfigureAwait(false)) return Result<RhPortalResumoResponse>.Failure("403");
        var portal = await _repo.PortalServidorAsync(TenantId, servidorId, ct).ConfigureAwait(false);
        return portal is null ? Result<RhPortalResumoResponse>.Failure("Servidor não encontrado.") : Result<RhPortalResumoResponse>.Success(portal);
    }

    public async Task<Result<long>> IntegrarFinanceiroAsync(RhFinanceiroIntegracaoRequest request, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<long>();
        if (!await CanAsync(RhPermissoes.IntegrarFinanceiro, ct).ConfigureAwait(false)) return Result<long>.Failure("403");
        var eventoId = await _repo.PrepararIntegracaoFinanceiraAsync(TenantId, request, _user.UsuarioId, ct).ConfigureAwait(false);
        await _audit.RegistrarAsync("rh", "INTEGRAR_FINANCEIRO", "sigov.rh_evento", eventoId.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request, ct).ConfigureAwait(false);
        return Result<long>.Success(eventoId);
    }

    public async Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct)
    {
        if (!EscopoValido) return EscopoFailure<byte[]>();
        if (!RecursoValido(recurso)) return Result<byte[]>.Failure("Recurso de RH inválido.");
        if (!await CanAsync(RhPermissoes.Exportar, ct).ConfigureAwait(false)) return Result<byte[]>.Failure("403");
        await _audit.RegistrarAsync("rh", "EXPORTAR", Tabela(recurso), formato, null, new { recurso, formato }, ct).ConfigureAwait(false);
        return Result<byte[]>.Success(await _repo.ExportarAsync(TenantId, Normalizar(recurso), formato, ct).ConfigureAwait(false));
    }
}
