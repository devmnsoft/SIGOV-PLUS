namespace Sigov.Application.Compras;

public static class ComprasPermissoes
{
 public const string Dashboard="compras.dashboard.visualizar",FornecedorVer="compras.fornecedor.visualizar",FornecedorCriar="compras.fornecedor.criar",FornecedorEditar="compras.fornecedor.editar",SolicitacaoVer="compras.solicitacao.visualizar",SolicitacaoCriar="compras.solicitacao.criar",SolicitacaoAprovar="compras.solicitacao.aprovar",ProcessoVer="compras.processo.visualizar",ProcessoCriar="compras.processo.criar",ProcessoAvancar="compras.processo.avancar",CotacaoVer="compras.cotacao.visualizar",CotacaoCriar="compras.cotacao.criar",Julgar="compras.julgamento.executar",ContratoVer="compras.contrato.visualizar",ContratoCriar="compras.contrato.criar",AtaVer="compras.ata.visualizar",AtaCriar="compras.ata.criar",Receber="compras.recebimento.executar",Exportar="compras.exportar";
}
public sealed record CompraFiltro(string? Status=null,string? Busca=null,long? FornecedorId=null,DateOnly? De=null,DateOnly? Ate=null);
public sealed record FornecedorDto(long Id,string Nome,string TipoPessoa,string DocumentoMascarado,string? Email,string? Telefone,string? Endereco,string Status,string? Observacoes);
public sealed record FornecedorInput(long EntidadeId,string Nome,string TipoPessoa,string Documento,string? Email,string? Telefone,string? Endereco,string? Observacoes);
public sealed record SolicitacaoItemInput(string Descricao,decimal Quantidade,string Unidade,string Tipo,decimal ValorEstimado,bool GeraPendenciaPatrimonial=false);
public sealed record SolicitacaoInput(long EntidadeId,string UnidadeSolicitante,string Justificativa,string Prioridade,string Origem,string? OrigemReferencia,string? Observacao,IReadOnlyList<SolicitacaoItemInput> Itens);
public sealed record SolicitacaoDto(long Id,string UnidadeSolicitante,string Justificativa,string Prioridade,string Origem,string Status,decimal ValorEstimado,DateTimeOffset CreatedAt);
public sealed record ProcessoInput(long EntidadeId,int Exercicio,string Numero,string ModalidadeCodigo,string CriterioCodigo,string Objeto,string Justificativa,DateOnly? DataAbertura,DateOnly? DataLimite,long? SolicitacaoId);
public sealed record ProcessoDto(long Id,string Numero,int Exercicio,string Modalidade,string Criterio,string Objeto,string Status,decimal ValorEstimado,DateOnly? DataLimite);
public sealed record CotacaoInput(long ProcessoItemId,long FornecedorId,decimal ValorUnitario,decimal Quantidade,int? PrazoEntregaDias,DateOnly Validade,string? Observacao,string Status="RECEBIDA",string? JustificativaDesclassificacao=null);
public sealed record JulgamentoInput(long ProcessoItemId,long CotacaoId,string Status,string Justificativa);
public sealed record ContratoInput(long EntidadeId,long ProcessoId,long FornecedorId,string Numero,string Objeto,decimal Valor,DateOnly Inicio,DateOnly Fim,string? Gestor,string? Fiscal,string? Observacoes);
public sealed record AtaInput(long EntidadeId,long ProcessoId,long FornecedorId,string Numero,string Objeto,decimal ValorGlobal,DateOnly Inicio,DateOnly Fim);
public sealed record CompraLinhaDto(long Id,string Numero,string Descricao,string Status,string? Fornecedor,decimal Valor,DateOnly? Inicio,DateOnly? Fim);
public sealed record CompraEventoDto(string Tipo,string Descricao,DateTimeOffset Data);
public sealed record ComprasDashboard(long SolicitacoesPendentes,long ProcessosSemCotacao,long ContratosVencendo,long AtasVencendo,decimal ValorAberto,IReadOnlyList<CompraLinhaDto> Processos,IReadOnlyList<CompraEventoDto> Eventos);
public sealed record ProcessoDetalhe(ProcessoDto Processo,IReadOnlyList<CompraLinhaDto> Itens,IReadOnlyList<CompraEventoDto> Historico);
public interface IComprasService
{
 Task<ComprasDashboard> DashboardAsync(long t,long e,CancellationToken ct); Task<IReadOnlyList<FornecedorDto>> FornecedoresAsync(long t,long e,CompraFiltro f,CancellationToken ct); Task<long> CriarFornecedorAsync(long t,long u,string c,FornecedorInput i,CancellationToken ct); Task<byte[]> ExportarFornecedoresAsync(long t,long e,long u,string c,CancellationToken ct);
 Task<IReadOnlyList<SolicitacaoDto>> SolicitacoesAsync(long t,long e,CompraFiltro f,CancellationToken ct); Task<long> CriarSolicitacaoAsync(long t,long u,string c,SolicitacaoInput i,CancellationToken ct); Task AlterarSolicitacaoAsync(long t,long e,long u,string c,long id,string acao,string? justificativa,CancellationToken ct);
 Task<IReadOnlyList<ProcessoDto>> ProcessosAsync(long t,long e,CompraFiltro f,CancellationToken ct); Task<ProcessoDetalhe?> ProcessoAsync(long t,long e,long id,CancellationToken ct); Task<long> CriarProcessoAsync(long t,long u,string c,ProcessoInput i,CancellationToken ct); Task AvancarAsync(long t,long e,long u,string c,long id,string fase,CancellationToken ct); Task<long> CotarAsync(long t,long e,long u,string c,long id,CotacaoInput i,CancellationToken ct); Task JulgarAsync(long t,long e,long u,string c,long id,JulgmentoInputCompat i,CancellationToken ct); Task HomologarAsync(long t,long e,long u,string c,long id,CancellationToken ct);
 Task<IReadOnlyList<CompraLinhaDto>> ContratosAsync(long t,long e,CompraFiltro f,CancellationToken ct); Task<long> CriarContratoAsync(long t,long u,string c,ContratoInput i,CancellationToken ct); Task<IReadOnlyList<CompraLinhaDto>> AtasAsync(long t,long e,CompraFiltro f,CancellationToken ct); Task<long> CriarAtaAsync(long t,long u,string c,AtaInput i,CancellationToken ct);
}
public sealed record JulgmentoInputCompat(long ProcessoItemId,long CotacaoId,string Status,string Justificativa);
