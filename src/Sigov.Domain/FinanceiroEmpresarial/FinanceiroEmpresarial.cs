namespace Sigov.Domain.FinanceiroEmpresarial;

public enum StatusContaReceber { Rascunho, Aberta, Parcial, Recebida, Vencida, Cancelada, Estornada, Renegociada }
public enum StatusContaPagar { Rascunho, Aberta, Aprovada, Parcial, Paga, Vencida, Cancelada, Estornada, Renegociada }
public enum TipoMovimentoFinanceiro { Entrada, Saida, TransferenciaEntrada, TransferenciaSaida, EstornoEntrada, EstornoSaida, AjustePositivo, AjusteNegativo, SaldoInicial }

public static class RegrasFinanceiras
{
    public static decimal ValorEfetivo(decimal valor, decimal desconto, decimal acrescimo)
    {
        if (valor <= 0) throw new ArgumentOutOfRangeException(nameof(valor), "O valor deve ser maior que zero.");
        if (desconto < 0 || acrescimo < 0 || valor - desconto + acrescimo <= 0)
            throw new ArgumentOutOfRangeException(nameof(desconto), "Desconto e acréscimo devem resultar em valor positivo.");
        return valor - desconto + acrescimo;
    }

    public static void ValidarBaixa(decimal saldoAberto, decimal valor, bool permiteParcial)
    {
        if (valor <= 0 || valor > saldoAberto) throw new InvalidOperationException("Valor da baixa excede o saldo aberto ou é inválido.");
        if (!permiteParcial && valor != saldoAberto) throw new InvalidOperationException("Baixa parcial não permitida para o tenant.");
    }
}
