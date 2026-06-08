namespace Sigov.Infrastructure.Agro.Sql;

public static class AgroPatrulhaMecanizadaSql
{
    public const string AgendaConflito = "select exists(select 1 from sigov.agro_agenda_maquina where tenant_id=@TenantId and entidade_id=@EntidadeId and maquina_id=@MaquinaId and is_deleted=false and status not in ('CANCELADA') and (@IgnorarId is null or id<>@IgnorarId) and data_inicio < @DataFim and data_fim > @DataInicio);";
}
