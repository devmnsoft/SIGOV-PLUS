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

    public async Task<PendenciasPaginaDto> ListarPendenciasAsync(PendenciaOperacionalFiltro filtro, CancellationToken ct)
    {
        Demand("governanca.pendencias.visualizar");
        await RequireTable("pendencia_operacional", ct).ConfigureAwait(false);
        var pagina = Math.Max(1, filtro.Pagina); var tamanho = Math.Clamp(filtro.Tamanho, 1, 100);
        var visao = Normalize(filtro.Visao) ?? "MINHAS";
        if (visao is not ("MINHAS" or "NAO_ATRIBUIDAS" or "TODAS" or "ENCERRADAS")) throw new ArgumentException("Visão de trabalho inválida.", nameof(filtro));
        var ordem = Normalize(filtro.Ordenacao) is "ABERTURA_DESC" ? "ABERTURA_DESC" : "PRIORIDADE";
        const string sql = @"with autorizadas as (
select po.id,po.modulo,po.recurso,po.tipo,po.entidade,po.entidade_id,po.gravidade,po.titulo,po.descricao,po.prazo,
po.responsavel_usuario_id,po.rota_acao,po.status,po.created_at,po.resolved_at,coalesce(pe.nome_social,pe.nome,u.login) responsavel_nome
from sigov.pendencia_operacional po
left join sigov.usuario u on u.id=po.responsavel_usuario_id and u.tenant_id=po.tenant_id
left join sigov.pessoa pe on pe.id=u.pessoa_id
where po.tenant_id=@TenantId
and (case @Visao when 'MINHAS' then po.status in ('ABERTA','EM_TRATAMENTO') and po.responsavel_usuario_id=@UsuarioId
when 'NAO_ATRIBUIDAS' then po.status in ('ABERTA','EM_TRATAMENTO') and po.responsavel_usuario_id is null
when 'ENCERRADAS' then po.status in ('RESOLVIDA','CANCELADA') else true end)
and (@Modulo is null or po.modulo=@Modulo) and (@Gravidade is null or po.gravidade=@Gravidade)
and (@Situacao is null or po.status=@Situacao) and (@Responsavel is null or po.responsavel_usuario_id=@Responsavel)
and (@Prazo is null or (@Prazo='VENCIDAS' and po.prazo is not null and po.prazo<now() and po.status in ('ABERTA','EM_TRATAMENTO'))
 or (@Prazo='FUTURO' and po.prazo>=now()) or (@Prazo='SEM_PRAZO' and po.prazo is null))
and (@AberturaDe is null or po.created_at>=@AberturaDe) and (@AberturaAte is null or po.created_at<@AberturaAte + interval '1 day')
and (@EncerramentoDe is null or po.resolved_at>=@EncerramentoDe) and (@EncerramentoAte is null or po.resolved_at<@EncerramentoAte + interval '1 day')
), pagina as (select * from autorizadas order by
case when @Ordem='PRIORIDADE' then case gravidade when 'CRITICA' then 1 when 'ALTA' then 2 when 'MEDIA' then 3 when 'BAIXA' then 4 else 5 end end,
case when @Ordem='PRIORIDADE' then prazo end nulls last,created_at desc,id desc limit @Limit offset @Offset)
select id,modulo,recurso,tipo,entidade,entidade_id as EntidadeId,gravidade,titulo,descricao,prazo,
responsavel_usuario_id as ResponsavelUsuarioId,rota_acao as RotaAcao,status,created_at as CreatedAt,responsavel_nome as ResponsavelNome from pagina;
with autorizadas as (
select po.id,po.modulo,po.recurso,po.tipo,po.entidade,po.entidade_id,po.gravidade,po.titulo,po.descricao,po.prazo,
po.responsavel_usuario_id,po.rota_acao,po.status,po.created_at,po.resolved_at,coalesce(pe.nome_social,pe.nome,u.login) responsavel_nome
from sigov.pendencia_operacional po
left join sigov.usuario u on u.id=po.responsavel_usuario_id and u.tenant_id=po.tenant_id
left join sigov.pessoa pe on pe.id=u.pessoa_id
where po.tenant_id=@TenantId
and (case @Visao when 'MINHAS' then po.status in ('ABERTA','EM_TRATAMENTO') and po.responsavel_usuario_id=@UsuarioId
when 'NAO_ATRIBUIDAS' then po.status in ('ABERTA','EM_TRATAMENTO') and po.responsavel_usuario_id is null
when 'ENCERRADAS' then po.status in ('RESOLVIDA','CANCELADA') else true end)
and (@Modulo is null or po.modulo=@Modulo) and (@Gravidade is null or po.gravidade=@Gravidade)
and (@Situacao is null or po.status=@Situacao) and (@Responsavel is null or po.responsavel_usuario_id=@Responsavel)
and (@Prazo is null or (@Prazo='VENCIDAS' and po.prazo is not null and po.prazo<now() and po.status in ('ABERTA','EM_TRATAMENTO'))
 or (@Prazo='FUTURO' and po.prazo>=now()) or (@Prazo='SEM_PRAZO' and po.prazo is null))
and (@AberturaDe is null or po.created_at>=@AberturaDe) and (@AberturaAte is null or po.created_at<@AberturaAte + interval '1 day')
and (@EncerramentoDe is null or po.resolved_at>=@EncerramentoDe) and (@EncerramentoAte is null or po.resolved_at<@EncerramentoAte + interval '1 day')
)
select count(*) TotalFiltrado,count(*) filter(where prazo is not null and prazo<now() and status in ('ABERTA','EM_TRATAMENTO')) Vencidas,
count(*) filter(where prazo>=now() and status in ('ABERTA','EM_TRATAMENTO')) PrazoFuturo,count(*) filter(where prazo is null) SemPrazo,
count(*) filter(where responsavel_usuario_id is null) NaoAtribuidas,count(*) filter(where status in ('RESOLVIDA','CANCELADA')) Encerradas from autorizadas";
        var args = new { TenantId = Tenant(), UsuarioId = _user.UsuarioId, Visao = visao, Modulo = Normalize(filtro.Modulo),
            Gravidade = Normalize(filtro.Classificacao), Situacao = Normalize(filtro.Situacao), filtro.ResponsavelUsuarioId,
            Responsavel = filtro.ResponsavelUsuarioId, Prazo = Normalize(filtro.Prazo), filtro.AberturaDe, filtro.AberturaAte,
            filtro.EncerramentoDe, filtro.EncerramentoAte, Ordem = ordem, Limit = tamanho, Offset = Offset(pagina, tamanho) };
        using var connection = _connections.CreateConnection(); using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, args, cancellationToken: ct)).ConfigureAwait(false);
        var itens = (await multi.ReadAsync<PendenciaOperacionalDto>().ConfigureAwait(false)).AsList();
        var indicadores = await multi.ReadSingleAsync<PendenciaIndicadoresDto>().ConfigureAwait(false);
        return new(itens, pagina, tamanho, indicadores.TotalFiltrado, pagina * (long)tamanho < indicadores.TotalFiltrado, ordem, indicadores);
    }

    public async Task<IReadOnlyCollection<AlertaOperacionalDto>> ListarAlertasAsync(string? tipo, string? severidade, int pagina, int tamanho, CancellationToken ct)
    {
        Demand("governanca.alertas.visualizar");
        await RequireTable("alerta_operacional", ct).ConfigureAwait(false);
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
        await RequireTable("qualidade_dados_ocorrencia", ct).ConfigureAwait(false);
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
        var history = (await connection.QueryAsync<GovernancaHistoricoDto>(new CommandDefinition(@"select h.id,h.evento,h.usuario_id as UsuarioId,h.justificativa,h.ocorrido_em as OcorridoEm,coalesce(p.nome_social,p.nome,u.login) as UsuarioNome from sigov.governanca_ocorrencia_historico h left join sigov.usuario u on u.id=h.usuario_id and u.tenant_id=h.tenant_id left join sigov.pessoa p on p.id=u.pessoa_id where h.tenant_id=@TenantId and h.ocorrencia_tipo=@Tipo and h.ocorrencia_id=@Id order by h.ocorrido_em desc,h.id desc", new { TenantId = tenantId, Tipo = quality ? "QUALIDADE" : "PENDENCIA", Id = id }, cancellationToken: ct)).ConfigureAwait(false)).AsList();
        return new(item.Id, item.Tipo, item.Modulo, item.Titulo, item.Descricao, item.RegraOuMotivo, item.Entidade, item.EntidadeId, item.Classificacao, item.Status, SafeRoute(item.RotaOrigem), item.ResponsavelUsuarioId, item.ResponsavelNome, item.Prazo, item.DetectadaEm, item.VerificadaEm, item.UltimoResultado, item.Versao, history);
    }

    public async Task<GovernancaComandoResultado> AtribuirAsync(string tipo, long id, long responsavelUsuarioId, long versao, string justificativa, CancellationToken ct)
    {
        Demand("governanca.ocorrencias.atribuir");
        if (string.IsNullOrWhiteSpace(justificativa) || justificativa.Trim().Length > 1000) throw new ArgumentException("A justificativa é obrigatória e deve ter no máximo 1000 caracteres.", nameof(justificativa));
        var tenantId = Tenant();
        var table = tipo.Equals("qualidade", StringComparison.OrdinalIgnoreCase) ? "qualidade_dados_ocorrencia" : tipo.Equals("pendencia", StringComparison.OrdinalIgnoreCase) ? "pendencia_operacional" : throw new ArgumentException("Tipo de ocorrência inválido.", nameof(tipo));
        await using var connection = _connections.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var eligible = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"select exists(select 1 from sigov.usuario u where u.id=@Responsavel and u.tenant_id=@TenantId and u.ativo and not u.bloqueado and not u.is_deleted and (@EntidadeId is null or exists(select 1 from sigov.usuario_entidade ue where ue.usuario_id=u.id and ue.entidade_id=@EntidadeId and ue.ativo)) and (@ExercicioId is null or exists(select 1 from sigov.usuario_exercicio ux where ux.usuario_id=u.id and ux.exercicio_id=@ExercicioId and ux.ativo)))", new { Responsavel = responsavelUsuarioId, TenantId = tenantId, _tenant.EntidadeId, _tenant.ExercicioId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!eligible) return new(false, "RESPONSAVEL_INELEGIVEL", "Responsável inativo ou fora do contexto selecionado.", versao);
        var sql = $@"with anterior as (select responsavel_usuario_id from sigov.{table} where tenant_id=@TenantId and id=@Id and versao=@Versao and status in ('ABERTA','{(table == "pendencia_operacional" ? "EM_TRATAMENTO" : "EM_CORRECAO")}') for update), atualizado as (update sigov.{table} x set responsavel_usuario_id=@Responsavel,status=case when status='ABERTA' then '{(table == "pendencia_operacional" ? "EM_TRATAMENTO" : "EM_CORRECAO")}' else status end,versao=versao+1,updated_at=now() from anterior where x.tenant_id=@TenantId and x.id=@Id and x.versao=@Versao returning x.versao,anterior.responsavel_usuario_id), historico as (insert into sigov.governanca_ocorrencia_historico(tenant_id,ocorrencia_tipo,ocorrencia_id,evento,usuario_id,justificativa,dados_antes,dados_depois) select @TenantId,@Tipo,@Id,case when responsavel_usuario_id is null then 'ATRIBUIDA' else 'REDISTRIBUIDA' end,@Usuario,@Justificativa,jsonb_build_object('responsavel_usuario_id',responsavel_usuario_id),jsonb_build_object('responsavel_usuario_id',@Responsavel) from atualizado returning id) select versao from atualizado where exists(select 1 from historico)";
        var next = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, Versao = versao, Responsavel = responsavelUsuarioId, Tipo = table == "pendencia_operacional" ? "PENDENCIA" : "QUALIDADE", Usuario = _user.UsuarioId, Justificativa = justificativa.Trim() }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!next.HasValue) { await tx.RollbackAsync(ct).ConfigureAwait(false); return new(false, "CONFLITO", "A ocorrência foi alterada; atualize a tela.", versao); }
        await tx.CommitAsync(ct).ConfigureAwait(false); return new(true, "ATRIBUIDA", "Responsabilidade atualizada com histórico; a atribuição não concede acesso ao registro de origem.", next.Value);
    }

    public async Task<ResponsaveisElegiveisPaginaDto> BuscarResponsaveisAsync(string? busca, int pagina, int tamanhoLogico, CancellationToken ct)
    {
        Demand("governanca.ocorrencias.atribuir");
        pagina = Math.Max(1, pagina); tamanhoLogico = Math.Clamp(tamanhoLogico, 1, 50);
        var texto = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim()[..Math.Min(busca.Trim().Length, 120)];
        var term = texto is null ? null : $"%{texto}%";
        const string sql = @"select u.id as UsuarioId,coalesce(p.nome_social,p.nome,u.login) as Nome,un.nome as Unidade,'ATIVO' as Situacao
from sigov.usuario u left join sigov.pessoa p on p.id=u.pessoa_id
left join sigov.usuario_entidade ue on ue.usuario_id=u.id and ue.entidade_id=@EntidadeId and ue.ativo
left join sigov.entidade un on un.id=ue.entidade_id
where u.tenant_id=@TenantId and u.ativo and not u.bloqueado and not u.is_deleted
and (@EntidadeId is null or ue.usuario_id is not null)
and (@ExercicioId is null or exists(select 1 from sigov.usuario_exercicio ux where ux.usuario_id=u.id and ux.exercicio_id=@ExercicioId and ux.ativo))
and (@Term is null or p.nome_social ilike @Term or p.nome ilike @Term or u.login ilike @Term)
order by coalesce(p.nome_social,p.nome,u.login),u.id limit @Limit offset @Offset";
        var found = await QuerySafe<ResponsavelElegivelDto>(sql, new { TenantId = Tenant(), _tenant.EntidadeId, _tenant.ExercicioId, Term = term, Limit = tamanhoLogico + 1, Offset = Offset(pagina, tamanhoLogico) }, ct).ConfigureAwait(false);
        return new(found.Take(tamanhoLogico).ToArray(), pagina, tamanhoLogico, found.Count > tamanhoLogico);
    }

    public async Task<ResponsavelElegivelDto?> ObterResponsavelElegivelAsync(long usuarioId, CancellationToken ct)
    {
        var page = await BuscarResponsaveisAsync(usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, 1, ct).ConfigureAwait(false);
        if (page.Itens.FirstOrDefault(x => x.UsuarioId == usuarioId) is { } bySearch) return bySearch;
        const string sql = @"select u.id UsuarioId,coalesce(p.nome_social,p.nome,u.login) Nome,un.nome Unidade,'ATIVO' Situacao from sigov.usuario u left join sigov.pessoa p on p.id=u.pessoa_id left join sigov.usuario_entidade ue on ue.usuario_id=u.id and ue.entidade_id=@EntidadeId and ue.ativo left join sigov.entidade un on un.id=ue.entidade_id where u.id=@UsuarioId and u.tenant_id=@TenantId and u.ativo and not u.bloqueado and not u.is_deleted and (@EntidadeId is null or ue.usuario_id is not null) and (@ExercicioId is null or exists(select 1 from sigov.usuario_exercicio ux where ux.usuario_id=u.id and ux.exercicio_id=@ExercicioId and ux.ativo))";
        return (await QuerySafe<ResponsavelElegivelDto>(sql, new { UsuarioId = usuarioId, TenantId = Tenant(), _tenant.EntidadeId, _tenant.ExercicioId }, ct).ConfigureAwait(false)).SingleOrDefault();
    }

    public async Task<GovernancaComandoResultado> RevalidarQualidadeAsync(long id, long versao, CancellationToken ct)
    {
        Demand("governanca.qualidade.revalidar"); var tenantId = Tenant();
        await using var connection = _connections.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<RevalidationRow>(new CommandDefinition(@"select versao,status,regra,condicao_presente,origem_verificada_em,verificado_em from sigov.qualidade_dados_ocorrencia where tenant_id=@TenantId and id=@Id for update", new { TenantId = tenantId, Id = id }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (row is null) return new(false, "NAO_ENCONTRADA", "Ocorrência não encontrada no contexto selecionado.", versao);
        if (row.Versao != versao) return new(false, "CONFLITO", "Existe resultado mais recente; atualize a tela.", row.Versao);
        if (row.Status is "RESOLVIDA" or "ACEITA") return new(true, "ESTADO_TERMINAL", "A ocorrência já está encerrada e não foi reaberta.", row.Versao);
        // Nenhuma regra do catálogo atual possui verificador tipado registrado. Os campos
        // de observação publicados por produtores legados não são aceitos como autoridade.
        return new(false, "VERIFICADOR_INDISPONIVEL", "Verificação automática indisponível para esta regra. Corrija na origem; o estado foi preservado.", row.Versao);
    }

    public async Task<IReadOnlyCollection<IntegracaoInternaDto>> ListarIntegracoesAsync(CancellationToken ct)
    {
        Demand("governanca.integracoes.visualizar");
        await RequireTable("integracao_interna_evento", ct).ConfigureAwait(false);
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
            var status = table ? "ESTRUTURA_DETECTADA" : "NAO_VERIFICADO";
            result.Add(new ModuloStatusFuncionalDto(definition.Item1, false, false, false, false, false, false, false, false,
                false, false, false, false, table ? $"Estrutura sigov.{definition.Item2} detectada; capacidades não verificadas" : "Capacidades não verificadas", status));
        }
        return result;
    }

    public async Task<bool> ResolverAlertaAsync(long id, string justificativa, CancellationToken ct)
    {
        Demand("governanca.alertas.resolver");
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Justificativa obrigatória.", nameof(justificativa));
        await RequireTable("alerta_operacional", ct).ConfigureAwait(false);
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
    private async Task RequireTable(string table, CancellationToken ct)
    {
        if (!await Exists(table, ct).ConfigureAwait(false))
        {
            _logger.LogError("Estrutura de governança indisponível. Table={Table} TenantId={TenantId}", table, _tenant.TenantId);
            throw new InvalidOperationException("A estrutura de governança necessária não está disponível.");
        }
    }
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
    private static long Offset(int page, int size) => checked((long)(Math.Max(page, 1) - 1) * Size(size));
    private static string? SafeRoute(string? route) => !string.IsNullOrWhiteSpace(route) && route.StartsWith('/') && !route.StartsWith("//", StringComparison.Ordinal) && Uri.TryCreate(route, UriKind.Relative, out _) ? route : null;
    private sealed record OccurrenceRow(long Id,string Tipo,string Modulo,string Titulo,string Descricao,string RegraOuMotivo,string Entidade,string EntidadeId,string Classificacao,string Status,string? RotaOrigem,long? ResponsavelUsuarioId,string? ResponsavelNome,DateTimeOffset? Prazo,DateTimeOffset DetectadaEm,DateTimeOffset? VerificadaEm,string? UltimoResultado,long Versao);
    private sealed record RevalidationRow(long Versao,string Status,string Regra,bool? CondicaoPresente,DateTimeOffset? OrigemVerificadaEm,DateTimeOffset? VerificadoEm);
}
