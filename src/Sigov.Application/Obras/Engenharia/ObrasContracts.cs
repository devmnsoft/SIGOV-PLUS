namespace Sigov.Application.Obras.Engenharia;

public sealed record ObrasContexto(long TenantId,long EntidadeId,long? ExercicioId,long? UsuarioId,string CorrelationId,string? Ip);
public sealed record ObrasFiltro(string? Busca,string? Status,DateOnly? Inicio,DateOnly? Fim,string? Unidade,string? Localidade,string? Contrato,string? Fiscal,string? Fonte,int Pagina=1,int Tamanho=20);
// Read models materialized directly by Dapper deliberately expose a parameterless
// constructor and settable properties.  This also makes the aliases in the SQL
// projections explicit and avoids constructor-signature failures between providers.
public sealed class ObraResumo
{
 public long Id { get; set; } public string Codigo { get; set; }=""; public string Nome { get; set; }="";
 public string Tipo { get; set; }=""; public string? Unidade { get; set; } public string? Localidade { get; set; }
 public decimal ValorContratado { get; set; } public string Status { get; set; }="";
 public DateOnly? InicioPrevisto { get; set; } public DateOnly? FimPrevisto { get; set; }
}
public sealed record ObrasPagina(IReadOnlyList<ObraResumo> Items,int Pagina,int Tamanho,long Total);
public sealed class ObrasDashboard
{
 public long Planejadas { get; set; } public long Contratadas { get; set; } public long EmExecucao { get; set; }
 public long Paralisadas { get; set; } public long Concluidas { get; set; } public decimal ValorContratado { get; set; }
 public decimal ValorMedido { get; set; } public decimal SaldoMedir { get; set; } public long MedicoesPendentes { get; set; }
 public long MedicoesAprovadas { get; set; } public long FiscalizacoesPendentes { get; set; } public long OrdensAbertas { get; set; }
 public long OcorrenciasAbertas { get; set; } public long NaoConformidadesVencidas { get; set; } public long DiariosPendentes { get; set; }
 public long PublicacoesPendentes { get; set; } public decimal PercentualPrevisto { get; set; } public decimal PercentualExecutado { get; set; }
}
public sealed class ObraOpcao { public long Id { get; set; } public string Texto { get; set; }=""; }
public sealed record ObraRequest(string Codigo,string Nome,string? Descricao,string TipoObra,string? Unidade,string Localidade,string? Endereco,decimal? Latitude,decimal? Longitude,decimal ValorEstimado,decimal ValorContratado,DateOnly InicioPrevisto,DateOnly FimPrevisto,DateOnly? InicioReal,DateOnly? FimReal,string Status,string? Fiscal,string? Observacoes,string? Justificativa,string? ContratoReferencia,string? FonteRecurso);
public sealed class ObrasRegistro
{
 public long Id { get; set; } public long ObraId { get; set; } public string Codigo { get; set; }="";
 public string Descricao { get; set; }=""; public string Status { get; set; }=""; public decimal? Valor { get; set; }
 public DateOnly? Data { get; set; } public string DadosJson { get; set; }="{}"; public DateTimeOffset CriadoEm { get; set; }
}
public sealed record ObrasRegistroRequest(long ObraId,string Codigo,string Descricao,string Status,decimal? Valor,DateOnly? Data,string? Justificativa,string DadosJson="{}");
public interface IObrasEngenhariaRepository
{
 Task<ObrasDashboard> DashboardAsync(ObrasContexto c,ObrasFiltro f,CancellationToken ct);
 Task<ObrasPagina> ListarObrasAsync(ObrasContexto c,ObrasFiltro f,CancellationToken ct);
 Task<long> SalvarObraAsync(ObrasContexto c,ObraRequest r,long? id,CancellationToken ct);
 Task ExcluirObraAsync(ObrasContexto c,long id,string justificativa,CancellationToken ct);
 Task<IReadOnlyList<ObrasRegistro>> ListarAsync(ObrasContexto c,string recurso,ObrasFiltro f,CancellationToken ct);
 Task<IReadOnlyList<ObraOpcao>> ListarOpcoesObraAsync(ObrasContexto c,CancellationToken ct);
 Task<long> SalvarAsync(ObrasContexto c,string recurso,ObrasRegistroRequest r,long? id,CancellationToken ct);
 Task HomologarMedicaoAsync(ObrasContexto c,long id,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(ObrasContexto c,string relatorio,ObrasFiltro f,CancellationToken ct);
}
