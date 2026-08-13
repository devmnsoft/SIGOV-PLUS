using Dapper;
using Sigov.Application.Workflows;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Workflows;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly DapperContext _context;
    public WorkflowRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyList<WorkflowTarefaDto>> ListarTarefasAsync(long tenantId, long? responsavelId, string? modulo, string? status, CancellationToken ct)
    {
        const string sql = @"
            select t.id, t.instancia_id as InstanciaId, i.modulo, i.tipo_fluxo as TipoFluxo,
                   i.referencia_tipo as ReferenciaTipo, i.referencia_id as ReferenciaId,
                   t.status, t.etapa_atual as EtapaAtual, t.responsavel_id as ResponsavelId,
                   t.grupo_responsavel as GrupoResponsavel, t.prazo, t.prioridade,
                   t.dados::text as Dados, t.created_at as CreatedAt
              from sigov.workflow_tarefa t
              join sigov.workflow_instancia i on i.id=t.instancia_id and i.tenant_id=t.tenant_id and i.is_deleted=false
             where t.tenant_id=@TenantId and t.is_deleted=false
               and (@ResponsavelId is null or t.responsavel_id=@ResponsavelId)
               and (@Modulo is null or i.modulo=@Modulo)
               and (@Status is null or t.status=@Status)
             order by case t.prioridade when 'URGENTE' then 0 when 'ALTA' then 1 else 2 end, t.prazo nulls last, t.created_at desc;
            ";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<WorkflowTarefaDto>(new CommandDefinition(sql,
            new { TenantId=tenantId, ResponsavelId=responsavelId, Modulo=Normalize(modulo), Status=Normalize(status) }, cancellationToken:ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<WorkflowHistoricoDto>> ListarHistoricoAsync(long tenantId, long instanciaId, CancellationToken ct)
    {
        const string sql = @"select id, decisao, etapa_anterior as EtapaAnterior, etapa_nova as EtapaNova,
            justificativa, usuario_id as UsuarioId, created_at as CreatedAt, correlation_id as CorrelationId
            from sigov.workflow_historico where tenant_id=@TenantId and instancia_id=@InstanciaId and is_deleted=false order by created_at, id;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<WorkflowHistoricoDto>(new CommandDefinition(sql, new {TenantId=tenantId, InstanciaId=instanciaId}, cancellationToken:ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<bool> DecidirAsync(long tenantId, long instanciaId, long? usuarioId, string decisao, string? justificativa, long? responsavelId, string? grupoResponsavel, string correlationId, CancellationToken ct)
    {
        const string sql = @"
            with atual as (
              select id, etapa_atual from sigov.workflow_instancia
               where id=@InstanciaId and tenant_id=@TenantId and is_deleted=false and status not in ('APROVADO','REPROVADO','CANCELADO') for update
            ), alterada as (
              update sigov.workflow_instancia i set
                status=case @Decisao when 'APROVAR' then 'APROVADO' when 'REPROVAR' then 'REPROVADO' when 'CANCELAR' then 'CANCELADO' else i.status end,
                etapa_atual=case when @Decisao='ENCAMINHAR' then 'ENCAMINHADO' else i.etapa_atual end,
                responsavel_id=case when @Decisao='ENCAMINHAR' then @ResponsavelId else i.responsavel_id end,
                grupo_responsavel=case when @Decisao='ENCAMINHAR' then @GrupoResponsavel else i.grupo_responsavel end,
                updated_at=now(), updated_by=@UsuarioId,
                auditoria=i.auditoria || jsonb_build_object('ultima_decisao',@Decisao,'correlation_id',@CorrelationId)
              from atual where i.id=atual.id returning i.id, atual.etapa_atual,
                i.etapa_atual as etapa_nova, i.status
            ), tarefa as (
              update sigov.workflow_tarefa t set status=case when @Decisao='ENCAMINHAR' then 'PENDENTE' else (select status from alterada) end,
                responsavel_id=case when @Decisao='ENCAMINHAR' then @ResponsavelId else t.responsavel_id end,
                grupo_responsavel=case when @Decisao='ENCAMINHAR' then @GrupoResponsavel else t.grupo_responsavel end,
                updated_at=now(), updated_by=@UsuarioId
              where t.instancia_id in (select id from alterada) and t.tenant_id=@TenantId and t.is_deleted=false and t.status='PENDENTE'
            )
            insert into sigov.workflow_historico(tenant_id,instancia_id,decisao,justificativa,etapa_anterior,etapa_nova,usuario_id,correlation_id,auditoria)
            select @TenantId,id,@Decisao,@Justificativa,etapa_atual,etapa_nova,@UsuarioId,@CorrelationId,
                   jsonb_build_object('usuario_id',@UsuarioId,'decisao',@Decisao,'correlation_id',@CorrelationId)
              from alterada returning id;
            ";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql,
            new {TenantId=tenantId, InstanciaId=instanciaId, UsuarioId=usuarioId, Decisao=decisao.ToUpperInvariant(), Justificativa=justificativa,
                ResponsavelId=responsavelId, GrupoResponsavel=grupoResponsavel, CorrelationId=correlationId}, cancellationToken:ct)).ConfigureAwait(false) is not null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
