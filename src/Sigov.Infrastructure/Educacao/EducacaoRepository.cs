using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Educacao;
using Sigov.Application.Common;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;
using Sigov.Infrastructure.Persistence.Sql;

namespace Sigov.Infrastructure.Educacao;

public sealed class EducacaoRepository : BaseRepository, IEscolaRepository, IAnoLetivoRepository, ICursoRepository, ITurmaRepository, IAlunoRepository, IMatriculaRepository, IProfessorRepository, IFrequenciaRepository, IAvaliacaoRepository, IPreMatriculaRepository, IEducacensoRepository, IEducacaoDashboardRepository, IEducacaoExportacaoRepository, IEducacaoRepository, IEducacaoSequencialService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DapperContext _context;

    public EducacaoRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<T>> ListarAsync<T>(long tenantId, long entidadeId, string recurso, object filtro, CancellationToken ct)
    {
        var (page, pageSize, limit, offset) = Page(filtro);
        var where = "tenant_id = @TenantId and entidade_id = @EntidadeId and is_deleted = false" + ExtraWhere(recurso, filtro);
        var sql = $"select {Select(recurso)} from sigov.{Table(recurso)} where {where} order by id desc limit @Limit offset @Offset; select count(*) from sigov.{Table(recurso)} where {where};";
        using var connection = _context.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(Command(sql, Params(tenantId, entidadeId, filtro, new { Limit = limit, Offset = offset }), ct)).ConfigureAwait(false);
        var items = (await multi.ReadAsync<T>().ConfigureAwait(false)).AsList();
        var total = await multi.ReadFirstAsync<long>().ConfigureAwait(false);
        return new PagedResult<T>(items, page, pageSize, total);
    }

    public async Task<T?> ObterAsync<T>(long tenantId, long entidadeId, string recurso, long id, CancellationToken ct)
    {
        if (typeof(T) == typeof(object)) return default;
        var sql = $"select {Select(recurso)} from sigov.{Table(recurso)} where tenant_id = @TenantId and entidade_id = @EntidadeId and id = @Id and is_deleted = false;";
        using var connection = _context.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<T>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id }, ct)).ConfigureAwait(false);
    }

    public async Task<long> CriarAsync(long tenantId, long entidadeId, long? exercicioId, string recurso, object request, long? usuarioId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var p = ToDictionary(request);
            p["TenantId"] = tenantId; p["EntidadeId"] = entidadeId; p["ExercicioId"] = exercicioId; p["UsuarioId"] = usuarioId;
            ApplyDefaults(recurso, p);
            var sql = InsertSql(recurso);
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, p, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (recurso == "matricula")
            {
                await connection.ExecuteAsync(new CommandDefinition("update sigov.turma set vagas_ocupadas = vagas_ocupadas + 1, updated_by = @UsuarioId where tenant_id = @TenantId and entidade_id = @EntidadeId and id = @TurmaId and is_deleted = false and vagas_ocupadas < capacidade", p, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
            await RegistrarEventoAsync(connection, tx, tenantId, entidadeId, Evento(recurso, "Criada"), recurso, id, p, usuarioId, ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return id;
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task AtualizarAsync(long tenantId, long entidadeId, string recurso, long id, object request, long? usuarioId, CancellationToken ct)
    {
        var p = ToDictionary(request);
        p["TenantId"] = tenantId; p["EntidadeId"] = entidadeId; p["Id"] = id; p["UsuarioId"] = usuarioId;
        if (!p.ContainsKey("Observacao")) p["Observacao"] = p.TryGetValue("Motivo", out var motivo) ? motivo : null;
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(UpdateSql(recurso, p), p, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (recurso == "matricula" && p.TryGetValue("Status", out var status) && string.Equals(Convert.ToString(status, System.Globalization.CultureInfo.InvariantCulture), "CANCELADA", StringComparison.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(new CommandDefinition("update sigov.turma t set vagas_ocupadas = greatest(vagas_ocupadas - 1, 0), updated_by = @UsuarioId from sigov.matricula m where m.turma_id = t.id and m.id = @Id and m.tenant_id = @TenantId and m.entidade_id = @EntidadeId", p, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
            await RegistrarEventoAsync(connection, tx, tenantId, entidadeId, Evento(recurso, "Atualizada"), recurso, id, p, usuarioId, ct).ConfigureAwait(false);
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
        var sql = $"update sigov.{Table(recurso)} set is_deleted = true, ativo = false, deleted_at = now(), deleted_by = @UsuarioId where tenant_id = @TenantId and entidade_id = @EntidadeId and id = @Id and is_deleted = false";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<EducacaoDashboardResponse> DashboardAsync(long tenantId, long entidadeId, CancellationToken ct)
    {
        const string sql = @"select
  (select count(*) from sigov.escola where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as TotalEscolas,
  (select count(*) from sigov.aluno where tenant_id=@TenantId and entidade_id=@EntidadeId and situacao='ATIVO' and is_deleted=false) as TotalAlunosAtivos,
  (select count(*) from sigov.matricula where tenant_id=@TenantId and entidade_id=@EntidadeId and status='ATIVA' and is_deleted=false) as TotalMatriculasAtivas,
  (select count(*) from sigov.turma where tenant_id=@TenantId and entidade_id=@EntidadeId and status='ABERTA' and is_deleted=false) as TotalTurmasAbertas,
  (select coalesce(sum(capacidade),0) from sigov.turma where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as VagasTotais,
  (select coalesce(sum(vagas_ocupadas),0) from sigov.turma where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false) as VagasOcupadas,
  (select count(*) from sigov.pre_matricula_inscricao where tenant_id=@TenantId and entidade_id=@EntidadeId and status in ('RECEBIDA','EM_ANALISE') and is_deleted=false) as PreMatriculasPendentes,
  (select coalesce(avg(case when presente then 100.0 else 0.0 end),0)::numeric(9,2) from sigov.diario_frequencia where tenant_id=@TenantId and entidade_id=@EntidadeId and data_aula >= date_trunc('month', current_date) and is_deleted=false) as FrequenciaMediaMes,
  (select count(*) from sigov.avaliacao where tenant_id=@TenantId and entidade_id=@EntidadeId and status='ABERTA' and is_deleted=false) as AvaliacoesAbertas,
  (select count(*) from sigov.educacenso_registro where tenant_id=@TenantId and entidade_id=@EntidadeId and status='PENDENTE' and is_deleted=false) as RegistrosEducacensoPendentes;
";
        using var connection = _context.CreateConnection();
        var row = await connection.QueryFirstAsync(sql, new { TenantId = tenantId, EntidadeId = entidadeId }).ConfigureAwait(false);
        return new EducacaoDashboardResponse((long)row.totalescolas, (long)row.totalalunosativos, (long)row.totalmatriculasativas, (long)row.totalturmasabertas, (long)row.vagastotais, (long)row.vagasocupadas, (long)row.prematriculaspendentes, (decimal)row.frequenciamediames, (long)row.avaliacoesabertas, (long)row.registroseducacensopendentes, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(), new[] { "Educação base operacional carregada." });
    }

    public async Task<byte[]> ExportarAsync(long tenantId, long entidadeId, string recurso, string formato, CancellationToken ct)
    {
        var table = recurso switch { "alunos" => "aluno", "matriculas" => "matricula", "turmas" => "turma", "frequencias" => "diario_frequencia", "notas" => "nota", _ => "aluno" };
        var projection = table switch
        {
            "aluno" => "id,codigo_aluno,necessidade_especial,situacao,created_at",
            "matricula" => "id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,created_at",
            "turma" => "id,escola_id,ano_letivo_id,codigo,nome,turno,capacidade,vagas_ocupadas,status",
            "diario_frequencia" => "id,turma_id,aluno_id,data_aula,componente_curricular,presente,created_at",
            "nota" => "id,avaliacao_id,aluno_id,valor,observacao,created_at",
            _ => throw new InvalidOperationException("Exportação educacional não mapeada.")
        };
        var sql = $"select row_to_json(x) from (select {projection} from sigov.{table} where tenant_id=@TenantId and entidade_id=@EntidadeId and is_deleted=false order by id desc limit 1000) x";
        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<string>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId }, ct)).ConfigureAwait(false)).AsList();
        if (formato.Equals("json", StringComparison.OrdinalIgnoreCase)) return Encoding.UTF8.GetBytes("[" + string.Join(',', rows) + "]");
        var csv = new StringBuilder("dados\n");
        foreach (var row in rows) csv.Append('"').Append(row.Replace("\"", "\"\"", StringComparison.Ordinal)).AppendLine("\"");
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<BoletimResponse> ObterBoletimAsync(long tenantId, long entidadeId, long alunoId, CancellationToken ct)
    {
        const string sql = @"select a.componente_curricular as ComponenteCurricular,
       a.titulo as Avaliacao, a.data_avaliacao as DataAvaliacao,
       a.valor_maximo as ValorMaximo, n.valor as Nota,
       case when n.valor is null then 'PENDENTE'
            when n.valor >= (a.valor_maximo * 0.6) then 'APROVADO'
            else 'RECUPERACAO' end as Situacao
from sigov.avaliacao a
join sigov.matricula m on m.tenant_id=a.tenant_id and m.entidade_id=a.entidade_id
 and m.turma_id=a.turma_id and m.aluno_id=@AlunoId and m.is_deleted=false
left join sigov.nota n on n.tenant_id=a.tenant_id and n.entidade_id=a.entidade_id
 and n.avaliacao_id=a.id and n.aluno_id=@AlunoId and n.is_deleted=false
where a.tenant_id=@TenantId and a.entidade_id=@EntidadeId and a.is_deleted=false
order by a.data_avaliacao desc, a.id desc;";
        using var connection = _context.CreateConnection();
        var itens = (await connection.QueryAsync<BoletimItemResponse>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, AlunoId = alunoId }, ct)).ConfigureAwait(false)).AsList();
        var notas = itens.Where(x => x.Nota.HasValue).Select(x => x.Nota!.Value).ToArray();
        return new BoletimResponse(alunoId, notas.Length == 0 ? 0m : decimal.Round(notas.Average(), 2), itens);
    }

    public Task<string> ProximoAsync(string prefixo, int ano, CancellationToken ct)
    {
        _ = ct;
        return Task.FromResult($"{prefixo}-{ano}-000001");
    }

    private static (int Page, int PageSize, int Limit, int Offset) Page(object filtro)
    {
        var dict = ToDictionary(filtro);
        var page = GetInt(dict, "Page", 1);
        var size = GetInt(dict, "PageSize", 20);
        var built = SqlPaginationBuilder.Build(page, size);
        return (page < 1 ? 1 : page, size is < 1 or > 100 ? 20 : size, built.Limit, built.Offset);
    }

    private static object Params(long tenantId, long entidadeId, object filtro, object extra)
    {
        var dict = ToDictionary(filtro);
        foreach (var prop in extra.GetType().GetProperties()) dict[prop.Name] = prop.GetValue(extra);
        dict["TenantId"] = tenantId; dict["EntidadeId"] = entidadeId;
        return dict;
    }

    private static Dictionary<string, object?> ToDictionary(object value)
    {
        if (value is Dictionary<string, object?> d) return new Dictionary<string, object?>(d, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            result[prop.Name] = prop.GetValue(value);
        }

        return result;
    }

    private static int GetInt(Dictionary<string, object?> d, string key, int fallback) => d.TryGetValue(key, out var v) && int.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), out var i) ? i : fallback;
    private static string Json(object? value) => JsonSerializer.Serialize(SanitizeJson(value), JsonOptions);

    private static object? SanitizeJson(object? value)
    {
        if (value is null) return null;
        if (value is DateOnly date) return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (value is DateTime dt) return dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        if (value is string or bool or int or long or decimal or double) return value;
        if (value is IDictionary<string, object?> dict) return dict.ToDictionary(k => k.Key, v => SanitizeJson(v.Value), StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in value.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            result[prop.Name] = SanitizeJson(prop.GetValue(value));
        }

        return result;
    }
    private static string Table(string recurso) => recurso switch
    {
        "escola" or "ano_letivo" or "curso" or "serie_ano" or "turma" or "aluno" or "responsavel_aluno" or "matricula" or "professor" or "professor_turma" or "diario_frequencia" or "avaliacao" or "nota" or "pre_matricula_inscricao" or "educacenso_registro" => recurso,
        _ => throw new InvalidOperationException("Recurso de Educação não mapeado.")
    };

    private static string Select(string recurso) => recurso switch
    {
        "escola" => "id, codigo, nome, tipo_escola as TipoEscola, situacao, inep_codigo as InepCodigo, ativo",
        "ano_letivo" => "id, ano, data_inicio as DataInicio, data_fim as DataFim, status, escola_id as EscolaId",
        "curso" => "id, codigo, nome, etapa_ensino as EtapaEnsino, modalidade",
        "serie_ano" => "id, curso_id as CursoId, codigo, nome, ordem",
        "turma" => "id, escola_id as EscolaId, ano_letivo_id as AnoLetivoId, codigo, nome, turno, capacidade, vagas_ocupadas as VagasOcupadas, status",
        "aluno" => "id, pessoa_id as PessoaId, codigo_aluno as CodigoAluno, nis, cartao_sus as CartaoSus, necessidade_especial as NecessidadeEspecial, situacao, array[]::json[] as Responsaveis",
        "matricula" => "id, aluno_id as AlunoId, escola_id as EscolaId, ano_letivo_id as AnoLetivoId, turma_id as TurmaId, numero_matricula as NumeroMatricula, data_matricula as DataMatricula, status",
        "professor" => "id, pessoa_id as PessoaId, codigo_professor as CodigoProfessor, formacao, situacao",
        "diario_frequencia" => "id, turma_id as TurmaId, aluno_id as AlunoId, data_aula as DataAula, componente_curricular as ComponenteCurricular, presente",
        "avaliacao" => "id, turma_id as TurmaId, componente_curricular as ComponenteCurricular, titulo, data_avaliacao as DataAvaliacao, valor_maximo as ValorMaximo, peso, status",
        "nota" => "id, avaliacao_id as AvaliacaoId, aluno_id as AlunoId, valor, observacao",
        "pre_matricula_inscricao" => "id, protocolo, aluno_pessoa_id as AlunoPessoaId, ano_letivo as AnoLetivo, etapa_ensino as EtapaEnsino, status, pontuacao",
        "educacenso_registro" => "id, tipo_registro as TipoRegistro, status, payload as Payload, erro",
        _ => "*"
    };

    private static string ExtraWhere(string recurso, object filtro)
    {
        var d = ToDictionary(filtro);
        var sql = new StringBuilder();
        if (d.TryGetValue("Termo", out var termo) && !string.IsNullOrWhiteSpace(Convert.ToString(termo, System.Globalization.CultureInfo.InvariantCulture))) sql.Append(recurso switch { "escola" or "curso" or "turma" => " and (codigo ilike '%' || @Termo || '%' or nome ilike '%' || @Termo || '%')", "aluno" => " and codigo_aluno ilike '%' || @Termo || '%'", _ => string.Empty });
        if (d.TryGetValue("Status", out var status) && !string.IsNullOrWhiteSpace(Convert.ToString(status, System.Globalization.CultureInfo.InvariantCulture))) sql.Append(" and status = @Status");
        if (d.TryGetValue("EscolaId", out var escola) && escola is not null) sql.Append(" and escola_id = @EscolaId");
        if (d.TryGetValue("AlunoId", out var aluno) && aluno is not null) sql.Append(" and aluno_id = @AlunoId");
        if (d.TryGetValue("TurmaId", out var turma) && turma is not null) sql.Append(" and turma_id = @TurmaId");
        return sql.ToString();
    }

    private static void ApplyDefaults(string recurso, Dictionary<string, object?> p)
    {
        if (recurso == "matricula" && (!p.TryGetValue("NumeroMatricula", out var n) || string.IsNullOrWhiteSpace(Convert.ToString(n, System.Globalization.CultureInfo.InvariantCulture)))) p["NumeroMatricula"] = $"MAT-{DateTime.UtcNow.Year}-{DateTime.UtcNow.Ticks % 1000000:000000}";
        if (recurso == "pre_matricula_inscricao" && (!p.TryGetValue("Protocolo", out var pr) || string.IsNullOrWhiteSpace(Convert.ToString(pr, System.Globalization.CultureInfo.InvariantCulture)))) p["Protocolo"] = $"PRE-{DateTime.UtcNow.Year}-{DateTime.UtcNow.Ticks % 1000000:000000}";
        p["DadosSensiveisJson"] = Json(p.TryGetValue("DadosSensiveis", out var ds) ? ds : null);
        p["PayloadJson"] = Json(p.TryGetValue("Payload", out var payload) ? payload : null);
    }

    private static string InsertSql(string r) => r switch
    {
        "escola" => "insert into sigov.escola (tenant_id,entidade_id,codigo,nome,tipo_escola,situacao,inep_codigo,observacao,created_by) values (@TenantId,@EntidadeId,@Codigo,@Nome,@TipoEscola,@Situacao,@InepCodigo,@Observacao,@UsuarioId) returning id",
        "ano_letivo" => "insert into sigov.ano_letivo (tenant_id,entidade_id,exercicio_id,escola_id,ano,data_inicio,data_fim,status,observacao,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@EscolaId,@Ano,@DataInicio,@DataFim,@Status,@Observacao,@UsuarioId) returning id",
        "curso" => "insert into sigov.curso (tenant_id,entidade_id,codigo,nome,etapa_ensino,modalidade,created_by) values (@TenantId,@EntidadeId,@Codigo,@Nome,@EtapaEnsino,@Modalidade,@UsuarioId) returning id",
        "serie_ano" => "insert into sigov.serie_ano (tenant_id,entidade_id,curso_id,codigo,nome,ordem,created_by) values (@TenantId,@EntidadeId,@CursoId,@Codigo,@Nome,@Ordem,@UsuarioId) returning id",
        "turma" => "insert into sigov.turma (tenant_id,entidade_id,exercicio_id,escola_id,ano_letivo_id,curso_id,serie_ano_id,codigo,nome,turno,capacidade,status,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@EscolaId,@AnoLetivoId,@CursoId,@SerieAnoId,@Codigo,@Nome,@Turno,@Capacidade,@Status,@UsuarioId) returning id",
        "aluno" => "insert into sigov.aluno (tenant_id,entidade_id,pessoa_id,codigo_aluno,nis,cartao_sus,necessidade_especial,dados_sensiveis_json,situacao,created_by) values (@TenantId,@EntidadeId,@PessoaId,@CodigoAluno,@Nis,@CartaoSus,@NecessidadeEspecial,cast(@DadosSensiveisJson as jsonb),@Situacao,@UsuarioId) returning id",
        "responsavel_aluno" => "insert into sigov.responsavel_aluno (tenant_id,entidade_id,aluno_id,pessoa_id,parentesco,responsavel_legal,financeiro,autorizado_buscar,contato_emergencia,created_by) values (@TenantId,@EntidadeId,@AlunoId,@PessoaId,@Parentesco,@ResponsavelLegal,@Financeiro,@AutorizadoBuscar,@ContatoEmergencia,@UsuarioId) returning id",
        "matricula" => "insert into sigov.matricula (tenant_id,entidade_id,exercicio_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,origem,observacao,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@AlunoId,@EscolaId,@AnoLetivoId,@TurmaId,@NumeroMatricula,coalesce(@DataMatricula,current_date),@Status,@Origem,@Observacao,@UsuarioId) returning id",
        "professor" => "insert into sigov.professor (tenant_id,entidade_id,pessoa_id,servidor_id,codigo_professor,formacao,situacao,created_by) values (@TenantId,@EntidadeId,@PessoaId,@ServidorId,@CodigoProfessor,@Formacao,@Situacao,@UsuarioId) returning id",
        "professor_turma" => "insert into sigov.professor_turma (tenant_id,entidade_id,exercicio_id,professor_id,turma_id,componente_curricular,carga_horaria_semanal,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@ProfessorId,@TurmaId,@ComponenteCurricular,@CargaHorariaSemanal,@UsuarioId) returning id",
        "diario_frequencia" => "insert into sigov.diario_frequencia (tenant_id,entidade_id,exercicio_id,turma_id,aluno_id,professor_id,data_aula,componente_curricular,presente,justificativa,registrado_by,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@TurmaId,@AlunoId,@ProfessorId,@DataAula,@ComponenteCurricular,@Presente,@Justificativa,@UsuarioId,@UsuarioId) returning id",
        "avaliacao" => "insert into sigov.avaliacao (tenant_id,entidade_id,exercicio_id,turma_id,professor_id,componente_curricular,titulo,data_avaliacao,valor_maximo,peso,status,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@TurmaId,@ProfessorId,@ComponenteCurricular,@Titulo,@DataAvaliacao,@ValorMaximo,@Peso,@Status,@UsuarioId) returning id",
        "nota" => "insert into sigov.nota (tenant_id,entidade_id,exercicio_id,avaliacao_id,aluno_id,valor,observacao,registrado_by,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@AvaliacaoId,@AlunoId,@Valor,@Observacao,@UsuarioId,@UsuarioId) returning id",
        "pre_matricula_inscricao" => "insert into sigov.pre_matricula_inscricao (tenant_id,entidade_id,exercicio_id,escola_preferencial_id,aluno_pessoa_id,responsavel_pessoa_id,protocolo,ano_letivo,etapa_ensino,status,pontuacao,observacao,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@EscolaPreferencialId,@AlunoPessoaId,@ResponsavelPessoaId,@Protocolo,@AnoLetivo,@EtapaEnsino,@Status,@Pontuacao,@Observacao,@UsuarioId) returning id",
        "educacenso_registro" => "insert into sigov.educacenso_registro (tenant_id,entidade_id,exercicio_id,escola_id,aluno_id,turma_id,tipo_registro,status,payload,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@EscolaId,@AlunoId,@TurmaId,@TipoRegistro,@Status,cast(@PayloadJson as jsonb),@UsuarioId) returning id",
        _ => throw new InvalidOperationException("Recurso de Educação não mapeado.")
    };

    private static string UpdateSql(string r, Dictionary<string, object?> p) => r switch
    {
        "escola" => "update sigov.escola set codigo=@Codigo,nome=@Nome,tipo_escola=@TipoEscola,situacao=@Situacao,inep_codigo=@InepCodigo,observacao=@Observacao,updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        "turma" => "update sigov.turma set codigo=@Codigo,nome=@Nome,turno=@Turno,capacidade=@Capacidade,status=@Status,updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        "aluno" => "update sigov.aluno set codigo_aluno=@CodigoAluno,nis=@Nis,cartao_sus=@CartaoSus,necessidade_especial=@NecessidadeEspecial,dados_sensiveis_json=cast(@DadosSensiveisJson as jsonb),situacao=@Situacao,updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        "matricula" => "update sigov.matricula set status=@Status,observacao=coalesce(@Observacao,observacao),updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        "ano_letivo" => "update sigov.ano_letivo set status=@Status,updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        "pre_matricula_inscricao" => "update sigov.pre_matricula_inscricao set status=@Status,updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        "educacenso_registro" => "update sigov.educacenso_registro set status=@Status,updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false",
        _ => $"update sigov.{Table(r)} set updated_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@Id and is_deleted=false"
    };

    private static string Evento(string recurso, string sufixo) => recurso switch
    {
        "escola" => "EscolaCriada", "turma" => "TurmaCriada", "aluno" => "AlunoCriado", "matricula" => "MatriculaCriada", "diario_frequencia" => "FrequenciaRegistrada", "avaliacao" => "AvaliacaoCriada", "nota" => "NotaRegistrada", "pre_matricula_inscricao" => "PreMatriculaRecebida", "educacenso_registro" => "EducacensoRegistroCriado", _ => recurso + sufixo
    };

    private static async Task RegistrarEventoAsync(NpgsqlConnection c, NpgsqlTransaction tx, long tenantId, long entidadeId, string tipo, string agregacao, long agregadoId, object payload, long? usuarioId, CancellationToken ct)
    {
        const string sql = "insert into sigov.educacao_evento (tenant_id, entidade_id, tipo_evento, agregacao, agregado_id, payload, created_by) values (@TenantId,@EntidadeId,@Tipo,@Agregacao,@AgregadoId,cast(@Payload as jsonb),@UsuarioId)";
        await c.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Tipo = tipo, Agregacao = agregacao, AgregadoId = agregadoId, Payload = Json(payload), UsuarioId = usuarioId }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }
}
