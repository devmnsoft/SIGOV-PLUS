using System.Text.Json;
using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class AgendaRepository : IAgendaRepository { private readonly DapperContext _context; public AgendaRepository(DapperContext context)=>_context=context; public async Task<AgendaCompromissoDto> CriarAsync(CriarCompromissoRequest request, OperationalCommandContext context,CancellationToken cancellationToken){using var connection=_context.CreateConnection(); return await connection.QuerySingleAsync<AgendaCompromissoDto>(new CommandDefinition(@"insert into sigov.agenda_compromisso (tenant_id, titulo, descricao, inicio_em, fim_em, status, created_by, updated_by, correlation_id) values (@TenantId, @Titulo, @Descricao, @InicioEm, @FimEm, 'AGENDADO', @UserId, @UserId, @CorrelationId) returning id, tenant_id as TenantId, titulo, inicio_em as InicioEm, fim_em as FimEm, status;", new { context.TenantId, request.Titulo, request.Descricao, request.InicioEm, request.FimEm, context.UserId, context.CorrelationId}, cancellationToken:cancellationToken)).ConfigureAwait(false);} }
