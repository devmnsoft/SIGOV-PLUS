namespace Sigov.Worker.Outbox;

/// <summary>
/// RC-EVO-B §5: taxonomia de falhas do outbox. Falha DEFINITIVA (configuração ausente, payload
/// inválido/sem assinatura/checksum divergente, saldo inexistente) — reprocessar sem intervenção
/// humana nunca vai funcionar. O handler lança este tipo e o processor envia a mensagem direto
/// para dead-letter rastreável (FALHOU + erro nomeado), em vez de queimar 5 tentativas em backoff.
/// Transitórias (banco/rede) continuam como exceção comum e mantêm o retry com backoff.
/// </summary>
public sealed class OutboxPermanentFailureException : Exception
{
    public OutboxPermanentFailureException(string message) : base(message)
    {
    }

    public OutboxPermanentFailureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
