using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Workflows;

namespace Sigov.Web.Services.Workflows;

public sealed class WorkflowValidationService
{
    public IReadOnlyList<string> Validate(SaveWorkflowDesignInput design)
    {
        var errors = new List<string>();
        if (design.Etapas.Count < 2) errors.Add("Inclua ao menos duas etapas.");
        if (design.Etapas.Count(x => x.Inicial) != 1) errors.Add("Defina exatamente uma etapa inicial.");
        if (design.Etapas.Count(x => x.Final) < 1) errors.Add("Defina ao menos uma etapa final.");
        if (design.Etapas.Select(x => x.Nome.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != design.Etapas.Count) errors.Add("Os nomes das etapas devem ser únicos.");
        return errors;
    }
}

public sealed class WorkflowRepository
{
    private readonly NpgsqlConnectionFactory _factory;
    public WorkflowRepository(NpgsqlConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<WorkflowSummary>> ListAsync(long tenantId, CancellationToken ct)
    {
        const string sql = "select d.id, d.nome, d.modulo, d.status, coalesce(max(v.numero),0) as versao, d.updated_at as atualizadoEm from sigov.workflow_definicao d left join sigov.workflow_versao v on v.workflow_definicao_id=d.id and v.tenant_id=d.tenant_id where d.tenant_id=@TenantId and not d.is_deleted group by d.id,d.nome,d.modulo,d.status,d.updated_at order by d.updated_at desc";
        using var connection = _factory.CreateConnection();
        return (await connection.QueryAsync<WorkflowSummary>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))).AsList();
    }

