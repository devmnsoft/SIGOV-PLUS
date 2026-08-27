using Dapper;
using Sigov.Application.Cidadao;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Cidadao;

public sealed class CidadaoRepository(DapperContext db) : ICidadaoRepository
{
    public async Task<IReadOnlyList<CidadaoServico>> ListarServicosAsync(CidadaoContexto c, string? busca, string? categoria, CancellationToken ct)
    {
        const string sql = """select id, nome, categoria, descricao, publico_alvo PublicoAlvo, prazo_estimado_dias PrazoDias, canal, unidade_responsavel UnidadeResponsavel, documentos_necessarios Requisitos, destaque from sigov.cidadao_servico_catalogo where tenant_id=@TenantId and entidade_id=@EntidadeId and status='PUBLICADO' and ativo and not is_deleted and (@Busca is null or nome ilike '%'||@Busca||'%' or descricao ilike '%'||@Busca||'%') and (@Categoria is null or categoria=@Categoria) order by destaque desc, nome limit 100""";
        using var cn = db.CreateConnection();
        return (await cn.QueryAsync<CidadaoServico>(new CommandDefinition(sql, new { c.TenantId, c.EntidadeId, Busca=Normalize(busca), Categoria=Normalize(categoria)?.ToUpperInvariant() }, cancellationToken:ct))).AsList();
    }

    public async Task<CidadaoServico?> ObterServicoAsync(CidadaoContexto c,long id,CancellationToken ct)
    {
        const string sql="""select id,nome,categoria,descricao,publico_alvo PublicoAlvo,prazo_estimado_dias PrazoDias,canal,unidade_responsavel UnidadeResponsavel,documentos_necessarios Requisitos,destaque from sigov.cidadao_servico_catalogo where id=@id and tenant_id=@TenantId and entidade_id=@EntidadeId and status='PUBLICADO' and ativo and not is_deleted""";
        using var cn=db.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<CidadaoServico>(new CommandDefinition(sql,new{id,c.TenantId,c.EntidadeId},cancellationToken:ct));
    }

    public async Task<CidadaoSolicitacao> AbrirSolicitacaoAsync(CidadaoContexto c,AbrirSolicitacaoRequest r,CancellationToken ct)
    {
        if(c.PessoaId is null or <=0) throw new UnauthorizedAccessException("A solicitação exige cidadão identificado.");
        using var cn=db.CreateConnection(); cn.Open(); using var tx=cn.BeginTransaction();
        const string serviceSql="select nome,prazo_estimado_dias,unidade_responsavel from sigov.cidadao_servico_catalogo where id=@ServicoId and tenant_id=@TenantId and entidade_id=@EntidadeId and status='PUBLICADO' and ativo and not is_deleted for share";
        var servico=await cn.QuerySingleOrDefaultAsync<ServicoAbertura>(new CommandDefinition(serviceSql,new{r.ServicoId,c.TenantId,c.EntidadeId},tx,cancellationToken:ct));
        if(string.IsNullOrWhiteSpace(servico.Nome)) throw new InvalidOperationException("O serviço não está publicado ou foi suspenso.");
        const string sql="""with seq as (select nextval('sigov.cidadao_protocolo_seq') numero), ins as (insert into sigov.cidadao_solicitacao_servico(tenant_id,entidade_id,servico_id,pessoa_id,numero_protocolo,codigo_verificador,status,canal,descricao,email_contato,telefone_contato,aceite_lgpd_em,prazo_em,created_by) select @TenantId,@EntidadeId,@ServicoId,@PessoaId,to_char(current_date,'YYYY')||'-'||lpad(numero::text,10,'0'),upper(substr(encode(gen_random_bytes(8),'hex'),1,12)),'PROTOCOLADO','PORTAL',@Descricao,@Email,@Telefone,now(),now()+make_interval(days=>@PrazoDias),@UsuarioId from seq returning *) select id,numero_protocolo Protocolo,codigo_verificador CodigoVerificador,status,created_at CriadaEm,prazo_em PrazoEm from ins""";
        var row=await cn.QuerySingleAsync<SolicitacaoCriada>(new CommandDefinition(sql,new{c.TenantId,c.EntidadeId,r.ServicoId,c.PessoaId,r.Descricao,r.Email,r.Telefone,servico.PrazoDias,c.UsuarioId},tx,cancellationToken:ct));
        await cn.ExecuteAsync(new CommandDefinition("insert into sigov.cidadao_solicitacao_historico(tenant_id,entidade_id,solicitacao_id,status,descricao,visivel_cidadao,created_by) values(@TenantId,@EntidadeId,@Id,'PROTOCOLADO','Solicitação recebida no Portal Cidadão360.',true,@UsuarioId)",new{c.TenantId,c.EntidadeId,row.Id,c.UsuarioId},tx,cancellationToken:ct));
        await Audit(c,cn,tx,"CRIAR_SOLICITACAO",row.Id,ct); tx.Commit();
        return new CidadaoSolicitacao { Id=row.Id,Protocolo=row.Protocolo,CodigoVerificador=row.CodigoVerificador,Servico=servico.Nome,Status=row.Status,CriadaEm=row.CriadaEm,PrazoEm=row.PrazoEm,UnidadeResponsavel=servico.UnidadeResponsavel };
    }

