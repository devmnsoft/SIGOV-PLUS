using System.Text.Json;
using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Application.Security;
using Sigov.Infrastructure.Diagnostics;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Web.Services.Development;

public sealed record DevelopmentAuthReport(
    DateTimeOffset CheckedAt, string Environment, SafeDatabaseTarget ConnectionTarget,
    int AdminUserCount, long? CanonicalAdminId, int DuplicateAdminsHandled,
    bool PasswordMatches, int RolesCount, int PermissionsCount, bool HasGroup,
    bool HasProfile, bool HasEntity, bool HasExercise, string FinalReason,
    bool ResetPerformed, string LoginSmokeResult, DevelopmentAdmin? Admin = null,
    string? Error = null);

public sealed record DevelopmentAdmin(long Id, long? TenantId, string Login, string Email,
    string Nome, bool Ativo, bool Bloqueado, bool IsDeleted, bool DeveAlterarSenha,
    string TipoUsuario, bool TenantAtivo, bool TenantIsDeleted, bool HashExists,
    string HashPrefix, bool HashFormatValid);

public sealed class DevelopmentAuthDiagnosticService
{
    public const string AdminPassword = "SigovDevLocal!2026";
    private readonly NpgsqlConnectionFactory _connections;
    private readonly IPasswordHashService _passwords;
    private readonly IAuthenticationRepository _authentication;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevelopmentAuthDiagnosticService> _logger;

    public DevelopmentAuthDiagnosticService(NpgsqlConnectionFactory connections, IPasswordHashService passwords,
        IAuthenticationRepository authentication, IConfiguration configuration, IWebHostEnvironment environment,
        ILogger<DevelopmentAuthDiagnosticService> logger)
    {
        _connections = connections; _passwords = passwords; _authentication = authentication;
        _configuration = configuration; _environment = environment; _logger = logger;
    }

    public async Task<DevelopmentAuthReport> DiagnoseAsync(bool resetPerformed = false, int duplicatesHandled = 0, CancellationToken ct = default)
    {
        var target = SafeConnectionStringDiagnostics.Read(_configuration, _environment);
        try
        {
            using var connection = _connections.CreateConnection();
            const string sql = @"select u.id, u.tenant_id as TenantId, u.login, u.email, coalesce(u.nome,u.login) as Nome,
 u.ativo, u.bloqueado, u.is_deleted as IsDeleted, coalesce(u.deve_alterar_senha,false) as DeveAlterarSenha,
 coalesce(u.tipo_usuario,'') as TipoUsuario, coalesce(t.ativo,true) as TenantAtivo,
 coalesce(t.is_deleted,false) as TenantIsDeleted, u.senha_hash as PasswordHash,
 exists(select 1 from sigov.usuario_grupo x where x.usuario_id=u.id and not x.is_deleted) as HasGroup,
 exists(select 1 from sigov.usuario_grupo x join sigov.grupo_perfil gp on gp.grupo_acesso_id=x.grupo_acesso_id and not gp.is_deleted where x.usuario_id=u.id and not x.is_deleted) as HasProfile,
 exists(select 1 from sigov.usuario_entidade x where x.usuario_id=u.id and x.ativo) as HasEntity,
 exists(select 1 from sigov.usuario_exercicio x where x.usuario_id=u.id and x.ativo) as HasExercise
from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id
where lower(u.login)='admin' or lower(u.email)='admin@sigov.local'
order by u.is_deleted, u.ativo desc, u.bloqueado, u.id;";
            var rows = (await connection.QueryAsync<AdminRow>(new CommandDefinition(sql, cancellationToken: ct))).ToArray();
            var row = rows.FirstOrDefault();
            var repositoryUser = await _authentication.FindForLoginAsync("admin", ct);
            var access = repositoryUser is null ? new AuthenticationAccess(Array.Empty<string>(), Array.Empty<string>()) : await _authentication.GetAccessAsync(repositoryUser.Id, ct);
            var validFormat = IsValidHash(row?.PasswordHash);
            var matches = validFormat && _passwords.VerifyPassword(AdminPassword, row!.PasswordHash);
            var reason = Reason(rows.Length, row, repositoryUser, validFormat, matches, access);
            var admin = row is null ? null : new DevelopmentAdmin(row.Id, row.TenantId, row.Login, row.Email, row.Nome,
                row.Ativo, row.Bloqueado, row.IsDeleted, row.DeveAlterarSenha, row.TipoUsuario, row.TenantAtivo,
                row.TenantIsDeleted, !string.IsNullOrEmpty(row.PasswordHash), HashPrefix(row.PasswordHash), validFormat);
            var report = new DevelopmentAuthReport(DateTimeOffset.UtcNow, _environment.EnvironmentName, target, rows.Length,
                row?.Id, duplicatesHandled, matches, access.Roles.Count, access.Permissions.Count, row?.HasGroup == true,
                row?.HasProfile == true, row?.HasEntity == true, row?.HasExercise == true, reason, resetPerformed,
                reason == "OK" ? "OK (cookie não criado pelo teste)" : reason, admin);
            await WriteReportAsync(report, ct);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no diagnóstico interno de autenticação. CorrelationId={CorrelationId}", System.Diagnostics.Activity.Current?.Id);
            var report = new DevelopmentAuthReport(DateTimeOffset.UtcNow, _environment.EnvironmentName, target, 0, null, 0,
                false, 0, 0, false, false, false, false, "DATABASE_ERROR", resetPerformed, "DATABASE_ERROR", Error: "Consulte o log pelo CorrelationId.");
            await WriteReportAsync(report, ct);
            return report;
        }
    }

