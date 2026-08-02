namespace Sigov.Domain.ComprasEmpresariais;

public class ComprasDomainException(string message) : InvalidOperationException(message);
public sealed class ComprasConcurrencyException() : ComprasDomainException("O registro foi alterado por outro usuário.");

public enum FornecedorStatus { Rascunho, EmAnalise, Ativo, Suspenso, Bloqueado, Inativo }
public enum RequisicaoStatus { Rascunho, Enviada, PendenteAprovacao, Devolvida, Aprovada, Reprovada, EmCotacao, ParcialmentePedida, Pedida, ParcialmenteRecebida, Recebida, Cancelada, Encerrada }
public enum TipoCompra { Material, Servico, Ativo, PecaParaOs, CompraRecorrente }
public enum CotacaoStatus { Rascunho, Aberta, ConvitesPendentes, EmResposta, Encerrada, Julgada, Cancelada, Expirada }
public enum PedidoStatus { Rascunho, EmAprovacao, Aprovado, Emitido, Enviado, Confirmado, ParcialmenteRecebido, Recebido, ParcialmenteFaturado, Faturado, Cancelado, Encerrado }
public enum ResultadoInspecao { Pendente, Aceito, AceitoComRessalva, Recusado, Quarentena }
public enum ResultadoMatch { Compativel, CompativelComTolerancia, Divergente, Bloqueado }
public enum FaturaStatus { Recebida, EmConferencia, Divergente, Bloqueada, Aprovada, EnviadaFinanceiro, Cancelada }

public abstract class AggregateRoot
{
    protected AggregateRoot(Guid id, long tenantId, long version = 1)
    {
        if (id == Guid.Empty) throw new ComprasDomainException("Identificador obrigatório.");
        if (tenantId <= 0) throw new ComprasDomainException("Tenant obrigatório.");
        Id = id; TenantId = tenantId; Version = version;
    }
    public Guid Id { get; }
    public long TenantId { get; }
    public long Version { get; private set; }
    public void EnsureVersion(long expected) { if (Version != expected) throw new ComprasConcurrencyException(); }
    protected void Changed() => Version++;
}

public sealed class Fornecedor : AggregateRoot
{
    public Fornecedor(Guid id, long tenantId, string codigo, string razaoSocial, string documentoHash, string documentoMascarado)
        : base(id, tenantId)
    {
        Codigo = Required(codigo, "Código"); RazaoSocial = Required(razaoSocial, "Razão social");
        DocumentoHash = Required(documentoHash, "Hash do documento"); DocumentoMascarado = Required(documentoMascarado, "Documento mascarado");
    }
    public string Codigo { get; }
    public string RazaoSocial { get; private set; }
    public string DocumentoHash { get; }
    public string DocumentoMascarado { get; }
    public FornecedorStatus Status { get; private set; } = FornecedorStatus.Rascunho;
    public string? MotivoBloqueio { get; private set; }
    public decimal Score { get; private set; }
    public DateTimeOffset? UltimaCompraEm { get; private set; }
    public void Ativar() { if (Status is FornecedorStatus.Bloqueado or FornecedorStatus.Inativo) throw new ComprasDomainException("Fornecedor bloqueado ou inativo não pode ser ativado diretamente."); Status = FornecedorStatus.Ativo; Changed(); }
    public void Bloquear(string motivo) { MotivoBloqueio = Required(motivo, "Motivo"); Status = FornecedorStatus.Bloqueado; Changed(); }
    public void Suspender(string motivo) { MotivoBloqueio = Required(motivo, "Motivo"); Status = FornecedorStatus.Suspenso; Changed(); }
    public void Inativar() { Status = FornecedorStatus.Inativo; Changed(); }
    public void ExigirDisponivelParaPedido() { if (Status != FornecedorStatus.Ativo) throw new ComprasDomainException("Somente fornecedor ativo pode receber pedido."); }
    public void ConsolidarScore(IEnumerable<decimal> notas) { var valores = notas.ToArray(); if (valores.Length == 0 || valores.Any(x => x is < 0 or > 100)) throw new ComprasDomainException("Avaliações inválidas."); Score = Math.Round(valores.Average(), 2); Changed(); }
    private static string Required(string value, string field) => string.IsNullOrWhiteSpace(value) ? throw new ComprasDomainException($"{field} obrigatório.") : value.Trim();
}

public sealed record RequisicaoItem(Guid Id, TipoCompra Tipo, string Descricao, string Unidade, decimal Quantidade, decimal ValorEstimado, long? CentroCustoId, long? ContratoId, Guid? OrdemServicoId, bool ExigeInspecao, bool PermiteCompraParcial)
{
    public decimal Total => Quantidade * ValorEstimado;
    public void Validate() { if (Id == Guid.Empty || Quantidade <= 0 || ValorEstimado < 0 || string.IsNullOrWhiteSpace(Descricao) || string.IsNullOrWhiteSpace(Unidade)) throw new ComprasDomainException("Item de requisição inválido."); }
}

