using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Financeiro;

public static class FinanceiroPermissoes
{
    public const string Modulo = "financeiro";
    public const string PlanoContasVisualizar = "financeiro.plano_contas.visualizar"; public const string PlanoContasCriar = "financeiro.plano_contas.criar"; public const string PlanoContasEditar = "financeiro.plano_contas.editar"; public const string PlanoContasExcluir = "financeiro.plano_contas.excluir";
    public const string OrcamentoVisualizar = "financeiro.orcamento.visualizar"; public const string OrcamentoCriar = "financeiro.orcamento.criar"; public const string OrcamentoEditar = "financeiro.orcamento.editar"; public const string OrcamentoMovimentar = "financeiro.orcamento.movimentar";
    public const string EmpenhoVisualizar = "financeiro.empenho.visualizar"; public const string EmpenhoCriar = "financeiro.empenho.criar"; public const string EmpenhoEditar = "financeiro.empenho.editar"; public const string EmpenhoAnular = "financeiro.empenho.anular";
    public const string LiquidacaoVisualizar = "financeiro.liquidacao.visualizar"; public const string LiquidacaoCriar = "financeiro.liquidacao.criar"; public const string LiquidacaoAnular = "financeiro.liquidacao.anular";
    public const string PagamentoVisualizar = "financeiro.pagamento.visualizar"; public const string PagamentoCriar = "financeiro.pagamento.criar"; public const string PagamentoCancelar = "financeiro.pagamento.cancelar";
    public const string ReceitaVisualizar = "financeiro.receita.visualizar"; public const string ReceitaCriar = "financeiro.receita.criar"; public const string ReceitaArrecadar = "financeiro.receita.arrecadar";
    public const string DashboardVisualizar = "financeiro.dashboard.visualizar"; public const string Exportar = "financeiro.exportar";
}

public sealed record PlanoContasCreateRequest(string Codigo, string Nome, string TipoConta, int Nivel, long? ContaPaiId, string? NaturezaSaldo, bool AceitaLancamento);
public sealed record PlanoContasUpdateRequest(string Codigo, string Nome, string TipoConta, int Nivel, long? ContaPaiId, string? NaturezaSaldo, bool AceitaLancamento, bool Ativo);
public sealed record PlanoContasFiltro(int Page = 1, int PageSize = 20, string? Codigo = null, string? Nome = null, string? TipoConta = null, bool? Ativo = null);
public sealed record PlanoContasResponse(long Id, string Codigo, string Nome, string TipoConta, int Nivel, long? ContaPaiId, string? NaturezaSaldo, bool AceitaLancamento, bool Ativo);

public sealed record FonteRecursoCreateRequest(string Codigo, string Nome, string? Descricao);
public sealed record FonteRecursoUpdateRequest(string Codigo, string Nome, string? Descricao, bool Ativo);
public sealed record FonteRecursoFiltro(int Page = 1, int PageSize = 20, string? Codigo = null, string? Nome = null, bool? Ativo = null);
public sealed record FonteRecursoResponse(long Id, string Codigo, string Nome, string? Descricao, bool Ativo);

public sealed record ProgramaCreateRequest(string Codigo, string Nome, string? Objetivo);
public sealed record ProgramaUpdateRequest(string Codigo, string Nome, string? Objetivo, bool Ativo);
public sealed record ProgramaFiltro(int Page = 1, int PageSize = 20, string? Codigo = null, string? Nome = null, bool? Ativo = null);
public sealed record ProgramaResponse(long Id, string Codigo, string Nome, string? Objetivo, bool Ativo);
public sealed record AcaoCreateRequest(long ProgramaId, string Codigo, string Nome, string TipoAcao);
public sealed record AcaoUpdateRequest(long ProgramaId, string Codigo, string Nome, string TipoAcao, bool Ativo);
public sealed record AcaoFiltro(int Page = 1, int PageSize = 20, string? Codigo = null, string? Nome = null, long? ProgramaId = null, bool? Ativo = null);
public sealed record AcaoResponse(long Id, long ProgramaId, string Codigo, string Nome, string TipoAcao, bool Ativo);

