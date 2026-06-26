using System.Security.Claims;
using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.PostBuild;

namespace Sigov.Web.Services;

public sealed class MinhaCentralService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly PostBuildSaasService _saasService;
    private readonly ILogger<MinhaCentralService> _logger;

    public MinhaCentralService(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schemaInspector, PostBuildSaasService saasService, ILogger<MinhaCentralService> logger)
    {
        _connectionFactory = connectionFactory;
        _schemaInspector = schemaInspector;
        _saasService = saasService;
        _logger = logger;
    }

    public async Task<MinhaCentralViewModel> ObterResumoAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
            var tenant = tenantId.HasValue ? $"Tenant #{tenantId.Value}" : "Ambiente demonstração";
            if (tenantId.HasValue && await _schemaInspector.TableExistsAsync("sigov", "tenant", cancellationToken).ConfigureAwait(false))
            {
                using var cn = _connectionFactory.CreateConnection();
                tenant = await cn.ExecuteScalarAsync<string?>(new CommandDefinition("select nome from sigov.tenant where id=@Id", new { Id = tenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false) ?? tenant;
            }

            return new MinhaCentralViewModel
            {
                Perfil = user.IsInRole("ADMINISTRADOR_GERAL") ? "Administrador Geral" : "Operador",
                Tenant = tenant,
                Acoes = await ObterAcoesRecomendadasAsync(user, cancellationToken).ConfigureAwait(false),
                Modulos = await ObterModulosUsuarioAsync(user, cancellationToken).ConfigureAwait(false),
                Pendencias = await ObterPendenciasAsync(user, cancellationToken).ConfigureAwait(false),
                AlertasLgpd = await ObterAlertasLgpdAsync(user, cancellationToken).ConfigureAwait(false),
                Atividades = await ObterUltimasAtividadesAsync(user, cancellationToken).ConfigureAwait(false),
                Ambiente = _saasService.CriarAmbiente(true),
                MensagemFallback = "Quando alguma tabela opcional não existir, a Central mostra recomendações e sinaliza limitação sem simular dados."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar Minha Central.");
            return new MinhaCentralViewModel { Acoes = DefaultAcoes(), Pendencias = DefaultPendencias(), AlertasLgpd = DefaultAlertas(), Ambiente = _saasService.CriarAmbiente(false), MensagemFallback = "Central aberta em modo seguro; dados reais indisponíveis no momento." };
        }
    }

    public Task<IReadOnlyList<AcaoRecomendadaViewModel>> ObterAcoesRecomendadasAsync(ClaimsPrincipal user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AcaoRecomendadaViewModel>>(DefaultAcoes());

    public async Task<IReadOnlyList<ModuloResumoViewModel>> ObterModulosUsuarioAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
        var modulos = await _saasService.ListarModulosAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return modulos.Select(x => new ModuloResumoViewModel(x.Codigo, x.Nome, x.StatusDescricao)).ToArray();
    }

    public Task<IReadOnlyList<PendenciaViewModel>> ObterPendenciasAsync(ClaimsPrincipal user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PendenciaViewModel>>(DefaultPendencias());
    public Task<IReadOnlyList<AlertaLgpdViewModel>> ObterAlertasLgpdAsync(ClaimsPrincipal user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AlertaLgpdViewModel>>(DefaultAlertas());

    public async Task<IReadOnlyList<AtividadeRecenteViewModel>> ObterUltimasAtividadesAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "auditoria_evento", cancellationToken).ConfigureAwait(false)) return Array.Empty<AtividadeRecenteViewModel>();
        try
        {
            using var cn = _connectionFactory.CreateConnection();
            var rows = await cn.QueryAsync<AtividadeRecenteViewModel>(new CommandDefinition("select acao, entidade, created_at as Data from sigov.auditoria_evento order by created_at desc limit 5;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToArray();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Atividades recentes indisponíveis."); return Array.Empty<AtividadeRecenteViewModel>(); }
    }

    private static long? TryGetLong(string? value) => long.TryParse(value, out var parsed) ? parsed : null;
    private static AcaoRecomendadaViewModel[] DefaultAcoes() => new[] { new AcaoRecomendadaViewModel("Complete a implantação", "Finalize tenant, entidade, exercício e módulos.", "/Saas/Implantacao", "primary"), new AcaoRecomendadaViewModel("Cadastre usuários", "Crie operadores com dados mínimos e auditoria.", "/Seguranca/Usuarios", "info"), new AcaoRecomendadaViewModel("Configure permissões", "Revise perfis e matriz por módulo.", "/Seguranca/Permissoes", "warning"), new AcaoRecomendadaViewModel("Ative módulos", "Habilite módulos contratados.", "/Saas/Modulos", "success"), new AcaoRecomendadaViewModel("Revise auditorias", "Confira eventos e acessos a dados pessoais.", "/Auditoria/Trilhas", "danger"), new AcaoRecomendadaViewModel("Manual do perfil", "Veja rotinas diárias e cuidados LGPD.", "/Manual", "secondary") };
    private static PendenciaViewModel[] DefaultPendencias() => new[] { new PendenciaViewModel("Parâmetros globais", "Revisar valores sensíveis antes de produção.", "/Saas/Parametros"), new PendenciaViewModel("Permissões LGPD", "Confirmar perfis com acesso a dados pessoais.", "/Seguranca/Permissoes"), new PendenciaViewModel("Evidências de implantação", "Atualizar status no roteiro SaaS.", "/Saas/Implantacao") };
    private static AlertaLgpdViewModel[] DefaultAlertas() => new[] { new AlertaLgpdViewModel("Dados pessoais", "Listagens sensíveis usam máscara e ações críticas são auditáveis."), new AlertaLgpdViewModel("Exportações", "Relatórios CSV mascaram dados pessoais e não exportam segredos.") };
}
