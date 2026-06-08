namespace Sigov.Infrastructure.Agro.Sql;

public static class AgroRelatoriosSql
{
    public const string ListarIndicadores = @"select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, categoria as Categoria, descricao as Descricao, unidade_medida as UnidadeMedida, publico as Publico, ativo as Ativo
  from sigov.agro_indicador
 where tenant_id = @TenantId and is_deleted = false and ((@EntidadeId is null) or entidade_id = @EntidadeId or entidade_id is null)
 order by categoria, nome limit @PageSize offset @Offset;
";
    public const string ListarValores = @"select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, exercicio_id as ExercicioId, indicador_id as IndicadorId, competencia as Competencia, valor as Valor, calculado_at as CalculadoAt
  from sigov.agro_indicador_valor
 where tenant_id = @TenantId and indicador_id = @IndicadorId and ((@EntidadeId is null) or entidade_id = @EntidadeId or entidade_id is null)
 order by calculado_at desc limit @PageSize offset @Offset;
";
    public const string ListarModelos = @"select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, tipo_relatorio as TipoRelatorio, formato_padrao as FormatoPadrao, publico_no_tenant as PublicoNoTenant, contem_dados_pessoais as ContemDadosPessoais, ativo as Ativo
  from sigov.agro_relatorio_modelo
 where tenant_id = @TenantId and is_deleted = false and ((@EntidadeId is null) or entidade_id = @EntidadeId or entidade_id is null)
 order by tipo_relatorio, nome limit @PageSize offset @Offset;
";
    public const string ListarExecucoes = @"select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, exercicio_id as ExercicioId, modelo_id as ModeloId, usuario_id as UsuarioId, formato as Formato, status as Status, total_linhas as TotalLinhas, iniciou_at as IniciouAt, finalizou_at as FinalizouAt, erro as Erro
  from sigov.agro_relatorio_execucao
 where tenant_id = @TenantId and ((@EntidadeId is null) or entidade_id = @EntidadeId or entidade_id is null)
 order by iniciou_at desc limit @PageSize offset @Offset;
";
}
