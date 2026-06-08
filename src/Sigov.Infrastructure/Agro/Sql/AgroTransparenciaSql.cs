namespace Sigov.Infrastructure.Agro.Sql;

public static class AgroTransparenciaSql
{
    public const string ListarDatasets = """
        select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, tipo_dataset as TipoDataset, formato_padrao as FormatoPadrao, anonimizado as Anonimizado, publico as Publico, ativo as Ativo, ultima_publicacao_at as UltimaPublicacaoAt
          from sigov.agro_dataset_publico
         where tenant_id = @TenantId and is_deleted = false and (@SomentePublicos = false or (publico = true and anonimizado = true)) and ((@EntidadeId is null) or entidade_id = @EntidadeId or entidade_id is null)
         order by nome limit @PageSize offset @Offset;
        """;
}
