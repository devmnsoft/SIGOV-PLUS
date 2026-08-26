using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Fiscalizacao;

public sealed record FiscalizacaoContexto(long TenantId,long EntidadeId,long ExercicioId,long UsuarioId);
public sealed record FiscalizacaoFiltro(string? Modulo,string? Status,DateOnly? Inicio,DateOnly? Fim,string? Busca);
public sealed record FiscalizacaoDashboard(long OrdensAbertas,long VistoriasAgendadas,long VistoriasConcluidas,long AutosEmitidos,long SincronizacoesPendentes,long Evidencias);
public sealed record FiscalizacaoLinha(long Id,string Codigo,string Descricao,string Status,DateTimeOffset Data);
public sealed record FiscalizacaoOpcao(long Id,string Rotulo);
public sealed class OrdemFiscalizacaoRequest
{
 [Required] public string OrigemModulo {get;set;}="";
 [Required,StringLength(100)] public string Tipo {get;set;}="";
 [Required] public string Prioridade {get;set;}="NORMAL";
 [Required] public string Status {get;set;}="ABERTA";
 [Required] public long? RegistroFiscalizadoId {get;set;}
 [Required] public string RegistroFiscalizadoTipo {get;set;}="";
 public string RegistroFiscalizadoRotulo {get;set;}="";
 [Required] public long? EquipeId {get;set;}
 public long? ResponsavelUsuarioId {get;set;}
 public DateTimeOffset? AgendadaEm {get;set;}
 [Required,StringLength(2000)] public string Motivo {get;set;}="";
 [StringLength(4000)] public string? Observacoes {get;set;}
}
public interface IFiscalizacaoRepository
{
 Task<FiscalizacaoDashboard> DashboardAsync(FiscalizacaoContexto c,FiscalizacaoFiltro f,CancellationToken ct);
 Task<IReadOnlyList<FiscalizacaoLinha>> ListarAsync(FiscalizacaoContexto c,string recurso,FiscalizacaoFiltro f,CancellationToken ct);
 Task<OrdemFiscalizacaoRequest?> ObterOrdemAsync(FiscalizacaoContexto c,long id,CancellationToken ct);
 Task<IReadOnlyList<FiscalizacaoOpcao>> OpcoesAsync(FiscalizacaoContexto c,string tipo,string? origem,CancellationToken ct);
 Task<long> SalvarOrdemAsync(FiscalizacaoContexto c,OrdemFiscalizacaoRequest r,long? id,CancellationToken ct);
 Task TransicionarOrdemAsync(FiscalizacaoContexto c,long id,string destino,string? justificativa,CancellationToken ct);
 Task<byte[]> CsvAsync(FiscalizacaoContexto c,string recurso,FiscalizacaoFiltro f,CancellationToken ct);
}
