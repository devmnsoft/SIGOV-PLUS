namespace Sigov.Application.Tributario.ReceitaMunicipal;

public sealed record TributarioContexto(long TenantId, long EntidadeId, long? UsuarioId, string CorrelationId);
public sealed record TributarioDashboard(long ContribuintesAtivos,long Imoveis,long Mobiliarios,long LancamentosExercicio,long GuiasEmitidas,decimal ValorLancado,decimal ValorArrecadado,decimal Inadimplencia,decimal DividaAtiva,long ParcelamentosAtivos,long FiscalizacoesAbertas,long CertidoesEmitidas,IReadOnlyList<string> Alertas);
public sealed record TributarioLinha(long Id,string Codigo,string Descricao,string Status,decimal? Valor,DateTimeOffset CriadoEm);
public sealed record TributarioPagina(IReadOnlyList<TributarioLinha> Items,int Pagina,int Tamanho,long Total,string Recurso);
public sealed record ContribuinteRequest(string TipoPessoa,string Documento,string NomeRazaoSocial,string? NomeFantasia,string? InscricaoMunicipal,string? Email,string? Telefone,string? Logradouro,string? Numero,string? Bairro,string Situacao="ATIVO");
public interface ITributarioReceitaRepository
{
 Task<TributarioDashboard> DashboardAsync(long tenantId,long entidadeId,CancellationToken ct);
 Task<TributarioPagina> ListarAsync(long tenantId,long entidadeId,string recurso,string? busca,string? status,int pagina,int tamanho,CancellationToken ct);
 Task<long> CriarContribuinteAsync(TributarioContexto contexto,ContribuinteRequest request,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(long tenantId,long entidadeId,string recurso,CancellationToken ct);
 Task RegistrarAuditoriaAsync(TributarioContexto contexto,string acao,string recurso,long? recursoId,string finalidade,CancellationToken ct);
}