public sealed record NaturezaReceitaCreateRequest(string Codigo, string Nome, string? Categoria, string? Origem, string? Especie);
public sealed record NaturezaReceitaUpdateRequest(string Codigo, string Nome, string? Categoria, string? Origem, string? Especie, bool Ativo);
public sealed record NaturezaReceitaFiltro(int Page = 1, int PageSize = 20, string? Codigo = null, string? Nome = null, bool? Ativo = null);
public sealed record NaturezaReceitaResponse(long Id, string Codigo, string Nome, string? Categoria, string? Origem, string? Especie, bool Ativo);
public sealed record NaturezaDespesaCreateRequest(string Codigo, string Nome, string? Categoria, string? Grupo, string? Modalidade, string? Elemento);
public sealed record NaturezaDespesaUpdateRequest(string Codigo, string Nome, string? Categoria, string? Grupo, string? Modalidade, string? Elemento, bool Ativo);
public sealed record NaturezaDespesaFiltro(int Page = 1, int PageSize = 20, string? Codigo = null, string? Nome = null, bool? Ativo = null);
public sealed record NaturezaDespesaResponse(long Id, string Codigo, string Nome, string? Categoria, string? Grupo, string? Modalidade, string? Elemento, bool Ativo);

public sealed record OrcamentoDespesaCreateRequest(long? OrgaoUnidadeOrcamentariaId, long ProgramaId, long AcaoId, long NaturezaDespesaId, long FonteRecursoId, decimal DotacaoInicial);
public sealed record OrcamentoDespesaUpdateRequest(long? OrgaoUnidadeOrcamentariaId, long ProgramaId, long AcaoId, long NaturezaDespesaId, long FonteRecursoId, decimal DotacaoInicial, bool Ativo);
public sealed record OrcamentoDespesaFiltro(int Page = 1, int PageSize = 20, long? ProgramaId = null, long? AcaoId = null, long? NaturezaDespesaId = null, long? FonteRecursoId = null);
public sealed record OrcamentoDespesaResponse(long Id, long? OrgaoUnidadeOrcamentariaId, long ProgramaId, long AcaoId, long NaturezaDespesaId, long FonteRecursoId, decimal DotacaoInicial, decimal Suplementacoes, decimal Reducoes, decimal Reservado, decimal Empenhado, decimal Liquidado, decimal Pago, decimal SaldoDisponivel, bool Ativo);
public sealed record OrcamentoReceitaCreateRequest(long NaturezaReceitaId, long FonteRecursoId, decimal PrevisaoInicial);
public sealed record OrcamentoReceitaUpdateRequest(long NaturezaReceitaId, long FonteRecursoId, decimal PrevisaoInicial, decimal PrevisaoAtualizada, bool Ativo);
public sealed record OrcamentoReceitaFiltro(int Page = 1, int PageSize = 20, long? NaturezaReceitaId = null, long? FonteRecursoId = null);
public sealed record OrcamentoReceitaResponse(long Id, long NaturezaReceitaId, long FonteRecursoId, decimal PrevisaoInicial, decimal PrevisaoAtualizada, decimal Lancado, decimal Arrecadado, bool Ativo);
public sealed record MovimentacaoOrcamentariaRequest(string TipoMovimentacao, decimal Valor, string Historico);

public sealed record EmpenhoItemRequest(string Descricao, decimal Quantidade, decimal ValorUnitario);
public sealed record EmpenhoCreateRequest(long OrcamentoDespesaId, DateOnly DataEmpenho, long FornecedorPessoaId, string Historico, string TipoEmpenho, IReadOnlyCollection<EmpenhoItemRequest> Itens, string? Observacoes);
public sealed record EmpenhoUpdateRequest(DateOnly DataEmpenho, string Historico, string TipoEmpenho, IReadOnlyCollection<EmpenhoItemRequest> Itens, string? Observacoes);
public sealed record EmpenhoFiltro(int Page = 1, int PageSize = 20, string? Numero = null, string? Fornecedor = null, string? Status = null, DateOnly? Inicio = null, DateOnly? Fim = null, long? NaturezaDespesaId = null, long? FonteRecursoId = null, long? ProgramaId = null, long? AcaoId = null);
public sealed record EmpenhoResumoResponse(long Id, string Numero, DateOnly DataEmpenho, string Fornecedor, string Natureza, string Fonte, decimal ValorTotal, decimal ValorLiquidado, decimal ValorPago, decimal Saldo, string Status);
public sealed record EmpenhoDetalheResponse(long Id, string Numero, DateOnly DataEmpenho, long OrcamentoDespesaId, long FornecedorPessoaId, string Fornecedor, string Historico, string TipoEmpenho, decimal ValorTotal, decimal ValorAnulado, decimal ValorLiquidado, decimal ValorPago, string Status, IReadOnlyCollection<EmpenhoItemResponse> Itens);
public sealed record EmpenhoItemResponse(long Id, string Descricao, decimal Quantidade, decimal ValorUnitario, decimal ValorTotal);
public sealed record AnularEmpenhoRequest(decimal Valor, string Motivo);

