using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Common;
using Sigov.Application.Saude;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Saude;

public sealed class SaudeRepository : BaseRepository, ISaudeCrudRepository, IUnidadeSaudeRepository, IProfissionalSaudeRepository, IPacienteRepository, IProntuarioRepository, IAtendimentoSaudeRepository, IAgendaSaudeRepository, IFarmaciaRepository, IVacinacaoRepository, ILaboratorioRepository, IRegulacaoRepository, IAcsRepository, IAcsSyncRepository, ISaudeDashboardRepository, ISaudeExportacaoRepository, ISaudeSequencialService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DapperContext _context;
    public SaudeRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<T>> ListarAsync<T>(long tenantId, long entidadeId, long? exercicioId, string recurso, object filtro, CancellationToken ct)
    {
        var (page, pageSize, limit, offset) = Page(filtro);
        var where = $"tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false{ExtraWhere(recurso, filtro)}";
        var sql = $"select {Select(recurso)} from sigov.{Table(recurso)} where {where} order by id desc limit @Limit offset @Offset; select count(*) from sigov.{Table(recurso)} where {where};";
        using var connection = _context.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(Command(sql, Params(tenantId, entidadeId, exercicioId, filtro, new { Limit = limit, Offset = offset }), ct)).ConfigureAwait(false);
        var items = (await multi.ReadAsync<T>().ConfigureAwait(false)).AsList();
        var total = await multi.ReadFirstAsync<long>().ConfigureAwait(false);
        return new PagedResult<T>(items, page, pageSize, total);
    }

    public async Task<T?> ObterAsync<T>(long tenantId, long entidadeId, long? exercicioId, string recurso, long id, CancellationToken ct)
    {
        if (typeof(T) == typeof(object)) return default;
        var byPaciente = recurso == "prontuario_paciente";
        var idColumn = byPaciente ? "paciente_id" : "id";
        var sql = $"select {Select(recurso)} from sigov.{Table(recurso)} where tenant_id=@TenantId and entidade_id=@EntidadeId and {idColumn}=@Id and is_deleted=false limit 1;";
        using var connection = _context.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<T>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Id = id }, ct)).ConfigureAwait(false);
    }

    public async Task<long> CriarAsync(long tenantId, long entidadeId, long? exercicioId, string recurso, object request, long? usuarioId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var p = ToDictionary(request);
            p["TenantId"] = tenantId; p["EntidadeId"] = entidadeId; p["ExercicioId"] = exercicioId; p["UsuarioId"] = usuarioId; p["Ano"] = DateTimeOffset.UtcNow.Year;
            ApplyDefaults(recurso, p);
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(InsertSql(recurso), p, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (recurso == "paciente") await EnsureProntuarioAsync(connection, tx, p, id, ct).ConfigureAwait(false);
            if (recurso == "farmacia_dispensacao") await BaixarEstoqueAsync(connection, tx, p, ct).ConfigureAwait(false);
            if (recurso == "acs_sync") await InserirItensSyncAsync(connection, tx, p, id, ct).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, tx, tenantId, entidadeId, Evento(recurso), recurso, id, p, usuarioId, ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return id;
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task AtualizarAsync(long tenantId, long entidadeId, long? exercicioId, string recurso, long id, object request, long? usuarioId, CancellationToken ct)
    {
        var p = ToDictionary(request); p["TenantId"] = tenantId; p["EntidadeId"] = entidadeId; p["ExercicioId"] = exercicioId; p["Id"] = id; p["UsuarioId"] = usuarioId;
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(UpdateSql(recurso), p, tx, cancellationToken: ct)).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, tx, tenantId, entidadeId, Evento(recurso), recurso, id, p, usuarioId, ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task ExcluirAsync(long tenantId, long entidadeId, string recurso, long id, long? usuarioId, CancellationToken ct)
    {
        var sql = $"update sigov.{Table(recurso)} set is_deleted=true, ativo=false, deleted_at=now(), deleted_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id;";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<SaudeDashboardResponse> DashboardAsync(long tenantId, long entidadeId, CancellationToken ct)
    {
        const string sql = """
            select
              (select count(*) from sigov.unidade_saude where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as TotalUnidades,
              (select count(*) from sigov.profissional_saude where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as TotalProfissionais,
              (select count(*) from sigov.paciente where tenant_id=@TenantId and entidade_id=@EntidadeId and situacao='ATIVO' and is_deleted=false) as TotalPacientesAtivos,
              (select count(*) from sigov.atendimento_saude where tenant_id=@TenantId and entidade_id=@EntidadeId and data_atendimento::date=current_date and is_deleted=false) as AtendimentosHoje,
              (select count(*) from sigov.atendimento_saude where tenant_id=@TenantId and entidade_id=@EntidadeId and data_atendimento>=date_trunc('month', now()) and is_deleted=false) as AtendimentosMes,
              (select count(*) from sigov.agenda_saude where tenant_id=@TenantId and entidade_id=@EntidadeId and data_inicio::date=current_date and is_deleted=false) as AgendaHoje,
              (select count(*) from sigov.farmacia_dispensacao where tenant_id=@TenantId and entidade_id=@EntidadeId and data_dispensacao>=date_trunc('month', now()) and is_deleted=false) as DispensacoesMes,
              (select count(*) from sigov.farmacia_estoque where tenant_id=@TenantId and entidade_id=@EntidadeId and quantidade<=10) as EstoqueBaixo,
              (select count(*) from sigov.vacinacao where tenant_id=@TenantId and entidade_id=@EntidadeId and data_aplicacao>=date_trunc('month', current_date)::date and is_deleted=false) as VacinacoesMes,
              (select count(*) from sigov.laboratorio_exame where tenant_id=@TenantId and entidade_id=@EntidadeId and status in ('SOLICITADO','COLETADO') and is_deleted=false) as ExamesPendentes,
              (select count(*) from sigov.regulacao_solicitacao where tenant_id=@TenantId and entidade_id=@EntidadeId and status in ('SOLICITADA','EM_ANALISE') and is_deleted=false) as RegulacoesPendentes,
              (select count(*) from sigov.acs_microarea where tenant_id=@TenantId and entidade_id=@EntidadeId and ativo=true and is_deleted=false) as MicroareasAtivas,
              (select count(*) from sigov.acs_cadastro_domiciliar where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as DomiciliosCadastrados,
              (select count(*) from sigov.acs_cadastro_individual where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as IndividuosCadastrados,
              (select count(*) from sigov.acs_visita where tenant_id=@TenantId and entidade_id=@EntidadeId and data_visita>=date_trunc('month', now()) and is_deleted=false) as VisitasAcsMes,
              (select count(*) from sigov.acs_sync_lote where tenant_id=@TenantId and entidade_id=@EntidadeId and status='RECEBIDO' and is_deleted=false) as SyncsPendentes;
            """;
        using var connection = _context.CreateConnection();
        var row = await connection.QueryFirstAsync(sql, new { TenantId = tenantId, EntidadeId = entidadeId }).ConfigureAwait(false);
        return new SaudeDashboardResponse((long)row.totalunidades, (long)row.totalprofissionais, (long)row.totalpacientesativos, (long)row.atendimentoshoje, (long)row.atendimentosmes, (long)row.agendahoje, (long)row.dispensacoesmes, (long)row.estoquebaixo, (long)row.vacinacoesmes, (long)row.examespendentes, (long)row.regulacoespendentes, (long)row.microareasativas, (long)row.domicilioscadastrados, (long)row.individuoscadastrados, (long)row.visitasacsmes, (long)row.syncspendentes, Array.Empty<object>(), Array.Empty<object>(), new[] { "Saúde/ACS base operacional carregada." });
    }

    public async Task<byte[]> ExportarAsync(long tenantId, long entidadeId, string recurso, string formato, CancellationToken ct)
    {
        var table = recurso switch { "pacientes" => "paciente", "atendimentos" => "atendimento_saude", "acs-visitas" => "acs_visita", "farmacia" => "farmacia_dispensacao", _ => "paciente" };
        var sql = $"select row_to_json(x) from (select * from sigov.{table} where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false order by id desc limit 1000) x";
        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<string>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId }, ct)).ConfigureAwait(false)).AsList();
        if (formato.Equals("json", StringComparison.OrdinalIgnoreCase)) return Encoding.UTF8.GetBytes("[" + string.Join(',', rows) + "]");
        var csv = new StringBuilder("dados\n"); foreach (var row in rows) csv.Append('"').Append(row.Replace("\"", "\"\"", StringComparison.Ordinal)).AppendLine("\"");
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<AcsSyncLoteResponse?> ObterSyncAsync(long tenantId, long entidadeId, string loteId, CancellationToken ct)
    {
        const string loteSql = "select lote_id as LoteId, status as Status, total_itens as TotalItens, total_processados as TotalProcessados, total_erros as TotalErros from sigov.acs_sync_lote where tenant_id=@TenantId and entidade_id=@EntidadeId and lote_id=@LoteId and is_deleted=false limit 1;";
        const string itemSql = "select tipo_item as TipoItem, offline_id as OfflineId, status as Status, erro as Erro from sigov.acs_sync_item where tenant_id=@TenantId and entidade_id=@EntidadeId and acs_sync_lote_id=(select id from sigov.acs_sync_lote where tenant_id=@TenantId and entidade_id=@EntidadeId and lote_id=@LoteId limit 1) and is_deleted=false;";
        using var connection = _context.CreateConnection();
        var lote = await connection.QueryFirstOrDefaultAsync<AcsSyncLoteResponse>(Command(loteSql, new { TenantId = tenantId, EntidadeId = entidadeId, LoteId = loteId }, ct)).ConfigureAwait(false);
        if (lote is null) return null;
        var itens = (await connection.QueryAsync<AcsSyncItemResponse>(Command(itemSql, new { TenantId = tenantId, EntidadeId = entidadeId, LoteId = loteId }, ct)).ConfigureAwait(false)).AsList();
        return lote with { Itens = itens };
    }

    public Task<string> ProximoAsync(string prefixo, int ano, CancellationToken ct) { _ = ct; return Task.FromResult($"{prefixo}-{ano}-000001"); }

    private static string Select(string recurso) => recurso switch
    {
        "unidade" => "id as Id,codigo as Codigo,nome as Nome,tipo_unidade as TipoUnidade,situacao as Situacao,cnes as Cnes,ativo as Ativo",
        "profissional" => "id as Id,pessoa_id as PessoaId,codigo_profissional as CodigoProfissional,tipo_profissional as TipoProfissional,situacao as Situacao,unidade_saude_id as UnidadeSaudeId",
        "paciente" => "id as Id,pessoa_id as PessoaId,codigo_paciente as CodigoPaciente,cartao_sus as CartaoSus,prontuario_numero as ProntuarioNumero,grupo_sanguineo as GrupoSanguineo,alergias as Alergias,situacao as Situacao",
        "prontuario_paciente" => "id as Id,paciente_id as PacienteId,numero as Numero,resumo_clinico as ResumoClinico,alergias as Alergias,ultimo_atendimento_at as UltimoAtendimentoAt",
        "atendimento" => "id as Id,numero as Numero,unidade_saude_id as UnidadeSaudeId,paciente_id as PacienteId,data_atendimento as DataAtendimento,tipo_atendimento as TipoAtendimento,status as Status",
        "agenda" => "id as Id,unidade_saude_id as UnidadeSaudeId,data_inicio as DataInicio,data_fim as DataFim,tipo_agendamento as TipoAgendamento,status as Status",
        "farmacia_produto" => "id as Id,codigo as Codigo,nome as Nome,unidade_medida as UnidadeMedida,ativo as Ativo",
        "farmacia_estoque" => "id as Id,unidade_saude_id as UnidadeSaudeId,farmacia_produto_id as FarmaciaProdutoId,lote as Lote,validade as Validade,quantidade as Quantidade",
        "vacinacao" => "id as Id,paciente_id as PacienteId,vacina as Vacina,dose as Dose,data_aplicacao as DataAplicacao",
        "laboratorio" => "id as Id,paciente_id as PacienteId,tipo_exame as TipoExame,status as Status,data_solicitacao as DataSolicitacao,data_resultado as DataResultado",
        "regulacao" => "id as Id,paciente_id as PacienteId,tipo_solicitacao as TipoSolicitacao,prioridade as Prioridade,status as Status,data_solicitacao as DataSolicitacao",
        "acs_microarea" => "id as Id,codigo as Codigo,nome as Nome,ativo as Ativo",
        "acs_domicilio" => "id as Id,codigo_domicilio as CodigoDomicilio,latitude as Latitude,longitude as Longitude,status as Status",
        "acs_individuo" => "id as Id,pessoa_id as PessoaId,paciente_id as PacienteId,status as Status",
        "acs_visita" => "id as Id,profissional_acs_id as ProfissionalAcsId,data_visita as DataVisita,tipo_visita as TipoVisita,desfecho as Desfecho",
        _ => "id as Id"
    };
    private static string Table(string recurso) => recurso switch { "unidade" => "unidade_saude", "profissional" => "profissional_saude", "prontuario_paciente" => "prontuario", "atendimento" or "atendimento_conduta" or "atendimento_cancelar" => "atendimento_saude", "agenda" or "agenda_cancelar" => "agenda_saude", "laboratorio" or "laboratorio_resultado" => "laboratorio_exame", "regulacao" or "regulacao_status" => "regulacao_solicitacao", "acs_domicilio" => "acs_cadastro_domiciliar", "acs_individuo" => "acs_cadastro_individual", _ => recurso };
    private static string ExtraWhere(string recurso, object filtro) { var d = ToDictionary(filtro); var sb = new StringBuilder(); if (d.TryGetValue("Termo", out var termo) && !string.IsNullOrWhiteSpace(Convert.ToString(termo, System.Globalization.CultureInfo.InvariantCulture))) sb.Append(" and id is not null"); if (d.TryGetValue("Situacao", out var s) && !string.IsNullOrWhiteSpace(Convert.ToString(s, System.Globalization.CultureInfo.InvariantCulture))) sb.Append(" and situacao=@Situacao"); if (d.TryGetValue("Status", out var st) && !string.IsNullOrWhiteSpace(Convert.ToString(st, System.Globalization.CultureInfo.InvariantCulture))) sb.Append(" and status=@Status"); if (d.TryGetValue("PacienteId", out var pac) && pac is not null) sb.Append(" and paciente_id=@PacienteId"); if (d.TryGetValue("UnidadeSaudeId", out var uni) && uni is not null) sb.Append(" and unidade_saude_id=@UnidadeSaudeId"); _ = recurso; return sb.ToString(); }

    private static string InsertSql(string recurso) => recurso switch
    {
        "unidade" => "insert into sigov.unidade_saude(tenant_id,entidade_id,codigo,nome,cnes,tipo_unidade,situacao,latitude,longitude,observacao,created_by) values(@TenantId,@EntidadeId,@Codigo,@Nome,@Cnes,@TipoUnidade,@Situacao,@Latitude,@Longitude,@Observacao,@UsuarioId) returning id",
        "profissional" => "insert into sigov.profissional_saude(tenant_id,entidade_id,pessoa_id,servidor_id,unidade_saude_id,codigo_profissional,cbo,conselho_classe,numero_conselho,uf_conselho,tipo_profissional,situacao,created_by) values(@TenantId,@EntidadeId,@PessoaId,@ServidorId,@UnidadeSaudeId,@CodigoProfissional,@Cbo,@ConselhoClasse,@NumeroConselho,@UfConselho,@TipoProfissional,@Situacao,@UsuarioId) returning id",
        "paciente" => "insert into sigov.paciente(tenant_id,entidade_id,pessoa_id,codigo_paciente,cartao_sus,prontuario_numero,grupo_sanguineo,alergias,dados_sensiveis_json,situacao,created_by) values(@TenantId,@EntidadeId,@PessoaId,@CodigoPaciente,@CartaoSus,@ProntuarioNumero,@GrupoSanguineo,@Alergias,@DadosSensiveisJson::jsonb,@Situacao,@UsuarioId) returning id",
        "atendimento" => "insert into sigov.atendimento_saude(tenant_id,entidade_id,exercicio_id,unidade_saude_id,paciente_id,profissional_saude_id,numero,tipo_atendimento,classificacao_risco,queixa_principal,status,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@UnidadeSaudeId,@PacienteId,@ProfissionalSaudeId,@Numero,@TipoAtendimento,@ClassificacaoRisco,@QueixaPrincipal,'AGENDADO',@UsuarioId) returning id",
        "agenda" => "insert into sigov.agenda_saude(tenant_id,entidade_id,exercicio_id,unidade_saude_id,profissional_saude_id,paciente_id,data_inicio,data_fim,tipo_agendamento,status,observacao,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@UnidadeSaudeId,@ProfissionalSaudeId,@PacienteId,@DataInicio,@DataFim,@TipoAgendamento,@Status,@Observacao,@UsuarioId) returning id",
        "farmacia_produto" => "insert into sigov.farmacia_produto(tenant_id,entidade_id,codigo,nome,principio_ativo,concentracao,forma_farmaceutica,unidade_medida,controla_lote,medicamento_controlado,created_by) values(@TenantId,@EntidadeId,@Codigo,@Nome,@PrincipioAtivo,@Concentracao,@FormaFarmaceutica,@UnidadeMedida,@ControlaLote,@MedicamentoControlado,@UsuarioId) returning id",
        "farmacia_dispensacao" => "insert into sigov.farmacia_dispensacao(tenant_id,entidade_id,exercicio_id,unidade_saude_id,paciente_id,farmacia_produto_id,profissional_saude_id,quantidade,lote,observacao,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@UnidadeSaudeId,@PacienteId,@FarmaciaProdutoId,@ProfissionalSaudeId,@Quantidade,@Lote,@Observacao,@UsuarioId) returning id",
        "vacinacao" => "insert into sigov.vacinacao(tenant_id,entidade_id,exercicio_id,unidade_saude_id,paciente_id,profissional_saude_id,vacina,dose,lote,data_aplicacao,fabricante,observacao,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@UnidadeSaudeId,@PacienteId,@ProfissionalSaudeId,@Vacina,@Dose,@Lote,@DataAplicacao,@Fabricante,@Observacao,@UsuarioId) returning id",
        "laboratorio" => "insert into sigov.laboratorio_exame(tenant_id,entidade_id,exercicio_id,paciente_id,unidade_saude_id,profissional_solicitante_id,tipo_exame,status,observacao,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@PacienteId,@UnidadeSaudeId,@ProfissionalSolicitanteId,@TipoExame,'SOLICITADO',@Observacao,@UsuarioId) returning id",
        "regulacao" => "insert into sigov.regulacao_solicitacao(tenant_id,entidade_id,exercicio_id,paciente_id,unidade_origem_id,tipo_solicitacao,especialidade,prioridade,justificativa,status,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@PacienteId,@UnidadeOrigemId,@TipoSolicitacao,@Especialidade,@Prioridade,@Justificativa,'SOLICITADA',@UsuarioId) returning id",
        "acs_microarea" => "insert into sigov.acs_microarea(tenant_id,entidade_id,unidade_saude_id,codigo,nome,profissional_acs_id,poligono_geojson,created_by) values(@TenantId,@EntidadeId,@UnidadeSaudeId,@Codigo,@Nome,@ProfissionalAcsId,@PoligonoGeoJson::jsonb,@UsuarioId) returning id",
        "acs_dispositivo" => "insert into sigov.acs_dispositivo(tenant_id,entidade_id,profissional_acs_id,identificador,modelo,plataforma,status,created_by) values(@TenantId,@EntidadeId,@ProfissionalAcsId,@Identificador,@Modelo,@Plataforma,@Status,@UsuarioId) returning id",
        "acs_domicilio" => "insert into sigov.acs_cadastro_domiciliar(tenant_id,entidade_id,acs_microarea_id,codigo_domicilio,endereco_json,condicoes_moradia_json,latitude,longitude,precisao_metros,status,created_by) values(@TenantId,@EntidadeId,@AcsMicroareaId,@CodigoDomicilio,@EnderecoJson::jsonb,@CondicoesMoradiaJson::jsonb,@Latitude,@Longitude,@PrecisaoMetros,@Status,@UsuarioId) returning id",
        "acs_individuo" => "insert into sigov.acs_cadastro_individual(tenant_id,entidade_id,acs_cadastro_domiciliar_id,paciente_id,pessoa_id,condicoes_saude_json,vulnerabilidades_json,status,created_by) values(@TenantId,@EntidadeId,@AcsCadastroDomiciliarId,@PacienteId,@PessoaId,@CondicoesSaudeJson::jsonb,@VulnerabilidadesJson::jsonb,@Status,@UsuarioId) returning id",
        "acs_visita" => "insert into sigov.acs_visita(tenant_id,entidade_id,exercicio_id,profissional_acs_id,acs_cadastro_domiciliar_id,acs_cadastro_individual_id,paciente_id,data_visita,tipo_visita,desfecho,observacao,latitude,longitude,precisao_metros,offline_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@ProfissionalAcsId,@AcsCadastroDomiciliarId,@AcsCadastroIndividualId,@PacienteId,@DataVisita,@TipoVisita,@Desfecho,@Observacao,@Latitude,@Longitude,@PrecisaoMetros,@OfflineId,@UsuarioId) returning id",
        "acs_sync" => "insert into sigov.acs_sync_lote(tenant_id,entidade_id,acs_dispositivo_id,profissional_acs_id,lote_id,status,processado_at,total_itens,total_processados,total_erros,payload,created_by) values(@TenantId,@EntidadeId,@DispositivoId,@ProfissionalAcsId,@LoteId,@Status,now(),@TotalItens,@TotalProcessados,@TotalErros,@Payload::jsonb,@UsuarioId) returning id",
        _ => throw new InvalidOperationException("Recurso de Saúde não mapeado.")
    };
    private static string UpdateSql(string recurso) => recurso switch { "unidade" => "update sigov.unidade_saude set codigo=@Codigo,nome=@Nome,cnes=@Cnes,tipo_unidade=@TipoUnidade,situacao=@Situacao,latitude=@Latitude,longitude=@Longitude,observacao=@Observacao,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "profissional" => "update sigov.profissional_saude set codigo_profissional=@CodigoProfissional,tipo_profissional=@TipoProfissional,situacao=@Situacao,servidor_id=@ServidorId,unidade_saude_id=@UnidadeSaudeId,cbo=@Cbo,conselho_classe=@ConselhoClasse,numero_conselho=@NumeroConselho,uf_conselho=@UfConselho,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "paciente" => "update sigov.paciente set codigo_paciente=@CodigoPaciente,cartao_sus=@CartaoSus,prontuario_numero=@ProntuarioNumero,grupo_sanguineo=@GrupoSanguineo,alergias=@Alergias,dados_sensiveis_json=@DadosSensiveisJson::jsonb,situacao=@Situacao,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "prontuario_paciente" => "update sigov.prontuario set resumo_clinico=@ResumoClinico,alergias=@Alergias,observacoes_sensiveis=@ObservacoesSensiveis,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and paciente_id=@Id", "atendimento" => "update sigov.atendimento_saude set tipo_atendimento=@TipoAtendimento,status=@Status,profissional_saude_id=@ProfissionalSaudeId,classificacao_risco=@ClassificacaoRisco,queixa_principal=@QueixaPrincipal,conduta=@Conduta,cid10=@Cid10,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "atendimento_conduta" => "update sigov.atendimento_saude set conduta=@Conduta,cid10=@Cid10,status='ATENDIDO',updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and status <> 'CANCELADO'", "atendimento_cancelar" => "update sigov.atendimento_saude set status='CANCELADO',updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "agenda_cancelar" => "update sigov.agenda_saude set status='CANCELADA',updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "laboratorio_resultado" => "update sigov.laboratorio_exame set status='CONCLUIDO',resultado_json=@ResultadoJson::jsonb,data_resultado=coalesce(@DataResultado,current_date),observacao=@Observacao,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", "regulacao_status" => "update sigov.regulacao_solicitacao set status=@Status,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id", _ => throw new InvalidOperationException("Atualização de Saúde não mapeada.") };
    private static void ApplyDefaults(string recurso, Dictionary<string, object?> p) { p.TryAdd("Numero", $"ATD-{p["Ano"]}-000001"); p.TryAdd("Status", recurso == "acs_sync" ? "PROCESSADO" : "ATIVO"); p["DadosSensiveisJson"] = Json(p.GetValueOrDefault("DadosSensiveis") ?? new Dictionary<string, object?>()); p["PoligonoGeoJson"] = Json(p.GetValueOrDefault("PoligonoGeoJson") ?? new Dictionary<string, object?>()); p["EnderecoJson"] = Json(p.GetValueOrDefault("Endereco") ?? new Dictionary<string, object?>()); p["CondicoesMoradiaJson"] = Json(p.GetValueOrDefault("CondicoesMoradia") ?? new Dictionary<string, object?>()); p["CondicoesSaudeJson"] = Json(p.GetValueOrDefault("CondicoesSaude") ?? new Dictionary<string, object?>()); p["VulnerabilidadesJson"] = Json(p.GetValueOrDefault("Vulnerabilidades") ?? new Dictionary<string, object?>()); p["ResultadoJson"] = Json(p.GetValueOrDefault("Resultado") ?? new Dictionary<string, object?>()); if (recurso == "acs_sync") { var itens = p.GetValueOrDefault("Itens") as IEnumerable<AcsSyncItemResponse> ?? Array.Empty<AcsSyncItemResponse>(); p["TotalItens"] = itens.Count(); p["TotalProcessados"] = itens.Count(i => i.Status == "PROCESSADO"); p["TotalErros"] = itens.Count(i => i.Status == "ERRO"); p["Status"] = (int)p["TotalErros"]! > 0 ? "PROCESSADO_COM_ERROS" : "PROCESSADO"; } if (recurso == "acs_visita") p["DataVisita"] ??= DateTimeOffset.UtcNow; }
    private static async Task EnsureProntuarioAsync(NpgsqlConnection c, NpgsqlTransaction tx, Dictionary<string, object?> p, long pacienteId, CancellationToken ct) { p["PacienteId"] = pacienteId; p["ProntuarioNumero"] ??= $"PRONT-{p["Ano"]}-000001"; await c.ExecuteAsync(new CommandDefinition("insert into sigov.prontuario(tenant_id,entidade_id,paciente_id,numero,alergias,created_by) values(@TenantId,@EntidadeId,@PacienteId,@ProntuarioNumero,@Alergias,@UsuarioId) on conflict do nothing", p, tx, cancellationToken: ct)).ConfigureAwait(false); }
    private static async Task BaixarEstoqueAsync(NpgsqlConnection c, NpgsqlTransaction tx, Dictionary<string, object?> p, CancellationToken ct) { var affected = await c.ExecuteAsync(new CommandDefinition("update sigov.farmacia_estoque set quantidade=quantidade-@Quantidade,updated_at=now() where tenant_id=@TenantId and entidade_id=@EntidadeId and unidade_saude_id=@UnidadeSaudeId and farmacia_produto_id=@FarmaciaProdutoId and coalesce(lote,'')=coalesce(@Lote,'') and quantidade >= @Quantidade", p, tx, cancellationToken: ct)).ConfigureAwait(false); if (affected == 0) throw new InvalidOperationException("Dispensação não pode deixar estoque negativo."); }
    private static async Task InserirItensSyncAsync(NpgsqlConnection c, NpgsqlTransaction tx, Dictionary<string, object?> p, long loteId, CancellationToken ct) { if (p.GetValueOrDefault("Itens") is not IEnumerable<AcsSyncItemResponse> itens) return; foreach (var item in itens) await c.ExecuteAsync(new CommandDefinition("insert into sigov.acs_sync_item(tenant_id,entidade_id,acs_sync_lote_id,tipo_item,offline_id,status,payload,erro,processado_at,created_by) values(@TenantId,@EntidadeId,@LoteIdPk,@TipoItem,@OfflineId,@Status,'{}'::jsonb,@Erro,now(),@UsuarioId) on conflict do nothing", new { TenantId = p["TenantId"], EntidadeId = p["EntidadeId"], LoteIdPk = loteId, item.TipoItem, item.OfflineId, item.Status, item.Erro, UsuarioId = p["UsuarioId"] }, tx, cancellationToken: ct)).ConfigureAwait(false); }
    private static async Task RegistrarEventoAsync(NpgsqlConnection c, NpgsqlTransaction tx, long tenantId, long entidadeId, string tipo, string recurso, long aggregateId, object payload, long? usuarioId, CancellationToken ct) { await c.ExecuteAsync(new CommandDefinition("insert into sigov.saude_evento(tenant_id,entidade_id,tipo_evento,aggregate_type,aggregate_id,payload,created_by) values(@TenantId,@EntidadeId,@Tipo,@Recurso,@AggregateId,@Payload::jsonb,@UsuarioId)", new { TenantId = tenantId, EntidadeId = entidadeId, Tipo = tipo, Recurso = recurso, AggregateId = aggregateId, Payload = Json(payload), UsuarioId = usuarioId }, tx, cancellationToken: ct)).ConfigureAwait(false); }
    private static string Evento(string recurso) => recurso switch { "unidade" => "UnidadeSaudeCriada", "profissional" => "ProfissionalSaudeCriado", "paciente" => "PacienteCriado", "atendimento" => "AtendimentoCriado", "atendimento_conduta" => "AtendimentoFinalizado", "farmacia_dispensacao" => "MedicamentoDispensado", "vacinacao" => "VacinacaoRegistrada", "laboratorio" => "ExameSolicitado", "laboratorio_resultado" => "ResultadoExameRegistrado", "regulacao" => "RegulacaoSolicitada", "acs_domicilio" => "AcsDomicilioCadastrado", "acs_individuo" => "AcsIndividuoCadastrado", "acs_visita" => "AcsVisitaRegistrada", "acs_sync" => "AcsSyncProcessado", _ => "SaudeEventoRegistrado" };
    private static (int Page, int PageSize, int Limit, int Offset) Page(object filtro) { var d = ToDictionary(filtro); var page = GetInt(d, "Page", 1); var size = GetInt(d, "PageSize", 20); page = page < 1 ? 1 : page; size = size is < 1 or > 100 ? 20 : size; return (page, size, size, (page - 1) * size); }
    private static object Params(long tenantId, long entidadeId, long? exercicioId, object filtro, object extra) { var d = ToDictionary(filtro); foreach (var prop in extra.GetType().GetProperties()) d[prop.Name] = prop.GetValue(extra); d["TenantId"] = tenantId; d["EntidadeId"] = entidadeId; d["ExercicioId"] = exercicioId; return d; }
    private static Dictionary<string, object?> ToDictionary(object value) { if (value is Dictionary<string, object?> dict) return new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase); var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase); foreach (var prop in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) result[prop.Name] = prop.GetValue(value); return result; }
    private static int GetInt(Dictionary<string, object?> d, string key, int fallback) => d.TryGetValue(key, out var v) && int.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), out var i) ? i : fallback;
    private static string Json(object? value) => JsonSerializer.Serialize(SanitizeJson(value), JsonOptions);
    private static object? SanitizeJson(object? value)
    {
        if (value is null) return null;
        if (value is DateOnly date) return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto) return dto.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value is DateTime dt) return dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value is string or bool or int or long or decimal) return value;
        if (value is System.Collections.IDictionary dictionary)
        {
            var mapped = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in dictionary) mapped[Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty] = SanitizeJson(entry.Value);
            return mapped;
        }
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable) list.Add(SanitizeJson(item));
            return list;
        }
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) result[prop.Name] = SanitizeJson(prop.GetValue(value));
        return result;
    }
}
