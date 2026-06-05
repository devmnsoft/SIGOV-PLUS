namespace Sigov.Domain.Financeiro;

public enum TipoContaContabil { Ativo, Passivo, PatrimonioLiquido, Receita, Despesa, Controle }
public enum NaturezaSaldo { Devedor, Credor }
public enum TipoAcao { Projeto, Atividade, OperacaoEspecial }
public enum TipoEmpenho { Ordinario, Global, Estimativo }
public enum EmpenhoStatus { Rascunho, Emitido, Anulado, LiquidadoParcial, LiquidadoTotal, PagoParcial, PagoTotal }
public enum LiquidacaoStatus { Liquidada, Anulada, PagaParcial, PagaTotal }
public enum PagamentoStatus { Efetuado, Cancelado }
public enum ReceitaLancamentoStatus { Lancada, Cancelada, ArrecadadaParcial, ArrecadadaTotal }
public enum FormaPagamento { Transferencia, Pix, Cheque, Dinheiro, Boleto, Outros }
public enum FormaArrecadacao { Transferencia, Pix, Dinheiro, Boleto, Outros }
public enum TipoMovimentacaoOrcamentaria { Suplementacao, Reducao, Reserva, EstornoReserva }

public abstract class FinanceiroEntity
{
    protected FinanceiroEntity(long tenantId, long entidadeId, long exercicioId)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade obrigatória.", nameof(entidadeId));
        if (exercicioId <= 0) throw new ArgumentException("Exercício obrigatório.", nameof(exercicioId));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId;
    }
    public long Id { get; protected set; }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long ExercicioId { get; }
    public bool Ativo { get; protected set; } = true;
    public bool IsDeleted { get; protected set; }
}

public sealed class PlanoContas : FinanceiroEntity
{
    public PlanoContas(long tenantId, long entidadeId, long exercicioId, string codigo, string nome, TipoContaContabil tipoConta, int nivel) : base(tenantId, entidadeId, exercicioId)
    { Codigo = Require(codigo, nameof(codigo)); Nome = Require(nome, nameof(nome)); TipoConta = tipoConta; Nivel = nivel > 0 ? nivel : throw new ArgumentException("Nível deve ser maior que zero.", nameof(nivel)); }
    public string Codigo { get; }
    public string Nome { get; }
    public TipoContaContabil TipoConta { get; }
    public int Nivel { get; }
    public long? ContaPaiId { get; init; }
    public NaturezaSaldo? NaturezaSaldo { get; init; }
    public bool AceitaLancamento { get; init; } = true;
    private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim();
}

public sealed class FonteRecurso : FinanceiroEntity { public FonteRecurso(long tenantId, long entidadeId, long exercicioId, string codigo, string nome) : base(tenantId, entidadeId, exercicioId) { Codigo = codigo; Nome = nome; } public string Codigo { get; } public string Nome { get; } }
public sealed class OrgaoUnidadeOrcamentaria : FinanceiroEntity { public OrgaoUnidadeOrcamentaria(long tenantId, long entidadeId, long exercicioId, string codigo, string nome) : base(tenantId, entidadeId, exercicioId) { Codigo = codigo; Nome = nome; } public string Codigo { get; } public string Nome { get; } }
public sealed class Programa : FinanceiroEntity { public Programa(long tenantId, long entidadeId, long exercicioId, string codigo, string nome) : base(tenantId, entidadeId, exercicioId) { Codigo = codigo; Nome = nome; } public string Codigo { get; } public string Nome { get; } }
public sealed class Acao : FinanceiroEntity { public Acao(long tenantId, long entidadeId, long exercicioId, long programaId, string codigo, string nome, TipoAcao tipoAcao) : base(tenantId, entidadeId, exercicioId) { ProgramaId = programaId > 0 ? programaId : throw new ArgumentException("Programa obrigatório.", nameof(programaId)); Codigo = codigo; Nome = nome; TipoAcao = tipoAcao; } public long ProgramaId { get; } public string Codigo { get; } public string Nome { get; } public TipoAcao TipoAcao { get; } }
public sealed class NaturezaReceita : FinanceiroEntity { public NaturezaReceita(long tenantId, long entidadeId, long exercicioId, string codigo, string nome) : base(tenantId, entidadeId, exercicioId) { Codigo = codigo; Nome = nome; } public string Codigo { get; } public string Nome { get; } }
public sealed class NaturezaDespesa : FinanceiroEntity { public NaturezaDespesa(long tenantId, long entidadeId, long exercicioId, string codigo, string nome) : base(tenantId, entidadeId, exercicioId) { Codigo = codigo; Nome = nome; } public string Codigo { get; } public string Nome { get; } }