    public async Task<DevelopmentAuthReport> ResetAdminAsync(string correlationId, string? ip, string? userAgent, CancellationToken ct)
    {
        var hash = _passwords.HashPassword(AdminPassword);
        using var connection = _connections.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        const string sql = @"select pg_advisory_xact_lock(hashtext('SIGOV_DEV_ADMIN_RESET'));
insert into sigov.tenant(nome,nome_fantasia,slug,status,ambiente,ativo,is_deleted)
select 'SIGOV Local','SIGOV Local','sigov-local','ATIVO','DEVELOPMENT',true,false
where not exists(select 1 from sigov.tenant where slug='sigov-local');
update sigov.tenant set ativo=true,is_deleted=false,status='ATIVO',updated_at=now() where slug='sigov-local';
insert into sigov.entidade(tenant_id,nome,cnpj,ativo,is_deleted)
select t.id,'Entidade Local','00000000000000',true,false from sigov.tenant t where t.slug='sigov-local'
and not exists(select 1 from sigov.entidade e where e.tenant_id=t.id and e.cnpj='00000000000000');
insert into sigov.exercicio(tenant_id,entidade_id,ano,data_inicio,data_fim,ativo,is_deleted)
select t.id,e.id,extract(year from current_date)::int,make_date(extract(year from current_date)::int,1,1),make_date(extract(year from current_date)::int,12,31),true,false
from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.cnpj='00000000000000' where t.slug='sigov-local' on conflict(entidade_id,ano) do nothing;
insert into sigov.pessoa(tenant_id,entidade_id,exercicio_id,tipo_pessoa,nome,documento,ativo,is_deleted)
select t.id,e.id,x.id,'F','Administrador Geral','admin@sigov.local',true,false from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.cnpj='00000000000000' join sigov.exercicio x on x.entidade_id=e.id and x.ano=extract(year from current_date)::int
where t.slug='sigov-local' and not exists(select 1 from sigov.pessoa p where p.tenant_id=t.id and p.documento='admin@sigov.local');
with canonical as (select u.id from sigov.usuario u where lower(u.login)='admin' or lower(u.email)='admin@sigov.local' order by u.is_deleted,u.id limit 1), ctx as
(select t.id tenant_id,e.id entidade_id,x.id exercicio_id,p.id pessoa_id from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.cnpj='00000000000000' join sigov.exercicio x on x.entidade_id=e.id and x.ano=extract(year from current_date)::int join sigov.pessoa p on p.tenant_id=t.id and p.documento='admin@sigov.local' where t.slug='sigov-local')
insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,pessoa_id,nome,login,email,senha_hash,tipo_usuario,senha_deve_ser_alterada,deve_alterar_senha,bloqueado,tentativas_invalidas,ativo,is_deleted)
select tenant_id,entidade_id,exercicio_id,pessoa_id,'Administrador Geral','admin','admin@sigov.local',@Hash,'ADMINISTRADOR_GERAL',false,false,false,0,true,false from ctx where not exists(select 1 from canonical);
with chosen as (select id from sigov.usuario where lower(login)='admin' or lower(email)='admin@sigov.local' order by is_deleted,id limit 1), ctx as
(select t.id tenant_id,e.id entidade_id,x.id exercicio_id,p.id pessoa_id from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.cnpj='00000000000000' join sigov.exercicio x on x.entidade_id=e.id and x.ano=extract(year from current_date)::int join sigov.pessoa p on p.tenant_id=t.id and p.documento='admin@sigov.local' where t.slug='sigov-local')
update sigov.usuario u set tenant_id=c.tenant_id,entidade_id=c.entidade_id,exercicio_id=c.exercicio_id,pessoa_id=c.pessoa_id,nome='Administrador Geral',login='admin',email='admin@sigov.local',senha_hash=@Hash,tipo_usuario='ADMINISTRADOR_GERAL',senha_deve_ser_alterada=false,deve_alterar_senha=false,bloqueado=false,tentativas_invalidas=0,bloqueado_ate=null,ativo=true,is_deleted=false,updated_at=now() from chosen,ctx c where u.id=chosen.id;
with chosen as (select id from sigov.usuario where lower(login)='admin' or lower(email)='admin@sigov.local' order by is_deleted,id limit 1)
update sigov.usuario u set login='admin_duplicado_'||u.id,email='admin_duplicado_'||u.id||'@invalid.local',is_deleted=true,ativo=false,updated_at=now() from chosen where u.id<>chosen.id and (lower(u.login)='admin' or lower(u.email)='admin@sigov.local');
insert into sigov.grupo_acesso(tenant_id,entidade_id,exercicio_id,nome,descricao,ativo,is_deleted) select t.id,e.id,x.id,'Administradores','Administração local Development',true,false from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.cnpj='00000000000000' join sigov.exercicio x on x.entidade_id=e.id and x.ano=extract(year from current_date)::int where t.slug='sigov-local' and not exists(select 1 from sigov.grupo_acesso g where g.tenant_id=t.id and g.nome='Administradores');
insert into sigov.perfil_acesso(tenant_id,entidade_id,exercicio_id,nome,codigo_externo,ativo,is_deleted) select t.id,e.id,x.id,'Administrador Geral','ADMINISTRADOR_GERAL',true,false from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.cnpj='00000000000000' join sigov.exercicio x on x.entidade_id=e.id and x.ano=extract(year from current_date)::int where t.slug='sigov-local' and not exists(select 1 from sigov.perfil_acesso p where p.tenant_id=t.id and p.codigo_externo='ADMINISTRADOR_GERAL');
update sigov.grupo_acesso set ativo=true,is_deleted=false where nome='Administradores' and tenant_id=(select id from sigov.tenant where slug='sigov-local');
update sigov.perfil_acesso set ativo=true,is_deleted=false where codigo_externo='ADMINISTRADOR_GERAL' and tenant_id=(select id from sigov.tenant where slug='sigov-local');
insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,is_deleted) select g.id,p.id,false from sigov.grupo_acesso g join sigov.perfil_acesso p on p.tenant_id=g.tenant_id and p.codigo_externo='ADMINISTRADOR_GERAL' where g.nome='Administradores' and g.tenant_id=(select id from sigov.tenant where slug='sigov-local') on conflict(grupo_acesso_id,perfil_acesso_id) do update set is_deleted=false;
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id) select p.id,m.id from sigov.perfil_acesso p cross join sigov.permissao m where p.codigo_externo='ADMINISTRADOR_GERAL' and p.tenant_id=(select id from sigov.tenant where slug='sigov-local') and m.ativo and not m.is_deleted on conflict do nothing;
insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,is_deleted) select u.id,g.id,false from sigov.usuario u join sigov.grupo_acesso g on g.tenant_id=u.tenant_id and g.nome='Administradores' where u.login='admin' and not u.is_deleted on conflict(usuario_id,grupo_acesso_id) do update set is_deleted=false;
insert into sigov.usuario_entidade(usuario_id,entidade_id,ativo) select id,entidade_id,true from sigov.usuario where login='admin' and not is_deleted on conflict(usuario_id,entidade_id) do update set ativo=true;
insert into sigov.usuario_exercicio(usuario_id,exercicio_id,ativo) select id,exercicio_id,true from sigov.usuario where login='admin' and not is_deleted on conflict(usuario_id,exercicio_id) do update set ativo=true;";
        var before = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.usuario where lower(login)='admin' or lower(email)='admin@sigov.local'", transaction: transaction, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Hash = hash }, transaction, cancellationToken: ct));
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition("select id from sigov.usuario where login='admin' and not is_deleted order by id limit 1", transaction: transaction, cancellationToken: ct));
        const string auditSql = @"insert into sigov.auditoria_evento
    (tenant_id, usuario_id, acao, entidade, entidade_id, depois, ip, user_agent, correlation_id, created_at)
