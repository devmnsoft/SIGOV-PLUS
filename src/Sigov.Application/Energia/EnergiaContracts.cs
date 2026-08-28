using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Energia;

public sealed record EnergiaContexto(long TenantId,long EntidadeId,long? UsuarioId,string CorrelationId,string? Ip);
public sealed record EnergiaFiltro(string? Busca,string? Status,DateOnly? Inicio=null,DateOnly? Fim=null,long? UnidadeId=null,long? ConcessionariaId=null);
public sealed record EnergiaDashboard(decimal ConsumoMes,decimal CustoMes,long UnidadesAtivas,long FaturasPendentes,decimal DemandaContratada,decimal DemandaMedida,long AlertasDemanda,long ChamadosAbertos,long PontosInativos,decimal GeracaoMes,decimal CreditosDisponiveis,decimal EconomiaRealizada,decimal EmissoesEvitadas,IReadOnlyList<EnergiaRegistro> MaioresConsumidores);
public sealed record EnergiaRegistro(long Id,string Codigo,string Descricao,string Status,decimal Valor,DateOnly? Data,long? UnidadeConsumidoraId);
public sealed record EnergiaOpcao(long Id,string Texto);
public sealed class EnergiaRegistroRequest
{
 [Required] public string Codigo {get;set;}="";
 [Required] public string Descricao {get;set;}="";
 [Required] public string Status {get;set;}="ATIVA";
 [Range(0,double.MaxValue)] public decimal Valor {get;set;}
 public DateOnly? Data {get;set;}
 public long? UnidadeConsumidoraId {get;set;}
 public long? PessoaJuridicaId {get;set;}
 public long? UnidadeOrganizacionalId {get;set;}
 public long? ContratoId {get;set;}
 [Range(-90,90)] public decimal? Latitude {get;set;}
 [Range(-180,180)] public decimal? Longitude {get;set;}
 public string? Justificativa {get;set;}
}
public interface IEnergiaRepository
{
 Task<EnergiaDashboard> DashboardAsync(EnergiaContexto c,EnergiaFiltro f,CancellationToken ct);
 Task<IReadOnlyList<EnergiaRegistro>> ListarAsync(EnergiaContexto c,string recurso,EnergiaFiltro f,CancellationToken ct);
 Task<EnergiaRegistro?> ObterAsync(EnergiaContexto c,string recurso,long id,CancellationToken ct);
 Task<long> SalvarAsync(EnergiaContexto c,string recurso,EnergiaRegistroRequest request,long? id,CancellationToken ct);
 Task<IReadOnlyList<EnergiaOpcao>> OpcoesAsync(EnergiaContexto c,string tipo,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(EnergiaContexto c,string recurso,EnergiaFiltro f,CancellationToken ct);
}
