namespace Sigov.Application.Operational;

public sealed class AgendaService : IAgendaService
{
    private readonly IAgendaRepository _repository;
    private readonly IOperationalEventPublisher _events;

    public AgendaService(IAgendaRepository repository, IOperationalEventPublisher events)
    {
        _repository = repository;
        _events = events;
    }

    public async Task<AgendaCompromissoDto> CriarAsync(CriarCompromissoRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo)) throw new ArgumentException("Título do compromisso é obrigatório.", nameof(request));
        if (request.FimEm <= request.InicioEm) throw new InvalidOperationException("Fim do compromisso deve ser posterior ao início.");

        var compromisso = await _repository.CriarAsync(request, context, cancellationToken).ConfigureAwait(false);
        await _events.PublishAsync(new OperationalEvent("agenda.criada", "agenda_compromisso", compromisso.Id.ToString(), context.TenantId, context.UserId, context.CorrelationId, new { compromisso.Id, compromisso.Status }, $"agenda:{compromisso.Id}:criada"), cancellationToken).ConfigureAwait(false);
        return compromisso;
    }
}