public sealed record LiquidacaoCreateRequest(DateOnly DataLiquidacao, string? DocumentoFiscal, string Historico, decimal Valor);
public sealed record LiquidacaoFiltro(int Page = 1, int PageSize = 20, string? Numero = null, long? EmpenhoId = null, string? Status = null);
public sealed record LiquidacaoResponse(long Id, long EmpenhoId, string Numero, DateOnly DataLiquidacao, string? DocumentoFiscal, string Historico, decimal Valor, string Status);
public sealed record AnularLiquidacaoRequest(string Motivo);
public sealed record PagamentoCreateRequest(DateOnly DataPagamento, string FormaPagamento, string? ContaBancaria, string Historico, decimal Valor);
public sealed record PagamentoFiltro(int Page = 1, int PageSize = 20, string? Numero = null, long? LiquidacaoId = null, string? Status = null);
public sealed record PagamentoResponse(long Id, long LiquidacaoId, string Numero, DateOnly DataPagamento, string FormaPagamento, string? ContaBancaria, string Historico, decimal Valor, string Status);
public sealed record CancelarPagamentoRequest(string Motivo);

public sealed record ReceitaLancamentoCreateRequest(long OrcamentoReceitaId, DateOnly DataLancamento, long? ContribuintePessoaId, string Historico, decimal Valor);
public sealed record ReceitaLancamentoFiltro(int Page = 1, int PageSize = 20, string? Numero = null, string? Status = null, long? ContribuintePessoaId = null);
public sealed record ReceitaLancamentoResponse(long Id, long OrcamentoReceitaId, string Numero, DateOnly DataLancamento, long? ContribuintePessoaId, string? Contribuinte, string Historico, decimal Valor, decimal Arrecadado, string Status);
public sealed record ReceitaArrecadacaoCreateRequest(DateOnly DataArrecadacao, string FormaArrecadacao, decimal Valor, string Historico);
public sealed record ReceitaArrecadacaoResponse(long Id, long ReceitaLancamentoId, string Numero, DateOnly DataArrecadacao, string FormaArrecadacao, decimal Valor, string Historico, string Status);
public sealed record FinanceiroResumoDespesaResponse(decimal OrcamentoAutorizado, decimal Empenhado, decimal Liquidado, decimal Pago, decimal SaldoDisponivel);
public sealed record FinanceiroResumoReceitaResponse(decimal ReceitaPrevista, decimal ReceitaLancada, decimal ReceitaArrecadada);
public sealed record FinanceiroDashboardResponse(FinanceiroResumoDespesaResponse Despesa, FinanceiroResumoReceitaResponse Receita, DateTimeOffset AtualizadoEm);

public interface ICrudFinanceiroRepository<TCreate, TUpdate, TFiltro, TResponse>
{
    Task<PagedResult<TResponse>> ListarAsync(long tenantId, long entidadeId, long exercicioId, TFiltro filtro, CancellationToken ct);
    Task<TResponse?> ObterAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct);
    Task<long> CriarAsync(long tenantId, long entidadeId, long exercicioId, TCreate request, long? usuarioId, CancellationToken ct);
    Task AtualizarAsync(long tenantId, long entidadeId, long exercicioId, long id, TUpdate request, long? usuarioId, CancellationToken ct);
    Task ExcluirAsync(long tenantId, long entidadeId, long exercicioId, long id, long? usuarioId, CancellationToken ct);
}
public interface IPlanoContasRepository : ICrudFinanceiroRepository<PlanoContasCreateRequest, PlanoContasUpdateRequest, PlanoContasFiltro, PlanoContasResponse> { }
public interface IFonteRecursoRepository : ICrudFinanceiroRepository<FonteRecursoCreateRequest, FonteRecursoUpdateRequest, FonteRecursoFiltro, FonteRecursoResponse> { }
public interface IProgramaRepository : ICrudFinanceiroRepository<ProgramaCreateRequest, ProgramaUpdateRequest, ProgramaFiltro, ProgramaResponse> { }
public interface IAcaoRepository : ICrudFinanceiroRepository<AcaoCreateRequest, AcaoUpdateRequest, AcaoFiltro, AcaoResponse> { }
public interface INaturezaReceitaRepository : ICrudFinanceiroRepository<NaturezaReceitaCreateRequest, NaturezaReceitaUpdateRequest, NaturezaReceitaFiltro, NaturezaReceitaResponse> { }
public interface INaturezaDespesaRepository : ICrudFinanceiroRepository<NaturezaDespesaCreateRequest, NaturezaDespesaUpdateRequest, NaturezaDespesaFiltro, NaturezaDespesaResponse> { }

