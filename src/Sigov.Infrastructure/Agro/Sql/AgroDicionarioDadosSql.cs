namespace Sigov.Infrastructure.Agro.Sql;

public static class AgroDicionarioDadosSql
{
    public const string Listar = @"select id as Id, tenant_id as TenantId, tabela_nome as TabelaNome, campo_nome as CampoNome, nome_amigavel as NomeAmigavel, descricao as Descricao, categoria as Categoria, dado_pessoal as DadoPessoal, dado_sensivel as DadoSensivel, publico as Publico, mascara_padrao as MascaraPadrao
  from sigov.agro_dicionario_dados
 where tenant_id is null or tenant_id = @TenantId
 order by tabela_nome, campo_nome nulls first limit @PageSize offset @Offset;
";
}
