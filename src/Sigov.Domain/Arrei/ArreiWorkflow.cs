namespace Sigov.Domain.Arrei;

public enum ArreiStatus
{
    Inicial,
    EmAnalise,
    Deferido,
    Indeferido
}

/// <summary>Regras puras e centralizadas para a evolução de uma solicitação ARREI.</summary>
public static class ArreiWorkflow
{
    public static bool PodeTransicionar(ArreiStatus atual, ArreiStatus destino, bool permitirReanalise = false) =>
        (atual, destino) switch
        {
            (ArreiStatus.Inicial, ArreiStatus.EmAnalise) => true,
            (ArreiStatus.EmAnalise, ArreiStatus.Deferido) => true,
            (ArreiStatus.EmAnalise, ArreiStatus.Indeferido) => true,
            (ArreiStatus.Deferido, ArreiStatus.EmAnalise) => permitirReanalise,
            (ArreiStatus.Indeferido, ArreiStatus.EmAnalise) => permitirReanalise,
            _ => false
        };

    public static void ValidarTransicao(
        ArreiStatus atual,
        ArreiStatus destino,
        string? justificativa,
        bool permitirReanalise = false)
    {
        if (!PodeTransicionar(atual, destino, permitirReanalise))
            throw new ArreiTransitionException(atual, destino);

        if (destino == ArreiStatus.Indeferido && string.IsNullOrWhiteSpace(justificativa))
            throw new ArreiInvariantException("O indeferimento exige justificativa.");

        if ((atual is ArreiStatus.Deferido or ArreiStatus.Indeferido) &&
            destino == ArreiStatus.EmAnalise && string.IsNullOrWhiteSpace(justificativa))
            throw new ArreiInvariantException("O retorno para análise exige justificativa.");
    }
}

public sealed class ArreiTransitionException(ArreiStatus atual, ArreiStatus destino)
    : InvalidOperationException($"Transição ARREI inválida: {atual} -> {destino}.");

public sealed class ArreiInvariantException(string message) : InvalidOperationException(message);