public interface IOrcamentoRepository
{
    Task<PagedResult<OrcamentoDespesaResponse>> ListarDespesasAsync(long tenantId, long entidadeId, long exercicioId, OrcamentoDespesaFiltro filtro, CancellationToken ct);
    Task<OrcamentoDespesaResponse?> ObterDespesaAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct);
    Task<long> CriarDespesaAsync(long tenantId, long entidadeId, long exercicioId, OrcamentoDespesaCreateRequest request, long? usuarioId, CancellationToken ct);
    Task AtualizarDespesaAsync(long tenantId, long entidadeId, long exercicioId, long id, OrcamentoDespesaUpdateRequest request, long? usuarioId, CancellationToken ct);
    Task MovimentarDespesaAsync(long tenantId, long entidadeId, long exercicioId, long id, MovimentacaoOrcamentariaRequest request, long? usuarioId, CancellationToken ct);
    Task<PagedResult<OrcamentoReceitaResponse>> ListarReceitasAsync(long tenantId, long entidadeId, long exercicioId, OrcamentoReceitaFiltro filtro, CancellationToken ct);
    Task<OrcamentoReceitaResponse?> ObterReceitaAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct);
    Task<long> CriarReceitaAsync(long tenantId, long entidadeId, long exercicioId, OrcamentoReceitaCreateRequest request, long? usuarioId, CancellationToken ct);
    Task AtualizarReceitaAsync(long tenantId, long entidadeId, long exercicioId, long id, OrcamentoReceitaUpdateRequest request, long? usuarioId, CancellationToken ct);
}
public interface IEmpenhoRepository { Task<PagedResult<EmpenhoResumoResponse>> ListarAsync(long tenantId, long entidadeId, long exercicioId, EmpenhoFiltro filtro, CancellationToken ct); Task<EmpenhoDetalheResponse?> ObterAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, long entidadeId, long exercicioId, string numero, int ano, EmpenhoCreateRequest request, long? usuarioId, CancellationToken ct); Task AtualizarAsync(long tenantId, long entidadeId, long exercicioId, long id, EmpenhoUpdateRequest request, long? usuarioId, CancellationToken ct); Task AnularAsync(long tenantId, long entidadeId, long exercicioId, long id, AnularEmpenhoRequest request, long? usuarioId, CancellationToken ct); }
public interface ILiquidacaoRepository { Task<PagedResult<LiquidacaoResponse>> ListarAsync(long tenantId, long entidadeId, long exercicioId, LiquidacaoFiltro filtro, CancellationToken ct); Task<LiquidacaoResponse?> ObterAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, long entidadeId, long exercicioId, long empenhoId, string numero, LiquidacaoCreateRequest request, long? usuarioId, CancellationToken ct); Task AnularAsync(long tenantId, long entidadeId, long exercicioId, long id, AnularLiquidacaoRequest request, long? usuarioId, CancellationToken ct); }
public interface IPagamentoRepository { Task<PagedResult<PagamentoResponse>> ListarAsync(long tenantId, long entidadeId, long exercicioId, PagamentoFiltro filtro, CancellationToken ct); Task<PagamentoResponse?> ObterAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, long entidadeId, long exercicioId, long liquidacaoId, string numero, PagamentoCreateRequest request, long? usuarioId, CancellationToken ct); Task CancelarAsync(long tenantId, long entidadeId, long exercicioId, long id, CancelarPagamentoRequest request, long? usuarioId, CancellationToken ct); }
public interface IReceitaRepository { Task<PagedResult<ReceitaLancamentoResponse>> ListarLancamentosAsync(long tenantId, long entidadeId, long exercicioId, ReceitaLancamentoFiltro filtro, CancellationToken ct); Task<ReceitaLancamentoResponse?> ObterLancamentoAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct); Task<long> CriarLancamentoAsync(long tenantId, long entidadeId, long exercicioId, string numero, ReceitaLancamentoCreateRequest request, long? usuarioId, CancellationToken ct); Task<long> ArrecadarAsync(long tenantId, long entidadeId, long exercicioId, long lancamentoId, string numero, ReceitaArrecadacaoCreateRequest request, long? usuarioId, CancellationToken ct); }
public interface IFinanceiroSequencialService { Task<string> ProximoAsync(long tenantId, long entidadeId, long exercicioId, int ano, string escopo, string prefixo, CancellationToken ct); }
public interface IFinanceiroDashboardRepository { Task<FinanceiroDashboardResponse> ObterAsync(long tenantId, long entidadeId, long exercicioId, CancellationToken ct); }

