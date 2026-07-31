using Sigov.Application.Common;
using Sigov.Domain.Comercial;

namespace Sigov.Application.Commercial;

public sealed class CommercialApplicationService(ICommercialRepository repository) : ICommercialApplicationService
{
    private static void Validate(CommercialExecutionContext context)
    {
        if (context.TenantId == Guid.Empty) throw new UnauthorizedAccessException("Tenant comercial não resolvido.");
        if (context.UsuarioId == Guid.Empty) throw new UnauthorizedAccessException("Usuário comercial não resolvido.");
    }

    public Task<PagedResult<ClienteResumoDto>> ListarClientesAsync(CommercialExecutionContext c, ClienteFiltro f, CancellationToken ct) { Validate(c); return repository.ListarClientesAsync(c.TenantId, f, ct); }
    public Task<ClienteDetalheDto?> ObterClienteAsync(CommercialExecutionContext c, Guid id, CancellationToken ct) { Validate(c); return repository.ObterClienteAsync(c.TenantId, id, c.PodeVisualizarDadosPessoais, ct); }
    public Task<Guid> CriarClienteAsync(CommercialExecutionContext c, CriarClienteRequest r, CancellationToken ct) { Validate(c); return repository.CriarClienteAsync(c.TenantId, c.UsuarioId, r, c.CorrelationId, ct); }
    public Task<PagedResult<LeadResumoDto>> ListarLeadsAsync(CommercialExecutionContext c, int p, int s, string? b, CancellationToken ct) { Validate(c); return repository.ListarLeadsAsync(c.TenantId, p, s, b, ct); }
    public Task<Guid> CriarLeadAsync(CommercialExecutionContext c, CriarLeadRequest r, CancellationToken ct) { Validate(c); return repository.CriarLeadAsync(c.TenantId, c.UsuarioId, r, c.CorrelationId, ct); }
    public Task<ConversaoLeadDto> ConverterLeadAsync(CommercialExecutionContext c, Guid id, ConverterLeadRequest r, CancellationToken ct) { Validate(c); return repository.ConverterLeadAsync(c.TenantId, c.UsuarioId, id, r, c.CorrelationId, ct); }
    public Task<PagedResult<OportunidadeResumoDto>> ListarOportunidadesAsync(CommercialExecutionContext c, int p, int s, string? f, string? b, CancellationToken ct) { Validate(c); return repository.ListarOportunidadesAsync(c.TenantId, p, s, f, b, ct); }
    public Task MoverFaseAsync(CommercialExecutionContext c, Guid id, MoverOportunidadeRequest r, CancellationToken ct) { Validate(c); return repository.MoverOportunidadeAsync(c.TenantId, c.UsuarioId, id, r, c.CorrelationId, ct); }
    public Task<PagedResult<PropostaResumoDto>> ListarPropostasAsync(CommercialExecutionContext c, int p, int s, CancellationToken ct) { Validate(c); return repository.ListarPropostasAsync(c.TenantId, p, s, ct); }
    public Task<PropostaDetalheDto?> ObterPropostaAsync(CommercialExecutionContext c, Guid id, CancellationToken ct) { Validate(c); return repository.ObterPropostaAsync(c.TenantId, id, ct); }
    public Task<Guid> CriarPropostaAsync(CommercialExecutionContext c, CriarPropostaRequest r, CancellationToken ct) { Validate(c); return repository.CriarPropostaAsync(c.TenantId, c.UsuarioId, r, c.CorrelationId, ct); }
    public Task EmitirAsync(CommercialExecutionContext c, Guid id, long v, CancellationToken ct) { Validate(c); return repository.EmitirPropostaAsync(c.TenantId, c.UsuarioId, id, v, c.CorrelationId, ct); }
    public Task AprovarAsync(CommercialExecutionContext c, Guid id, long v, CancellationToken ct) { Validate(c); return repository.AprovarPropostaAsync(c.TenantId, c.UsuarioId, id, v, c.CorrelationId, ct); }
    public Task<PedidoDetalheDto> GerarPedidoAsync(CommercialExecutionContext c, Guid id, string key, CancellationToken ct) { Validate(c); RequireKey(key); return repository.GerarPedidoAsync(c.TenantId, c.UsuarioId, id, key, c.CorrelationId, ct); }
    public Task<PagedResult<PedidoResumoDto>> ListarPedidosAsync(CommercialExecutionContext c, int p, int s, CancellationToken ct) { Validate(c); return repository.ListarPedidosAsync(c.TenantId, p, s, ct); }
    public Task ConfirmarPedidoAsync(CommercialExecutionContext c, Guid id, long v, string key, CancellationToken ct) { Validate(c); RequireKey(key); return repository.ConfirmarPedidoAsync(c.TenantId, c.UsuarioId, id, v, key, c.CorrelationId, ct); }
    public Task<ComercialDashboardDto> ObterDashboardAsync(CommercialExecutionContext c, DateOnly i, DateOnly f, CancellationToken ct) { Validate(c); return repository.ObterDashboardAsync(c.TenantId, i, f, ct); }
    private static void RequireKey(string key) { if (string.IsNullOrWhiteSpace(key) || key.Length > 200) throw new CommercialRuleException("Idempotency-Key válido é obrigatório."); }
}
