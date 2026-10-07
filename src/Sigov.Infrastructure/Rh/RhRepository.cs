using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
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
  -- Janela coarse em SQL: o instante oficial e o DataHora do JSONB (filtro exato em C#).
  -- created_at cobre linhas legadas sem DataHora. Filtrar somente por created_at excluiria
  -- batidas registradas/reparadas fora do periodo e falaria ausencia falsa na apuracao.
  and (created_at >= @Inicio and created_at <= @Fim
       or coalesce(nullif(dados->>'dataHora',''), dados->>'DataHora') ~ '^\d{4}-\d{2}-\d{2}T\d{2}:')
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

    // ==== RC-EVO-RH §5: decisão de justificativas/ajustes (origem + invalidação de dependentes) ===

    private sealed class RegistroComOrigemRow
    {
        public long Id { get; init; }
        public string? Dados { get; init; }
        public long? ServidorId { get; init; }
        public long? CriadoPor { get; init; }
        public DateTimeOffset CriadoEm { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    private sealed class ApuracaoJanelaRow
    {
        public long Id { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public async Task<RhRegistroComOrigemDto?> ObterRegistroComOrigemAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        var table = Table(recurso);
        using var cn = _context.CreateConnection();
        var row = await cn.QueryFirstOrDefaultAsync<RegistroComOrigemRow>(Command(
            "select id, dados::text as Dados, servidor_id as ServidorId, created_by as CriadoPor, created_at as CriadoEm, coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') as Status from " + table + " where tenant_id = @TenantId and id = @Id and is_deleted = false;",
            new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false);
        return row is null ? null : new RhRegistroComOrigemDto(row.Id, row.Dados ?? "{}", row.ServidorId, row.CriadoPor, row.CriadoEm, row.Status);
    }

    public async Task<IReadOnlyList<RhApuracaoJanelaDto>> ApuracoesCobertasPorJanelaAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select id, coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') as status
from sigov.rh_ponto_apuracao
where tenant_id = @TenantId and is_deleted = false
  and (servidor_id = @ServidorId or coalesce(nullif(dados->>'servidorId',''), nullif(dados->>'ServidorId',''),'') = @ServidorIdText)
  and coalesce(periodo_inicio,
        (case when coalesce(nullif(dados->>'PeriodoInicio',''), nullif(dados->>'periodoInicio',''),'') ~ '^\d{4}-\d{2}-\d{2}$'
              then coalesce(nullif(dados->>'PeriodoInicio',''), nullif(dados->>'periodoInicio',''))::date end)) <= @Fim
  and coalesce(periodo_fim,
        (case when coalesce(nullif(dados->>'PeriodoFim',''), nullif(dados->>'periodoFim',''),'') ~ '^\d{4}-\d{2}-\d{2}$'
              then coalesce(nullif(dados->>'PeriodoFim',''), nullif(dados->>'periodoFim',''))::date end)) >= @Inicio
order by id desc;";
        var rows = await cn.QueryAsync<ApuracaoJanelaRow>(Command(sql, new
        {
            TenantId = tenantId,
            ServidorId = servidorId,
            ServidorIdText = servidorId.ToString(CultureInfo.InvariantCulture),
            Inicio = inicio,
            Fim = fim
        }, ct)).ConfigureAwait(false);
        return rows.Select(r => new RhApuracaoJanelaDto(r.Id, r.Status)).ToList();
    }

    public async Task<int> InvalidarApuracoesPorAjusteAsync(long tenantId, IReadOnlyCollection<long> ids, string motivo, long? usuarioId, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;
        using var cn = _context.CreateConnection();
        var delta = JsonSerializer.Serialize(new { status = "INVALIDADA", invalidadaPorAjuste = true }, JsonOptions);
        var auditoria = BuildAuditJson("INVALIDAR_POR_AJUSTE", usuarioId, null, new { motivo });
        return await cn.ExecuteAsync(Command(@"
update sigov.rh_ponto_apuracao
set status = 'INVALIDADA',
    motivo = @Motivo,
    dados = dados || cast(@Delta as jsonb),
    auditoria = coalesce(auditoria, '{}'::jsonb) || cast(@Auditoria as jsonb),
    updated_by = @UsuarioId,
    updated_at = now()
where tenant_id = @TenantId and id = any(@Ids) and is_deleted = false
  and coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') = 'APURADA';",
            new { TenantId = tenantId, Ids = ids.ToArray(), Motivo = motivo, Delta = delta, Auditoria = auditoria, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task AtualizarComDeltaAsync(long tenantId, string recurso, long id, string deltaJson, string operacao, object? antes, object? depois, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        using var cn = _context.CreateConnection();
        var auditoria = BuildAuditJson(operacao, usuarioId, antes, depois);
        await cn.ExecuteAsync(Command("update " + table + " set dados = dados || cast(@Delta as jsonb), auditoria = coalesce(auditoria, '{}'::jsonb) || cast(@Auditoria as jsonb), updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId and id = @Id and is_deleted = false;",
            new { TenantId = tenantId, Id = id, Delta = deltaJson, Auditoria = auditoria, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
        await RegistrarEventoAsync(cn, tenantId, recurso, operacao, id, ParseJsonb(deltaJson), usuarioId, ct).ConfigureAwait(false);
    }

    // RC-EVO-RH §6: homologação/reabertura — update guardado por status (concorrência sem efeito
    // duplicado), auditoria append-only, justificativa estruturada na reabertura e evento de fila.
    public async Task<int> AtualizarStatusApuracaoGuardadoAsync(long tenantId, long id, string statusGuard, string novoStatus, string deltaJson, string operacao, object? antes, object? depois, string? justificativa, long? usuarioId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var auditoria = BuildAuditJson(operacao, usuarioId, antes, depois);
        var linhas = await cn.ExecuteAsync(Command(@"
update sigov.rh_ponto_apuracao
set status = @NovoStatus,
    justificativa = coalesce(nullif(@Justificativa,''), justificativa),
    dados = dados || cast(@Delta as jsonb),
    auditoria = coalesce(auditoria, '{}'::jsonb) || cast(@Auditoria as jsonb),
    updated_by = @UsuarioId,
    updated_at = now()
where tenant_id = @TenantId and id = @Id and is_deleted = false
  and coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') = @StatusGuard;",
            new { TenantId = tenantId, Id = id, StatusGuard = statusGuard, NovoStatus = novoStatus, Delta = deltaJson, Auditoria = auditoria, Justificativa = justificativa, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
        if (linhas > 0)
        {
            await RegistrarEventoAsync(cn, tenantId, "ponto-apuracoes", operacao, id, ParseJsonb(deltaJson), usuarioId, ct).ConfigureAwait(false);
        }
        return linhas;
    }

    public async Task<IReadOnlyList<RhRegistroComOrigemDto>> ListarIntegracoesFolhaDaApuracaoAsync(long tenantId, long apuracaoId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var rows = await cn.QueryAsync<RegistroComOrigemRow>(Command(@"
select id, dados::text as Dados, servidor_id as ServidorId, created_by as CriadoPor, created_at as CriadoEm,
       coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') as Status
from sigov.rh_ponto_integracao_folha
where tenant_id = @TenantId and is_deleted = false
  and (case when coalesce(nullif(dados->>'apuracaoId',''), nullif(dados->>'ApuracaoId',''),'') ~ '^[0-9]+$'
            then coalesce(nullif(dados->>'apuracaoId',''), nullif(dados->>'ApuracaoId',''))::bigint end) = @ApuracaoId
order by id;", new { TenantId = tenantId, ApuracaoId = apuracaoId }, ct)).ConfigureAwait(false);
        return rows.Select(r => new RhRegistroComOrigemDto(r.Id, r.Dados ?? "{}", r.ServidorId, r.CriadoPor, r.CriadoEm, r.Status)).ToList();
    }

    // ==== RC-EVO-RH §7: integração real da apuração homologada na folha (transação única) ====
    // Revalida sob lock a linha de integração existente, a folha de destino e a homologação da
    // apuração; materializa um folha_evento real e um folha_lancamento por rubrica (valor positivo;
    // o sinal vem do tipo) e fecha a linha de integração em PROCESSADA. A unicidade origem→destino
    // (tenant + apuracaoId + evento, índice ux_rh_ponto_integracao_origem) resolve o retry
    // concorrente por ON CONFLICT DO NOTHING + releitura do efeito vencedor; linha PROCESSADA devolve
    // o efeito anterior sem gravar de novo (JaProcessada); PENDENTE/FALHA/CANCELADA reaproveita a
    // linha; outro status bloqueia como INTEGRACAO_EM_ANDAMENTO. Sem sucesso parcial: tudo commita
    // ou tudo volta.
    public async Task<RhIntegracaoFolhaTx> IntegrarApuracaoNaFolhaAsync(long tenantId, long apuracaoId, long folhaId, long servidorId, DateOnly periodoInicio, DateOnly periodoFim, string versaoRegras, string resumoJson, string criticasJson, IReadOnlyList<RhLancamentoPontoPayload> lancamentos, long? usuarioId, CancellationToken ct)
    {
        using var cn = (NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await cn.ExecuteAsync(new CommandDefinition("select set_config('sigov.tenant_id', @TenantId, true);", new { TenantId = tenantId.ToString(CultureInfo.InvariantCulture) }, tx, cancellationToken: ct)).ConfigureAwait(false);

            const string sqlBuscaIntegracao = @"
select id, dados::text as Dados,
       coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'') as Status
from sigov.rh_ponto_integracao_folha
where tenant_id=@TenantId and is_deleted=false
  and coalesce(nullif(dados->>'evento',''), nullif(dados->>'Evento',''),'')=@Evento
  and coalesce(nullif(dados->>'apuracaoId',''), nullif(dados->>'ApuracaoId',''),'')=@ApuracaoIdText";
            var parametrosBusca = new { TenantId = tenantId, Evento = Sigov.Domain.Rh.FolhaRegras.EventoIntegracaoFolha, ApuracaoIdText = apuracaoId.ToString(CultureInfo.InvariantCulture) };
            var existente = await cn.QueryFirstOrDefaultAsync<IntegracaoFolhaRow>(new CommandDefinition(sqlBuscaIntegracao + " for update;", parametrosBusca, tx, cancellationToken: ct)).ConfigureAwait(false);

            var folha = await cn.QueryFirstOrDefaultAsync<FolhaLockRow>(new CommandDefinition(@"
select id, dados::text as Dados
from sigov.folha
where tenant_id=@TenantId and id=@FolhaId and is_deleted=false
for update;", new { TenantId = tenantId, FolhaId = folhaId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (folha is null)
            {
                throw new InvalidOperationException($"FOLHA_INDISPONIVEL: a folha {folhaId.ToString(CultureInfo.InvariantCulture)} não existe no momento da integração; verifique o destino e repita.");
            }
            if (Sigov.Domain.Rh.FolhaRegras.ValidarStatusFolha(JsonText(ParseJsonb(folha.Dados), "status", "Status")) is { } falhaFolha)
            {
                throw new InvalidOperationException(falhaFolha);
            }

            var homologadas = await cn.ExecuteScalarAsync<int>(new CommandDefinition(@"
select count(*)
from sigov.rh_ponto_apuracao
where tenant_id=@TenantId and id=@ApuracaoId and is_deleted=false
  and coalesce(nullif(dados->>'status',''), nullif(dados->>'Status',''), status,'')='HOMOLOGADA';",
                new { TenantId = tenantId, ApuracaoId = apuracaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (homologadas == 0)
            {
                throw new InvalidOperationException($"NAO_HOMOLOGADA: a apuração {apuracaoId.ToString(CultureInfo.InvariantCulture)} não está HOMOLOGADA no momento da integração; homologue antes de repetir.");
            }

            long integracaoId = 0;
            if (existente is not null)
            {
                if (!Sigov.Domain.Rh.FolhaRegras.StatusIntegracaoReutilizavel(existente.Status))
                {
                    throw new InvalidOperationException($"INTEGRACAO_EM_ANDAMENTO: a integração {existente.Id.ToString(CultureInfo.InvariantCulture)} desta apuração está com status '{existente.Status}'; verifique antes de tentar novamente.");
                }
                integracaoId = existente.Id;
                var efeitosPrevios = await LerEfeitoProcessadoAsync(cn, tx, tenantId, existente, ct).ConfigureAwait(false);
                if (efeitosPrevios is not null)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return efeitosPrevios;
                }
            }
            else
            {
                const string sqlInsereIntegracao = @"
insert into sigov.rh_ponto_integracao_folha
(tenant_id, servidor_id, competencia, periodo_inicio, periodo_fim, tipo, status, descricao, auditoria, dados, created_by, updated_by)
values
(@TenantId, @ServidorId, @Competencia, @PeriodoInicio, @PeriodoFim, 'PONTO_INTEGRACAO_FOLHA', 'PROCESSADA', @Descricao, '{}'::jsonb,
 jsonb_build_object('status','PROCESSADA','origem','PONTO','evento','INTEGRACAO_FOLHA','apuracaoId',@ApuracaoId,'folhaId',@FolhaId,'versaoRegras',@VersaoRegras,'periodoInicio',@PeriodoInicioText,'periodoFim',@PeriodoFimText),
 @UsuarioId, @UsuarioId)
on conflict (tenant_id, (dados ->> 'apuracaoId'::text), (dados ->> 'evento'::text)) where is_deleted = false do nothing
returning id;";
                var inserido = await cn.ExecuteScalarAsync<long>(new CommandDefinition(sqlInsereIntegracao, new
                {
                    TenantId = tenantId,
                    ServidorId = servidorId,
                    Competencia = periodoInicio,
                    PeriodoInicio = periodoInicio,
                    PeriodoFim = periodoFim,
                    Descricao = $"Apuração de ponto {apuracaoId.ToString(CultureInfo.InvariantCulture)} integrada à folha {folhaId.ToString(CultureInfo.InvariantCulture)}",
                    ApuracaoId = apuracaoId,
                    FolhaId = folhaId,
                    VersaoRegras = versaoRegras,
                    PeriodoInicioText = periodoInicio.ToString("yyyy-MM-dd"),
                    PeriodoFimText = periodoFim.ToString("yyyy-MM-dd"),
                    UsuarioId = usuarioId
                }, tx, cancellationToken: ct)).ConfigureAwait(false);
                if (inserido == 0)
                {
                    // Integração concorrente venceu no índice único: releia a linha commitada e devolva o efeito dela.
                    var vencedor = await cn.QueryFirstOrDefaultAsync<IntegracaoFolhaRow>(new CommandDefinition(sqlBuscaIntegracao + ";", parametrosBusca, tx, cancellationToken: ct)).ConfigureAwait(false);
                    if (vencedor is null || !Sigov.Domain.Rh.FolhaRegras.StatusIntegracaoReutilizavel(vencedor.Status))
                    {
                        throw new InvalidOperationException($"INTEGRACAO_EM_ANDAMENTO: outra integração desta apuração está em andamento ou com status '{vencedor?.Status ?? "?"}'; aguarde e repita.");
                    }
                    var efeitoVencedor = await LerEfeitoProcessadoAsync(cn, tx, tenantId, vencedor, ct).ConfigureAwait(false);
                    if (efeitoVencedor is null)
                    {
                        throw new InvalidOperationException("INTEGRACAO_EM_ANDAMENTO: outra integração desta apuração ainda está sendo materializada; aguarde alguns segundos e repita.");
                    }
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return efeitoVencedor;
                }
                integracaoId = inserido;
            }

            const string sqlInsereEventoFolha = @"
insert into sigov.folha_evento (tenant_id, dados, created_by)
values (@TenantId,
 jsonb_build_object('tipo','folha.integracao.ponto','origem','PONTO','apuracaoId',@ApuracaoId,'folhaId',@FolhaId,'servidorId',@ServidorId,'versaoRegras',@VersaoRegras,'periodoInicio',@PeriodoInicioText,'periodoFim',@PeriodoFimText,'resumoApuracao',cast(@Resumo as jsonb),'criticasNaoBloqueantes',cast(@Criticas as jsonb)),
 @UsuarioId)
returning id;";
            var eventoFolhaId = await cn.ExecuteScalarAsync<long>(new CommandDefinition(sqlInsereEventoFolha, new
            {
                TenantId = tenantId,
                ApuracaoId = apuracaoId,
                FolhaId = folhaId,
                ServidorId = servidorId,
                VersaoRegras = versaoRegras,
                PeriodoInicioText = periodoInicio.ToString("yyyy-MM-dd"),
                PeriodoFimText = periodoFim.ToString("yyyy-MM-dd"),
                Resumo = resumoJson,
                Criticas = criticasJson,
                UsuarioId = usuarioId
            }, tx, cancellationToken: ct)).ConfigureAwait(false);

            // Defesa em profundidade: remove resquício de lançamentos órfãos desta integração
            // (caso atípico — tentativas falhas voltam inteiramente).
            await cn.ExecuteAsync(new CommandDefinition(@"
update sigov.folha_lancamento
set is_deleted=true, deleted_at=now(), deleted_by=@UsuarioId
where tenant_id=@TenantId and is_deleted=false
  and coalesce(nullif(dados->>'integracaoId',''), nullif(dados->>'IntegracaoId',''),'')=@IntegracaoIdText;",
                new { TenantId = tenantId, UsuarioId = usuarioId, IntegracaoIdText = integracaoId.ToString(CultureInfo.InvariantCulture) }, tx, cancellationToken: ct)).ConfigureAwait(false);

            // Materializa os lançamentos reais (um por rubrica do catálogo; valor positivo, sinal definido pelo tipo).
            const string sqlInsereLancamento = @"
insert into sigov.folha_lancamento (tenant_id, dados, created_by)
values (@TenantId,
 jsonb_build_object('folhaId',@FolhaId,'servidorId',@ServidorId,'eventoId',@EventoId,'integracaoId',@IntegracaoId,'apuracaoId',@ApuracaoId,'versaoRegras',@VersaoRegras,'rubricaCodigo',@Codigo,'rubricaNome',@Nome,'tipo',@Tipo,'base',@Base,'unidade',@Unidade,'quantidadeBase',@Qtd,'valor',@Valor,'origem','PONTO'),
 @UsuarioId)
returning id;";
            var lancamentoIds = new List<long>(lancamentos.Count);
            foreach (var lancamento in lancamentos)
            {
                var unidade = string.Equals(lancamento.Base, Sigov.Domain.Rh.FolhaRegras.BaseDiasFalta, StringComparison.OrdinalIgnoreCase) ? "DIAS" : "MINUTOS";
                var idLancamento = await cn.ExecuteScalarAsync<long>(new CommandDefinition(sqlInsereLancamento, new
                {
                    TenantId = tenantId,
                    FolhaId = folhaId,
                    ServidorId = servidorId,
                    EventoId = eventoFolhaId,
                    IntegracaoId = integracaoId,
                    ApuracaoId = apuracaoId,
                    VersaoRegras = versaoRegras,
                    Codigo = lancamento.RubricaCodigo,
                    Nome = lancamento.RubricaNome,
                    Tipo = lancamento.Tipo,
                    Base = lancamento.Base,
                    Unidade = unidade,
                    Qtd = lancamento.QuantidadeBase,
                    Valor = lancamento.Valor,
                    UsuarioId = usuarioId
                }, tx, cancellationToken: ct)).ConfigureAwait(false);
                lancamentoIds.Add(idLancamento);
            }

            var dtos = lancamentos.Select((lancamento, indice) => new RhIntegracaoLancamentoDto(lancamentoIds[indice], lancamento.RubricaCodigo, lancamento.RubricaNome, lancamento.Tipo, lancamento.Base, lancamento.QuantidadeBase, lancamento.Valor)).ToList();
            var (proventos, descontos) = TotaisLancamentos(dtos);
            var delta = new Dictionary<string, object?>
            {
                ["status"] = Sigov.Domain.Rh.FolhaRegras.IntegracaoProcessada,
                ["origem"] = "PONTO",
                ["evento"] = Sigov.Domain.Rh.FolhaRegras.EventoIntegracaoFolha,
                ["apuracaoId"] = apuracaoId,
                ["folhaId"] = folhaId,
                ["versaoRegras"] = versaoRegras,
                ["eventoFolhaId"] = eventoFolhaId,
                ["lancamentoIds"] = lancamentoIds,
                ["totalProventos"] = proventos,
                ["totalDescontos"] = descontos,
                ["liquido"] = proventos - descontos,
                ["processadaEm"] = DateTimeOffset.UtcNow
            };
            var auditoriaJson = BuildAuditJson("INTEGRAR_FOLHA_PONTO", usuarioId, existente is null ? null : ParseJsonb(existente.Dados), new { integracaoId, eventoFolhaId, lancamentoIds });
            var linhasFinal = await cn.ExecuteAsync(new CommandDefinition(@"
update sigov.rh_ponto_integracao_folha
set status='PROCESSADA',
    auditoria=coalesce(auditoria,'{}'::jsonb)||cast(@Auditoria as jsonb),
    dados=dados||cast(@Delta as jsonb),
    updated_by=@UsuarioId,
    updated_at=now()
where tenant_id=@TenantId and id=@Id and is_deleted=false;",
                new { Auditoria = auditoriaJson, Delta = JsonSerializer.Serialize(delta, JsonOptions), UsuarioId = usuarioId, TenantId = tenantId, Id = integracaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (linhasFinal == 0)
            {
                throw new InvalidOperationException($"INTEGRACAO_INDISPONIVEL: a linha de integração {integracaoId.ToString(CultureInfo.InvariantCulture)} foi removida durante a operação; verifique o registro antes de repetir.");
            }

            await RegistrarEventoAsync(cn, tenantId, "ponto-integracoes-folha", "INTEGRAR_FOLHA", integracaoId, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["eventoFolhaId"] = eventoFolhaId,
                ["lancamentoIds"] = lancamentoIds,
                ["totalProventos"] = proventos,
                ["totalDescontos"] = descontos,
                ["jaProcessada"] = false
            }, usuarioId, ct).ConfigureAwait(false);

            await tx.CommitAsync(ct).ConfigureAwait(false);
            return new RhIntegracaoFolhaTx(integracaoId, eventoFolhaId, dtos, proventos, descontos, false);
        }
        catch
        {
            // Sem efeito colateral parcial: o dispose de `await using tx` faz rollback em qualquer exceção;
            // as falhas nomeadas (InvalidOperationException) sobem para o serviço, que converte em Result.
            throw;
        }
    }

    // ==== RC-EVO-RH §8: portal com escopo próprio ==============================================
    // O portal nunca aceita o servidor via payload: o vínculo vive em sigov.rh_portal_usuario
    // (usuario_id/servidor_id), ativo, válido e apontando para servidor ativo e não excluído.
    // Vínculo ausente → null explícita (sem sucesso simulado). Todos os listamentos filtram pelo
    // próprio servidor (coluna estruturada quando a tabela tem; JSONB sempre considerado).

    public async Task<RhPortalVinculoDto?> ServidorDoPortalAsync(long tenantId, long usuarioId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select v.servidor_id as ServidorId,
       coalesce(nullif(trim(s.dados->>'nome'), ''), nullif(trim(s.dados->>'Nome'), ''), 'Servidor') as Nome
from sigov.rh_portal_usuario v
join sigov.servidor s
  on s.id = v.servidor_id and s.tenant_id = v.tenant_id and s.ativo = true and s.is_deleted = false
where v.tenant_id = @TenantId and v.usuario_id = @UsuarioId and v.ativo = true and v.is_deleted = false
order by v.id
limit 1;";
        var linha = await cn.QueryFirstOrDefaultAsync<PortalVinculoRow>(Command(sql, new { TenantId = tenantId, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
        return linha is null ? null : new RhPortalVinculoDto(linha.ServidorId, linha.Nome);
    }

    public async Task<long> ContarCompetenciasPortalAsync(long tenantId, long servidorId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select count(1)
from sigov.rh_ponto_integracao_folha i
where i.tenant_id = @TenantId and i.is_deleted = false
  and (i.servidor_id = @ServidorId or coalesce(nullif(i.dados->>'servidorId', ''), nullif(i.dados->>'ServidorId', '')) = @ServidorIdTexto);";
        return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, ServidorId = servidorId, ServidorIdTexto = servidorId.ToString(CultureInfo.InvariantCulture) }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RhPortalCompetenciaFonte>> ListarCompetenciasPortalAsync(long tenantId, long servidorId, int limite, int offset, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select i.id as Id,
       coalesce(nullif(i.dados->>'status', ''), nullif(i.dados->>'Status', ''), i.status, '') as StatusIntegracao,
       i.dados::text as IntegraDados,
       f.id as FolhaId,
       f.dados::text as FolhaDados,
       a.id as ApuracaoId,
       coalesce(nullif(a.dados->>'status', ''), nullif(a.dados->>'Status', ''), a.status, '') as StatusApuracao,
       a.dados::text as ApuracaoDados,
       e.id as EventoFolhaId,
       e.dados::text as EventoDados,
       to_char(i.periodo_inicio, 'YYYY-MM-DD') as PeriodoInicio,
       to_char(i.periodo_fim, 'YYYY-MM-DD') as PeriodoFim,
       i.created_at as CreatedAt,
       coalesce(i.updated_at, i.created_at) as UpdatedAt
from sigov.rh_ponto_integracao_folha i
left join sigov.folha f
  on f.tenant_id = i.tenant_id and f.is_deleted = false
 and f.id = coalesce(nullif(i.dados->>'folhaId', ''), nullif(i.dados->>'FolhaId', ''))::bigint
left join sigov.rh_ponto_apuracao a
  on a.tenant_id = i.tenant_id and a.is_deleted = false
 and a.id = coalesce(nullif(i.dados->>'apuracaoId', ''), nullif(i.dados->>'ApuracaoId', ''))::bigint
left join sigov.folha_evento e
  on e.tenant_id = i.tenant_id and e.is_deleted = false
 and e.id = coalesce(nullif(i.dados->>'eventoFolhaId', ''), nullif(i.dados->>'EventoFolhaId', ''))::bigint
where i.tenant_id = @TenantId and i.is_deleted = false
  and (i.servidor_id = @ServidorId or coalesce(nullif(i.dados->>'servidorId', ''), nullif(i.dados->>'ServidorId', '')) = @ServidorIdTexto)
order by i.competencia desc nulls last, i.id desc
limit @Limite offset @Offset;";
        var linhas = (await cn.QueryAsync<PortalCompetenciaRow>(Command(sql, new
        {
            TenantId = tenantId,
            ServidorId = servidorId,
            ServidorIdTexto = servidorId.ToString(CultureInfo.InvariantCulture),
            Limite = Math.Max(1, limite),
            Offset = Math.Max(0, offset)
        }, ct)).ConfigureAwait(false)).ToList();
        var resultado = new List<RhPortalCompetenciaFonte>(linhas.Count);
        foreach (var linha in linhas)
        {
            var integracao = ParseJsonb(linha.IntegraDados);
            var folha = ParseJsonb(linha.FolhaDados);
            resultado.Add(new RhPortalCompetenciaFonte(
                IntegracaoId: linha.Id,
                StatusIntegracao: linha.StatusIntegracao,
                ProcessadaEm: TryJsonDateTimeOffset(integracao, out var processadaEm, "processadaEm", "ProcessadaEm") ? processadaEm : null,
                FolhaId: linha.FolhaId,
                StatusFolha: JsonText(folha, "status", "Status"),
                AnoFolha: TryJsonInt(folha, out var anoFolha, "ano", "Ano") ? anoFolha : null,
                MesFolha: TryJsonInt(folha, out var mesFolha, "mes", "Mes") ? mesFolha : null,
                ApuracaoId: linha.ApuracaoId,
                StatusApuracao: linha.StatusApuracao,
                EventoFolhaId: linha.EventoFolhaId,
                VersaoRegras: JsonText(integracao, "versaoRegras", "VersaoRegras"),
                PeriodoInicio: DateOnly.TryParse(linha.PeriodoInicio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var periodoInicio) ? periodoInicio : null,
                PeriodoFim: DateOnly.TryParse(linha.PeriodoFim, CultureInfo.InvariantCulture, DateTimeStyles.None, out var periodoFim) ? periodoFim : null,
                ApuracaoDadosJson: linha.ApuracaoDados,
                CriticasNaoBloqueantes: ExtrairCriticasNaoBloqueantes(ParseJsonb(linha.EventoDados)),
                Lancamentos: await CarregarLancamentosDoPortalAsync(cn, tenantId, linha.Id, ct).ConfigureAwait(false),
                CriadoEm: linha.CreatedAt,
                AtualizadoEm: linha.UpdatedAt));
        }
        return resultado;
    }

    public async Task<IReadOnlyList<RhPortalPendenciaItem>> ListarPendenciasPortalAsync(long tenantId, long servidorId, int limite, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
select 'APURACAO' as Tipo, p.id as Id,
       coalesce(nullif(p.dados->>'status', ''), nullif(p.dados->>'Status', ''), p.status, '') as Status,
       'Apuracao de ponto: periodo ' || coalesce(to_char(p.periodo_inicio, 'YYYY-MM-DD'), '?') || ' a ' || coalesce(to_char(p.periodo_fim, 'YYYY-MM-DD'), '?') as Descricao,
       coalesce(p.updated_at, p.created_at)::timestamptz as Em
from sigov.rh_ponto_apuracao p
where p.tenant_id = @TenantId and p.is_deleted = false
  and (p.servidor_id = @ServidorId or coalesce(nullif(p.dados->>'servidorId', ''), nullif(p.dados->>'ServidorId', '')) = @ServidorIdTexto)
union all
select 'JUSTIFICATIVA'::varchar, j.id,
       coalesce(nullif(j.dados->>'status', ''), nullif(j.dados->>'Status', ''), j.status, ''),
       'Justificativa de ponto em ' || coalesce(to_char(j.data_referencia, 'YYYY-MM-DD'), '?') || ': ' || coalesce(btrim(j.motivo), 'sem motivo'),
       coalesce(j.updated_at, j.created_at)::timestamptz
from sigov.rh_ponto_justificativa j
where j.tenant_id = @TenantId and j.is_deleted = false
  and (j.servidor_id = @ServidorId or coalesce(nullif(j.dados->>'servidorId', ''), nullif(j.dados->>'ServidorId', '')) = @ServidorIdTexto)
union all
select 'INTEGRACAO_FOLHA'::varchar, i.id,
       coalesce(nullif(i.dados->>'status', ''), nullif(i.dados->>'Status', ''), i.status, ''),
       'Integracao de ponto na folha ' || coalesce(nullif(i.dados->>'folhaId', ''), nullif(i.dados->>'FolhaId', ''), '?') || ' (status ' || i.status || ')',
       coalesce(i.updated_at, i.created_at)::timestamptz
from sigov.rh_ponto_integracao_folha i
where i.tenant_id = @TenantId and i.is_deleted = false
  and (i.servidor_id = @ServidorId or coalesce(nullif(i.dados->>'servidorId', ''), nullif(i.dados->>'ServidorId', '')) = @ServidorIdTexto)
order by Em desc
limit @Limite;";
        return (await cn.QueryAsync<RhPortalPendenciaItem>(Command(sql, new
        {
            TenantId = tenantId,
            ServidorId = servidorId,
            ServidorIdTexto = servidorId.ToString(CultureInfo.InvariantCulture),
            Limite = Math.Max(1, limite)
        }, ct)).ConfigureAwait(false)).ToList();
    }

    public async Task<PagedResult<RhRegistroResponse>> ListarPorServidorAsync(long tenantId, string recurso, long servidorId, RhFiltro filtro, CancellationToken ct)
    {
        var table = Table(recurso);
        var page = Math.Max(1, filtro.Page);
        var pageSize = Math.Clamp(filtro.PageSize, 1, 100);
        var escopo = RecursosComColunaServidorId.Contains(recurso)
            ? "(servidor_id = @ServidorId or coalesce(nullif(dados->>'servidorId', ''), nullif(dados->>'ServidorId', '')) = @ServidorIdTexto)"
            : "coalesce(nullif(dados->>'servidorId', ''), nullif(dados->>'ServidorId', '')) = @ServidorIdTexto";
        var where = new StringBuilder("tenant_id = @TenantId and is_deleted = false");
        if (filtro.Ativo.HasValue) where.Append(" and ativo = @Ativo");
        if (!string.IsNullOrWhiteSpace(filtro.Termo)) where.Append(" and dados::text ilike @Termo");
        where.Append(" and ").Append(escopo);
        var parameters = new
        {
            TenantId = tenantId,
            ServidorId = servidorId,
            ServidorIdTexto = servidorId.ToString(CultureInfo.InvariantCulture),
            filtro.Ativo,
            Termo = $"%{filtro.Termo}%",
            Limit = pageSize,
            Offset = (page - 1) * pageSize
        };
        using var cn = _context.CreateConnection();
        var total = await cn.ExecuteScalarAsync<long>(Command($"select count(1) from {table} where {where};", parameters, ct)).ConfigureAwait(false);
        var rows = await cn.QueryAsync<Row>(Command($"select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from {table} where {where} order by id desc limit @Limit offset @Offset;", parameters, ct)).ConfigureAwait(false);
        return new PagedResult<RhRegistroResponse>(rows.Select(r => ToResponse(recurso, r)).ToArray(), page, pageSize, total);
    }

    private async Task<List<RhIntegracaoLancamentoDto>> CarregarLancamentosDoPortalAsync(System.Data.IDbConnection cn, long tenantId, long integracaoId, CancellationToken ct)
    {
        const string sql = @"
select id, dados::text as Dados
from sigov.folha_lancamento
where tenant_id=@TenantId and is_deleted=false
  and coalesce(nullif(dados->>'integracaoId', ''), nullif(dados->>'IntegracaoId', ''), '')=@IntegracaoIdTexto
order by id;";
        var rows = await cn.QueryAsync<LancamentoRow>(Command(sql, new { TenantId = tenantId, IntegracaoIdTexto = integracaoId.ToString(CultureInfo.InvariantCulture) }, ct)).ConfigureAwait(false);
        var lista = new List<RhIntegracaoLancamentoDto>();
        foreach (var row in rows)
        {
            var dados = ParseJsonb(row.Dados);
            lista.Add(new RhIntegracaoLancamentoDto(row.Id,
                JsonText(dados, "rubricaCodigo", "RubricaCodigo") ?? string.Empty,
                JsonText(dados, "rubricaNome", "RubricaNome") ?? string.Empty,
                JsonText(dados, "tipo", "Tipo") ?? string.Empty,
                JsonText(dados, "base", "Base") ?? string.Empty,
                TryJsonInt(dados, out var quantidade, "quantidadeBase", "QuantidadeBase") ? quantidade : 0,
                TryJsonDecimal(dados, out var valor, "valor", "Valor") ? valor : 0m));
        }
        return lista;
    }

    private static IReadOnlyList<string>? ExtrairCriticasNaoBloqueantes(Dictionary<string, object?> eventoDados)
    {
        foreach (var chave in new[] { "criticasNaoBloqueantes", "CriticasNaoBloqueantes" })
        {
            if (!eventoDados.TryGetValue(chave, out var raw) || raw is not JsonElement { ValueKind: JsonValueKind.Array } array) continue;
            var itens = new List<string>();
            foreach (var item in array.EnumerateArray())
            {
                var texto = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText();
                if (!string.IsNullOrWhiteSpace(texto)) itens.Add(texto);
            }
            return itens;
        }
        return null;
    }

    private async Task<RhIntegracaoFolhaTx?> LerEfeitoProcessadoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long tenantId, IntegracaoFolhaRow linha, CancellationToken ct)
    {
        if (!string.Equals(linha.Status, Sigov.Domain.Rh.FolhaRegras.IntegracaoProcessada, StringComparison.OrdinalIgnoreCase)) return null;
        var lancamentos = await CarregarLancamentosDaIntegracaoAsync(cn, tx, tenantId, linha.Id, ct).ConfigureAwait(false);
        if (lancamentos.Count == 0) return null;
        var (proventos, descontos) = TotaisLancamentos(lancamentos);
        long? eventoId = null;
        if (TryJsonLong(ParseJsonb(linha.Dados), out var eventoIdGravado, "eventoFolhaId", "EventoFolhaId")) eventoId = eventoIdGravado;
        return new RhIntegracaoFolhaTx(linha.Id, eventoId, lancamentos, proventos, descontos, true);
    }

    private async Task<List<RhIntegracaoLancamentoDto>> CarregarLancamentosDaIntegracaoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long tenantId, long integracaoId, CancellationToken ct)
    {
        const string sql = @"
select id, dados::text as Dados
from sigov.folha_lancamento
where tenant_id=@TenantId and is_deleted=false
  and coalesce(nullif(dados->>'integracaoId',''), nullif(dados->>'IntegracaoId',''),'')=@IntegracaoIdText
order by id;";
        var rows = (await cn.QueryAsync<LancamentoRow>(new CommandDefinition(sql, new { TenantId = tenantId, IntegracaoIdText = integracaoId.ToString(CultureInfo.InvariantCulture) }, tx, cancellationToken: ct)).ConfigureAwait(false)).ToList();
        var lista = new List<RhIntegracaoLancamentoDto>(rows.Count);
        foreach (var row in rows)
        {
            var dados = ParseJsonb(row.Dados);
            var quantidade = TryJsonInt(dados, out var qtd, "quantidadeBase", "QuantidadeBase") ? qtd : 0;
            var valor = TryJsonDecimal(dados, out var val, "valor", "Valor") ? val : 0m;
            lista.Add(new RhIntegracaoLancamentoDto(row.Id,
                JsonText(dados, "rubricaCodigo", "RubricaCodigo") ?? string.Empty,
                JsonText(dados, "rubricaNome", "RubricaNome") ?? string.Empty,
                (JsonText(dados, "tipo", "Tipo") ?? string.Empty).ToUpperInvariant(),
                (JsonText(dados, "base", "Base") ?? string.Empty).ToUpperInvariant(),
                quantidade,
                valor));
        }
        return lista;
    }

    private static (decimal Proventos, decimal Descontos) TotaisLancamentos(IReadOnlyList<RhIntegracaoLancamentoDto> lancamentos)
    {
        decimal proventos = 0m;
        decimal descontos = 0m;
        foreach (var lancamento in lancamentos)
        {
            if (string.Equals(lancamento.Tipo, Sigov.Domain.Rh.FolhaRegras.RubricaProvento, StringComparison.OrdinalIgnoreCase)) proventos += lancamento.Valor;
            else if (string.Equals(lancamento.Tipo, Sigov.Domain.Rh.FolhaRegras.RubricaDesconto, StringComparison.OrdinalIgnoreCase)) descontos += lancamento.Valor;
        }
        return (proventos, descontos);
    }

    private sealed class IntegracaoFolhaRow
    {
        public long Id { get; init; }
        public string Dados { get; init; } = "{}";
        public string Status { get; init; } = string.Empty;
    }

    private sealed class FolhaLockRow
    {
        public long Id { get; init; }
        public string Dados { get; init; } = "{}";
    }

    private sealed class LancamentoRow
    {
        public long Id { get; init; }
        public string Dados { get; init; } = "{}";
    }

    private sealed class PortalVinculoRow
    {
        public long ServidorId { get; init; }
        public string Nome { get; init; } = "Servidor";
    }

    private sealed class PortalCompetenciaRow
    {
        public long Id { get; init; }
        public string StatusIntegracao { get; init; } = string.Empty;
        public string IntegraDados { get; init; } = "{}";
        public long? FolhaId { get; init; }
        public string? FolhaDados { get; init; }
        public long? ApuracaoId { get; init; }
        public string StatusApuracao { get; init; } = string.Empty;
        public string? ApuracaoDados { get; init; }
        public long? EventoFolhaId { get; init; }
        public string? EventoDados { get; init; }
        public string? PeriodoInicio { get; init; }
        public string? PeriodoFim { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }

    // §8: recursos cuja tabela possui coluna estruturada `servidor_id` (sigov.afastamento NÃO tem).
    private static readonly HashSet<string> RecursosComColunaServidorId = new(StringComparer.OrdinalIgnoreCase)
    {
        "ponto-registros", "ponto-justificativas", "ponto-apuracoes", "ponto-integracoes-folha", "ferias-programacoes"
    };

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