public interface IPlanoContasService { Task<Result<PagedResult<PlanoContasResponse>>> ListarAsync(PlanoContasFiltro filtro, CancellationToken ct); Task<Result<PlanoContasResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(PlanoContasCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, PlanoContasUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IFonteRecursoService { Task<Result<PagedResult<FonteRecursoResponse>>> ListarAsync(FonteRecursoFiltro filtro, CancellationToken ct); Task<Result<FonteRecursoResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(FonteRecursoCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, FonteRecursoUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IProgramaService { Task<Result<PagedResult<ProgramaResponse>>> ListarAsync(ProgramaFiltro filtro, CancellationToken ct); Task<Result<ProgramaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(ProgramaCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, ProgramaUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IAcaoService { Task<Result<PagedResult<AcaoResponse>>> ListarAsync(AcaoFiltro filtro, CancellationToken ct); Task<Result<AcaoResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(AcaoCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, AcaoUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface INaturezaReceitaService { Task<Result<PagedResult<NaturezaReceitaResponse>>> ListarAsync(NaturezaReceitaFiltro filtro, CancellationToken ct); Task<Result<NaturezaReceitaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(NaturezaReceitaCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, NaturezaReceitaUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface INaturezaDespesaService { Task<Result<PagedResult<NaturezaDespesaResponse>>> ListarAsync(NaturezaDespesaFiltro filtro, CancellationToken ct); Task<Result<NaturezaDespesaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(NaturezaDespesaCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, NaturezaDespesaUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IOrcamentoService { Task<Result<PagedResult<OrcamentoDespesaResponse>>> ListarDespesasAsync(OrcamentoDespesaFiltro filtro, CancellationToken ct); Task<Result<OrcamentoDespesaResponse>> ObterDespesaAsync(long id, CancellationToken ct); Task<Result<long>> CriarDespesaAsync(OrcamentoDespesaCreateRequest request, CancellationToken ct); Task<Result> AtualizarDespesaAsync(long id, OrcamentoDespesaUpdateRequest request, CancellationToken ct); Task<Result> MovimentarDespesaAsync(long id, MovimentacaoOrcamentariaRequest request, CancellationToken ct); Task<Result<PagedResult<OrcamentoReceitaResponse>>> ListarReceitasAsync(OrcamentoReceitaFiltro filtro, CancellationToken ct); Task<Result<OrcamentoReceitaResponse>> ObterReceitaAsync(long id, CancellationToken ct); Task<Result<long>> CriarReceitaAsync(OrcamentoReceitaCreateRequest request, CancellationToken ct); Task<Result> AtualizarReceitaAsync(long id, OrcamentoReceitaUpdateRequest request, CancellationToken ct); }
public interface IEmpenhoService { Task<Result<PagedResult<EmpenhoResumoResponse>>> ListarAsync(EmpenhoFiltro filtro, CancellationToken ct); Task<Result<EmpenhoDetalheResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(EmpenhoCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, EmpenhoUpdateRequest request, CancellationToken ct); Task<Result> AnularAsync(long id, AnularEmpenhoRequest request, CancellationToken ct); }
public interface ILiquidacaoService { Task<Result<PagedResult<LiquidacaoResponse>>> ListarAsync(LiquidacaoFiltro filtro, CancellationToken ct); Task<Result<LiquidacaoResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(long empenhoId, LiquidacaoCreateRequest request, CancellationToken ct); Task<Result> AnularAsync(long id, AnularLiquidacaoRequest request, CancellationToken ct); }
public interface IPagamentoService { Task<Result<PagedResult<PagamentoResponse>>> ListarAsync(PagamentoFiltro filtro, CancellationToken ct); Task<Result<PagamentoResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(long liquidacaoId, PagamentoCreateRequest request, CancellationToken ct); Task<Result> CancelarAsync(long id, CancelarPagamentoRequest request, CancellationToken ct); }
public interface IReceitaService { Task<Result<PagedResult<ReceitaLancamentoResponse>>> ListarLancamentosAsync(ReceitaLancamentoFiltro filtro, CancellationToken ct); Task<Result<ReceitaLancamentoResponse>> ObterLancamentoAsync(long id, CancellationToken ct); Task<Result<long>> CriarLancamentoAsync(ReceitaLancamentoCreateRequest request, CancellationToken ct); Task<Result<long>> ArrecadarAsync(long lancamentoId, ReceitaArrecadacaoCreateRequest request, CancellationToken ct); }
public interface IFinanceiroDashboardService { Task<Result<FinanceiroDashboardResponse>> ObterAsync(CancellationToken ct); }
public interface IFinanceiroExportacaoService { Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct); }
