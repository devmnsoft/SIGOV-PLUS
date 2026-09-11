using System.Security.Claims;
using Dapper;
using Sigov.Application.Authorization;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.PostBuild;

namespace Sigov.Web.Services;

public sealed class MinhaCentralService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly PostBuildSaasService _saasService;
    private readonly ILogger<MinhaCentralService> _logger;
    private readonly IRequestAuthorizationSnapshot _authorization;

    public MinhaCentralService(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schemaInspector, PostBuildSaasService saasService, ILogger<MinhaCentralService> logger, IRequestAuthorizationSnapshot authorization)
    {
        _connectionFactory = connectionFactory;
        _schemaInspector = schemaInspector;
        _saasService = saasService;
        _logger = logger;
        _authorization = authorization;
    }

    public async Task<MinhaCentralViewModel> ObterResumoAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var authorization = await _authorization.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!authorization.Authenticated || !authorization.TenantId.HasValue || authorization.UserId <= 0)
        {
            throw new UnauthorizedAccessException("É necessário selecionar um contexto institucional autorizado.");
        }
        var tenantId = authorization.TenantId.Value;
        var userId = authorization.UserId;
        if (!await _schemaInspector.TableExistsAsync("sigov", "tenant", cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A estrutura obrigatória sigov.tenant não está disponível.");
        }

        using var cn = _connectionFactory.CreateConnection();
        var context = await cn.QuerySingleOrDefaultAsync<CentralContextRow>(new CommandDefinition(
            @"select t.nome as Tenant, e.ano::text as Exercicio
              from sigov.tenant t
              left join sigov.exercicio e on e.id=@ExercicioId and e.entidade_id=@EntidadeId and not e.is_deleted
              where t.id=@TenantId and t.ativo and not t.is_deleted",
            new { TenantId = tenantId, authorization.EntidadeId, authorization.ExercicioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (context is null || string.IsNullOrWhiteSpace(context.Tenant))
        {
            throw new UnauthorizedAccessException("O contexto institucional não está ativo ou não pertence à sessão.");
        }

        if (authorization.ExercicioId.HasValue && string.IsNullOrWhiteSpace(context.Exercicio))
        {
            throw new UnauthorizedAccessException("O exercício selecionado não pertence ao contexto institucional ativo.");
        }
        var pendencias = await ObterResumoPendenciasAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var totalAlertas = await ObterTotalAlertasAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return new MinhaCentralViewModel
        {
            Perfil = Perfil(user),
            Tenant = context.Tenant,
            Exercicio = context.Exercicio ?? "Não selecionado",
            TotalPendencias = pendencias.Total,
            TotalVencidas = pendencias.Vencidas,
            TotalAlertas = totalAlertas,
            AtualizadoEm = pendencias.AtualizadoEm,
            Acoes = await ObterAcoesRecomendadasAsync(user, cancellationToken).ConfigureAwait(false),
            Modulos = await ObterModulosUsuarioAsync(user, cancellationToken).ConfigureAwait(false),
            Pendencias = pendencias.Itens,
            Ambiente = _saasService.CriarAmbiente(true)
        };
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
        var tenantId = RequiredPositiveClaim(user, "tenant_id");
        var userId = RequiredPositiveClaim(user, ClaimTypes.NameIdentifier, "sub", "usuario_id");
        return (await ObterResumoPendenciasAsync(tenantId, userId, cancellationToken).ConfigureAwait(false)).Itens;
    }

    private async Task<PendenciasResumo> ObterResumoPendenciasAsync(long tenantId, long userId, CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "pendencia_operacional", cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A estrutura obrigatória sigov.pendencia_operacional não está disponível.");
        }

        using var cn = _connectionFactory.CreateConnection();
        const string sql = @"select count(*) as Total,
                                    count(*) filter (where prazo < now()) as Vencidas,
                                    now() as AtualizadoEm
                             from sigov.pendencia_operacional
                             where tenant_id=@TenantId and responsavel_usuario_id=@UserId
                               and status in ('ABERTA','EM_TRATAMENTO');
                             select titulo as Titulo, coalesce(descricao,'') as Descricao,
                                    rota_acao as Url, prazo as Prazo
                             from sigov.pendencia_operacional
                             where tenant_id=@TenantId and responsavel_usuario_id=@UserId
                               and status in ('ABERTA','EM_TRATAMENTO')
                             order by prazo nulls last, created_at desc limit 8;";
        using var results = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var totals = await results.ReadSingleAsync<PendenciasTotals>().ConfigureAwait(false);
        var items = (await results.ReadAsync<PendenciaViewModel>().ConfigureAwait(false)).ToArray();
        return new PendenciasResumo(totals.Total, totals.Vencidas, totals.AtualizadoEm, items);
    }
    public async Task<IReadOnlyList<AlertaLgpdViewModel>> ObterAlertasLgpdAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tenantId = TryGetLong(user.FindFirst("tenant_id")?.Value);
        if (!tenantId.HasValue || !await _schemaInspector.TableExistsAsync("sigov", "alerta_operacional", cancellationToken).ConfigureAwait(false)) return Array.Empty<AlertaLgpdViewModel>();
        using var cn = _connectionFactory.CreateConnection();
        const string sql = "select titulo as Titulo, coalesce(descricao,'') as Descricao from sigov.alerta_operacional where tenant_id=@TenantId and status in ('ATIVO','ABERTO') and tipo in ('LGPD','SEGURANCA','TECNICO','RISCO') order by created_at desc limit 8";
        return (await cn.QueryAsync<AlertaLgpdViewModel>(new CommandDefinition(sql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
    }

    private async Task<long> ObterTotalAlertasAsync(long tenantId, CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "alerta_operacional", cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A estrutura obrigatória sigov.alerta_operacional não está disponível.");
        }
        using var cn = _connectionFactory.CreateConnection();
        const string sql = "select count(*) from sigov.alerta_operacional where tenant_id=@TenantId and status='ATIVO' and tipo in ('LGPD','SEGURANCA','TECNICO','RISCO')";
        return await cn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
    private static long RequiredPositiveClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            if (long.TryParse(user.FindFirst(claimType)?.Value, out var value) && value > 0) return value;
        }

        throw new UnauthorizedAccessException($"Contexto obrigatório ausente: {string.Join("/", claimTypes)}.");
    }
    private static string Perfil(ClaimsPrincipal user) => user.Claims.FirstOrDefault(x => x.Type is ClaimTypes.Role or "role")?.Value ?? "Operador";
    private static AcaoRecomendadaViewModel[] AcoesPerfil(ClaimsPrincipal user)
    {
        if (user.IsInRole("ADMINISTRADOR_GERAL") || user.IsInRole("SUPER_ADMIN") || user.IsInRole("SUPERADMIN") || user.IsInRole("ADMIN_GERAL")) return new[] { A("Status funcional", "Comprove estruturas e módulos.", "/Modulos/StatusFuncional"), A("Alertas críticos", "Acompanhe riscos técnicos e funcionais.", "/Alertas"), A("Matriz de acesso", "Revise concessões e negativas.", "/Seguranca/MatrizAcesso") };
        if (user.IsInRole("ADMIN_TENANT")) return new[] { A("Módulos contratados", "Revise o catálogo do tenant.", "/Saas/Modulos"), A("Pendências", "Resolva pendências do tenant.", "/Pendencias"), A("Auditoria e LGPD", "Acompanhe trilhas autorizadas.", "/Auditoria/Trilhas") };
        if (user.IsInRole("PROFESSOR")) return new[] { A("Minhas turmas", "Consulte somente turmas vinculadas.", "/Educacao/Turmas"), A("Frequência", "Registre a frequência pendente.", "/Educacao/Frequencias") };
        if (user.IsInRole("ACS")) return new[] { A("Visitas pendentes", "Consulte sua microárea.", "/Acs/Visitas"), A("Domicílios", "Consulte somente sua área autorizada.", "/Acs/Domicilios") };
        if (user.IsInRole("FUNCIONARIO_FINANCEIRO")) return new[] { A("Pagamentos", "Trate pagamentos permitidos.", "/Financeiro/Pagamentos"), A("Pendências financeiras", "Consulte baixas, DAMs e faturas.", "/Pendencias") };
        if (user.IsInRole("AUDITOR")) return new[] { A("Trilhas", "Consulte eventos sem alterar operação.", "/Auditoria/Trilhas"), A("Alertas", "Veja negativas, exportações e LGPD.", "/Alertas") };
        if (user.IsInRole("ATENDIMENTO")) return new[] { A("Protocolos", "Acompanhe os atendimentos autorizados.", "/AtendimentoDigital/Chamados"), A("Ouvidoria", "Consulte manifestações do tenant.", "/AtendimentoDigital/Ouvidoria"), A("e-SIC", "Acompanhe solicitações de informação.", "/AtendimentoDigital/ESic") };
        if (user.IsInRole("ALMOXARIFADO")) return new[] { A("Estoque", "Consulte saldos e estoque crítico.", "/Almoxarifado"), A("Pendências", "Trate requisições autorizadas.", "/Pendencias") };
        return new[] { A("Meu acesso", "Consulte os módulos e ações liberados para seu perfil.", "/Modulos/MeuAcesso") };
    }
    private static AcaoRecomendadaViewModel A(string title, string description, string url) => new(title, description, url, "info");
    private sealed record CentralContextRow(string Tenant, string? Exercicio);
    private sealed record PendenciasTotals(long Total, long Vencidas, DateTimeOffset AtualizadoEm);
    private sealed record PendenciasResumo(long Total, long Vencidas, DateTimeOffset AtualizadoEm, IReadOnlyList<PendenciaViewModel> Itens);
}