    public async Task<IReadOnlyList<CidadaoSolicitacao>> MinhasSolicitacoesAsync(CidadaoContexto c,CancellationToken ct)
    {
        if(c.PessoaId is null or <=0) throw new UnauthorizedAccessException("Cidadão não identificado.");
        const string sql="""select s.id,s.numero_protocolo Protocolo,s.codigo_verificador CodigoVerificador,v.nome Servico,s.status,s.created_at CriadaEm,s.prazo_em PrazoEm,v.unidade_responsavel UnidadeResponsavel from sigov.cidadao_solicitacao_servico s join sigov.cidadao_servico_catalogo v on v.id=s.servico_id and v.tenant_id=s.tenant_id and v.entidade_id=s.entidade_id where s.tenant_id=@TenantId and s.entidade_id=@EntidadeId and s.pessoa_id=@PessoaId and not s.is_deleted order by s.created_at desc limit 100""";
        using var cn=db.CreateConnection();return (await cn.QueryAsync<CidadaoSolicitacao>(new CommandDefinition(sql,c,cancellationToken:ct))).AsList();
    }

    public async Task<CidadaoSolicitacao?> ConsultarProtocoloAsync(CidadaoContexto c,string protocolo,string verificador,bool proprietario,CancellationToken ct)
    {
        const string sql="""select s.id,s.numero_protocolo Protocolo,s.codigo_verificador CodigoVerificador,v.nome Servico,s.status,s.created_at CriadaEm,s.prazo_em PrazoEm,v.unidade_responsavel UnidadeResponsavel from sigov.cidadao_solicitacao_servico s join sigov.cidadao_servico_catalogo v on v.id=s.servico_id and v.tenant_id=s.tenant_id and v.entidade_id=s.entidade_id where s.tenant_id=@TenantId and s.entidade_id=@EntidadeId and s.numero_protocolo=@protocolo and not s.is_deleted and ((@proprietario and s.pessoa_id=@PessoaId) or s.codigo_verificador=upper(@verificador))""";
        using var cn=db.CreateConnection();var item=await cn.QuerySingleOrDefaultAsync<CidadaoSolicitacao>(new CommandDefinition(sql,new{c.TenantId,c.EntidadeId,c.PessoaId,protocolo,verificador,proprietario},cancellationToken:ct));if(item is null)return null;
        var history=(await cn.QueryAsync<CidadaoHistorico>(new CommandDefinition("select status,descricao,created_at RegistradoEm from sigov.cidadao_solicitacao_historico where tenant_id=@TenantId and entidade_id=@EntidadeId and solicitacao_id=@Id and visivel_cidadao and not is_deleted order by created_at",new{c.TenantId,c.EntidadeId,item.Id},cancellationToken:ct))).AsList();
        await Audit(c,cn,null,"CONSULTAR_PROTOCOLO",item.Id,ct);item.Historico=history;return item;
    }

    public async Task<CidadaoDashboard> DashboardAsync(CidadaoContexto c,CancellationToken ct){const string sql="""select (select count(*) from sigov.cidadao_solicitacao_servico where tenant_id=@TenantId and entidade_id=@EntidadeId and status not in('DEFERIDO','INDEFERIDO','CANCELADO','ENCERRADO') and not is_deleted) Abertas,(select count(*) from sigov.cidadao_solicitacao_servico where tenant_id=@TenantId and entidade_id=@EntidadeId and prazo_em<now() and status not in('DEFERIDO','INDEFERIDO','CANCELADO','ENCERRADO') and not is_deleted) Vencidas,(select count(*) from sigov.atendimento_ouvidoria_manifestacao where tenant_id=@TenantId and entidade_id=@EntidadeId and status not in('ENCERRADO','ARQUIVADO') and not is_deleted) OuvidoriasAbertas,(select count(*) from sigov.cidadao_agendamento where tenant_id=@TenantId and entidade_id=@EntidadeId and inicio_em::date=current_date and not is_deleted) AgendamentosHoje,(select avg(nota) from sigov.cidadao_avaliacao_atendimento where tenant_id=@TenantId and entidade_id=@EntidadeId and not is_deleted) AvaliacaoMedia""";using var cn=db.CreateConnection();return await cn.QuerySingleAsync<CidadaoDashboard>(new CommandDefinition(sql,c,cancellationToken:ct));}
    private static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private sealed class ServicoAbertura { public string Nome { get; set; }=string.Empty; public int PrazoDias { get; set; } public string? UnidadeResponsavel { get; set; } }
    private sealed class SolicitacaoCriada { public long Id { get; set; } public string Protocolo { get; set; }=string.Empty; public string CodigoVerificador { get; set; }=string.Empty; public string Status { get; set; }=string.Empty; public DateTimeOffset CriadaEm { get; set; } public DateTimeOffset? PrazoEm { get; set; } }
    private static Task Audit(CidadaoContexto c,System.Data.IDbConnection cn,System.Data.IDbTransaction? tx,string acao,long id,CancellationToken ct)=>cn.ExecuteAsync(new CommandDefinition("insert into sigov.atendimento_auditoria(acao,recurso,registro_id,usuario_id,correlation_id,ip,tenant_id,entidade_id,created_by,descricao,status,dados) values(@acao,'cidadao-solicitacao',@id,@UsuarioId,@CorrelationId,cast(@Ip as inet),@TenantId,@EntidadeId,@UsuarioId,'Cidadão360','REGISTRADA','{}')",new{acao,id,c.UsuarioId,c.CorrelationId,c.Ip,c.TenantId,c.EntidadeId},tx,cancellationToken:ct));
}