    public async Task<long> CreateAsync(long tenantId, long? userId, CreateWorkflowInput input, CancellationToken ct)
    {
        const string sql = "insert into sigov.workflow_definicao(tenant_id,nome,descricao,modulo,status,created_by,updated_by) values(@TenantId,@Nome,@Descricao,@Modulo,'RASCUNHO',@UserId,@UserId) returning id";
        using var connection = _factory.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, Nome = input.Nome.Trim(), input.Descricao, input.Modulo }, cancellationToken: ct));
    }

    public async Task<WorkflowDesignerViewModel?> GetDesignerAsync(long tenantId, long id, CancellationToken ct)
    {
        using var connection = _factory.CreateConnection();
        var summary = await connection.QuerySingleOrDefaultAsync<WorkflowSummary>(new CommandDefinition("select id,nome,modulo,status,versao_atual as versao,updated_at as atualizadoEm from sigov.workflow_definicao where tenant_id=@TenantId and id=@Id and not is_deleted", new { TenantId=tenantId, Id=id }, cancellationToken:ct));
        if (summary is null) return null;
        var steps = (await connection.QueryAsync<WorkflowStep>(new CommandDefinition("select id,nome,descricao,tipo,ordem,prazo_horas as prazoHoras,inicial,final,exige_justificativa as exigeJustificativa,exige_anexo as exigeAnexo,exige_aprovacao as exigeAprovacao,permite_retorno as permiteRetorno from sigov.workflow_etapa where tenant_id=@TenantId and workflow_definicao_id=@Id order by ordem,id", new {TenantId=tenantId,Id=id}, cancellationToken:ct))).AsList();
        var transitions = (await connection.QueryAsync<WorkflowTransition>(new CommandDefinition("select id,de_etapa_id as deEtapaId,para_etapa_id as paraEtapaId,acao,condicao,permissao_necessaria as permissaoNecessaria,mensagem_usuario as mensagemUsuario from sigov.workflow_transicao where tenant_id=@TenantId and workflow_definicao_id=@Id order by id", new {TenantId=tenantId,Id=id}, cancellationToken:ct))).AsList();
        return new(summary, steps, transitions, []);
    }

    public async Task SaveDesignAsync(long tenantId, long id, SaveWorkflowDesignInput design, CancellationToken ct)
    {
        using var connection = _factory.CreateConnection(); await connection.OpenAsync(ct); using var tx = await connection.BeginTransactionAsync(ct);
        var editable = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.workflow_definicao where tenant_id=@TenantId and id=@Id and status='RASCUNHO' and not is_deleted)",new{TenantId=tenantId,Id=id},tx,cancellationToken:ct));
        if (!editable) throw new InvalidOperationException("Workflow publicado é somente leitura. Crie uma nova versão.");
        await connection.ExecuteAsync(new CommandDefinition("delete from sigov.workflow_transicao where tenant_id=@TenantId and workflow_definicao_id=@Id; delete from sigov.workflow_etapa where tenant_id=@TenantId and workflow_definicao_id=@Id",new{TenantId=tenantId,Id=id},tx,cancellationToken:ct));
        var ids = new Dictionary<long,long>();
        for (var i=0;i<design.Etapas.Count;i++) { var step=design.Etapas[i]; var newId=await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.workflow_etapa(tenant_id,workflow_definicao_id,nome,descricao,tipo,ordem,prazo_horas,inicial,final,exige_justificativa,exige_anexo,exige_aprovacao,permite_retorno) values(@TenantId,@WorkflowId,@Nome,@Descricao,@Tipo,@Ordem,@PrazoHoras,@Inicial,@Final,@ExigeJustificativa,@ExigeAnexo,@ExigeAprovacao,@PermiteRetorno) returning id",new{TenantId=tenantId,WorkflowId=id,step.Nome,step.Descricao,step.Tipo,Ordem=i+1,step.PrazoHoras,step.Inicial,step.Final,step.ExigeJustificativa,step.ExigeAnexo,step.ExigeAprovacao,step.PermiteRetorno},tx,cancellationToken:ct)); ids[step.Id ?? -(i+1)]=newId; }
        foreach(var transition in design.Transicoes) await connection.ExecuteAsync(new CommandDefinition("insert into sigov.workflow_transicao(tenant_id,workflow_definicao_id,de_etapa_id,para_etapa_id,acao,condicao,permissao_necessaria,mensagem_usuario) values(@TenantId,@WorkflowId,@From,@To,@Acao,@Condicao,@PermissaoNecessaria,@MensagemUsuario)",new{TenantId=tenantId,WorkflowId=id,From=ids.GetValueOrDefault(transition.DeEtapaId,transition.DeEtapaId),To=ids.GetValueOrDefault(transition.ParaEtapaId,transition.ParaEtapaId),transition.Acao,transition.Condicao,transition.PermissaoNecessaria,transition.MensagemUsuario},tx,cancellationToken:ct));
        await connection.ExecuteAsync(new CommandDefinition("update sigov.workflow_definicao set updated_at=now() where tenant_id=@TenantId and id=@Id",new{TenantId=tenantId,Id=id},tx,cancellationToken:ct)); await tx.CommitAsync(ct);
    }

    public async Task PublishAsync(long tenantId,long id,long? userId,CancellationToken ct)
    { using var connection=_factory.CreateConnection(); await connection.OpenAsync(ct); using var tx=await connection.BeginTransactionAsync(ct); var version=await connection.ExecuteScalarAsync<int>(new CommandDefinition("select versao_atual+1 from sigov.workflow_definicao where tenant_id=@TenantId and id=@Id and status='RASCUNHO' for update",new{TenantId=tenantId,Id=id},tx,cancellationToken:ct)); if(version==0) throw new InvalidOperationException("Workflow não está disponível para publicação."); await connection.ExecuteAsync(new CommandDefinition("insert into sigov.workflow_versao(tenant_id,workflow_definicao_id,numero,status,conteudo_json,publicado_por,publicado_em) select tenant_id,id,@Version,'PUBLICADO',jsonb_build_object('nome',nome,'modulo',modulo),@UserId,now() from sigov.workflow_definicao where tenant_id=@TenantId and id=@Id; update sigov.workflow_definicao set status='PUBLICADO',versao_atual=@Version,updated_at=now(),updated_by=@UserId where tenant_id=@TenantId and id=@Id",new{TenantId=tenantId,Id=id,Version=version,UserId=userId},tx,cancellationToken:ct)); await tx.CommitAsync(ct); }
}