select
    tenant_id,
    id,
    'DEV_ADMIN_RESET',
    'sigov.usuario',
    id::text,
    jsonb_build_object('resetPerformed', true),
    @Ip,
    @UserAgent,
    cast(@CorrelationId as uuid),
    now()
from sigov.usuario
where id = @Id;";
        await connection.ExecuteAsync(new CommandDefinition(
            auditSql,
            new { Id = id, Ip = ip, UserAgent = userAgent, CorrelationId = correlationId },
            transaction,
            cancellationToken: ct));
        await transaction.CommitAsync(ct);
        _logger.LogWarning("DEV_ADMIN_RESET concluído. UserId={UserId}; DuplicatesHandled={DuplicatesHandled}; CorrelationId={CorrelationId}", id, Math.Max(0, before - 1), correlationId);
        return await DiagnoseAsync(true, Math.Max(0, before - 1), ct);
    }

    private static string Reason(int count, AdminRow? u, AuthenticationUser? selected, bool format, bool match, AuthenticationAccess access)
    {
        if (u is null) return "LOGIN_NOT_FOUND"; if (count > 1) return "DUPLICATE_LOGIN";
        if (selected is null || selected.Id != u.Id) return "AUTH_REPOSITORY_SELECTED_DIFFERENT_USER";
        if (!u.Ativo) return "USER_INACTIVE"; if (u.Bloqueado) return "USER_BLOCKED"; if (u.IsDeleted) return "USER_DELETED";
        if (!u.TenantAtivo) return "TENANT_INACTIVE"; if (u.TenantIsDeleted) return "TENANT_DELETED";
        if (string.IsNullOrWhiteSpace(u.PasswordHash)) return "PASSWORD_HASH_MISSING"; if (!format) return "PASSWORD_HASH_INVALID_FORMAT";
        if (!match) return "PASSWORD_MISMATCH"; if (!u.HasGroup) return "NO_GROUP"; if (!u.HasProfile || access.Roles.Count == 0) return "NO_PROFILE";
        if (access.Permissions.Count == 0) return "NO_PERMISSIONS"; if (!u.HasEntity) return "NO_ENTITY"; if (!u.HasExercise) return "NO_EXERCISE"; return "OK";
    }
    private static bool IsValidHash(string? value) { var p = value?.Split('$'); return p is { Length: 4 } && p[0] == "SIGOV_PBKDF2_V1" && int.TryParse(p[1], out var n) && n is >= 100000 and <= 1000000; }
    private static string HashPrefix(string? value) => string.IsNullOrEmpty(value) ? "" : value[..Math.Min(value.Length, 18)] + "…";
    private async Task WriteReportAsync(DevelopmentAuthReport report, CancellationToken ct)
    {
        var path = Path.Combine(_environment.ContentRootPath, "..", "..", "artifacts", "local-setup", "dev-auth-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), ct);
    }
    private sealed class AdminRow { public long Id { get; set; } public long? TenantId { get; set; } public string Login { get; set; } = ""; public string Email { get; set; } = ""; public string Nome { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string TipoUsuario { get; set; } = ""; public bool Ativo { get; set; } public bool Bloqueado { get; set; } public bool IsDeleted { get; set; } public bool DeveAlterarSenha { get; set; } public bool TenantAtivo { get; set; } public bool TenantIsDeleted { get; set; } public bool HasGroup { get; set; } public bool HasProfile { get; set; } public bool HasEntity { get; set; } public bool HasExercise { get; set; } }
}
