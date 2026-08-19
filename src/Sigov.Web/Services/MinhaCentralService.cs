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
                Perfil = Perfil(user),
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
            return new MinhaCentralViewModel { Acoes = Array.Empty<AcaoRecomendadaViewModel>(), Pendencias = Array.Empty<PendenciaViewModel>(), AlertasLgpd = Array.Empty<AlertaLgpdViewModel>(), Ambiente = _saasService.CriarAmbiente(false), MensagemFallback = "Central aberta em modo seguro; dados reais indisponíveis no momento." };
        }
    }

    public Task<IReadOnlyList<AcaoRecomendadaViewModel>> ObterAcoesRecomendadasAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AcaoRecomendadaViewModel>>(AcoesPerfil(user));

    public async Task<IReadOnlyList<ModuloResumoViewModel>> ObterModulosUsuarioAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
        var modulos = await _saasService.ListarModulosAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return modulos.Select(x => new ModuloResumoViewModel(x.Codigo, x.Nome, x.StatusDescricao)).ToArray();
    }

    public async Task<IReadOnlyList<PendenciaViewModel>> ObterPendenciasAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
        if (!tenantId.HasValue || !await _schemaInspector.TableExistsAsync("sigov", "pendencia_operacional", cancellationToken).ConfigureAwait(false)) return Array.Empty<PendenciaViewModel>();
        using var cn = _connectionFactory.CreateConnection();
        const string sql = "select titulo as Titulo, coalesce(descricao,'') as Descricao, rota_acao as Url from sigov.pendencia_operacional where tenant_id=@TenantId and status in ('ABERTA','EM_TRATAMENTO') order by prazo nulls last, created_at desc limit 8";
        return (await cn.QueryAsync<PendenciaViewModel>(new CommandDefinition(sql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
    }
    public async Task<IReadOnlyList<AlertaLgpdViewModel>> ObterAlertasLgpdAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
        if (!tenantId.HasValue || !await _schemaInspector.TableExistsAsync("sigov", "alerta_operacional", cancellationToken).ConfigureAwait(false)) return Array.Empty<AlertaLgpdViewModel>();
        using var cn = _connectionFactory.CreateConnection();
        const string sql = "select titulo as Titulo, coalesce(descricao,'') as Descricao from sigov.alerta_operacional where tenant_id=@TenantId and status in ('ATIVO','ABERTO') and tipo in ('LGPD','SEGURANCA','TECNICO','RISCO') order by created_at desc limit 8";
        return (await cn.QueryAsync<AlertaLgpdViewModel>(new CommandDefinition(sql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
    }

    public async Task<IReadOnlyList<AtividadeRecenteViewModel>> ObterUltimasAtividadesAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
        if (!tenantId.HasValue || !await _schemaInspector.TableExistsAsync("sigov", "auditoria_evento", cancellationToken).ConfigureAwait(false)) return Array.Empty<AtividadeRecenteViewModel>();
        try
        {
            using var cn = _connectionFactory.CreateConnection();
            var rows = await cn.QueryAsync<AtividadeRecenteViewModel>(new CommandDefinition("select acao, entidade, created_at as Data from sigov.auditoria_evento where tenant_id=@TenantId order by created_at desc limit 5;", new { TenantId = tenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToArray();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Atividades recentes indisponíveis."); return Array.Empty<AtividadeRecenteViewModel>(); }
    }

    private static long? TryGetLong(string? value) => long.TryParse(value, out var parsed) ? parsed : null;
    private static string Perfil(ClaimsPrincipal user) => user.Claims.FirstOrDefault(x => x.Type is ClaimTypes.Role or "role")?.Value ?? "Operador";
    private static AcaoRecomendadaViewModel[] AcoesPerfil(ClaimsPrincipal user)
    {
        if (user.IsInRole("ADMINISTRADOR_GERAL") || user.IsInRole("SUPER_ADMIN")) return new[] { A("Status funcional", "Comprove estruturas e módulos.", "/Modulos/StatusFuncional"), A("Alertas críticos", "Acompanhe riscos técnicos e funcionais.", "/Alertas"), A("Matriz de acesso", "Revise concessões e negativas.", "/Seguranca/Permissoes") };
        if (user.IsInRole("ADMIN_TENANT")) return new[] { A("Módulos contratados", "Revise o catálogo do tenant.", "/Saas/Modulos"), A("Pendências", "Resolva pendências do tenant.", "/Pendencias"), A("Auditoria e LGPD", "Acompanhe trilhas autorizadas.", "/Auditoria/Trilhas") };
        if (user.IsInRole("PROFESSOR")) return new[] { A("Minhas turmas", "Consulte somente turmas vinculadas.", "/Educacao/Turmas"), A("Frequência", "Registre a frequência pendente.", "/Educacao/Frequencias") };
        if (user.IsInRole("ACS")) return new[] { A("Visitas pendentes", "Consulte sua microárea.", "/Saude/Acs"), A("Ocorrências", "Registre o desfecho da visita.", "/Saude/Acs") };
        if (user.IsInRole("FUNCIONARIO_FINANCEIRO")) return new[] { A("Pagamentos", "Trate pagamentos permitidos.", "/Financeiro/Pagamentos"), A("Pendências financeiras", "Consulte baixas, DAMs e faturas.", "/Pendencias") };
        if (user.IsInRole("AUDITOR")) return new[] { A("Trilhas", "Consulte eventos sem alterar operação.", "/Auditoria/Trilhas"), A("Alertas", "Veja negativas, exportações e LGPD.", "/Alertas") };
        if (user.IsInRole("ALMOXARIFADO")) return new[] { A("Estoque", "Consulte saldos e estoque crítico.", "/Almoxarifado"), A("Pendências", "Trate requisições autorizadas.", "/Pendencias") };
        return new[] { A("Minhas pendências", "Veja somente ações do seu perfil.", "/Pendencias"), A("Alertas", "Acompanhe alertas autorizados.", "/Alertas") };
    }
    private static AcaoRecomendadaViewModel A(string title, string description, string url) => new(title, description, url, "info");
}
