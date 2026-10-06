using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.Rh;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Rh;

public sealed class RhRepository : BaseRepository, IRhRepository
{
    private readonly DapperContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string> Tabelas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["servidores"] = "sigov.servidor", ["cargos"] = "sigov.cargo", ["lotacoes"] = "sigov.lotacao", ["vinculos"] = "sigov.vinculo",
        ["folhas"] = "sigov.folha", ["folha-eventos"] = "sigov.folha_evento", ["folha-lancamentos"] = "sigov.folha_lancamento",
        ["pontos"] = "sigov.ponto", ["ferias"] = "sigov.ferias", ["afastamentos"] = "sigov.afastamento", ["saude-ocupacional"] = "sigov.saude_ocupacional",
        ["esocial"] = "sigov.esocial", ["portal-usuarios"] = "sigov.portal_usuario", ["portal-acessos"] = "sigov.portal_acesso", ["eventos"] = "sigov.rh_evento",
        ["ponto-jornadas"] = "sigov.rh_jornada", ["ponto-escalas"] = "sigov.rh_escala", ["ponto-registros"] = "sigov.rh_ponto_registro",
        ["ponto-justificativas"] = "sigov.rh_ponto_justificativa", ["ponto-apuracoes"] = "sigov.rh_ponto_apuracao", ["ponto-homologacoes"] = "sigov.rh_ponto_homologacao",
        ["ponto-integracoes-folha"] = "sigov.rh_ponto_integracao_folha", ["ferias-periodos"] = "sigov.rh_ferias_periodo_aquisitivo",
        ["ferias-programacoes"] = "sigov.rh_ferias_programacao", ["ferias-historicos"] = "sigov.rh_ferias_historico", ["afastamento-tipos"] = "sigov.rh_afastamento_tipo",
        ["afastamento-historicos"] = "sigov.rh_afastamento_historico", ["portal-solicitacoes"] = "sigov.rh_portal_solicitacao",
        ["portal-atualizacoes"] = "sigov.rh_portal_atualizacao_cadastral", ["portal-mensagens"] = "sigov.rh_portal_mensagem"
        , ["atos"] = "sigov.rh_ato_funcional", ["licencas"] = "sigov.rh_licenca", ["banco-horas"] = "sigov.rh_banco_horas"
        , ["ocorrencias-frequencia"] = "sigov.rh_frequencia", ["quadro-pessoal"] = "sigov.rh_vinculo_funcional"
        , ["pericias"] = "sigov.rh_afastamento", ["previdencia"] = "sigov.rh_previdencia_parametro"
        , ["consignacoes"] = "sigov.rh_consignacao"
        , ["servidor-documentos"] = "sigov.rh_servidor_documento", ["historico-funcional"] = "sigov.rh_movimentacao"
    };

    public RhRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<RhRegistroResponse>> ListarAsync(long tenantId, string recurso, RhFiltro filtro, CancellationToken ct)
    {
        var table = Table(recurso);
        var page = Math.Max(1, filtro.Page);
        var pageSize = Math.Clamp(filtro.PageSize, 1, 100);
        var where = new StringBuilder("tenant_id = @TenantId and is_deleted = false");
        if (filtro.Ativo.HasValue) where.Append(" and ativo = @Ativo");
        if (!string.IsNullOrWhiteSpace(filtro.Termo)) where.Append(" and dados::text ilike @Termo");
        var parameters = new { TenantId = tenantId, filtro.Ativo, Termo = $"%{filtro.Termo}%", Limit = pageSize, Offset = (page - 1) * pageSize };
        using var cn = _context.CreateConnection();
        var total = await cn.ExecuteScalarAsync<long>(Command($"select count(1) from {table} where {where};", parameters, ct)).ConfigureAwait(false);
        var rows = await cn.QueryAsync<Row>(Command($"select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from {table} where {where} order by id desc limit @Limit offset @Offset;", parameters, ct)).ConfigureAwait(false);
        return new PagedResult<RhRegistroResponse>(rows.Select(r => ToResponse(recurso, r)).ToArray(), page, pageSize, total);
    }

    public async Task<RhRegistroResponse?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        var table = Table(recurso);
        using var cn = _context.CreateConnection();
        var row = await cn.QuerySingleOrDefaultAsync<Row>(Command($"select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from {table} where tenant_id = @TenantId and id = @Id and is_deleted = false;", new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false);
        return row is null ? null : ToResponse(recurso, row);
    }

    public async Task<long> CriarAsync(long tenantId, string recurso, RhRegistroCreateRequest request, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        var dados = EnriquecerDados(recurso, request.Dados);
        var json = JsonSerializer.Serialize(dados, JsonOptions);
        using var cn = _context.CreateConnection();
        var id = await cn.ExecuteScalarAsync<long>(Command($"insert into {table} (tenant_id, dados, auditoria, created_by) values (@TenantId, cast(@Dados as jsonb), jsonb_build_object('operacao','CRIAR','usuarioId',@UsuarioId,'recurso',@Recurso), @UsuarioId) returning id;", new { TenantId = tenantId, Dados = json, UsuarioId = usuarioId, Recurso = recurso }, ct)).ConfigureAwait(false);
        await RegistrarEventoAsync(cn, tenantId, recurso, "CRIAR", id, dados, usuarioId, ct).ConfigureAwait(false);
        return id;
    }

    public async Task AtualizarAsync(long tenantId, string recurso, long id, RhRegistroUpdateRequest request, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        var dados = EnriquecerDados(recurso, request.Dados);
        var json = JsonSerializer.Serialize(dados, JsonOptions);
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(Command($"update {table} set dados = cast(@Dados as jsonb), ativo = @Ativo, auditoria = auditoria || jsonb_build_object('ultimaOperacao','EDITAR','usuarioId',@UsuarioId,'recurso',@Recurso), updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId and id = @Id and is_deleted = false;", new { TenantId = tenantId, Id = id, Dados = json, request.Ativo, UsuarioId = usuarioId, Recurso = recurso }, ct)).ConfigureAwait(false);
        await RegistrarEventoAsync(cn, tenantId, recurso, "EDITAR", id, dados, usuarioId, ct).ConfigureAwait(false);
    }

    public async Task ExcluirAsync(long tenantId, string recurso, long id, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        var anterior = await ObterAsync(tenantId, recurso, id, ct).ConfigureAwait(false);
        var auditoria = BuildAuditJson("EXCLUIR", usuarioId, anterior?.Dados, new { softDelete = true });
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(Command($"update {table} set is_deleted = true, ativo = false, auditoria = coalesce(auditoria, '{{}}'::jsonb) || cast(@Auditoria as jsonb), deleted_by = @UsuarioId, deleted_at = now(), updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId and id = @Id and is_deleted = false;", new { TenantId = tenantId, Id = id, UsuarioId = usuarioId, Recurso = recurso, Auditoria = auditoria }, ct)).ConfigureAwait(false);
        await RegistrarEventoAsync(cn, tenantId, recurso, "EXCLUIR", id, new Dictionary<string, object?> { ["softDelete"] = true }, usuarioId, ct).ConfigureAwait(false);
    }

    public async Task<RhDashboardResponse> DashboardAsync(long tenantId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
        select
          (select count(1) from sigov.servidor where tenant_id=@TenantId and ativo=true and is_deleted=false) as ServidoresAtivos,
          (select count(1) from sigov.vinculo where tenant_id=@TenantId and ativo=true and is_deleted=false) as VinculosAtivos,
          (select count(1) from sigov.folha where tenant_id=@TenantId and is_deleted=false and coalesce(dados->>'status','Aberta')='Aberta') as FolhasAbertas,
          (select count(1) from sigov.ferias where tenant_id=@TenantId and is_deleted=false and coalesce(dados->>'status','Programada') in ('Programada','Aprovada')) as FeriasProgramadas,
          (select count(1) from sigov.afastamento where tenant_id=@TenantId and is_deleted=false and coalesce(dados->>'status','EmCurso') in ('Aprovado','EmCurso')) as AfastamentosAtivos,
          (select coalesce(sum((dados->>'valor')::numeric),0) from sigov.folha_lancamento where tenant_id=@TenantId and is_deleted=false) as TotalFolhaMes;
        ";
        return await cn.QuerySingleAsync<RhDashboardResponse>(Command(sql, new { TenantId = tenantId }, ct)).ConfigureAwait(false);
    }

    public async Task<RhPortalResumoResponse?> PortalServidorAsync(long tenantId, long servidorId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var servidor = await ObterAsync(tenantId, "servidores", servidorId, ct).ConfigureAwait(false);
        if (servidor is null) return null;
        var nome = servidor.Dados.TryGetValue("nome", out var n) ? Convert.ToString(n) ?? "Servidor" : "Servidor";
        var contracheques = await cn.QueryAsync<Row>(Command("select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from sigov.folha_lancamento where tenant_id=@TenantId and is_deleted=false and (dados->>'servidorId')::bigint=@ServidorId order by id desc limit 24;", new { TenantId = tenantId, ServidorId = servidorId }, ct)).ConfigureAwait(false);
        var ferias = await cn.QueryAsync<Row>(Command("select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from sigov.ferias where tenant_id=@TenantId and is_deleted=false and (dados->>'servidorId')::bigint=@ServidorId order by id desc;", new { TenantId = tenantId, ServidorId = servidorId }, ct)).ConfigureAwait(false);
        var afastamentos = await cn.QueryAsync<Row>(Command("select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from sigov.afastamento where tenant_id=@TenantId and is_deleted=false and (dados->>'servidorId')::bigint=@ServidorId order by id desc;", new { TenantId = tenantId, ServidorId = servidorId }, ct)).ConfigureAwait(false);
        return new RhPortalResumoResponse(servidorId, nome, contracheques.Select(r => ToResponse("folha-lancamentos", r)).ToArray(), ferias.Select(r => ToResponse("ferias", r)).ToArray(), afastamentos.Select(r => ToResponse("afastamentos", r)).ToArray());
    }

    public async Task<decimal> TotalLancamentosFolhaAsync(long tenantId, long folhaId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = "select coalesce(sum((dados->>'valor')::numeric),0) from sigov.folha_lancamento where tenant_id=@TenantId and is_deleted=false and (dados->>'folhaId')::bigint=@FolhaId and (dados->>'valor') ~ '^-?[0-9]+(\\.[0-9]+)?$' and (dados->>'valor')::numeric >= 0;";
        return await cn.ExecuteScalarAsync<decimal>(Command(sql, new { TenantId = tenantId, FolhaId = folhaId }, ct)).ConfigureAwait(false);
    }

    public async Task<long> PrepararIntegracaoFinanceiraAsync(long tenantId, RhFinanceiroIntegracaoRequest request, long? usuarioId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var totalFolha = await TotalLancamentosFolhaAsync(tenantId, request.FolhaId, ct).ConfigureAwait(false);
        var correlationId = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(new
        {
            tipo = "folha.financeiro.integracao.solicitada",
            destino = "financeiro-siafic",
            publicado = false,
            tenantId,
            folhaId = request.FolhaId,
            competencia = request.DataCompetencia,
            valorTotal = totalFolha,
            naturezaDespesaId = request.NaturezaDespesaId,
            fonteRecursoId = request.FonteRecursoId,
            historico = request.Historico,
            usuarioId,
            correlationId
        }, JsonOptions);
        return await cn.ExecuteScalarAsync<long>(Command("insert into sigov.rh_evento (tenant_id, dados, created_by) values (@TenantId, cast(@Dados as jsonb), @UsuarioId) returning id;", new { TenantId = tenantId, Dados = payload, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<byte[]> ExportarAsync(long tenantId, string recurso, string formato, CancellationToken ct)
    {
        var all = await ListarAsync(tenantId, recurso, new RhFiltro(1, 100), ct).ConfigureAwait(false);
        if (formato.Equals("json", StringComparison.OrdinalIgnoreCase)) return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(all.Items, JsonOptions));
        var sb = new StringBuilder("id;recurso;ativo;dados\n");
        foreach (var item in all.Items) sb.Append(item.Id).Append(';').Append(item.Recurso).Append(';').Append(item.Ativo).Append(';').Append(EscapeCsv(JsonSerializer.Serialize(item.Dados, JsonOptions))).AppendLine();
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<bool> ExercicioAbertoAsync(long tenantId, long? exercicioId, CancellationToken ct)
    {
        if (!exercicioId.HasValue) return true;
        using var cn = _context.CreateConnection();
        const string sql = "select exists (select 1 from sigov.exercicio where tenant_id=@TenantId and id=@ExercicioId and is_deleted=false and ativo=true);";
        return await cn.ExecuteScalarAsync<bool>(Command(sql, new { TenantId = tenantId, ExercicioId = exercicioId.Value }, ct)).ConfigureAwait(false);
    }

    // ==== RC-EVO-RH §4: leitura/gravacao da apuracao real de ponto =================
    // As tabelas ponto-* sao genericas em JSONB; os metodos abaixo aceitam as duas
    // casings (PascalCase escrito pela API e camelCase escrito pela engine) e as
    // colunas estruturadas quando populadas (legacy pode ter tudo em `dados`).

    public async Task<IReadOnlyList<Sigov.Domain.Rh.JornadaPontoRegra>> ListarJornadasAtivasAsync(long tenantId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = "select id, dados::text as Dados, ativo, created_at as CreatedAt from sigov.rh_jornada where tenant_id = @TenantId and is_deleted = false and ativo = true order by id;";
        var rows = await cn.QueryAsync<Row>(Command(sql, new { TenantId = tenantId }, ct)).ConfigureAwait(false);
        var result = new List<Sigov.Domain.Rh.JornadaPontoRegra>();
        foreach (var row in rows)
        {
            var dados = ParseJsonb(row.Dados);
            if (TryJsonText(dados, out var nome, "Nome", "nome")
                && TryJsonDecimal(dados, out var carga, "CargaHoraria", "cargaHoraria")
                && TryJsonTimeOnly(dados, out var entrada, "Entrada", "entrada")
                && TryJsonTimeOnly(dados, out var saida, "Saida", "saida")
                && TryParseDiasSemana(JsonText(dados, "DiasSemana", "diasSemana"), out var dias))
            {
                var tolerancia = TryJsonInt(dados, out var toleranciaRaw, "ToleranciaMinutos", "toleranciaMinutos") ? toleranciaRaw : 0;
                try { result.Add(new Sigov.Domain.Rh.JornadaPontoRegra(row.Id, nome, carga, entrada, saida, tolerancia, dias)); }
                catch (ArgumentException) { /* Jornada incompleta: fica explícita na apuração como JORNADA_AUSENTE */ }
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<Sigov.Domain.Rh.EscalaPontoResumo>> ListarEscalasPorServidorAsync(long tenantId, long servidorId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select id, dados::text as Dados, ativo, created_at as CreatedAt
from sigov.rh_escala
where tenant_id = @TenantId and is_deleted = false
  and coalesce(nullif(dados->>'ServidorId',''), dados->>'servidorId','') = @ServidorIdText
order by id;";
        var rows = await cn.QueryAsync<Row>(Command(sql, new { TenantId = tenantId, ServidorIdText = servidorId.ToString(CultureInfo.InvariantCulture) }, ct)).ConfigureAwait(false);
        var result = new List<Sigov.Domain.Rh.EscalaPontoResumo>();
        foreach (var row in rows)
        {
            var dados = ParseJsonb(row.Dados);
            if (!TryJsonLong(dados, out var jornadaId, "JornadaId", "jornadaId")) continue;
            if (!TryJsonDate(dados, out var periodoInicio, "PeriodoInicio", "periodoInicio")) continue;
            var temFim = TryJsonDate(dados, out var periodoFim, "PeriodoFim", "periodoFim");
            var status = JsonText(dados, "Status", "status") ?? string.Empty;
            var ativa = row.Ativo && status is not ("INATIVA" or "CANCELADA" or "EXPIRADA" or "ENCERRADA");
            result.Add(new Sigov.Domain.Rh.EscalaPontoResumo(row.Id, jornadaId, periodoInicio, temFim ? periodoFim : null, ativa));
        }
        return result;
    }

    public async Task<IReadOnlyList<Sigov.Domain.Rh.BatidaPonto>> ListarBatidasPeriodoAsync(long tenantId, long servidorId, DateTimeOffset inicioUtc, DateTimeOffset fimUtc, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select id, dados::text as Dados, ativo, created_at as CreatedAt
from sigov.rh_ponto_registro
where tenant_id = @TenantId and is_deleted = false
  and (servidor_id = @ServidorId or coalesce(nullif(dados->>'ServidorId',''), dados->>'servidorId','') = @ServidorIdText)
  and created_at >= @Inicio and created_at <= @Fim
order by created_at, id;";
        var rows = await cn.QueryAsync<Row>(Command(sql, new { TenantId = tenantId, ServidorId = servidorId, ServidorIdText = servidorId.ToString(CultureInfo.InvariantCulture), Inicio = inicioUtc, Fim = fimUtc }, ct)).ConfigureAwait(false);
        var result = new List<Sigov.Domain.Rh.BatidaPonto>();
        foreach (var row in rows)
        {
            var dados = ParseJsonb(row.Dados);
            // Instante oficial da batida vem do JSONB; texto ausente/inválido cai no
            // created_at (registrado como fallback, nunca inventado).
            if (!TryJsonDateTimeOffset(dados, out var instante, "DataHora", "dataHora")) instante = row.CreatedAt;
            if (instante < inicioUtc || instante > fimUtc) continue;
            if (!TryJsonText(dados, out var tipoTexto, "Tipo", "tipo") || !Enum.TryParse<Sigov.Domain.Rh.PontoTipo>(tipoTexto, true, out var tipo)) continue;
            var origem = JsonText(dados, "Origem", "origem") ?? "MANUAL";
            result.Add(new Sigov.Domain.Rh.BatidaPonto(row.Id, instante, tipo, origem));
        }
        return result;
    }

    public async Task<IReadOnlyCollection<DateOnly>> ListarFeriadosPeriodoAsync(long tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var existeTabela = await cn.ExecuteScalarAsync<long>(Command(
            "select count(1) from pg_class c join pg_namespace n on n.oid = c.relnamespace where n.nspname = 'sigov' and c.relname = 'rh_feriado';", null, ct)).ConfigureAwait(false);
        if (existeTabela == 0) throw new InvalidOperationException("Migrations de RH pendentes: a tabela sigov.rh_feriado não existe neste banco. Aplique a migration grande RH.");
        const string sql = "select distinct data_referencia::date from sigov.rh_feriado where tenant_id = @TenantId and is_deleted = false and ativo = true and data_referencia::date between @Inicio and @Fim;";
        var rows = await cn.QueryAsync<DateOnly>(Command(sql, new { TenantId = tenantId, Inicio = inicio, Fim = fim }, ct)).ConfigureAwait(false);
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<DateOnly>> ListarAusenciasJustificadasAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        // Justificativa APROVADA cobre a data na coluna data_referencia ou no JSONB
        // (duas casings), com guard de formato antes de castar para date.
        const string sql = @"
select distinct coalesce(
      j.data_referencia,
      (case when (coalesce(nullif(j.dados->>'DataReferencia',''), j.dados->>'dataReferencia','')) ~ '^\d{4}-\d{2}-\d{2}$'
            then coalesce(nullif(j.dados->>'DataReferencia',''), j.dados->>'dataReferencia','') end)::date
    ) as data
from sigov.rh_ponto_justificativa j
where j.tenant_id = @TenantId and j.is_deleted = false
  and (j.servidor_id = @ServidorId or coalesce(nullif(j.dados->>'ServidorId',''), j.dados->>'servidorId','') = @ServidorIdText)
  and coalesce(nullif(j.dados->>'status',''), j.status,'') = 'APROVADA'
  and coalesce(
      j.data_referencia,
      (case when (coalesce(nullif(j.dados->>'DataReferencia',''), j.dados->>'dataReferencia','')) ~ '^\d{4}-\d{2}-\d{2}$'
            then coalesce(nullif(j.dados->>'DataReferencia',''), j.dados->>'dataReferencia','') end)::date
    ) between @Inicio and @Fim;";
        var rows = await cn.QueryAsync<DateOnly>(Command(sql, new { TenantId = tenantId, ServidorId = servidorId, ServidorIdText = servidorId.ToString(CultureInfo.InvariantCulture), Inicio = inicio, Fim = fim }, ct)).ConfigureAwait(false);
        return rows.ToArray();
    }

    public async Task<string?> ObterFusoOperacaoAsync(CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        return await cn.ExecuteScalarAsync<string>(Command("select current_setting('TimeZone');", null, ct)).ConfigureAwait(false);
    }

    public async Task<RhApuracaoExistenteDto?> ObterApuracaoExistenteAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select id,
       coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') as status,
       dados::text as anterior
from sigov.rh_ponto_apuracao
where tenant_id = @TenantId and is_deleted = false
  and (servidor_id = @ServidorId or coalesce(nullif(dados->>'servidorId',''), dados->>'ServidorId','') = @ServidorIdText)
  and ((periodo_inicio = @Inicio and periodo_fim = @Fim)
       or (coalesce(nullif(dados->>'periodoInicio',''), dados->>'PeriodoInicio','') = @InicioText
           and coalesce(nullif(dados->>'periodoFim',''), dados->>'PeriodoFim','') = @FimText))
order by id desc limit 1;";
        var row = await cn.QueryFirstOrDefaultAsync<ApuracaoExistenteRow>(Command(sql, new
        {
            TenantId = tenantId,
            ServidorId = servidorId,
            ServidorIdText = servidorId.ToString(CultureInfo.InvariantCulture),
            Inicio = inicio,
            Fim = fim,
            InicioText = inicio.ToString("yyyy-MM-dd"),
            FimText = fim.ToString("yyyy-MM-dd")
        }, ct)).ConfigureAwait(false);
        return row is null ? null : new RhApuracaoExistenteDto(row.Id, row.Status, row.Anterior);
    }

    public async Task<long> SalvarApuracaoPontoAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, string dadosJson, long? anteriorId, string? anteriorDadosJson, long? usuarioId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        long id;
        if (anteriorId.HasValue)
        {
            // Retry/reprocessamento: mesmo registro, colunas estruturadas garantidas,
            // auditoria acumulada com o estado anterior (nunca sobrescrito).
            var auditoria = JsonSerializer.Serialize(new { operacao = "REPROCESSAR", usuarioId, before = ParseJsonb(anteriorDadosJson) }, JsonOptions);
            id = await cn.ExecuteScalarAsync<long>(Command(@"
update sigov.rh_ponto_apuracao
set status = 'APURADA',
    servidor_id = @ServidorId,
    competencia = @Inicio,
    periodo_inicio = @Inicio,
    periodo_fim = @Fim,
    dados = cast(@Dados as jsonb),
    auditoria = coalesce(auditoria, '{}'::jsonb) || cast(@Auditoria as jsonb),
    updated_by = @UsuarioId,
    updated_at = now()
where tenant_id = @TenantId and id = @Id and is_deleted = false
returning id;", new { TenantId = tenantId, Id = anteriorId.Value, ServidorId = servidorId, Inicio = inicio, Fim = fim, Dados = dadosJson, Auditoria = auditoria, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
        }
        else
        {
            id = await cn.ExecuteScalarAsync<long>(Command(@"
insert into sigov.rh_ponto_apuracao (tenant_id, servidor_id, competencia, periodo_inicio, periodo_fim, status, auditoria, dados, created_by)
values (@TenantId, @ServidorId, @Inicio, @Inicio, @Fim, 'APURADA',
        jsonb_build_object('operacao','CRIAR','usuarioId',@UsuarioId,'recurso','ponto-apuracoes'),
        cast(@Dados as jsonb), @UsuarioId)
returning id;", new { TenantId = tenantId, ServidorId = servidorId, Inicio = inicio, Fim = fim, Dados = dadosJson, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
        }
        var dados = ParseJsonb(dadosJson);
        await RegistrarEventoAsync(cn, tenantId, "ponto-apuracoes", "APURAR", id, dados, usuarioId, ct).ConfigureAwait(false);
        return id;
    }

    // ==== Helpers JSONB case-insensitive (PascalCase da API / camelCase da engine) ===

    private static Dictionary<string, object?> ParseJsonb(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions) ?? new Dictionary<string, object?>(); }
        catch (JsonException) { return new Dictionary<string, object?>(); }
    }

    private static string? JsonText(Dictionary<string, object?> dados, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!dados.TryGetValue(key, out var value) || value is null) continue;
            var text = value switch
            {
                JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
                JsonElement el => el.GetRawText(),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return null;
    }

    private static bool TryJsonText(Dictionary<string, object?> dados, out string value, params string[] keys)
    {
        value = string.Empty;
        var text = JsonText(dados, keys);
        if (string.IsNullOrWhiteSpace(text)) return false;
        value = text.Trim();
        return true;
    }

    private static bool TryJsonDate(Dictionary<string, object?> dados, out DateOnly value, params string[] keys)
    {
        value = default;
        var text = JsonText(dados, keys);
        if (text is null) return false;
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return true;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) { value = DateOnly.FromDateTime(dt); return true; }
        return false;
    }

    private static bool TryJsonDateTimeOffset(Dictionary<string, object?> dados, out DateTimeOffset value, params string[] keys)
    {
        value = default;
        var text = JsonText(dados, keys);
        if (text is null) return false;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static bool TryJsonTimeOnly(Dictionary<string, object?> dados, out TimeOnly value, params string[] keys)
    {
        value = default;
        var text = JsonText(dados, keys);
        if (text is null) return false;
        if (TimeOnly.TryParseExact(text, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return true;
        return TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static bool TryJsonInt(Dictionary<string, object?> dados, out int value, params string[] keys)
    {
        value = default;
        var text = JsonText(dados, keys);
        return text is not null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryJsonLong(Dictionary<string, object?> dados, out long value, params string[] keys)
    {
        value = default;
        var text = JsonText(dados, keys);
        return text is not null && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryJsonDecimal(Dictionary<string, object?> dados, out decimal value, params string[] keys)
    {
        value = default;
        var text = JsonText(dados, keys);
        return text is not null && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseDiasSemana(string? text, out List<int> dias)
    {
        dias = new List<int>();
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var parte in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(parte, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dia) || dia is < 1 or > 7) return false;
            if (!dias.Contains(dia)) dias.Add(dia);
        }
        return dias.Count > 0;
    }

    private sealed class ApuracaoExistenteRow
    {
        public long Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Anterior { get; init; }
    }

    private static Dictionary<string, object?> EnriquecerDados(string recurso, Dictionary<string, object?>? dados)
    {
        var copy = dados is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(dados, StringComparer.OrdinalIgnoreCase);
        copy["tenantIsolation"] = true;
        copy["softDelete"] = true;
        if (recurso.Equals("servidores", StringComparison.OrdinalIgnoreCase) || copy.ContainsKey("cpf") || copy.ContainsKey("cnpj") || copy.ContainsKey("email") || copy.ContainsKey("telefone"))
        {
            copy["classificacaoLgpd"] = "dados_pessoais_sensiveis";
        }

        return copy;
    }

    private async Task RegistrarEventoAsync(System.Data.IDbConnection cn, long tenantId, string recurso, string operacao, long registroId, Dictionary<string, object?> dados, long? usuarioId, CancellationToken ct)
    {
        var dadosAuditaveis = new Dictionary<string, object?>(dados, StringComparer.OrdinalIgnoreCase);
        MaskDadosPessoais(dadosAuditaveis);
        var payload = JsonSerializer.Serialize(new { tipo = $"rh.{recurso}.{operacao.ToLowerInvariant()}", recurso, operacao, registroId, publicado = false, dados = dadosAuditaveis }, JsonOptions);
        await cn.ExecuteAsync(Command("insert into sigov.rh_evento (tenant_id, dados, created_by) values (@TenantId, cast(@Dados as jsonb), @UsuarioId);", new { TenantId = tenantId, Dados = payload, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }


    private static string BuildAuditJson(string operacao, long? usuarioId, object? before, object? after)
    {
        return JsonSerializer.Serialize(new { operacao, usuarioId, before, after }, JsonOptions);
    }

    private static string EscapeCsv(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        // Neutraliza fórmulas interpretáveis por Excel/LibreOffice antes do escaping RFC 4180.
        var first = text.AsSpan().TrimStart();
        if (!first.IsEmpty && first[0] is '=' or '+' or '-' or '@') text = "'" + text;
        text = text.Replace("\"", "\"\"", StringComparison.Ordinal);

        return text.IndexOfAny(new[] { ';', '\"', '\r', '\n' }) >= 0
            ? $"\"{text}\""
            : text;
    }

    private static string Table(string recurso) => Tabelas.TryGetValue(recurso, out var table) ? table : throw new InvalidOperationException("Recurso de RH inválido.");
    private static RhRegistroResponse ToResponse(string recurso, Row row)
    {
        var dados = JsonSerializer.Deserialize<Dictionary<string, object?>>(row.Dados ?? "{}", JsonOptions) ?? new();
        MaskDadosPessoais(dados);

        return new RhRegistroResponse(row.Id, recurso, dados, row.Ativo, row.CreatedAt, row.UpdatedAt);
    }

    private static void MaskDadosPessoais(IDictionary<string, object?> dados)
    {
        MaskDocumento(dados, "cpf", 3, 2);
        MaskDocumento(dados, "cnpj", 2, 2);
        MaskDocumento(dados, "documento", 3, 2);
        MaskEmail(dados, "email");
        MaskEmail(dados, "emailInstitucional");
        MaskTelefone(dados, "telefone");
        MaskSensitive(dados, "dadosBancarios");
        MaskSensitive(dados, "banco");
        MaskSensitive(dados, "agencia");
        MaskSensitive(dados, "conta");
        MaskSensitive(dados, "resultadoExame");
        MaskSensitive(dados, "resultado");
        MaskSensitive(dados, "laudo");
        MaskSensitive(dados, "cid");
        MaskSensitive(dados, "motivoSensivel");
        MaskSensitive(dados, "observacaoSaude");
    }

    private static void MaskSensitive(IDictionary<string, object?> dados, string key)
    {
        if (dados.ContainsKey(key)) dados[key] = "***";
    }

    private static void MaskDocumento(IDictionary<string, object?> dados, string key, int visibleStart, int visibleEnd)
    {
        if (!dados.TryGetValue(key, out var value) || value is null) return;
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;
        dados[key] = text.Length <= visibleStart + visibleEnd ? "***" : text[..visibleStart] + new string('*', Math.Max(0, text.Length - visibleStart - visibleEnd)) + text[^visibleEnd..];
    }

    private static void MaskEmail(IDictionary<string, object?> dados, string key)
    {
        if (!dados.TryGetValue(key, out var value) || value is null) return;
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        var at = text.IndexOf('@', StringComparison.Ordinal);
        if (at <= 1) { dados[key] = "***"; return; }
        dados[key] = text[0] + "***" + text[at..];
    }

    private static void MaskTelefone(IDictionary<string, object?> dados, string key)
    {
        if (!dados.TryGetValue(key, out var value) || value is null) return;
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        dados[key] = text.Length <= 4 ? "***" : "***" + text[^4..];
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string? Dados { get; init; }
        public bool Ativo { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
