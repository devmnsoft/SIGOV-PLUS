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
order by case gravidade when 'CRITICA' then 1 when 'ALTA' then 2 when 'MEDIA' then 3 when 'BAIXA' then 4 else 5 end, prazo nulls last, id
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
rota_correcao as RotaCorrecao, status, detected_at as DetectedAt,responsavel_usuario_id as ResponsavelUsuarioId,
versao,ultimo_resultado as UltimoResultado,verificado_em as VerificadoEm from sigov.qualidade_dados_ocorrencia
where tenant_id=@TenantId and status in ('ABERTA','EM_CORRECAO') and (@Modulo is null or modulo=@Modulo)
and (@Severidade is null or severidade=@Severidade) order by detected_at desc, id limit @Limit offset @Offset";
        return await QuerySafe<QualidadeDadosDto>(sql, new { TenantId = Tenant(), Modulo = Normalize(modulo), Severidade = Normalize(severidade), Limit = Size(tamanho), Offset = Offset(pagina, tamanho) }, ct).ConfigureAwait(false);
    }

    public async Task<GovernancaOcorrenciaDto?> ObterOcorrenciaAsync(string tipo, long id, CancellationToken ct)
    {
        if (tipo is not ("qualidade" or "pendencia")) throw new ArgumentException("Tipo de ocorrência inválido.", nameof(tipo));
        Demand(tipo == "qualidade" ? "governanca.qualidade.visualizar" : "governanca.pendencias.visualizar");
        var tenantId = Tenant();
        using var connection = _connections.CreateConnection();
        var quality = tipo.Equals("qualidade", StringComparison.OrdinalIgnoreCase);
        var sql = quality
            ? @"select q.id,'qualidade' as Tipo,q.modulo,q.descricao as Titulo,q.descricao,q.regra as RegraOuMotivo,q.entidade,q.entidade_id as EntidadeId,q.severidade as Classificacao,q.status,q.rota_correcao as RotaOrigem,q.responsavel_usuario_id as ResponsavelUsuarioId,coalesce(p.nome_social,p.nome,u.login) as ResponsavelNome,null::timestamptz as Prazo,q.detected_at as DetectadaEm,q.verificado_em as VerificadaEm,q.ultimo_resultado as UltimoResultado,q.versao from sigov.qualidade_dados_ocorrencia q left join sigov.usuario u on u.id=q.responsavel_usuario_id left join sigov.pessoa p on p.id=u.pessoa_id where q.tenant_id=@TenantId and q.id=@Id"
            : @"select x.id,'pendencia' as Tipo,x.modulo,x.titulo,x.coalesce_descricao as Descricao,x.tipo as RegraOuMotivo,x.entidade,x.entidade_id as EntidadeId,x.gravidade as Classificacao,x.status,x.rota_acao as RotaOrigem,x.responsavel_usuario_id as ResponsavelUsuarioId,coalesce(p.nome_social,p.nome,u.login) as ResponsavelNome,x.prazo,x.created_at as DetectadaEm,null::timestamptz as VerificadaEm,null::varchar as UltimoResultado,x.versao from (select p.*,coalesce(p.descricao,p.titulo) coalesce_descricao from sigov.pendencia_operacional p) x left join sigov.usuario u on u.id=x.responsavel_usuario_id left join sigov.pessoa p on p.id=u.pessoa_id where x.tenant_id=@TenantId and x.id=@Id";
        var item = await connection.QuerySingleOrDefaultAsync<OccurrenceRow>(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: ct)).ConfigureAwait(false);
        if (item is null) return null;
        var history = (await connection.QueryAsync<GovernancaHistoricoDto>(new CommandDefinition(@"select id,evento,usuario_id as UsuarioId,justificativa,ocorrido_em as OcorridoEm from sigov.governanca_ocorrencia_historico where tenant_id=@TenantId and ocorrencia_tipo=@Tipo and ocorrencia_id=@Id order by ocorrido_em desc,id desc", new { TenantId = tenantId, Tipo = quality ? "QUALIDADE" : "PENDENCIA", Id = id }, cancellationToken: ct)).ConfigureAwait(false)).AsList();
        return new(item.Id, item.Tipo, item.Modulo, item.Titulo, item.Descricao, item.RegraOuMotivo, item.Entidade, item.EntidadeId, item.Classificacao, item.Status, SafeRoute(item.RotaOrigem), item.ResponsavelUsuarioId, item.ResponsavelNome, item.Prazo, item.DetectadaEm, item.VerificadaEm, item.UltimoResultado, item.Versao, history);
    }

    public async Task<GovernancaComandoResultado> AtribuirAsync(string tipo, long id, long responsavelUsuarioId, long versao, string justificativa, CancellationToken ct)
    {
        Demand("governanca.ocorrencias.atribuir");
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Justificativa obrigatória.", nameof(justificativa));
        var tenantId = Tenant();
        var table = tipo.Equals("qualidade", StringComparison.OrdinalIgnoreCase) ? "qualidade_dados_ocorrencia" : tipo.Equals("pendencia", StringComparison.OrdinalIgnoreCase) ? "pendencia_operacional" : throw new ArgumentException("Tipo de ocorrência inválido.", nameof(tipo));
        await using var connection = _connections.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var eligible = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"select exists(select 1 from sigov.usuario u where u.id=@Responsavel and u.tenant_id=@TenantId and u.ativo and not u.bloqueado and not u.is_deleted)", new { Responsavel = responsavelUsuarioId, TenantId = tenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!eligible) return new(false, "RESPONSAVEL_INELEGIVEL", "Responsável inativo ou fora do contexto selecionado.", versao);
        var sql = $@"with anterior as (select responsavel_usuario_id from sigov.{table} where tenant_id=@TenantId and id=@Id and versao=@Versao for update), atualizado as (update sigov.{table} x set responsavel_usuario_id=@Responsavel,status=case when status='ABERTA' then '{(table == "pendencia_operacional" ? "EM_TRATAMENTO" : "EM_CORRECAO")}' else status end,versao=versao+1,updated_at=now() from anterior where x.tenant_id=@TenantId and x.id=@Id and x.versao=@Versao returning x.versao,anterior.responsavel_usuario_id), historico as (insert into sigov.governanca_ocorrencia_historico(tenant_id,ocorrencia_tipo,ocorrencia_id,evento,usuario_id,justificativa,dados_antes,dados_depois) select @TenantId,@Tipo,@Id,case when responsavel_usuario_id is null then 'ATRIBUIDA' else 'REDISTRIBUIDA' end,@Usuario,@Justificativa,jsonb_build_object('responsavel_usuario_id',responsavel_usuario_id),jsonb_build_object('responsavel_usuario_id',@Responsavel) from atualizado returning id) select versao from atualizado where exists(select 1 from historico)";
        var next = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, Versao = versao, Responsavel = responsavelUsuarioId, Tipo = table == "pendencia_operacional" ? "PENDENCIA" : "QUALIDADE", Usuario = _user.UsuarioId, Justificativa = justificativa.Trim() }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!next.HasValue) { await tx.RollbackAsync(ct).ConfigureAwait(false); return new(false, "CONFLITO", "A ocorrência foi alterada; atualize a tela.", versao); }
        await tx.CommitAsync(ct).ConfigureAwait(false); return new(true, "ATRIBUIDA", "Responsabilidade atualizada com histórico; a atribuição não concede acesso ao registro de origem.", next.Value);
    }

    public async Task<GovernancaComandoResultado> RevalidarQualidadeAsync(long id, long versao, CancellationToken ct)
    {
        Demand("governanca.qualidade.revalidar"); var tenantId = Tenant();
        await using var connection = _connections.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<RevalidationRow>(new CommandDefinition(@"select versao,condicao_presente,origem_verificada_em,verificado_em from sigov.qualidade_dados_ocorrencia where tenant_id=@TenantId and id=@Id for update", new { TenantId = tenantId, Id = id }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (row is null) return new(false, "NAO_ENCONTRADA", "Ocorrência não encontrada no contexto selecionado.", versao);
        if (row.Versao != versao) return new(false, "CONFLITO", "Existe resultado mais recente; atualize a tela.", row.Versao);
        var current = row.OrigemVerificadaEm.HasValue && (!row.VerificadoEm.HasValue || row.OrigemVerificadaEm > row.VerificadoEm);
        var result = !current ? "VERIFICACAO_BLOQUEADA" : row.CondicaoPresente == true ? "CONDICAO_PRESENTE" : row.CondicaoPresente == false ? "CONDICAO_CORRIGIDA" : "REGISTRO_NAO_DISPONIVEL";
        var status = result == "CONDICAO_CORRIGIDA" ? "RESOLVIDA" : "EM_CORRECAO";
        var next = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"update sigov.qualidade_dados_ocorrencia set ultimo_resultado=@Resultado,status=@Status,verificado_em=now(),resolved_at=case when @Status='RESOLVIDA' then now() else null end,versao=versao+1,updated_at=now() where tenant_id=@TenantId and id=@Id returning versao", new { Resultado = result, Status = status, TenantId = tenantId, Id = id }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.governanca_ocorrencia_historico(tenant_id,ocorrencia_tipo,ocorrencia_id,evento,usuario_id,dados_depois) values(@TenantId,'QUALIDADE',@Id,'REVALIDADA',@Usuario,jsonb_build_object('resultado',@Resultado,'status',@Status))", new { TenantId = tenantId, Id = id, Usuario = _user.UsuarioId, Resultado = result, Status = status }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false); return new(true, result, result == "CONDICAO_CORRIGIDA" ? "A fonte confirmou que a condição foi corrigida." : "Revalidação registrada sem presumir correção.", next);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar a central transversal. TenantId={TenantId}", _tenant.TenantId);
            throw;
        }
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
    private static string? SafeRoute(string? route) => !string.IsNullOrWhiteSpace(route) && route.StartsWith('/') && !route.StartsWith("//", StringComparison.Ordinal) && Uri.TryCreate(route, UriKind.Relative, out _) ? route : null;
    private sealed record OccurrenceRow(long Id,string Tipo,string Modulo,string Titulo,string Descricao,string RegraOuMotivo,string Entidade,string EntidadeId,string Classificacao,string Status,string? RotaOrigem,long? ResponsavelUsuarioId,string? ResponsavelNome,DateTimeOffset? Prazo,DateTimeOffset DetectadaEm,DateTimeOffset? VerificadaEm,string? UltimoResultado,long Versao);
    private sealed record RevalidationRow(long Versao,bool? CondicaoPresente,DateTimeOffset? OrigemVerificadaEm,DateTimeOffset? VerificadoEm);
}
