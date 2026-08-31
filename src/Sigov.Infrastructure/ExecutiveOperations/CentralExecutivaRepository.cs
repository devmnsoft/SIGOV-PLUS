using Dapper;
using Sigov.Application.Executive;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.ExecutiveOperations;

public sealed class CentralExecutivaRepository(DapperContext context) : ICentralExecutivaRepository
{
    private const string Filter = "tenant_id=@tenantId and entidade_id=@entidadeId and (@Modulo is null or modulo=@Modulo) and (@Status is null or status=@Status) and (@Prioridade is null or prioridade=@Prioridade) and (@ResponsavelId is null or responsavel_id=@ResponsavelId) and (@Inicio is null or created_at>=@Inicio) and (@Fim is null or created_at<(@Fim::date+1))";
    private static object Args(long tenantId,long entidadeId,ExecutivoFiltro f)=>new{tenantId,entidadeId,Modulo=Norm(f.Modulo),Status=Norm(f.Status),Prioridade=Norm(f.Prioridade),f.ResponsavelId,Inicio=f.Inicio?.ToDateTime(TimeOnly.MinValue),Fim=f.Fim?.ToDateTime(TimeOnly.MinValue)};
    public async Task<ExecutivoDashboard> DashboardAsync(long tenantId,long entidadeId,ExecutivoFiltro f,CancellationToken ct)
    {
        using var cn=context.CreateConnection(); var p=Args(tenantId,entidadeId,f);
        var resumo=await cn.QuerySingleAsync<ExecutivoResumo>(new CommandDefinition("select (select count(*) from sigov.executivo_meta where tenant_id=@tenantId and entidade_id=@entidadeId and status in ('EM_DIA','ATRASADA','CRITICA','REPROGRAMADA')) MetasAtivas,(select count(*) from sigov.executivo_meta where tenant_id=@tenantId and entidade_id=@entidadeId and (status in ('ATRASADA','CRITICA') or (prazo<current_date and percentual<100))) MetasAtrasadas,(select count(*) from sigov.executivo_pendencia where tenant_id=@tenantId and entidade_id=@entidadeId and prazo<current_date and status not in ('RESOLVIDA','CANCELADA')) PendenciasVencidas,(select count(*) from sigov.executivo_alerta where tenant_id=@tenantId and entidade_id=@entidadeId and severidade='CRITICA' and status='ATIVO') AlertasCriticos,(select count(*) from sigov.executivo_aprovacao where tenant_id=@tenantId and entidade_id=@entidadeId and status='PENDENTE') AprovacoesPendentes,(select count(*) from sigov.integracao_interna_evento where tenant_id=@tenantId and status='FALHA' and is_deleted=false) IntegracoesFalhas",p,cancellationToken:ct));
        var itens=await ListarAsync(tenantId,entidadeId,"pendencias",f,ct); var indicadores=await ListarAsync(tenantId,entidadeId,"indicadores",f,ct); return new(resumo,itens.Take(12).ToList(),indicadores,DateTimeOffset.UtcNow);
    }
    public async Task<IReadOnlyList<ExecutivoItem>> ListarAsync(long tenantId,long entidadeId,string recurso,ExecutivoFiltro f,CancellationToken ct)
    {
        var table=recurso.ToLowerInvariant() switch {"metas"=>"executivo_meta","pendencias"=>"executivo_pendencia","alertas"=>"executivo_alerta","encaminhamentos"=>"executivo_encaminhamento","aprovacoes"=>"executivo_aprovacao","decisoes"=>"executivo_decisao","briefing"=>"executivo_briefing","salasituacao"=>"executivo_sala_situacao","indicadores"=>"executivo_indicador",_=>throw new ArgumentOutOfRangeException(nameof(recurso))};
        var tipo=recurso.ToUpperInvariant(); var sql=$"select id,@tipo Tipo,titulo,modulo,status,prioridade,severidade,percentual,prazo,responsavel_nome Responsavel,updated_at AtualizadoEm from sigov.{table} where {Filter} order by coalesce(prazo,current_date+36500),updated_at desc limit 500";
        using var cn=context.CreateConnection(); var a=Args(tenantId,entidadeId,f); var dp=new DynamicParameters(a);dp.Add("tipo",tipo);return (await cn.QueryAsync<ExecutivoItem>(new CommandDefinition(sql,dp,cancellationToken:ct))).AsList();
    }
    public async Task MarcarAlertaCienteAsync(long tenantId,long entidadeId,long id,long? user,CancellationToken ct){using var cn=context.CreateConnection();var n=await cn.ExecuteAsync(new CommandDefinition("update sigov.executivo_alerta set ciente_at=coalesce(ciente_at,now()),ciente_by=coalesce(ciente_by,@user),updated_at=now() where id=@id and tenant_id=@tenantId and entidade_id=@entidadeId and status='ATIVO'",new{tenantId,entidadeId,id,user},cancellationToken:ct));if(n==0)throw new KeyNotFoundException("Alerta ativo não encontrado.");}
    public async Task DecidirAprovacaoAsync(long tenantId,long entidadeId,long id,bool ok,string justificativa,long? user,CancellationToken ct){using var cn=context.CreateConnection();var n=await cn.ExecuteAsync(new CommandDefinition("update sigov.executivo_aprovacao set status=case when @ok then 'APROVADA' else 'REJEITADA' end,justificativa=@justificativa,decidido_at=now(),decidido_by=@user,updated_at=now() where id=@id and tenant_id=@tenantId and entidade_id=@entidadeId and status='PENDENTE'",new{tenantId,entidadeId,id,ok,justificativa,user},cancellationToken:ct));if(n==0)throw new InvalidOperationException("Aprovação pendente não encontrada.");}
    public async Task RegistrarExportacaoAsync(long tenantId,long entidadeId,string tipo,long? user,CancellationToken ct){using var cn=context.CreateConnection();await cn.ExecuteAsync(new CommandDefinition("insert into sigov.executivo_relatorio_execucao(tenant_id,entidade_id,titulo,tipo,status,created_by,updated_by) values(@tenantId,@entidadeId,'Exportação executiva',@tipo,'CONCLUIDO',@user,@user)",new{tenantId,entidadeId,tipo,user},cancellationToken:ct));}
    private static string? Norm(string? x)=>string.IsNullOrWhiteSpace(x)?null:x.Trim().ToUpperInvariant();
}
