using System.Text.Json;
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
}
