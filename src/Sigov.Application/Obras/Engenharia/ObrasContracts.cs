namespace Sigov.Application.Obras.Engenharia;

public sealed record ObrasContexto(long TenantId,long EntidadeId,long? ExercicioId,long? UsuarioId,string CorrelationId,string? Ip);
public sealed record ObrasFiltro(string? Busca,string? Status,DateOnly? Inicio,DateOnly? Fim,string? Unidade,string? Localidade,string? Contrato,string? Fiscal,string? Fonte,int Pagina=1,int Tamanho=20);
public sealed record ObraResumo(long Id,string Codigo,string Nome,string Tipo,string? Unidade,string? Localidade,decimal ValorContratado,string Status,DateOnly? InicioPrevisto,DateOnly? FimPrevisto);
public sealed record ObrasPagina(IReadOnlyList<ObraResumo> Items,int Pagina,int Tamanho,long Total);
public sealed record ObrasDashboard(long Planejadas,long Contratadas,long EmExecucao,long Paralisadas,long Concluidas,decimal ValorContratado,decimal ValorMedido,decimal SaldoMedir,long MedicoesPendentes,long MedicoesAprovadas,long FiscalizacoesPendentes,long OrdensAbertas,long OcorrenciasAbertas,long NaoConformidadesVencidas,long DiariosPendentes,long PublicacoesPendentes,decimal PercentualPrevisto,decimal PercentualExecutado);
public sealed record ObraOpcao(long Id,string Texto);
public sealed record ObraRequest(string Codigo,string Nome,string? Descricao,string TipoObra,string? Unidade,string Localidade,string? Endereco,decimal? Latitude,decimal? Longitude,decimal ValorEstimado,decimal ValorContratado,DateOnly InicioPrevisto,DateOnly FimPrevisto,DateOnly? InicioReal,DateOnly? FimReal,string Status,string? Fiscal,string? Observacoes,string? Justificativa,string? ContratoReferencia,string? FonteRecurso);
public sealed record ObrasRegistro(long Id,long ObraId,string Codigo,string Descricao,string Status,decimal? Valor,DateOnly? Data,string DadosJson,DateTimeOffset CriadoEm);
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
