using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Transito;

public sealed record TransitoContexto(long TenantId,long EntityId,string Usuario);
public sealed record TransitoFiltro(string? Busca,string? Status,DateOnly? Inicio=null,DateOnly? Fim=null,int Pagina=1,int Tamanho=25);
public sealed record TransitoRegistro(long Id,string Codigo,string Descricao,string Status,DateTimeOffset CriadoEm);
public sealed record TransitoPagina(IReadOnlyList<TransitoRegistro> Itens,int Pagina,int Tamanho,long Total);
public sealed record TransitoOpcao(long Id,string Rotulo);
public sealed record TransitoDashboard(long AutosMes,long AutosPendentes,long NotificacoesVencendo,long RecursosPendentes,long OcorrenciasAbertas,long SinalizacoesManutencao,long AutorizacoesVencendo,long VistoriasReprovadas,long CredenciaisVencendo);
public sealed class TransitoRegistroRequest
{
    [Required(ErrorMessage="Informe o código, número ou identificação.")] public string Codigo { get; set; }="";
    [Required(ErrorMessage="Informe a descrição ou nome.")] public string Descricao { get; set; }="";
    [Required(ErrorMessage="Selecione o status.")] public string Status { get; set; }="ATIVO";
    public Dictionary<string,string?> Campos { get; set; }=[];
}
public interface ITransitoRepository
{
 Task<TransitoDashboard> DashboardAsync(TransitoContexto c,CancellationToken ct);
 Task<TransitoPagina> ListarAsync(TransitoContexto c,string recurso,TransitoFiltro filtro,CancellationToken ct);
 Task<IReadOnlyList<TransitoOpcao>> OpcoesAsync(TransitoContexto c,string tipo,CancellationToken ct);
 Task<long> SalvarAsync(TransitoContexto c,string recurso,TransitoRegistroRequest request,CancellationToken ct);
 Task ExcluirAsync(TransitoContexto c,string recurso,long id,CancellationToken ct);
 Task<byte[]> CsvAsync(TransitoContexto c,string recurso,TransitoFiltro filtro,CancellationToken ct);
}
