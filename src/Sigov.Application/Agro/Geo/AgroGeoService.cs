using System.Globalization;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Agro.Permissions;
using Sigov.Application.Common;
using Sigov.Application.Saas;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Geo;

public sealed class AgroGeoService : IAgroGeoService
{
    private static readonly ISet<string> TiposCamada = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PRODUTORES", "PROPRIEDADES", "TALHOES", "CULTURAS", "ESTRADAS", "PONTOS_CRITICOS", "FEIRAS", "AGROINDUSTRIAS", "OCORRENCIAS", "OUTROS" };
    private static readonly ISet<string> TiposGeometria = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "POINT", "LINESTRING", "POLYGON", "MULTIPOLYGON", "GEOJSON" };
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IModuloLicenciamentoService _modulos;
    private readonly IPermissionService _permissoes;
    private readonly ICoordenadaValidator _coordenadas;
    private readonly IGeoJsonValidator _geoJson;
    private readonly IAuditService _audit;
    private readonly IAgroGeoRepository _repository;
    private readonly ILogger<AgroGeoService> _logger;

    public AgroGeoService(ICurrentTenant tenant, ICurrentUser user, IModuloLicenciamentoService modulos, IPermissionService permissoes, ICoordenadaValidator coordenadas, IGeoJsonValidator geoJson, IAuditService audit, IAgroGeoRepository repository, ILogger<AgroGeoService> logger)
    {
        _tenant = tenant; _user = user; _modulos = modulos; _permissoes = permissoes; _coordenadas = coordenadas; _geoJson = geoJson; _audit = audit; _repository = repository; _logger = logger;
    }

    public async Task<Result<PagedResult<AgroGeoCamadaResponse>>> ListarCamadasAsync(AgroGeoFiltro filtro, CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "visualizar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<PagedResult<AgroGeoCamadaResponse>>.Failure(ctx.Error ?? "Contexto inválido.");
        return Result<PagedResult<AgroGeoCamadaResponse>>.Success(await _repository.ListarCamadasAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, Page(filtro), cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AgroGeoCamadaResponse>> ObterCamadaAsync(long id, CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "visualizar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<AgroGeoCamadaResponse>.Failure(ctx.Error ?? "Contexto inválido.");
        var item = await _repository.ObterCamadaAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, id, cancellationToken).ConfigureAwait(false);
        return item is null ? Result<AgroGeoCamadaResponse>.Failure("Camada não encontrada.") : Result<AgroGeoCamadaResponse>.Success(item);
    }

    public async Task<Result<long>> CriarCamadaAsync(AgroGeoCamadaRequest request, CancellationToken cancellationToken)
    {
        var validacao = ValidarCamada(request); if (validacao.IsFailure) return Result<long>.Failure(validacao.Error!);
        var ctx = await Ctx("geo", "criar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<long>.Failure(ctx.Error ?? "Contexto inválido.");
        var id = await _repository.CriarCamadaAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, ctx.Value!.UsuarioId, request, cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(AgroPermissions.Modulo, "CRIAR_CAMADA_GEO", "sigov.agro_geo_camada", id.ToString(CultureInfo.InvariantCulture), null, new { id, request.Codigo, request.Nome, request.TipoCamada }, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Camada Agro {CamadaId} criada para tenant {TenantId}.", id, ctx.Value!.TenantId);
        return Result<long>.Success(id);
    }

    public async Task<Result> AtualizarCamadaAsync(long id, AgroGeoCamadaRequest request, CancellationToken cancellationToken)
    {
        var validacao = ValidarCamada(request); if (validacao.IsFailure) return validacao;
        var ctx = await Ctx("geo", "editar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result.Failure(ctx.Error ?? "Contexto inválido.");
        await _repository.AtualizarCamadaAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, id, ctx.Value!.UsuarioId, request, cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(AgroPermissions.Modulo, "ATUALIZAR_CAMADA_GEO", "sigov.agro_geo_camada", id.ToString(CultureInfo.InvariantCulture), null, new { id, request.Codigo, request.Nome, request.TipoCamada }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ExcluirCamadaAsync(long id, CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "excluir", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result.Failure(ctx.Error ?? "Contexto inválido.");
        await _repository.ExcluirCamadaAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, id, ctx.Value!.UsuarioId, cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(AgroPermissions.Modulo, "EXCLUIR_CAMADA_GEO", "sigov.agro_geo_camada", id.ToString(CultureInfo.InvariantCulture), null, new { id }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<PagedResult<AgroGeoFeicaoResponse>>> ListarFeicoesAsync(AgroGeoFiltro filtro, CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "visualizar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<PagedResult<AgroGeoFeicaoResponse>>.Failure(ctx.Error ?? "Contexto inválido.");
        return Result<PagedResult<AgroGeoFeicaoResponse>>.Success(await _repository.ListarFeicoesAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, Page(filtro), cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AgroGeoFeicaoResponse>> ObterFeicaoAsync(long id, CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "visualizar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<AgroGeoFeicaoResponse>.Failure(ctx.Error ?? "Contexto inválido.");
        var item = await _repository.ObterFeicaoAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, id, cancellationToken).ConfigureAwait(false);
        return item is null ? Result<AgroGeoFeicaoResponse>.Failure("Feição não encontrada.") : Result<AgroGeoFeicaoResponse>.Success(item);
    }

    public async Task<Result<long>> CriarFeicaoAsync(AgroGeoFeicaoRequest request, CancellationToken cancellationToken)
    {
        var validacao = ValidarFeicao(request); if (validacao.IsFailure) return Result<long>.Failure(validacao.Error!);
        var ctx = await Ctx("geo", "criar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<long>.Failure(ctx.Error ?? "Contexto inválido.");
        var id = await _repository.CriarFeicaoAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, ctx.Value!.UsuarioId, request, cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(AgroPermissions.Modulo, "CRIAR_FEICAO_GEO", "sigov.agro_geo_feicao", id.ToString(CultureInfo.InvariantCulture), null, new { id, request.CamadaId, request.Nome, request.TipoGeometria }, cancellationToken).ConfigureAwait(false);
        return Result<long>.Success(id);
    }

    public async Task<Result> AtualizarFeicaoAsync(long id, AgroGeoFeicaoRequest request, CancellationToken cancellationToken)
    {
        var validacao = ValidarFeicao(request); if (validacao.IsFailure) return validacao;
        var ctx = await Ctx("geo", "editar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result.Failure(ctx.Error ?? "Contexto inválido.");
        await _repository.AtualizarFeicaoAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, id, ctx.Value!.UsuarioId, request, cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(AgroPermissions.Modulo, "ATUALIZAR_FEICAO_GEO", "sigov.agro_geo_feicao", id.ToString(CultureInfo.InvariantCulture), null, new { id, request.CamadaId, request.Nome, request.TipoGeometria }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ExcluirFeicaoAsync(long id, CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "excluir", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result.Failure(ctx.Error ?? "Contexto inválido.");
        await _repository.ExcluirFeicaoAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, id, ctx.Value!.UsuarioId, cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(AgroPermissions.Modulo, "EXCLUIR_FEICAO_GEO", "sigov.agro_geo_feicao", id.ToString(CultureInfo.InvariantCulture), null, new { id }, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<string>> ExportarGeoJsonAsync(CancellationToken cancellationToken)
    {
        var ctx = await Ctx("geo", "exportar", cancellationToken).ConfigureAwait(false); if (ctx.IsFailure) return Result<string>.Failure(ctx.Error ?? "Contexto inválido.");
        return Result<string>.Success(await _repository.ExportarGeoJsonAsync(ctx.Value!.TenantId, ctx.Value!.EntidadeId, cancellationToken).ConfigureAwait(false));
    }

    private Result ValidarCamada(AgroGeoCamadaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) return Result.Failure("Código e nome da camada são obrigatórios.");
        if (!TiposCamada.Contains(r.TipoCamada)) return Result.Failure("Tipo de camada Agro inválido.");
        return Result.Success();
    }

    private Result ValidarFeicao(AgroGeoFeicaoRequest r)
    {
        if (r.CamadaId <= 0 || string.IsNullOrWhiteSpace(r.Nome)) return Result.Failure("Camada e nome da feição são obrigatórios.");
        if (!TiposGeometria.Contains(r.TipoGeometria)) return Result.Failure("Tipo de geometria inválido.");
        var coordenadas = _coordenadas.Validar(r.Latitude, r.Longitude); if (coordenadas.IsFailure) return coordenadas;
        return _geoJson.Validar(r.GeoJson);
    }

    private async Task<Result<(long TenantId, long? EntidadeId, long? UsuarioId)>> Ctx(string recurso, string acao, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Result<(long, long?, long?)>.Failure("Tenant obrigatório.");
        if (!_user.IsAuthenticated) return Result<(long, long?, long?)>.Failure("Usuário não autenticado.");
        if (!await _modulos.IsModuleEnabledAsync(_tenant.TenantId.Value, AgroPermissions.Modulo, cancellationToken).ConfigureAwait(false)) return Result<(long, long?, long?)>.Failure("Módulo Agro não contratado ou desabilitado para o tenant.");
        if (_user.UsuarioId.HasValue && !await _permissoes.HasPermissionAsync(_user.UsuarioId.Value, AgroPermissions.Modulo, recurso, acao, cancellationToken).ConfigureAwait(false)) return Result<(long, long?, long?)>.Failure("403");
        return Result<(long, long?, long?)>.Success((_tenant.TenantId.Value, _tenant.EntidadeId, _user.UsuarioId));
    }

    private static AgroGeoFiltro Page(AgroGeoFiltro filtro) => filtro with { Page = filtro.Page <= 0 ? 1 : filtro.Page, PageSize = filtro.PageSize is <= 0 or > 100 ? 20 : filtro.PageSize };
}
