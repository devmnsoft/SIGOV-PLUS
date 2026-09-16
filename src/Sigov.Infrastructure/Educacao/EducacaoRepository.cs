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
            if (recurso == "matricula")
            {
                const string reservarVaga = @"update sigov.turma t
set vagas_ocupadas = vagas_ocupadas + 1, updated_by = @UsuarioId
from sigov.aluno a, sigov.escola e, sigov.ano_letivo l
where t.tenant_id = @TenantId and t.entidade_id = @EntidadeId and t.id = @TurmaId
  and t.escola_id = @EscolaId and t.ano_letivo_id = @AnoLetivoId
  and t.status in ('PLANEJADA','ABERTA') and not t.is_deleted
  and t.vagas_ocupadas < t.capacidade
  and a.id = @AlunoId and a.tenant_id = t.tenant_id and a.entidade_id = t.entidade_id
  and a.situacao = 'ATIVO' and not a.is_deleted
  and e.id = t.escola_id and e.tenant_id = t.tenant_id and e.entidade_id = t.entidade_id
  and e.situacao = 'ATIVA' and not e.is_deleted
  and l.id = t.ano_letivo_id and l.tenant_id = t.tenant_id and l.entidade_id = t.entidade_id
  and l.status <> 'ENCERRADO' and not l.is_deleted";
                var reservadas = await connection.ExecuteAsync(new CommandDefinition(reservarVaga, p, tx, cancellationToken: ct)).ConfigureAwait(false);
                if (reservadas != 1)
                    throw new InvalidOperationException("Matrícula rejeitada: contexto incompatível, cadastro inativo, período encerrado ou turma sem vaga.");
            }
            var sql = InsertSql(recurso);
            var insertedId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, p, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (!insertedId.HasValue || insertedId.Value <= 0)
                throw new InvalidOperationException(recurso switch
                {
                    "diario_frequencia" => "Frequência rejeitada: aluno sem matrícula elegível, professor sem atribuição na turma/componente ou período encerrado.",
                    "avaliacao" => "Avaliação rejeitada: professor sem atribuição, data fora do ano letivo ou turma/período fechado.",
                    "nota" => "Resultado rejeitado: aluno inelegível, avaliação fechada ou valor fora da escala configurada.",
                    _ => "Cadastro rejeitado por inconsistência de contexto ou estado."
                });
            var id = insertedId.Value;
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
       case when n.valor is null then 'NAO_LANCADO'
            else 'REGISTRADO' end as Situacao
from sigov.avaliacao a
join sigov.matricula m on m.tenant_id=a.tenant_id and m.entidade_id=a.entidade_id
 and m.turma_id=a.turma_id and m.aluno_id=@AlunoId and m.is_deleted=false
left join sigov.nota n on n.tenant_id=a.tenant_id and n.entidade_id=a.entidade_id
 and n.avaliacao_id=a.id and n.aluno_id=@AlunoId and n.is_deleted=false
