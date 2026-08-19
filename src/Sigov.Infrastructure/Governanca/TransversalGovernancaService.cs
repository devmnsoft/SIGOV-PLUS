using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Governanca;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Governanca;

public sealed class TransversalGovernancaService : ITransversalGovernancaService
{
    private readonly NpgsqlConnectionFactory _connections;
    private readonly IDatabaseObjectInspector _inspector;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly ILogger<TransversalGovernancaService> _logger;

    public TransversalGovernancaService(NpgsqlConnectionFactory connections, IDatabaseObjectInspector inspector,
        ICurrentTenant tenant, ICurrentUser user, IAuditService audit, ILogger<TransversalGovernancaService> logger)
    {
        _connections = connections; _inspector = inspector; _tenant = tenant; _user = user; _audit = audit; _logger = logger;
    }

    public async Task<IReadOnlyCollection<PendenciaOperacionalDto>> ListarPendenciasAsync(string? modulo, string? gravidade, int pagina, int tamanho, CancellationToken ct)
    {
        Demand("governanca.pendencias.visualizar");
        if (!await Exists("pendencia_operacional", ct).ConfigureAwait(false)) return Array.Empty<PendenciaOperacionalDto>();
        const string sql = @"select id, modulo, recurso, tipo, entidade, entidade_id as EntidadeId, gravidade, titulo,
descricao, prazo, responsavel_usuario_id as ResponsavelUsuarioId, rota_acao as RotaAcao, status, created_at as CreatedAt
from sigov.pendencia_operacional where tenant_id=@TenantId and status in ('ABERTA','EM_TRATAMENTO')
and (@Modulo is null or modulo=@Modulo) and (@Gravidade is null or gravidade=@Gravidade)
order by case gravidade when 'CRITICA' then 1 when 'ALTA' then 2 when 'MEDIA' then 3 when 'BAIXA' then 4 else 5 end, prazo nulls last
limit @Limit offset @Offset";
        return await QuerySafe<PendenciaOperacionalDto>(sql, new { TenantId = Tenant(), Modulo = Normalize(modulo), Gravidade = Normalize(gravidade), Limit = Size(tamanho), Offset = Offset(pagina, tamanho) }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<AlertaOperacionalDto>> ListarAlertasAsync(string? tipo, string? severidade, int pagina, int tamanho, CancellationToken ct)
    {
        Demand("governanca.alertas.visualizar");
        if (!await Exists("alerta_operacional", ct).ConfigureAwait(false)) return Array.Empty<AlertaOperacionalDto>();
        const string sql = @"select id, modulo, tipo, severidade, titulo, descricao, rota_acao as RotaAcao, status, created_at as CreatedAt
from sigov.alerta_operacional where tenant_id=@TenantId and status in ('ATIVO','ABERTO')
and (@Tipo is null or tipo=@Tipo) and (@Severidade is null or severidade=@Severidade)
order by case severidade when 'CRITICA' then 1 when 'ALTA' then 2 when 'MEDIA' then 3 when 'BAIXA' then 4 else 5 end, created_at desc
limit @Limit offset @Offset";
        return await QuerySafe<AlertaOperacionalDto>(sql, new { TenantId = Tenant(), Tipo = Normalize(tipo), Severidade = Normalize(severidade), Limit = Size(tamanho), Offset = Offset(pagina, tamanho) }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<QualidadeDadosDto>> ListarQualidadeAsync(string? modulo, string? severidade, int pagina, int tamanho, CancellationToken ct)
    {
        Demand("governanca.qualidade.visualizar");
        if (!await Exists("qualidade_dados_ocorrencia", ct).ConfigureAwait(false)) return Array.Empty<QualidadeDadosDto>();
        const string sql = @"select id, modulo, regra, entidade, entidade_id as EntidadeId, severidade, descricao,
rota_correcao as RotaCorrecao, status, detected_at as DetectedAt from sigov.qualidade_dados_ocorrencia
where tenant_id=@TenantId and status in ('ABERTA','EM_CORRECAO') and (@Modulo is null or modulo=@Modulo)
and (@Severidade is null or severidade=@Severidade) order by detected_at desc limit @Limit offset @Offset";
        return await QuerySafe<QualidadeDadosDto>(sql, new { TenantId = Tenant(), Modulo = Normalize(modulo), Severidade = Normalize(severidade), Limit = Size(tamanho), Offset = Offset(pagina, tamanho) }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<IntegracaoInternaDto>> ListarIntegracoesAsync(CancellationToken ct)
    {
        Demand("governanca.integracoes.visualizar");
        if (!await Exists("integracao_interna_evento", ct).ConfigureAwait(false)) return Array.Empty<IntegracaoInternaDto>();
        const string sql = @"select origem, destino, (array_agg(status order by created_at desc))[1] as Status,
max(created_at) as UltimoEvento, count(*) filter (where status='PENDENTE_CONFIGURACAO') as QuantidadePendente,
count(*) filter (where status='COM_ERRO') as QuantidadeErro,
(array_agg(rota_correcao order by created_at desc))[1] as RotaCorrecao,
bool_or(preparatoria) as Preparatoria from sigov.integracao_interna_evento where tenant_id=@TenantId
group by origem, destino order by origem, destino";
        return await QuerySafe<IntegracaoInternaDto>(sql, new { TenantId = Tenant() }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ModuloStatusFuncionalDto>> ListarStatusFuncionalAsync(CancellationToken ct)
    {
        Demand("governanca.status_funcional.visualizar");
        var definitions = new[] { ("TRIBUTARIO", "tributario_lancamento"), ("FINANCEIRO", "pagamento"), ("SANEAMENTO", "saneamento_ligacao"),
            ("EDUCACAO", "educacao_aluno"), ("SAUDE", "saude_paciente"), ("PROCESSOS", "processo"), ("GED", "ged_documento"),
            ("RH", "servidor"), ("CONTRATOS", "contrato"), ("ALMOXARIFADO", "almoxarifado_material"), ("PATRIMONIO", "patrimonio_bem"),
            ("FROTAS", "frota_veiculo"), ("OBRAS", "obra") };
        var result = new List<ModuloStatusFuncionalDto>();
        foreach (var definition in definitions)
        {
            var table = await Exists(definition.Item2, ct).ConfigureAwait(false);
            var status = table ? "FUNCIONAL_COM_PENDENCIAS" : "ESTRUTURA_PENDENTE";
            result.Add(new ModuloStatusFuncionalDto(definition.Item1, table, table, table, table, false, false, true, true,
                definition.Item1 is "SAUDE" or "EDUCACAO" or "RH" or "GED", false, false, false,
                table ? $"Tabela sigov.{definition.Item2} comprovada" : $"Tabela sigov.{definition.Item2} não comprovada", status));
        }
        return result;
    }

    public async Task<bool> ResolverAlertaAsync(long id, string justificativa, CancellationToken ct)
    {
        Demand("governanca.alertas.resolver");
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Justificativa obrigatória.", nameof(justificativa));
        if (!await Exists("alerta_operacional", ct).ConfigureAwait(false)) return false;
        const string sql = @"update sigov.alerta_operacional set status='RESOLVIDO', resolved_at=now(), resolved_by=@UserId,
justificativa=@Justificativa where id=@Id and tenant_id=@TenantId and status in ('ATIVO','ABERTO','SILENCIADO')";
        using var connection = _connections.CreateConnection();
        var changed = await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, TenantId = Tenant(), UserId = _user.UsuarioId, Justificativa = justificativa.Trim() }, cancellationToken: ct)).ConfigureAwait(false) == 1;
        if (changed) await _audit.RegistrarAsync("governanca", "ALERTA_RESOLVIDO", "sigov.alerta_operacional", id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { justificativa = justificativa.Trim() }, ct).ConfigureAwait(false);
        return changed;
    }

    private async Task<IReadOnlyCollection<T>> QuerySafe<T>(string sql, object parameters, CancellationToken ct)
    {
        try { using var connection = _connections.CreateConnection(); return (await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false)).ToArray(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Central transversal indisponível para o tenant {TenantId}.", _tenant.TenantId); return Array.Empty<T>(); }
    }
    private Task<bool> Exists(string table, CancellationToken ct) => _inspector.TableExistsAsync("sigov", table, ct);
    private long Tenant() => _tenant.TenantId is > 0 ? _tenant.TenantId.Value : throw new UnauthorizedAccessException("Tenant obrigatório.");
    private void Demand(string permission)
    {
        if (!_user.IsAuthenticated) throw new UnauthorizedAccessException("Autenticação obrigatória.");
        if (_user.Roles.Any(IsSuper) || _user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return;
        throw new UnauthorizedAccessException("Permissão insuficiente.");
    }
    private static bool IsSuper(string role) => role is "SUPER_ADMIN" or "SIGOV_ADMIN" or "ADMINISTRADOR_GERAL";
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static int Size(int value) => Math.Clamp(value, 1, 100);
    private static int Offset(int page, int size) => (Math.Max(page, 1) - 1) * Size(size);
}
