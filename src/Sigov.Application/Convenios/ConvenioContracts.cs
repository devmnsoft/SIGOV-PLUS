using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Convenios;

public sealed record ConvenioContexto(long TenantId,long EntityId,string Usuario);
public sealed record ConvenioFiltro(string? Busca,string? Status,DateOnly? Inicio=null,DateOnly? Fim=null,int Pagina=1,int Tamanho=25);
public sealed record ConvenioRegistro(long Id,string Codigo,string Descricao,string Status,DateTimeOffset CriadoEm);
public sealed record ConvenioPagina(IReadOnlyList<ConvenioRegistro> Itens,int Pagina,int Tamanho,long Total);
public sealed record ConvenioOpcao(long Id,string Rotulo);
public sealed record ConvenioDashboard(long InstrumentosAtivos,long InstrumentosVencendo,long InstrumentosVencidos,decimal ValorGlobalExecucao,decimal ValorRepassado,decimal ValorContrapartida,decimal ValorExecutado,long ProjetosAtrasados,long MetasPendentes,long PrestacoesPendentes,long DiligenciasAbertas,long DiligenciasVencidas,long EmendasAno,decimal SaldoContas);
public sealed class ConvenioRegistroRequest
{
    [Required(ErrorMessage="Informe a identificação.")] public string Codigo { get; set; }="";
    [Required(ErrorMessage="Informe o nome ou descrição.")] public string Descricao { get; set; }="";
    [Required(ErrorMessage="Selecione o status.")] public string Status { get; set; }="ATIVO";
    public Dictionary<string,string?> Campos { get; set; }=[];
}
public interface IConvenioRepository
{
    Task<ConvenioDashboard> DashboardAsync(ConvenioContexto contexto,CancellationToken ct);
    Task<ConvenioPagina> ListarAsync(ConvenioContexto contexto,string recurso,ConvenioFiltro filtro,CancellationToken ct);
    Task<ConvenioRegistroRequest?> ObterAsync(ConvenioContexto contexto,string recurso,long id,CancellationToken ct);
    Task<IReadOnlyList<ConvenioOpcao>> OpcoesAsync(ConvenioContexto contexto,string recurso,CancellationToken ct);
    Task<long> SalvarAsync(ConvenioContexto contexto,string recurso,ConvenioRegistroRequest request,long? id,CancellationToken ct);
    Task ExcluirAsync(ConvenioContexto contexto,string recurso,long id,CancellationToken ct);
    Task<byte[]> CsvAsync(ConvenioContexto contexto,string recurso,ConvenioFiltro filtro,CancellationToken ct);
}
