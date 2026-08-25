namespace Sigov.Application.Agro;

public static class AgroPermissoes
{
    public const string DashboardView="AGRO_DASHBOARD_VIEW", ProdutorView="AGRO_PRODUTOR_VIEW", ProdutorManage="AGRO_PRODUTOR_MANAGE",
        PropriedadeView="AGRO_PROPRIEDADE_VIEW", PropriedadeManage="AGRO_PROPRIEDADE_MANAGE", AtividadeView="AGRO_ATIVIDADE_VIEW", AtividadeManage="AGRO_ATIVIDADE_MANAGE",
        AssistenciaView="AGRO_ASSISTENCIA_VIEW", AssistenciaManage="AGRO_ASSISTENCIA_MANAGE", ProgramaView="AGRO_PROGRAMA_VIEW", ProgramaManage="AGRO_PROGRAMA_MANAGE",
        InsumoView="AGRO_INSUMO_VIEW", InsumoManage="AGRO_INSUMO_MANAGE", PatrulhaView="AGRO_PATRULHA_VIEW", PatrulhaManage="AGRO_PATRULHA_MANAGE",
        FeiraView="AGRO_FEIRA_VIEW", FeiraManage="AGRO_FEIRA_MANAGE", AgroindustriaView="AGRO_AGROINDUSTRIA_VIEW", AgroindustriaManage="AGRO_AGROINDUSTRIA_MANAGE",
        SolicitacaoView="AGRO_SOLICITACAO_VIEW", SolicitacaoManage="AGRO_SOLICITACAO_MANAGE", RelatorioExport="AGRO_RELATORIO_EXPORT", AuditoriaView="AGRO_AUDITORIA_VIEW";
}
public sealed record AgroFiltro(DateOnly? Inicio=null,DateOnly? Fim=null,string? Comunidade=null,string? Programa=null,string? Status=null,string? Busca=null,int Pagina=1,int TamanhoPagina=25);
public sealed record AgroDashboard(long ProdutoresAtivos,long Propriedades,decimal AreaTotal,long VisitasTecnicas,long ProgramasAtivos,long Beneficiarios,decimal InsumosDistribuidos,long ServicosPatrulha,long SolicitacoesAbertas,long SolicitacoesConcluidas);
public sealed record AgroRegistro(long Id,string Titulo,string? Subtitulo,string Status,DateTimeOffset CriadoEm);
public sealed record AgroPagina(IReadOnlyList<AgroRegistro> Itens,int Pagina,int TamanhoPagina,long Total);
public sealed record ProdutorInput(string TipoPessoa,string NomeRazaoSocial,string CpfCnpj,string? Telefone,string? Email,string? Endereco,string? Comunidade,string? Localidade,string? CafPronaf,string Situacao,string? Observacoes,bool Ativo=true);
public sealed record PropriedadeInput(string Nome,decimal AreaTotal,decimal AreaProdutiva,string? Localizacao,string? Comunidade,decimal? Latitude,decimal? Longitude,string? Car,string? Itr,string? Ccir,string Situacao,string? Observacoes,long[]? ProdutorIds,bool Ativo=true);
public sealed record AgroOperacaoInput(string Titulo,string? Descricao,string? Status,long? ProdutorId=null,long? PropriedadeId=null,long? ProgramaId=null,long? MaquinaId=null,decimal? Quantidade=null,DateOnly? Data=null,DateOnly? DataFim=null,string? Justificativa=null);
public interface IAgroService
{
 Task<AgroDashboard> DashboardAsync(long tenant,long entidade,AgroFiltro filtro,CancellationToken ct);
 Task<AgroPagina> ListarAsync(long tenant,long entidade,string recurso,AgroFiltro filtro,CancellationToken ct);
 Task<long> CriarProdutorAsync(long tenant,long entidade,long usuario,string correlation,ProdutorInput input,CancellationToken ct);
 Task<long> CriarPropriedadeAsync(long tenant,long entidade,long usuario,string correlation,PropriedadeInput input,CancellationToken ct);
 Task<long> CriarAsync(long tenant,long entidade,long usuario,string correlation,string recurso,AgroOperacaoInput input,CancellationToken ct);
 Task ExcluirAsync(long tenant,long entidade,long usuario,string correlation,string recurso,long id,CancellationToken ct);
 Task<byte[]> ExportarAsync(long tenant,long entidade,long usuario,string correlation,string tipo,AgroFiltro filtro,CancellationToken ct);
}