public sealed class OrcamentoDespesa : FinanceiroEntity
{
    public OrcamentoDespesa(long tenantId, long entidadeId, long exercicioId, decimal dotacaoInicial, decimal suplementacoes = 0m, decimal reducoes = 0m, decimal reservado = 0m, decimal empenhado = 0m, decimal liquidado = 0m, decimal pago = 0m) : base(tenantId, entidadeId, exercicioId)
    {
        if (dotacaoInicial < 0m) throw new InvalidOperationException("Orçamento da despesa não pode ter dotação negativa.");
        DotacaoInicial = dotacaoInicial; Suplementacoes = suplementacoes; Reducoes = reducoes; Reservado = reservado; Empenhado = empenhado; Liquidado = liquidado; Pago = pago;
    }
    public decimal DotacaoInicial { get; private set; }
    public decimal Suplementacoes { get; private set; }
    public decimal Reducoes { get; private set; }
    public decimal Reservado { get; private set; }
    public decimal Empenhado { get; private set; }
    public decimal Liquidado { get; private set; }
    public decimal Pago { get; private set; }
    public decimal SaldoDisponivel => DotacaoInicial + Suplementacoes - Reducoes - Reservado - Empenhado;
    public void ValidarEmpenho(decimal valor, bool exercicioEncerrado)
    {
        if (exercicioEncerrado) throw new InvalidOperationException("Exercício encerrado bloqueia novas operações financeiras.");
        if (valor <= 0m) throw new InvalidOperationException("Empenho deve ter valor maior que zero.");
        if (valor > SaldoDisponivel) throw new InvalidOperationException("Empenho não pode ultrapassar saldo disponível.");
    }
}

public sealed class OrcamentoReceita : FinanceiroEntity
{
    public OrcamentoReceita(long tenantId, long entidadeId, long exercicioId, decimal previsaoInicial, decimal lancado = 0m, decimal arrecadado = 0m) : base(tenantId, entidadeId, exercicioId)
    { if (previsaoInicial < 0m) throw new InvalidOperationException("Previsão de receita não pode ser negativa."); PrevisaoInicial = previsaoInicial; PrevisaoAtualizada = previsaoInicial; Lancado = lancado; Arrecadado = arrecadado; }
    public decimal PrevisaoInicial { get; }
    public decimal PrevisaoAtualizada { get; }
    public decimal Lancado { get; private set; }
    public decimal Arrecadado { get; private set; }
}

public sealed class OrcamentoMovimentacao : FinanceiroEntity { public OrcamentoMovimentacao(long tenantId, long entidadeId, long exercicioId, long orcamentoDespesaId, TipoMovimentacaoOrcamentaria tipo, decimal valor) : base(tenantId, entidadeId, exercicioId) { if (valor <= 0m) throw new InvalidOperationException("Movimentação deve ter valor maior que zero."); OrcamentoDespesaId = orcamentoDespesaId; Tipo = tipo; Valor = valor; } public long OrcamentoDespesaId { get; } public TipoMovimentacaoOrcamentaria Tipo { get; } public decimal Valor { get; } }

