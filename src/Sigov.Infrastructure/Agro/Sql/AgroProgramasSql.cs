namespace Sigov.Infrastructure.Agro.Sql;

public static class AgroProgramasSql
{
    public const string EventoInsert = "insert into sigov.agro_evento(tenant_id,entidade_id,exercicio_id,tipo_evento,origem,origem_id,payload_json,created_by,correlation_id) values(@TenantId,@EntidadeId,@ExercicioId,@TipoEvento,@Origem,@OrigemId,@Payload::jsonb,@UsuarioId,@CorrelationId);";
}