public sealed class RequisicaoCompra : AggregateRoot
{
    private readonly List<RequisicaoItem> _itens = [];
    public RequisicaoCompra(Guid id, long tenantId, string numero, long solicitanteId, string justificativa) : base(id, tenantId)
    { Numero = string.IsNullOrWhiteSpace(numero) ? throw new ComprasDomainException("Número obrigatório.") : numero; SolicitanteId = solicitanteId > 0 ? solicitanteId : throw new ComprasDomainException("Solicitante obrigatório."); Justificativa = justificativa; }
    public string Numero { get; }
    public long SolicitanteId { get; }
    public string Justificativa { get; private set; }
    public RequisicaoStatus Status { get; private set; } = RequisicaoStatus.Rascunho;
    public IReadOnlyList<RequisicaoItem> Itens => _itens;
    public decimal TotalEstimado => _itens.Sum(x => x.Total);
    public void AdicionarItem(RequisicaoItem item) { EnsureDraft(); item.Validate(); if (_itens.Any(x => x.Id == item.Id)) throw new ComprasDomainException("Item duplicado."); _itens.Add(item); Changed(); }
    public void Enviar() { EnsureDraft(); if (_itens.Count == 0) throw new ComprasDomainException("A requisição deve possuir ao menos um item."); if (string.IsNullOrWhiteSpace(Justificativa)) throw new ComprasDomainException("Justificativa obrigatória."); Status = RequisicaoStatus.PendenteAprovacao; Changed(); }
    public void Aprovar(long aprovadorId, decimal limite, bool permiteAutoAprovacao) { if (Status != RequisicaoStatus.PendenteAprovacao) throw new ComprasDomainException("Requisição não aguarda aprovação."); if (!permiteAutoAprovacao && aprovadorId == SolicitanteId) throw new ComprasDomainException("O solicitante não pode aprovar a própria requisição."); if (TotalEstimado > limite) throw new ComprasDomainException("Valor acima da alçada do aprovador."); Status = RequisicaoStatus.Aprovada; Changed(); }
    public void Devolver(string motivo) { RequireReason(motivo); Status = RequisicaoStatus.Devolvida; Changed(); }
    public void Rejeitar(string motivo) { RequireReason(motivo); Status = RequisicaoStatus.Reprovada; Changed(); }
    public void Cancelar(string motivo) { RequireReason(motivo); if (Status is RequisicaoStatus.Recebida or RequisicaoStatus.Encerrada) throw new ComprasDomainException("Requisição recebida ou encerrada não pode ser cancelada."); Status = RequisicaoStatus.Cancelada; Changed(); }
    private void EnsureDraft() { if (Status != RequisicaoStatus.Rascunho) throw new ComprasDomainException("Alterações exigem retorno a rascunho e nova versão."); }
    private static void RequireReason(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ComprasDomainException("Motivo obrigatório."); }
}

public sealed record ToleranciasMatch(decimal PrecoPercentual, decimal QuantidadePercentual, decimal TotalAbsoluto, decimal FreteAbsoluto, decimal ImpostoAbsoluto, decimal Arredondamento)
{
    public void Validate() { if (new[] { PrecoPercentual, QuantidadePercentual, TotalAbsoluto, FreteAbsoluto, ImpostoAbsoluto, Arredondamento }.Any(x => x < 0)) throw new ComprasDomainException("Tolerâncias não podem ser negativas."); }
}

public sealed record MatchItem(decimal PedidoQuantidade, decimal RecebidoQuantidade, decimal FaturadoQuantidade, decimal PedidoUnitario, decimal FaturadoUnitario);
public sealed class ThreeWayMatch
{
    public static ResultadoMatch Conferir(IEnumerable<MatchItem> itens, decimal totalPedido, decimal totalFatura, ToleranciasMatch tolerancias, bool possuiDivergenciaBloqueante)
    {
        tolerancias.Validate(); if (possuiDivergenciaBloqueante) return ResultadoMatch.Bloqueado;
        var dentro = true; var exato = totalPedido == totalFatura;
        foreach (var item in itens)
        {
            var quantidadeBase = Math.Max(item.PedidoQuantidade, 0.0001m); var precoBase = Math.Max(item.PedidoUnitario, 0.0001m);
            var q = Math.Abs(item.FaturadoQuantidade - item.RecebidoQuantidade) / quantidadeBase * 100;
            var p = Math.Abs(item.FaturadoUnitario - item.PedidoUnitario) / precoBase * 100;
            dentro &= q <= tolerancias.QuantidadePercentual && p <= tolerancias.PrecoPercentual;
            exato &= q == 0 && p == 0;
        }
        dentro &= Math.Abs(totalFatura - totalPedido) <= tolerancias.TotalAbsoluto + tolerancias.Arredondamento;
        return !dentro ? ResultadoMatch.Divergente : exato ? ResultadoMatch.Compativel : ResultadoMatch.CompativelComTolerancia;
    }
}