public sealed class Empenho : FinanceiroEntity
{
    public Empenho(long tenantId, long entidadeId, long exercicioId, long fornecedorPessoaId, decimal valorTotal, EmpenhoStatus status = EmpenhoStatus.Emitido) : base(tenantId, entidadeId, exercicioId)
    { if (fornecedorPessoaId <= 0) throw new InvalidOperationException("Empenho não pode ser emitido sem fornecedor."); if (valorTotal <= 0m) throw new InvalidOperationException("Empenho emitido deve ter valor maior que zero."); FornecedorPessoaId = fornecedorPessoaId; ValorTotal = valorTotal; Status = status; }
    public long FornecedorPessoaId { get; }
    public decimal ValorTotal { get; private set; }
    public decimal ValorAnulado { get; private set; }
    public decimal ValorLiquidado { get; private set; }
    public decimal ValorPago { get; private set; }
    public EmpenhoStatus Status { get; private set; }
    public decimal SaldoALiquidar => ValorTotal - ValorAnulado - ValorLiquidado;
    public decimal SaldoAPagar => ValorLiquidado - ValorPago;
    public void ValidarContraOrcamento(OrcamentoDespesa orcamento, bool exercicioEncerrado) => orcamento.ValidarEmpenho(ValorTotal, exercicioEncerrado);
    public void Anular(decimal valor) { if (valor <= 0m || valor > ValorTotal - ValorAnulado) throw new InvalidOperationException("Valor de anulação inválido."); ValorAnulado += valor; Status = EmpenhoStatus.Anulado; }
    public void RegistrarLiquidacao(decimal valor, bool exercicioEncerrado) { if (exercicioEncerrado) throw new InvalidOperationException("Exercício encerrado bloqueia novas operações financeiras."); if (Status == EmpenhoStatus.Anulado) throw new InvalidOperationException("Empenho anulado não pode ser liquidado."); if (valor <= 0m || valor > SaldoALiquidar) throw new InvalidOperationException("Liquidação não pode ultrapassar saldo do empenho."); ValorLiquidado += valor; Status = SaldoALiquidar == 0m ? EmpenhoStatus.LiquidadoTotal : EmpenhoStatus.LiquidadoParcial; }
    public void RegistrarPagamento(decimal valor, bool exercicioEncerrado) { if (exercicioEncerrado) throw new InvalidOperationException("Exercício encerrado bloqueia novas operações financeiras."); if (valor <= 0m || valor > SaldoAPagar) throw new InvalidOperationException("Pagamento não pode ultrapassar saldo liquidado."); ValorPago += valor; Status = SaldoAPagar == 0m ? EmpenhoStatus.PagoTotal : EmpenhoStatus.PagoParcial; }
}

public sealed class EmpenhoItem : FinanceiroEntity { public EmpenhoItem(long tenantId, long entidadeId, long exercicioId, string descricao, decimal quantidade, decimal valorUnitario) : base(tenantId, entidadeId, exercicioId) { if (quantidade <= 0m || valorUnitario <= 0m) throw new InvalidOperationException("Item de empenho deve ter quantidade e valor positivos."); Descricao = descricao; Quantidade = quantidade; ValorUnitario = valorUnitario; } public string Descricao { get; } public decimal Quantidade { get; } public decimal ValorUnitario { get; } public decimal ValorTotal => decimal.Round(Quantidade * ValorUnitario, 2); }
public sealed class Liquidacao : FinanceiroEntity { public Liquidacao(long tenantId, long entidadeId, long exercicioId, Empenho empenho, decimal valor) : base(tenantId, entidadeId, exercicioId) { empenho.RegistrarLiquidacao(valor, false); Valor = valor; Status = LiquidacaoStatus.Liquidada; } public decimal Valor { get; } public LiquidacaoStatus Status { get; } }
public sealed class Pagamento : FinanceiroEntity { public Pagamento(long tenantId, long entidadeId, long exercicioId, Empenho empenho, decimal valor) : base(tenantId, entidadeId, exercicioId) { empenho.RegistrarPagamento(valor, false); Valor = valor; Status = PagamentoStatus.Efetuado; } public decimal Valor { get; } public PagamentoStatus Status { get; } }

public sealed class ReceitaLancamento : FinanceiroEntity
{
    public ReceitaLancamento(long tenantId, long entidadeId, long exercicioId, decimal valor) : base(tenantId, entidadeId, exercicioId) { if (valor <= 0m) throw new InvalidOperationException("Receita lançada deve ter valor maior que zero."); Valor = valor; }
    public decimal Valor { get; }
    public decimal Arrecadado { get; private set; }
    public decimal SaldoArrecadar => Valor - Arrecadado;
    public void RegistrarArrecadacao(decimal valor, bool exercicioEncerrado) { if (exercicioEncerrado) throw new InvalidOperationException("Exercício encerrado bloqueia novas operações financeiras."); if (valor <= 0m || valor > SaldoArrecadar) throw new InvalidOperationException("Arrecadação não pode ultrapassar saldo da receita lançada."); Arrecadado += valor; }
}
public sealed class ReceitaArrecadacao : FinanceiroEntity { public ReceitaArrecadacao(long tenantId, long entidadeId, long exercicioId, ReceitaLancamento lancamento, decimal valor) : base(tenantId, entidadeId, exercicioId) { lancamento.RegistrarArrecadacao(valor, false); Valor = valor; } public decimal Valor { get; } }
public sealed class PrestacaoContas : FinanceiroEntity { public PrestacaoContas(long tenantId, long entidadeId, long exercicioId, string competencia) : base(tenantId, entidadeId, exercicioId) { Competencia = competencia; } public string Competencia { get; } }
