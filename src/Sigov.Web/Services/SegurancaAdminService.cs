using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Lote1;
using System.Security.Cryptography;

namespace Sigov.Web.Services;

public sealed class SegurancaAdminService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ILogger<SegurancaAdminService> _logger;

    public SegurancaAdminService(NpgsqlConnectionFactory connectionFactory, IPasswordHashService passwordHashService, ILogger<SegurancaAdminService> logger)
    { _connectionFactory = connectionFactory; _passwordHashService = passwordHashService; _logger = logger; }

    public async Task<IReadOnlyCollection<UsuarioListItemViewModel>> ListarUsuariosAsync(UsuarioFiltroViewModel filtro, CancellationToken ct)
    {
        const string sql = @"select u.id, coalesce(u.nome,u.login) as Nome, u.login, coalesce(u.email,'') as Email, coalesce(t.nome,'Global') as Tenant, coalesce(u.tipo_usuario,'Operador') as Perfil, u.ativo, coalesce(u.bloqueado,false) as Bloqueado
from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id
where coalesce(u.is_deleted,false)=false
  and (@Termo is null or coalesce(u.nome,'') ilike '%'||@Termo||'%' or u.login ilike '%'||@Termo||'%' or coalesce(u.email,'') ilike '%'||@Termo||'%')
  and (@Ativo is null or u.ativo=@Ativo)
  and (@Bloqueado is null or coalesce(u.bloqueado,false)=@Bloqueado)
order by coalesce(u.nome,u.login) limit 100;";
        try { using var cn = _connectionFactory.CreateConnection(); var rows = await cn.QueryAsync<UsuarioRow>(new CommandDefinition(sql, new { Termo = string.IsNullOrWhiteSpace(filtro.Termo) ? null : filtro.Termo.Trim(), filtro.Ativo, filtro.Bloqueado }, cancellationToken: ct)).ConfigureAwait(false); return rows.Select(x => new UsuarioListItemViewModel { Id=x.Id, Nome=x.Nome, Login=x.Login, EmailMascarado=MaskEmail(x.Email), Tenant=x.Tenant, Perfil=x.Perfil, Ativo=x.Ativo, Bloqueado=x.Bloqueado }).ToArray(); }
        catch(Exception ex){ _logger.LogError(ex,"Falha ao listar usuários reais."); return Array.Empty<UsuarioListItemViewModel>(); }
    }

    public async Task<UsuarioDetalheViewModel?> ObterUsuarioAsync(long id, CancellationToken ct)
    {
        const string sql = @"select u.id, coalesce(u.nome,u.login) as Nome, u.login, coalesce(u.email,'') as Email, u.pessoa_id as PessoaId, u.ativo, coalesce(u.bloqueado,false) as Bloqueado, coalesce(u.deve_alterar_senha,true) as DeveAlterarSenha, coalesce(u.mfa_habilitado,false) as MfaHabilitado, coalesce(t.nome,'Global') as Tenant from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id where u.id=@Id and coalesce(u.is_deleted,false)=false;";
        try { using var cn=_connectionFactory.CreateConnection(); var u=await cn.QuerySingleOrDefaultAsync<UsuarioDetalheViewModel>(new CommandDefinition(sql,new{Id=id},cancellationToken:ct)).ConfigureAwait(false); if(u!=null) u.Status=u.Bloqueado?"Bloqueado":u.Ativo?"Ativo":"Inativo"; return u; }
        catch(Exception ex){ _logger.LogError(ex,"Falha ao obter usuário {Id}.", id); return null; }
    }

    public async Task<(bool Ok,string Mensagem,long? Id)> SalvarUsuarioAsync(UsuarioFormViewModel form, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(form.Login) || string.IsNullOrWhiteSpace(form.Email)) return (false,"Login e e-mail são obrigatórios.",form.Id);
        try
        {
            using var cn=_connectionFactory.CreateConnection();
            var dup=await cn.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.usuario where coalesce(is_deleted,false)=false and (@Id is null or id<>@Id) and (lower(login)=lower(@Login) or lower(email)=lower(@Email));", new{form.Id, Login=form.Login.Trim(), Email=form.Email.Trim()}, cancellationToken:ct)).ConfigureAwait(false);
            if(dup>0) return (false,"Login ou e-mail já cadastrado.",form.Id);
            if(form.Id.HasValue)
            {
                await cn.ExecuteAsync(new CommandDefinition("update sigov.usuario set login=@Login,email=@Email,pessoa_id=@PessoaId,ativo=@Ativo,bloqueado=@Bloqueado,deve_alterar_senha=@DeveAlterarSenha,mfa_habilitado=@MfaHabilitado,updated_at=now() where id=@Id;", form, cancellationToken:ct)).ConfigureAwait(false);
                await AuditarAsync(cn,"USUARIO_EDITAR",form.Id.Value,form,ct).ConfigureAwait(false); return (true,"Usuário atualizado com sucesso.",form.Id);
            }
            var senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
            var hash = _passwordHashService.HashPassword(senha);
            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.usuario(login,email,pessoa_id,ativo,bloqueado,deve_alterar_senha,mfa_habilitado,senha_hash,tipo_usuario,created_at) values(@Login,@Email,@PessoaId,@Ativo,@Bloqueado,true,@MfaHabilitado,@Hash,'OPERADOR',now()) returning id;", new{Login=form.Login.Trim(),Email=form.Email.Trim(),form.PessoaId,form.Ativo,form.Bloqueado,form.MfaHabilitado,Hash=hash}, cancellationToken:ct)).ConfigureAwait(false);
            await AuditarAsync(cn,"USUARIO_CRIAR",id,new{form.Login,form.Email,form.Ativo},ct).ConfigureAwait(false); return (true,"Usuário criado com senha temporária e troca obrigatória no próximo login.",id);
        }
        catch(Exception ex){ _logger.LogError(ex,"Falha ao salvar usuário."); return (false,"Não foi possível persistir o usuário. Nenhum sucesso foi simulado.",form.Id); }
    }

    public Task<bool> AlterarStatusUsuarioAsync(long id,bool ativo,CancellationToken ct)=>ExecutarUsuarioAsync(id, ativo?"USUARIO_ATIVAR":"USUARIO_INATIVAR", "update sigov.usuario set ativo=@Ativo, updated_at=now() where id=@Id;", new{Id=id,Ativo=ativo}, ct);
    public async Task<bool> ResetarSenhaAsync(long id,CancellationToken ct){ var senha=Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(); return await ExecutarUsuarioAsync(id,"USUARIO_RESET_SENHA","update sigov.usuario set senha_hash=@Hash,deve_alterar_senha=true,updated_at=now() where id=@Id;", new{Id=id,Hash=_passwordHashService.HashPassword(senha)}, ct).ConfigureAwait(false); }

    public async Task<IReadOnlyCollection<PerfilListItemViewModel>> ListarPerfisAsync(CancellationToken ct){ try{ using var cn=_connectionFactory.CreateConnection(); return (await cn.QueryAsync<PerfilListItemViewModel>(new CommandDefinition("select id,codigo,nome,coalesce(descricao,'') as Descricao,ativo from sigov.perfil where coalesce(is_deleted,false)=false order by nome limit 100;", cancellationToken:ct)).ConfigureAwait(false)).ToArray(); }catch(Exception ex){_logger.LogWarning(ex,"Perfis indisponíveis; exibindo limitação honesta."); return Array.Empty<PerfilListItemViewModel>();}}
    public async Task<bool> CriarPerfilAsync(PerfilFormViewModel form,CancellationToken ct){ try{using var cn=_connectionFactory.CreateConnection(); var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.perfil(codigo,nome,descricao,ativo,created_at) values(@Codigo,@Nome,@Descricao,true,now()) returning id;", form,cancellationToken:ct)).ConfigureAwait(false); await AuditarAsync(cn,"PERFIL_CRIAR",id,form,ct).ConfigureAwait(false); return true;}catch(Exception ex){_logger.LogError(ex,"Falha ao criar perfil."); return false;}}

    private async Task<bool> ExecutarUsuarioAsync(long id,string acao,string sql,object args,CancellationToken ct){ try{using var cn=_connectionFactory.CreateConnection(); var n=await cn.ExecuteAsync(new CommandDefinition(sql,args,cancellationToken:ct)).ConfigureAwait(false); if(n>0) await AuditarAsync(cn,acao,id,args,ct).ConfigureAwait(false); return n>0;}catch(Exception ex){_logger.LogError(ex,"Falha em ação crítica {Acao}.",acao); return false;}}
    private static async Task AuditarAsync(System.Data.IDbConnection cn,string acao,long id,object payload,CancellationToken ct){ try{ await cn.ExecuteAsync(new CommandDefinition("insert into sigov.auditoria_evento(acao,entidade,entidade_id,depois,created_at) values(@Acao,@Entidade,@Id,@Json::jsonb,now());", new{Acao=acao,Entidade="sigov.usuario",Id=id.ToString(),Json=System.Text.Json.JsonSerializer.Serialize(payload)}, cancellationToken:ct)).ConfigureAwait(false);}catch{}}
    private static string MaskEmail(string value){ if(string.IsNullOrWhiteSpace(value)||!value.Contains('@')) return "***"; var p=value.Split('@',2); return $"{p[0][0]}***@{p[1]}"; }
    private sealed record UsuarioRow(long Id,string Nome,string Login,string Email,string Tenant,string Perfil,bool Ativo,bool Bloqueado);
}
