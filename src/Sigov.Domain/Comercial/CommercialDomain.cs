namespace Sigov.Domain.Comercial;

public enum LeadStatus { Novo, EmContato, Qualificado, Descartado, Convertido }
public enum OportunidadeFase { Prospeccao, Qualificacao, Levantamento, Proposta, Negociacao, Ganha, Perdida }
public enum PropostaStatus { Rascunho, Emitida, Aprovada, Reprovada, Expirada, Cancelada }
public enum PedidoStatus { Aberto, Confirmado, EmSeparacao, Pronto, EmExecucao, Concluido, Cancelado }

public sealed class CommercialRuleException(string message) : InvalidOperationException(message);
public sealed class CommercialConflictException(string message) : InvalidOperationException(message);
public sealed class CommercialNotFoundException(string message) : InvalidOperationException(message);

public sealed record PropostaItem(Guid? ProdutoId, string Descricao, string Unidade, decimal Quantidade, decimal ValorUnitario, decimal Desconto, int Ordem)
{
    public decimal Total
    {
        get
        {
            if (Quantidade <= 0) throw new CommercialRuleException("A quantidade deve ser maior que zero.");
            if (ValorUnitario < 0 || Desconto < 0) throw new CommercialRuleException("Valores e descontos não podem ser negativos.");
            var total = decimal.Round(Quantidade * ValorUnitario - Desconto, 2, MidpointRounding.AwayFromZero);
            if (total < 0) throw new CommercialRuleException("O desconto não pode tornar o item negativo.");
            return total;
        }
    }
}

public static class CommercialRules
{
    public static void PodeConverter(LeadStatus status)
    {
        if (status is LeadStatus.Descartado or LeadStatus.Convertido)
            throw new CommercialConflictException("Lead descartado ou já convertido não pode ser convertido.");
    }

    public static void ValidarMovimento(OportunidadeFase fase, string? motivo)
    {
        if (fase == OportunidadeFase.Perdida && string.IsNullOrWhiteSpace(motivo))
            throw new CommercialRuleException("O motivo da perda é obrigatório.");
    }

    public static (decimal Subtotal, decimal Total) CalcularProposta(IEnumerable<PropostaItem> itens, decimal desconto, decimal acrescimo)
    {
        var lista = itens.ToArray();
        if (lista.Length == 0) throw new CommercialRuleException("A proposta deve possuir pelo menos um item.");
        if (desconto < 0 || acrescimo < 0) throw new CommercialRuleException("Desconto e acréscimo não podem ser negativos.");
        var subtotal = lista.Sum(x => x.Total);
        var total = decimal.Round(subtotal - desconto + acrescimo, 2, MidpointRounding.AwayFromZero);
        if (total < 0) throw new CommercialRuleException("O desconto não pode tornar a proposta negativa.");
        return (subtotal, total);
    }
}
