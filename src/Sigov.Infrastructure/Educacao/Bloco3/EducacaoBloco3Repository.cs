using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Dapper;
using Sigov.Application.Educacao.Bloco3;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Educacao.Bloco3;

public sealed class EducacaoBloco3Repository : IEducacaoSecretariaRepository, IEducacaoDiarioClasseRepository, IEducacaoPortalRepository
{
    private readonly DapperContext _context;
    public EducacaoBloco3Repository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<T>> ListarAsync<T>(long tenantId, string recurso, EducacaoBloco3Filtro filtro, long? usuarioId, bool administrativo, CancellationToken ct)
    {
        var definition = Definicao(recurso);
        var where = "x.tenant_id=@TenantId and x.is_deleted=false";
        if (filtro.AlunoId.HasValue && definition.TemAluno) where += " and x.aluno_id=@AlunoId";
        if (!string.IsNullOrWhiteSpace(filtro.Status) && definition.TemStatus) where += " and x.status=@Status";
        if (!string.IsNullOrWhiteSpace(filtro.Tipo) && definition.TemTipo) where += " and x.tipo=@Tipo";
        if (!administrativo && definition.ProtegidoPorVinculo) where += " and exists (select 1 from sigov.educacao_portal_vinculo v where v.tenant_id=x.tenant_id and v.aluno_id=x.aluno_id and v.usuario_id=@UsuarioId and v.status='ATIVO' and v.is_deleted=false)";
        if (recurso == "portal-ocorrencia") where += " and x.visivel_portal=true and x.sensivel=false";
        if (!administrativo && recurso == "portal-solicitacao") where += " and x.usuario_id=@UsuarioId";
        if (!administrativo && recurso == "portal-mensagem") where += " and x.usuario_id=@UsuarioId";
        if (!administrativo && recurso == "portal-comunicado") where += " and exists (select 1 from sigov.educacao_comunicado_destinatario d where d.tenant_id=x.tenant_id and d.comunicado_id=x.id and d.usuario_id=@UsuarioId)";
        var sql = $"select {definition.Colunas} from sigov.{definition.Tabela} x where {where} order by x.id desc limit 250";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<T>(new CommandDefinition(sql, new { TenantId = tenantId, filtro.AlunoId, filtro.Status, filtro.Tipo, UsuarioId = usuarioId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<T?> ObterAsync<T>(long tenantId, string recurso, long id, long? usuarioId, bool administrativo, CancellationToken ct)
    {
        var itens = await ListarAsync<T>(tenantId, recurso, new EducacaoBloco3Filtro(), usuarioId, administrativo, ct).ConfigureAwait(false);
        return itens.FirstOrDefault(x => Convert.ToInt64(x?.GetType().GetProperty("Id")?.GetValue(x), System.Globalization.CultureInfo.InvariantCulture) == id);
    }

    public async Task<long> CriarAsync(long tenantId, long entidadeId, long? exercicioId, string recurso, object dados, long usuarioId, string correlationId, CancellationToken ct)
    {
        var p = Parametros(dados); p.Add("TenantId", tenantId); p.Add("EntidadeId", entidadeId); p.Add("ExercicioId", exercicioId); p.Add("UsuarioId", usuarioId); p.Add("CorrelationId", correlationId); p.Add("Dados", JsonSerializer.Serialize(dados));
        var sql = Insercao(recurso);
        using var connection = _context.CreateConnection();
        if (new[] { "aula", "conteudo", "frequencia", "avaliacao", "reposicao" }.Contains(recurso, StringComparer.OrdinalIgnoreCase))
        {
            var diarioId = p.Get<long>("DiarioId");
            var editavel = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_diario_classe where tenant_id=@TenantId and id=@DiarioId and status in ('ABERTO','PENDENTE','REABERTO') and is_deleted=false)", new { TenantId=tenantId, DiarioId=diarioId }, cancellationToken:ct)).ConfigureAwait(false);
            if (!editavel) throw new InvalidOperationException("Somente diário aberto, pendente ou reaberto do contexto autorizado aceita lançamentos.");
        }
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task AlterarStatusAsync(long tenantId, string recurso, long id, string status, string justificativa, long usuarioId, string correlationId, CancellationToken ct)
    {
        var d = Definicao(recurso);
        var historico = recurso switch { "solicitacao" => "educacao_solicitacao_historico", "portal-solicitacao" => "educacao_portal_solicitacao_historico", "diario" => "educacao_diario_historico", _ => null };
        using var connection = _context.CreateConnection(); connection.Open();
        using var tx = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition($"update sigov.{d.Tabela} set status=@Status,updated_at=now(),updated_by=@UsuarioId,auditoria=jsonb_build_object('acao','STATUS','justificativa',@Justificativa,'usuario_id',@UsuarioId),correlation_id=@CorrelationId where tenant_id=@TenantId and id=@Id and is_deleted=false", new { TenantId=tenantId, Id=id, Status=status, Justificativa=justificativa, UsuarioId=usuarioId, CorrelationId=correlationId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        if (historico is not null)
        {
            var fk = recurso == "diario" ? "diario_id" : "solicitacao_id";
            var texto = recurso == "portal-solicitacao" ? "descricao" : "justificativa";
            await connection.ExecuteAsync(new CommandDefinition($"insert into sigov.{historico}(tenant_id,{fk},status,{texto},auditoria,correlation_id,created_by) values(@TenantId,@Id,@Status,@Justificativa,jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId)", new { TenantId=tenantId, Id=id, Status=status, Justificativa=justificativa, UsuarioId=usuarioId, CorrelationId=correlationId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        }
        tx.Commit();
    }

    public async Task<bool> MatriculaValidaAsync(long tenantId, long alunoId, long matriculaId, CancellationToken ct)
    { using var c=_context.CreateConnection(); return await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.matricula where tenant_id=@TenantId and id=@Id and aluno_id=@AlunoId and status in ('ATIVA','CONCLUIDA') and is_deleted=false)",new{TenantId=tenantId,Id=matriculaId,AlunoId=alunoId},cancellationToken:ct)).ConfigureAwait(false); }
    public async Task<bool> UsuarioVinculadoAsync(long tenantId, long usuarioId, long alunoId, CancellationToken ct)
    { using var c=_context.CreateConnection(); return await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.educacao_portal_vinculo where tenant_id=@TenantId and usuario_id=@UsuarioId and aluno_id=@AlunoId and status='ATIVO' and is_deleted=false)",new{TenantId=tenantId,UsuarioId=usuarioId,AlunoId=alunoId},cancellationToken:ct)).ConfigureAwait(false); }

    private static DynamicParameters Parametros(object value) { var p=new DynamicParameters(); foreach(var prop in value.GetType().GetProperties()) p.Add(prop.Name,prop.GetValue(value)); return p; }
    private static string Insercao(string r) => r switch
    {
        "documento" => "insert into sigov.educacao_documento_escolar(tenant_id,entidade_id,exercicio_id,aluno_id,matricula_id,tipo,status,titulo,html_emitido,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@AlunoId,@MatriculaId,'DECLARACAO_MATRICULA','EMITIDO',@Titulo,'<!doctype html><html><body><h1>'||@Titulo||'</h1></body></html>',cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "documento-frequencia" => "insert into sigov.educacao_documento_escolar(tenant_id,entidade_id,exercicio_id,aluno_id,matricula_id,tipo,status,titulo,descricao,html_emitido,dados,auditoria,correlation_id,created_by) select @TenantId,@EntidadeId,@ExercicioId,@AlunoId,@MatriculaId,'DECLARACAO_FREQUENCIA','EMITIDO',@Titulo,format('Período de %s a %s: %s aulas, %s presenças.',@Inicio,@Fim,count(f.id),count(f.id) filter (where f.presente)),format('<!doctype html><html><body><h1>%s</h1><p>Período de %s a %s</p><p>Frequência: %s%%</p></body></html>',@Titulo,@Inicio,@Fim,coalesce(round(100.0*count(f.id) filter (where f.presente)/nullif(count(f.id),0),2),0)),cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId,'fonte','diario_frequencia'),@CorrelationId,@UsuarioId from sigov.matricula m left join sigov.diario_frequencia f on f.tenant_id=m.tenant_id and f.aluno_id=m.aluno_id and f.data_aula between @Inicio and @Fim and f.is_deleted=false where m.tenant_id=@TenantId and m.id=@MatriculaId and m.aluno_id=@AlunoId group by m.id returning id",
        "solicitacao" => "insert into sigov.educacao_solicitacao_escolar(tenant_id,entidade_id,exercicio_id,aluno_id,responsavel_id,tipo,descricao,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@AlunoId,@ResponsavelId,@Tipo,@Descricao,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "pendencia" => "insert into sigov.educacao_pendencia_documental(tenant_id,entidade_id,aluno_id,matricula_id,tipo,descricao,data_vencimento,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@AlunoId,@MatriculaId,@Tipo,@Descricao,@DataVencimento,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "transferencia" => "insert into sigov.educacao_transferencia(tenant_id,entidade_id,exercicio_id,aluno_id,matricula_id,escola_destino_id,turma_destino_id,justificativa_externa,descricao,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@AlunoId,@MatriculaId,@EscolaDestinoId,@TurmaDestinoId,@JustificativaExterna,coalesce(@JustificativaExterna,'Transferência interna'),cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "ocorrencia" => "insert into sigov.educacao_ocorrencia_escolar(tenant_id,entidade_id,exercicio_id,aluno_id,matricula_id,tipo,descricao,data_ocorrencia,visivel_portal,sensivel,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@AlunoId,@MatriculaId,@Tipo,@Descricao,@DataOcorrencia,@VisivelPortal,@Sensivel,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "diario" => "insert into sigov.educacao_diario_classe(tenant_id,entidade_id,escola_id,turma_id,disciplina_id,professor_id,ano_letivo_id,periodo,status,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@EscolaId,@TurmaId,@DisciplinaId,@ProfessorId,@AnoLetivoId,@Periodo,'ABERTO',cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "aula" => "insert into sigov.educacao_diario_aula(tenant_id,diario_id,data_aula,carga_horaria,observacoes,dados,auditoria,correlation_id,created_by) values(@TenantId,@DiarioId,@DataAula,@CargaHoraria,@Observacoes,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "conteudo" => "insert into sigov.educacao_diario_conteudo(tenant_id,diario_id,aula_id,conteudo,observacoes,dados,auditoria,correlation_id,created_by) values(@TenantId,@DiarioId,@AulaId,@Conteudo,@Observacoes,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "frequencia" => "insert into sigov.educacao_diario_frequencia(tenant_id,diario_id,aula_id,aluno_id,status,justificativa,dados,auditoria,correlation_id,created_by) select @TenantId,@DiarioId,@AulaId,(a->>'AlunoId')::bigint,upper(a->>'Status'),nullif(a->>'Justificativa',''),a,jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId from jsonb_array_elements(cast(@Dados as jsonb)->'Alunos') a on conflict (tenant_id,aula_id,aluno_id) where is_deleted=false do update set status=excluded.status,justificativa=excluded.justificativa,updated_at=now(),updated_by=@UsuarioId,auditoria=excluded.auditoria returning id",
        "avaliacao" => "insert into sigov.educacao_diario_avaliacao(tenant_id,diario_id,aula_id,titulo,valor_maximo,peso,dados,auditoria,correlation_id,created_by) values(@TenantId,@DiarioId,@AulaId,@Titulo,@ValorMaximo,@Peso,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "reposicao" => "insert into sigov.educacao_diario_reposicao(tenant_id,diario_id,aula_id,data_reposicao,justificativa,dados,auditoria,correlation_id,created_by) values(@TenantId,@DiarioId,@AulaId,@DataReposicao,@Justificativa,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "portal-solicitacao" => "insert into sigov.educacao_portal_solicitacao(tenant_id,usuario_id,aluno_id,tipo,descricao,dados,auditoria,correlation_id,created_by) values(@TenantId,@UsuarioId,@AlunoId,@Tipo,@Descricao,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@CorrelationId,@UsuarioId) returning id",
        "portal-vinculo" => "insert into sigov.educacao_portal_vinculo(tenant_id,usuario_id,aluno_id,responsavel_id,dados,auditoria,created_by) values(@TenantId,@UsuarioVinculadoId,@AlunoId,@ResponsavelId,cast(@Dados as jsonb),jsonb_build_object('created_by',@UsuarioId),@UsuarioId) returning id",
        "portal-comunicado" => "insert into sigov.educacao_comunicado(tenant_id,escola_id,turma_id,titulo,mensagem,dados,auditoria,created_by) values(@TenantId,@EscolaId,@TurmaId,@Titulo,@Mensagem,cast(@Dados as jsonb),jsonb_build_object('usuario_id',@UsuarioId),@UsuarioId) returning id",
        _ => throw new ArgumentOutOfRangeException(nameof(r), "Recurso sem operação de criação.")
    };

    private static Def Definicao(string r) => r switch
    {
        "documento" => new("educacao_documento_escolar","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.matricula_id as \"MatriculaId\",x.tipo as \"Tipo\",x.status as \"Status\",x.titulo as \"Titulo\",x.created_at as \"CreatedAt\"",true,true,true,false),
        "documento-frequencia" => new("educacao_documento_escolar","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.matricula_id as \"MatriculaId\",x.tipo as \"Tipo\",x.status as \"Status\",x.titulo as \"Titulo\",x.created_at as \"CreatedAt\"",true,true,true,false),
        "solicitacao" => new("educacao_solicitacao_escolar","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.tipo as \"Tipo\",x.status as \"Status\",x.descricao as \"Descricao\",x.created_at as \"CreatedAt\"",true,true,true,true),
        "pendencia" => new("educacao_pendencia_documental","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.tipo as \"Tipo\",x.status as \"Status\",x.data_vencimento as \"DataVencimento\",(x.status='PENDENTE' and x.data_vencimento<now()) as \"Vencida\"",true,true,true,false),
        "transferencia" => new("educacao_transferencia","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.matricula_id as \"MatriculaId\",x.status as \"Status\",x.descricao as \"Descricao\",x.created_at as \"CreatedAt\"",true,true,true,false),
        "ocorrencia" or "portal-ocorrencia" => new("educacao_ocorrencia_escolar","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.tipo as \"Tipo\",x.descricao as \"Descricao\",x.visivel_portal as \"VisivelPortal\",x.sensivel as \"Sensivel\",x.data_ocorrencia as \"DataOcorrencia\"",true,true,true,r.StartsWith("portal")),
        "diario" => new("educacao_diario_classe","x.id as \"Id\",x.escola_id as \"EscolaId\",x.turma_id as \"TurmaId\",x.disciplina_id as \"DisciplinaId\",x.professor_id as \"ProfessorId\",x.periodo as \"Periodo\",x.status as \"Status\"",false,true,false,false),
        "diario-pendencia" => new("educacao_diario_pendencia","x.id as \"Id\",x.diario_id as \"DiarioId\",x.tipo as \"Tipo\",x.descricao as \"Descricao\",x.status as \"Status\"",false,true,true,false),
        "portal-solicitacao" => new("educacao_portal_solicitacao","x.id as \"Id\",x.aluno_id as \"AlunoId\",x.tipo as \"Tipo\",x.status as \"Status\",x.descricao as \"Descricao\",x.created_at as \"CreatedAt\"",true,true,true,true),
        "portal-comunicado" => new("educacao_comunicado","x.id as \"Id\",x.titulo as \"Titulo\",x.mensagem as \"Mensagem\",x.created_at as \"CreatedAt\"",false,true,true,false),
        "portal-mensagem" => new("educacao_portal_mensagem","x.id as \"Id\",x.titulo as \"Titulo\",x.mensagem as \"Mensagem\",(x.status='LIDA') as \"Lida\",x.created_at as \"CreatedAt\"",false,true,true,false),
        "portal-vinculo" => new("educacao_portal_vinculo","x.id as \"Id\",x.usuario_id as \"UsuarioId\",x.aluno_id as \"AlunoId\",x.responsavel_id as \"ResponsavelId\",x.status as \"Status\"",true,true,true,false),
        _ => throw new ArgumentOutOfRangeException(nameof(r), "Recurso de consulta inválido.")
    };
    private sealed record Def(string Tabela,string Colunas,bool TemAluno,bool TemStatus,bool TemTipo,bool ProtegidoPorVinculo);

    public async Task<string?> ObterStatusAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        var tabela = Definicao(recurso).Tabela;
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"select status from sigov.{tabela} where tenant_id=@TenantId and id=@Id and is_deleted=false",
            new { TenantId = tenantId, Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<bool> DiarioProntoParaFechamentoAsync(long tenantId, long diarioId, CancellationToken ct)
    {
        const string sql = @"select exists (
 select 1 from sigov.educacao_diario_aula a
 where a.tenant_id=@TenantId and a.diario_id=@DiarioId and a.is_deleted=false
) and not exists (
 select 1 from sigov.educacao_diario_aula a
 where a.tenant_id=@TenantId and a.diario_id=@DiarioId and a.is_deleted=false
 and (not exists (select 1 from sigov.educacao_diario_conteudo c where c.tenant_id=a.tenant_id and c.aula_id=a.id and c.is_deleted=false)
   or not exists (select 1 from sigov.educacao_diario_frequencia f where f.tenant_id=a.tenant_id and f.aula_id=a.id and f.is_deleted=false))
)";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { TenantId = tenantId, DiarioId = diarioId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<EducacaoDiarioConferenciaDto?> ConferirDiarioAsync(long tenantId, long diarioId, CancellationToken ct)
    {
        using var connection = _context.CreateConnection();
        return await ConferirAsync(connection, null, tenantId, diarioId, ct).ConfigureAwait(false);
    }

    public async Task<long> FecharDiarioAsync(long tenantId, long diarioId, string tokenConferencia, string justificativa, long usuarioId, string correlationId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var status = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select status from sigov.educacao_diario_classe where tenant_id=@TenantId and id=@DiarioId and is_deleted=false for update",
            new { TenantId = tenantId, DiarioId = diarioId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (status is null) throw new InvalidOperationException("Diário não encontrado no contexto autorizado.");
        if (status == "FECHADO")
        {
            var existente = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("select id from sigov.educacao_diario_fechamento where tenant_id=@TenantId and diario_id=@DiarioId and status='FECHADO' order by versao desc limit 1", new { TenantId=tenantId, DiarioId=diarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
            if (existente.HasValue) { await tx.CommitAsync(ct).ConfigureAwait(false); return existente.Value; }
        }
        if (status is not ("ABERTO" or "PENDENTE" or "REABERTO")) throw new InvalidOperationException($"O diário em estado {status} não pode ser fechado.");
        var conferencia = await ConferirAsync(connection, tx, tenantId, diarioId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Diário não encontrado no contexto autorizado.");
        if (tokenConferencia.Length != conferencia.TokenConferencia.Length || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(conferencia.TokenConferencia), Encoding.UTF8.GetBytes(tokenConferencia)))
            throw new InvalidOperationException("Os lançamentos mudaram após a prévia. Faça uma nova conferência antes de confirmar.");
        if (!conferencia.PodeFechar) throw new InvalidOperationException("O fechamento está bloqueado pelas pendências obrigatórias apresentadas na conferência.");
        var anterior = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("select id from sigov.educacao_diario_fechamento where tenant_id=@TenantId and diario_id=@DiarioId order by versao desc limit 1", new { TenantId=tenantId, DiarioId=diarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        var versao = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select coalesce(max(versao),0)+1 from sigov.educacao_diario_fechamento where tenant_id=@TenantId and diario_id=@DiarioId", new { TenantId=tenantId, DiarioId=diarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        var snapshot = JsonSerializer.Serialize(conferencia);
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.educacao_diario_fechamento
(tenant_id,diario_id,periodo,status,justificativa,dados,auditoria,correlation_id,created_by,versao,token_conferencia,retifica_fechamento_id)
select @TenantId,d.id,d.periodo,'FECHADO',@Justificativa,cast(@Snapshot as jsonb),jsonb_build_object('usuario_id',@UsuarioId,'acao','FECHAMENTO'),@CorrelationId,@UsuarioId,@Versao,@Token,@Anterior
from sigov.educacao_diario_classe d where d.tenant_id=@TenantId and d.id=@DiarioId returning id", new { TenantId=tenantId, DiarioId=diarioId, Justificativa=justificativa, Snapshot=snapshot, UsuarioId=usuarioId, CorrelationId=correlationId, Versao=versao, Token=conferencia.TokenConferencia, Anterior=anterior }, tx, cancellationToken:ct)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("update sigov.educacao_diario_classe set status='FECHADO',updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and id=@DiarioId", new { TenantId=tenantId, DiarioId=diarioId, UsuarioId=usuarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_diario_historico(tenant_id,diario_id,status,justificativa,auditoria,correlation_id,created_by) values(@TenantId,@DiarioId,'FECHADO',@Justificativa,jsonb_build_object('usuario_id',@UsuarioId,'fechamento_id',@Id),@CorrelationId,@UsuarioId)", new { TenantId=tenantId, DiarioId=diarioId, Justificativa=justificativa, UsuarioId=usuarioId, Id=id, CorrelationId=correlationId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return id;
    }

    public async Task ReabrirDiarioAsync(long tenantId, long diarioId, string justificativa, long usuarioId, string correlationId, CancellationToken ct)
    {
        using var connection = (NpgsqlConnection)_context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var changed = await connection.ExecuteAsync(new CommandDefinition("update sigov.educacao_diario_classe set status='REABERTO',updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and id=@DiarioId and status='FECHADO' and is_deleted=false", new { TenantId=tenantId, DiarioId=diarioId, UsuarioId=usuarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        if (changed != 1) throw new InvalidOperationException("Somente um diário fechado do contexto autorizado pode ser reaberto.");
        await connection.ExecuteAsync(new CommandDefinition("insert into sigov.educacao_diario_historico(tenant_id,diario_id,status,justificativa,auditoria,correlation_id,created_by) values(@TenantId,@DiarioId,'REABERTO',@Justificativa,jsonb_build_object('usuario_id',@UsuarioId,'acao','REABERTURA'),@CorrelationId,@UsuarioId)", new { TenantId=tenantId, DiarioId=diarioId, Justificativa=justificativa, UsuarioId=usuarioId, CorrelationId=correlationId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<EducacaoDiarioFechamentoDto>> HistoricoFechamentoAsync(long tenantId, long diarioId, CancellationToken ct)
    {
        const string sql = "select id as \"Id\",diario_id as \"DiarioId\",versao as \"Versao\",status as \"Status\",justificativa as \"Justificativa\",created_at as \"CreatedAt\",created_by as \"CreatedBy\",retifica_fechamento_id as \"RetificaFechamentoId\" from sigov.educacao_diario_fechamento where tenant_id=@TenantId and diario_id=@DiarioId order by versao desc";
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<EducacaoDiarioFechamentoDto>(new CommandDefinition(sql, new { TenantId=tenantId, DiarioId=diarioId }, cancellationToken:ct)).ConfigureAwait(false)).AsList();
    }

    private static async Task<EducacaoDiarioConferenciaDto?> ConferirAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction? tx, long tenantId, long diarioId, CancellationToken ct)
    {
        const string summarySql = @"with d as (
 select * from sigov.educacao_diario_classe where tenant_id=@TenantId and id=@DiarioId and is_deleted=false
), aulas as (
 select a.* from sigov.educacao_diario_aula a join d on d.id=a.diario_id where a.tenant_id=@TenantId and a.is_deleted=false and a.status<>'CANCELADA'
), elegiveis as (
 select distinct m.aluno_id from sigov.matricula m join d on d.turma_id=m.turma_id and d.ano_letivo_id=m.ano_letivo_id
 where m.tenant_id=@TenantId and m.is_deleted=false and m.status in ('ATIVA','CONFIRMADA','ENCERRADA')
 and (not exists(select 1 from aulas) or m.data_matricula<=(select max(data_aula) from aulas))
)
select d.status as ""Status"",count(distinct e.aluno_id) as ""Alunos"",count(distinct a.id) as ""Aulas"",
 count(distinct e.aluno_id)*count(distinct a.id) as ""Esperados"",
 (select count(*) from sigov.educacao_diario_frequencia f join aulas ax on ax.id=f.aula_id join elegiveis ex on ex.aluno_id=f.aluno_id where f.tenant_id=@TenantId and f.is_deleted=false) as ""Realizados"",
 coalesce(greatest(d.updated_at,d.created_at),d.created_at) as ""Atualizado""
from d left join aulas a on true left join elegiveis e on true group by d.status,d.updated_at,d.created_at";
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(summarySql, new { TenantId=tenantId, DiarioId=diarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        if (row is null) return null;
        long alunos=(long)row.Alunos, aulas=(long)row.Aulas, esperados=(long)row.Esperados, realizados=(long)row.Realizados;
        var details = new List<EducacaoDiarioPendenciaDetalheDto>();
        if (aulas == 0) details.Add(new("SEM_AULA", $"Diário {diarioId}", "Nenhuma aula válida foi registrada no período.", "Registre ao menos uma aula antes da conferência.", $"/Educacao/DiarioAulas?diarioId={diarioId}"));
        if (alunos == 0) details.Add(new("SEM_MATRICULA_ELEGIVEL", $"Diário {diarioId}", "Nenhuma matrícula vigente foi localizada para a turma e ano letivo.", "Confira matrículas, enturmação e vigências.", $"/Educacao/Matriculas?diarioId={diarioId}"));
        var missingContent = await connection.QueryAsync<long>(new CommandDefinition("select a.id from sigov.educacao_diario_aula a where a.tenant_id=@TenantId and a.diario_id=@DiarioId and a.is_deleted=false and a.status<>'CANCELADA' and not exists(select 1 from sigov.educacao_diario_conteudo c where c.tenant_id=a.tenant_id and c.aula_id=a.id and c.is_deleted=false) order by a.id", new { TenantId=tenantId, DiarioId=diarioId }, tx, cancellationToken:ct)).ConfigureAwait(false);
        details.AddRange(missingContent.Select(id => new EducacaoDiarioPendenciaDetalheDto("CONTEUDO_PENDENTE", $"Aula {id}", "Conteúdo ministrado não lançado.", "Informe o conteúdo da aula.", $"/Educacao/DiarioConteudo?diarioId={diarioId}&aulaId={id}")));
        if (realizados < esperados) details.Add(new("FREQUENCIA_PENDENTE", $"Diário {diarioId}", $"Faltam {esperados-realizados} lançamentos de frequência; ausência de lançamento não equivale a falta.", "Complete a chamada dos alunos elegíveis.", $"/Educacao/DiarioFrequencia?diarioId={diarioId}"));
        var lancamentos = await connection.ExecuteScalarAsync<string>(new CommandDefinition(@"select concat_ws('|',
 coalesce((select string_agg(concat_ws(':',a.id,a.data_aula,a.status,a.updated_at,a.created_at),',' order by a.id) from sigov.educacao_diario_aula a where a.tenant_id=@TenantId and a.diario_id=@DiarioId and a.is_deleted=false),''),
 coalesce((select string_agg(concat_ws(':',c.aula_id,c.conteudo,c.updated_at,c.created_at),',' order by c.aula_id,c.id) from sigov.educacao_diario_conteudo c where c.tenant_id=@TenantId and c.diario_id=@DiarioId and c.is_deleted=false),''),
 coalesce((select string_agg(concat_ws(':',f.aula_id,f.aluno_id,f.status,f.justificativa,f.updated_at,f.created_at),',' order by f.aula_id,f.aluno_id) from sigov.educacao_diario_frequencia f where f.tenant_id=@TenantId and f.diario_id=@DiarioId and f.is_deleted=false),''))", new { TenantId=tenantId, DiarioId=diarioId }, tx, cancellationToken:ct)).ConfigureAwait(false) ?? string.Empty;
        var raw = $"{diarioId}|{row.Status}|{alunos}|{aulas}|{esperados}|{realizados}|{string.Join(',', missingContent)}|{lancamentos}";
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var atualizado = row.Atualizado is DateTimeOffset dto ? dto : new DateTimeOffset(DateTime.SpecifyKind((DateTime)row.Atualizado, DateTimeKind.Utc));
        var statusAtual = (string)row.Status;
        var podeFechar = details.Count == 0 && statusAtual is ("ABERTO" or "PENDENTE" or "REABERTO");
        return new(diarioId,statusAtual,alunos,aulas,esperados,realizados,details.Count,details,token,atualizado,podeFechar);
    }

}
