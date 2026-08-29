using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Royalties;
public sealed record RoyaltiesContexto(long TenantId,long EntidadeId,long? UsuarioId,string CorrelationId);
public sealed record RoyaltiesFiltro(string? Busca,string? Status,long? ExercicioId=null,long? FonteId=null,string? TipoReceita=null,DateOnly? Inicio=null,DateOnly? Fim=null);
public sealed record RoyaltiesRegistro(long Id,string Codigo,string Nome,string Status,string? TipoReceita,decimal Valor,DateOnly? Competencia,DateOnly? DataReferencia,long? FonteRecursoId,string? Descricao);
public sealed record RoyaltiesOpcao(long Id,string Texto);
public sealed record RoyaltiesDashboard(decimal Prevista,decimal Realizada,decimal Repasses,decimal Aplicacoes,decimal Saldo,long RepassesPendentes,long Projetos,long ProjetosAtrasados,long Obras,long Sustentabilidade,long Alertas,IReadOnlyList<RoyaltiesRegistro> Recentes);
public sealed class RoyaltiesRequest
{
 [Required,StringLength(80)] public string Codigo {get;set;}="";
 [Required,StringLength(200)] public string Nome {get;set;}="";
 [StringLength(4000)] public string? Descricao {get;set;}
 [Required] public string Status {get;set;}="RASCUNHO";
 [Required] public string TipoReceita {get;set;}="ROYALTIES";
 [Range(0,double.MaxValue)] public decimal Valor {get;set;}
 [Range(0,double.MaxValue)] public decimal ValorBruto {get;set;}
 [Range(0,double.MaxValue)] public decimal Deducoes {get;set;}
 [Range(0,double.MaxValue)] public decimal ValorLiquido {get;set;}
 [Range(0,100)] public decimal? Percentual {get;set;}
 public DateOnly? Competencia {get;set;}
 public DateOnly? DataReferencia {get;set;}
 public DateOnly? VigenciaInicio {get;set;}
 public DateOnly? VigenciaFim {get;set;}
 public DateOnly? DataFimPrevista {get;set;}
 public long? ExercicioId {get;set;}
 [Required] public long? FonteRecursoId {get;set;}
 public long? ParametroId {get;set;}
 public long? ProjetoId {get;set;}
 public long? UnidadeId {get;set;}
 public long? PessoaJuridicaId {get;set;}
 public long? ProgramaId {get;set;}
 public long? AcaoId {get;set;}
 public long? ContratoId {get;set;}
 public string? Cenario {get;set;}
 [StringLength(1000)] public string? FonteNormativa {get;set;}
 [StringLength(4000)] public string? FormulaTextual {get;set;}
 [StringLength(2000)] public string? Premissas {get;set;}
 [StringLength(2000)] public string? Justificativa {get;set;}
 public string Visibilidade {get;set;}="INTERNA";
 public bool Publicavel {get;set;}
}
public interface IRoyaltiesRepository
{
 Task<RoyaltiesDashboard> DashboardAsync(RoyaltiesContexto c,RoyaltiesFiltro f,CancellationToken ct);
 Task<IReadOnlyList<RoyaltiesRegistro>> ListarAsync(RoyaltiesContexto c,string recurso,RoyaltiesFiltro f,CancellationToken ct);
 Task<RoyaltiesRegistro?> ObterAsync(RoyaltiesContexto c,string recurso,long id,CancellationToken ct);
 Task<long> SalvarAsync(RoyaltiesContexto c,string recurso,RoyaltiesRequest request,long? id,CancellationToken ct);
 Task<IReadOnlyList<RoyaltiesOpcao>> OpcoesAsync(RoyaltiesContexto c,string tipo,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(RoyaltiesContexto c,string recurso,RoyaltiesFiltro f,CancellationToken ct);
}
