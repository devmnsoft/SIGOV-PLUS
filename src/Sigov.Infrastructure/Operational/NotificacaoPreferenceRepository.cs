using System.Text.Json;
using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class NotificacaoPreferenceRepository : INotificacaoPreferenceService { private readonly DapperContext _context; public NotificacaoPreferenceRepository(DapperContext context)=>_context=context; public async Task SalvarAsync(long tenantId,long usuarioId,string tipo,bool habilitada,CancellationToken cancellationToken){using var connection=_context.CreateConnection(); await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.notificacao_preferencia (tenant_id, usuario_id, tipo, habilitada) values (@TenantId, @UsuarioId, @Tipo, @Habilitada) on conflict (tenant_id, usuario_id, tipo) do update set habilitada = excluded.habilitada, updated_at = now();", new {TenantId=tenantId, UsuarioId=usuarioId, Tipo=tipo, Habilitada=habilitada}, cancellationToken:cancellationToken)).ConfigureAwait(false);} }
