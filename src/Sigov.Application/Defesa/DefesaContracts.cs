using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Defesa;

public sealed record DefesaContexto(long TenantId,long EntityId,string Usuario);
public sealed record DefesaFiltro(string? Busca,string? Status,DateOnly? Inicio=null,DateOnly? Fim=null,int Pagina=1,int Tamanho=25);
public sealed record DefesaRegistro(long Id,string Codigo,string Descricao,string Status,DateTimeOffset CriadoEm);
public sealed record DefesaPagina(IReadOnlyList<DefesaRegistro> Itens,int Pagina,int Tamanho,long Total);
public sealed record DefesaOpcao(long Id,string Rotulo);
public sealed record DefesaDashboard(long OcorrenciasAbertas,long OcorrenciasCriticas,long OcorrenciasFechadasMes,long AcionamentosAndamento,long VistoriasPendentes,long AreasCriticas,long AbrigosAtivos,long CapacidadeAbrigos,long OcupacaoAbrigos,long RondasAndamento,long OrdensAbertas,long NotificacoesPendentes);
public sealed class DefesaRegistroRequest
{
    [Required(ErrorMessage="Informe o número, matrícula ou identificação.")] public string Codigo { get; set; }="";
    [Required(ErrorMessage="Informe o nome ou descrição.")] public string Descricao { get; set; }="";
    [Required(ErrorMessage="Selecione o status.")] public string Status { get; set; }="ATIVO";
    public Dictionary<string,string?> Campos { get; set; }=[];
    public List<long> AgentesSelecionados { get; set; }=[];
}
public interface IDefesaRepository
{
    Task<DefesaDashboard> DashboardAsync(DefesaContexto contexto,CancellationToken ct);
    Task<DefesaPagina> ListarAsync(DefesaContexto contexto,string recurso,DefesaFiltro filtro,CancellationToken ct);
    Task<IReadOnlyList<DefesaOpcao>> OpcoesAsync(DefesaContexto contexto,string tipo,bool somenteAtivos,CancellationToken ct);
    Task<long> SalvarAsync(DefesaContexto contexto,string recurso,DefesaRegistroRequest request,CancellationToken ct);
    Task ExcluirAsync(DefesaContexto contexto,string recurso,long id,CancellationToken ct);
    Task<byte[]> CsvAsync(DefesaContexto contexto,string recurso,DefesaFiltro filtro,CancellationToken ct);
}