where a.tenant_id=@TenantId and a.entidade_id=@EntidadeId and a.is_deleted=false
order by a.data_avaliacao desc, a.id desc;";
        using var connection = _context.CreateConnection();
        var itens = (await connection.QueryAsync<BoletimItemResponse>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, AlunoId = alunoId }, ct)).ConfigureAwait(false)).AsList();
        // Sem uma política acadêmica versionada, não há base legítima para inferir
        // média, aprovação, recuperação ou equivalência. O relatório preserva os
        // lançamentos e explicita a indisponibilidade do resultado calculado.
        return new BoletimResponse(alunoId, null, itens);
    }

    public Task<string> ProximoAsync(string prefixo, int ano, CancellationToken ct)
    {
        _ = ct;
        return Task.FromResult($"{prefixo}-{ano}-000001");
    }

    public async Task AtualizarPreMatriculaAsync(long tenantId, long entidadeId, long id, object request, long versao, long? usuarioId, CancellationToken ct)
    {
        var p = ToDictionary(request);
        p["TenantId"] = tenantId; p["EntidadeId"] = entidadeId; p["Id"] = id; p["Versao"] = versao; p["UsuarioId"] = usuarioId;
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var atual = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(@"select status, versao from sigov.pre_matricula_inscricao where id=@Id and tenant_id=@TenantId and entidade_id=@EntidadeId and not is_deleted for update", p, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (atual is null) throw new InvalidOperationException("Pré-matrícula não encontrada no contexto autorizado.");
        if ((long)atual.versao != versao) throw new InvalidOperationException("A pré-matrícula foi alterada por outra pessoa. Recarregue os dados; seu conteúdo não foi sobrescrito.");
        if (p.TryGetValue("StatusEsperado", out var esperado) && !string.Equals((string)atual.status, Convert.ToString(esperado, System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)) throw new InvalidOperationException("Somente pré-matrícula em rascunho pode ser editada.");
        var destino = p.TryGetValue("Status", out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null;
        if (destino is not null && !TransicaoPermitida((string)atual.status, destino)) throw new InvalidOperationException("Transição incompatível com o estado atual da pré-matrícula.");
        const string sql = @"update sigov.pre_matricula_inscricao set
status=coalesce(@Status,status), responsavel_pessoa_id=coalesce(@ResponsavelPessoaId,responsavel_pessoa_id),
escola_preferencial_id=coalesce(@EscolaPreferencialId,escola_preferencial_id), ano_letivo=coalesce(@AnoLetivo,ano_letivo),
etapa_ensino=coalesce(@EtapaEnsino,etapa_ensino), turno=coalesce(@Turno,turno),
observacao=coalesce(@Observacao,@Motivo,observacao), responsavel_analise_id=coalesce(@ResponsavelAnaliseId,responsavel_analise_id),
versao=versao+1, updated_by=@UsuarioId where id=@Id and tenant_id=@TenantId and entidade_id=@EntidadeId and versao=@Versao";
        foreach (var key in new[] { "Status", "ResponsavelPessoaId", "EscolaPreferencialId", "AnoLetivo", "EtapaEnsino", "Turno", "Observacao", "Motivo", "ResponsavelAnaliseId" }) if (!p.ContainsKey(key)) p[key] = null;
        if (await connection.ExecuteAsync(new CommandDefinition(sql, p, tx, cancellationToken: ct)).ConfigureAwait(false) != 1) throw new InvalidOperationException("Conflito de edição da pré-matrícula.");
        await RegistrarEventoAsync(connection, tx, tenantId, entidadeId, "PreMatriculaTransicionada", "pre_matricula_inscricao", id, p, usuarioId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<long> CriarOfertaAsync(long tenantId, long entidadeId, OfertaVagaRequest request, long usuarioId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var p = new { TenantId=tenantId, EntidadeId=entidadeId, request.PreMatriculaId, request.EscolaId, request.AnoLetivoId, request.SerieAnoId, request.Turno, request.Inicio, request.ValidaAte, request.VersaoPreMatricula, UsuarioId=usuarioId };
        await connection.ExecuteAsync(new CommandDefinition("select pg_advisory_xact_lock(hashtextextended(concat_ws(':',@TenantId,@EntidadeId,@EscolaId,@AnoLetivoId,@SerieAnoId,@Turno),0))", p, tx, cancellationToken: ct)).ConfigureAwait(false);
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"with capacidade as (
 select coalesce(sum(t.capacidade),0) total, coalesce(sum(t.vagas_ocupadas),0) ocupadas
 from sigov.turma t where t.tenant_id=@TenantId and t.entidade_id=@EntidadeId and t.escola_id=@EscolaId
 and t.ano_letivo_id=@AnoLetivoId and t.serie_ano_id=@SerieAnoId and t.turno=@Turno and t.status in ('PLANEJADA','ABERTA') and not t.is_deleted),
reservas as (select count(*) total from sigov.educacao_oferta_vaga where tenant_id=@TenantId and entidade_id=@EntidadeId and escola_id=@EscolaId and ano_letivo_id=@AnoLetivoId and serie_ano_id=@SerieAnoId and turno=@Turno and status in ('OFERTADA','ACEITA') and (valida_ate is null or valida_ate>now()) and not is_deleted),
solicitacao as (
 update sigov.pre_matricula_inscricao set status='OFERTA_REALIZADA',versao=versao+1,updated_by=@UsuarioId
 where id=@PreMatriculaId and tenant_id=@TenantId and entidade_id=@EntidadeId and versao=@VersaoPreMatricula and status='APROVADA'
 and exists(select 1 from capacidade c,reservas r where c.total>c.ocupadas+r.total) returning id)
insert into sigov.educacao_oferta_vaga(tenant_id,entidade_id,pre_matricula_id,escola_id,ano_letivo_id,serie_ano_id,turno,inicio,valida_ate,status,responsavel_id,created_by)
select @TenantId,@EntidadeId,id,@EscolaId,@AnoLetivoId,@SerieAnoId,@Turno,@Inicio,@ValidaAte,'OFERTADA',@UsuarioId,@UsuarioId from solicitacao returning id", p, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!id.HasValue) throw new InvalidOperationException("Oferta rejeitada: solicitação alterada, não aprovada ou fora do contexto.");
        await tx.CommitAsync(ct).ConfigureAwait(false); return id.Value;
    }

    public async Task DecidirOfertaAsync(long tenantId, long entidadeId, long id, OfertaVagaDecisaoRequest request, long usuarioId, CancellationToken ct)
    {
        var decisao = request.Decisao.Trim().ToUpperInvariant();
        if (decisao is not ("ACEITA" or "RECUSADA")) throw new InvalidOperationException("Decisão da oferta deve ser ACEITA ou RECUSADA.");
        if (decisao == "RECUSADA" && string.IsNullOrWhiteSpace(request.Motivo)) throw new InvalidOperationException("Recusa exige motivo.");
        using var connection = _context.CreateConnection();
        var count = await connection.ExecuteAsync(Command(@"update sigov.educacao_oferta_vaga set status=@Decisao,motivo_decisao=@Motivo,decidida_em=now(),updated_by=@UsuarioId
where id=@Id and tenant_id=@TenantId and entidade_id=@EntidadeId and status='OFERTADA' and (valida_ate is null or valida_ate>now())", new { TenantId=tenantId, EntidadeId=entidadeId, Id=id, Decisao=decisao, request.Motivo, UsuarioId=usuarioId }, ct)).ConfigureAwait(false);
        if (count != 1) throw new InvalidOperationException("Oferta indisponível, já decidida ou expirada.");
    }

    public async Task<long> ConverterOfertaAsync(long tenantId, long entidadeId, long? exercicioId, long preMatriculaId, ConverterPreMatriculaRequest request, long usuarioId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var p = new { TenantId=tenantId, EntidadeId=entidadeId, ExercicioId=exercicioId, PreMatriculaId=preMatriculaId, request.OfertaId, request.TurmaId, request.NumeroMatricula, request.DataMatricula, UsuarioId=usuarioId };
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"select sigov.fn_educacao_converter_oferta(@TenantId,@EntidadeId,@ExercicioId,@PreMatriculaId,@OfertaId,@TurmaId,@NumeroMatricula,@DataMatricula,@UsuarioId)", p, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!id.HasValue || id <= 0) throw new InvalidOperationException("Não foi possível converter a oferta em matrícula.");
        await tx.CommitAsync(ct).ConfigureAwait(false); return id.Value;
    }

    public async Task EnturmarAsync(long tenantId, long entidadeId, long matriculaId, EnturmarMatriculaRequest request, long usuarioId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection(); await connection.OpenAsync(ct).ConfigureAwait(false); await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var p = new { TenantId=tenantId, EntidadeId=entidadeId, MatriculaId=matriculaId, request.TurmaId, request.DataEntrada, UsuarioId=usuarioId };
        var count = await connection.ExecuteAsync(new CommandDefinition(@"with turma_valida as (
 select t.id from sigov.turma t join sigov.matricula m on m.id=@MatriculaId and m.tenant_id=t.tenant_id and m.entidade_id=t.entidade_id
 where t.id=@TurmaId and t.tenant_id=@TenantId and t.entidade_id=@EntidadeId and t.escola_id=m.escola_id and t.ano_letivo_id=m.ano_letivo_id
 and t.status='ABERTA' and t.vagas_ocupadas<t.capacidade and m.status='ATIVA' and m.turma_id is null and @DataEntrada>=m.data_matricula and not t.is_deleted and not m.is_deleted for update),
ocupada as (update sigov.turma set vagas_ocupadas=vagas_ocupadas+1,updated_by=@UsuarioId where id in(select id from turma_valida) returning id)
update sigov.matricula set turma_id=@TurmaId,data_enturmacao=@DataEntrada,updated_by=@UsuarioId where id=@MatriculaId and tenant_id=@TenantId and entidade_id=@EntidadeId and turma_id is null and exists(select 1 from ocupada)", p, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (count != 1) throw new InvalidOperationException("Enturmação rejeitada: matrícula já enturmada, turma incompatível, encerrada, sem vaga ou data inválida.");
        await RegistrarEventoAsync(connection, tx, tenantId, entidadeId, "MatriculaEnturmada", "matricula", matriculaId, p, usuarioId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    private static bool TransicaoPermitida(string origem, string destino) => (origem, destino) switch
    {
        ("RASCUNHO", "EM_ANALISE") or ("RASCUNHO", "CANCELADA") or ("COMPLEMENTACAO_PENDENTE", "EM_ANALISE") or
        ("COMPLEMENTACAO_PENDENTE", "CANCELADA") or ("EM_ANALISE", "COMPLEMENTACAO_PENDENTE") or
        ("EM_ANALISE", "APROVADA") or ("EM_ANALISE", "INDEFERIDA") or ("EM_ANALISE", "CANCELADA") => true,
        _ => false
    };

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
        "diario_frequencia" => "id, turma_id as TurmaId, aluno_id as AlunoId, data_aula as DataAula, componente_curricular as ComponenteCurricular, presente, situacao as Status",
        "avaliacao" => "id, turma_id as TurmaId, componente_curricular as ComponenteCurricular, titulo, data_avaliacao as DataAvaliacao, valor_maximo as ValorMaximo, peso, status",
        "nota" => "id, avaliacao_id as AvaliacaoId, aluno_id as AlunoId, valor, observacao",
        "pre_matricula_inscricao" => "id, protocolo, aluno_pessoa_id as AlunoPessoaId, responsavel_pessoa_id as ResponsavelPessoaId, escola_preferencial_id as EscolaPreferencialId, ano_letivo as AnoLetivo, etapa_ensino as EtapaEnsino, turno, status, pontuacao, versao, responsavel_analise_id as ResponsavelAnaliseId, observacao",
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
        if (recurso == "pre_matricula_inscricao" && d.TryGetValue("EtapaEnsino", out var etapa) && etapa is not null) sql.Append(" and etapa_ensino = @EtapaEnsino");
        if (recurso == "pre_matricula_inscricao" && d.TryGetValue("Turno", out var turno) && turno is not null) sql.Append(" and turno = @Turno");
        if (recurso == "pre_matricula_inscricao" && d.TryGetValue("AnoLetivo", out var ano) && ano is not null) sql.Append(" and ano_letivo = @AnoLetivo");
        if (recurso == "pre_matricula_inscricao" && d.TryGetValue("ResponsavelAnaliseId", out var responsavel) && responsavel is not null) sql.Append(" and responsavel_analise_id = @ResponsavelAnaliseId");
        if (recurso == "pre_matricula_inscricao" && d.TryGetValue("Protocolo", out var protocolo) && !string.IsNullOrWhiteSpace(Convert.ToString(protocolo, System.Globalization.CultureInfo.InvariantCulture))) sql.Append(" and protocolo ilike '%' || @Protocolo || '%'");
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
        "diario_frequencia" => @"insert into sigov.diario_frequencia
 (tenant_id,entidade_id,exercicio_id,turma_id,aluno_id,professor_id,data_aula,componente_curricular,presente,situacao,justificativa,registrado_by,created_by)
select @TenantId,@EntidadeId,@ExercicioId,@TurmaId,@AlunoId,@ProfessorId,@DataAula,@ComponenteCurricular,@Presente,@Status,@Justificativa,@UsuarioId,@UsuarioId
from sigov.matricula m
join sigov.turma t on t.id=m.turma_id and t.tenant_id=m.tenant_id and t.entidade_id=m.entidade_id and not t.is_deleted
join sigov.ano_letivo l on l.id=m.ano_letivo_id and l.tenant_id=m.tenant_id and l.entidade_id=m.entidade_id and not l.is_deleted
join sigov.professor_turma pt on pt.tenant_id=m.tenant_id and pt.entidade_id=m.entidade_id
 and pt.turma_id=m.turma_id and pt.professor_id=@ProfessorId and not pt.is_deleted
	 and upper(pt.componente_curricular)=upper(@ComponenteCurricular)
where m.tenant_id=@TenantId and m.entidade_id=@EntidadeId and m.turma_id=@TurmaId and m.aluno_id=@AlunoId
  and m.status in ('ATIVA','CONFIRMADA') and not m.is_deleted
  and @DataAula between greatest(m.data_matricula,coalesce(m.data_enturmacao,m.data_matricula),l.data_inicio) and l.data_fim
  and l.status <> 'ENCERRADO'
returning id",
        "avaliacao" => @"insert into sigov.avaliacao
 (tenant_id,entidade_id,exercicio_id,turma_id,professor_id,componente_curricular,titulo,data_avaliacao,valor_maximo,peso,status,created_by)
select @TenantId,@EntidadeId,@ExercicioId,t.id,@ProfessorId,@ComponenteCurricular,@Titulo,@DataAvaliacao,@ValorMaximo,@Peso,@Status,@UsuarioId
from sigov.turma t
join sigov.ano_letivo l on l.id=t.ano_letivo_id and l.tenant_id=t.tenant_id and l.entidade_id=t.entidade_id and not l.is_deleted
join sigov.professor_turma pt on pt.tenant_id=t.tenant_id and pt.entidade_id=t.entidade_id and pt.turma_id=t.id
 and pt.professor_id=@ProfessorId and upper(pt.componente_curricular)=upper(@ComponenteCurricular) and not pt.is_deleted
where t.tenant_id=@TenantId and t.entidade_id=@EntidadeId and t.id=@TurmaId and not t.is_deleted
 and t.status not in ('FECHADA','CANCELADA') and l.status<>'ENCERRADO'
 and @DataAvaliacao between l.data_inicio and l.data_fim
returning id",
        "nota" => @"insert into sigov.nota
 (tenant_id,entidade_id,exercicio_id,avaliacao_id,aluno_id,valor,observacao,registrado_by,created_by)
select @TenantId,@EntidadeId,@ExercicioId,a.id,@AlunoId,@Valor,@Observacao,@UsuarioId,@UsuarioId
from sigov.avaliacao a
join sigov.matricula m on m.tenant_id=a.tenant_id and m.entidade_id=a.entidade_id and m.turma_id=a.turma_id
 and m.aluno_id=@AlunoId and not m.is_deleted
where a.tenant_id=@TenantId and a.entidade_id=@EntidadeId and a.id=@AvaliacaoId and not a.is_deleted
 and a.status='ABERTA' and @Valor between 0 and a.valor_maximo
 and m.status in ('ATIVA','CONFIRMADA','TRANSFERIDA','CONCLUIDA')
 and m.data_matricula<=a.data_avaliacao
returning id",
        "pre_matricula_inscricao" => "insert into sigov.pre_matricula_inscricao (tenant_id,entidade_id,exercicio_id,escola_preferencial_id,aluno_pessoa_id,responsavel_pessoa_id,protocolo,ano_letivo,etapa_ensino,turno,status,pontuacao,observacao,created_by) values (@TenantId,@EntidadeId,@ExercicioId,@EscolaPreferencialId,@AlunoPessoaId,@ResponsavelPessoaId,@Protocolo,@AnoLetivo,@EtapaEnsino,@Turno,'RASCUNHO',@Pontuacao,@Observacao,@UsuarioId) returning id",
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
