using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Helpers;
using Sigov.Web.Models.Lote1;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Sigov.Web.Services;

public sealed class SegurancaAdminService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ILogger<SegurancaAdminService> _logger;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly IAuditTrailService _auditTrail;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SegurancaAdminService(NpgsqlConnectionFactory connectionFactory, IPasswordHashService passwordHashService, ILogger<SegurancaAdminService> logger, IDatabaseSchemaInspector schemaInspector, IAuditTrailService auditTrail, IHttpContextAccessor httpContextAccessor)
    { _connectionFactory = connectionFactory; _passwordHashService = passwordHashService; _logger = logger; _schemaInspector = schemaInspector; _auditTrail = auditTrail; _httpContextAccessor = httpContextAccessor; }

    public async Task<IReadOnlyCollection<UsuarioListItemViewModel>> ListarUsuariosAsync(UsuarioFiltroViewModel filtro, CancellationToken ct)
    {
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "usuario", ct).ConfigureAwait(false)) return Array.Empty<UsuarioListItemViewModel>();
            var c = await ColumnsAsync("usuario", ct).ConfigureAwait(false);
            var select = $"select id, {Expr(c,"nome","coalesce(nome,login)","login")} as Nome, login, {Expr(c,"email","coalesce(email,'')","''")} as Email, {Expr(c,"tipo_usuario","coalesce(tipo_usuario,'Operador')","'Operador'")} as Perfil, ativo, {Expr(c,"bloqueado","coalesce(bloqueado,false)","false")} as Bloqueado from sigov.usuario";
            var where = new List<string>();
            if (c.Contains("is_deleted")) where.Add("coalesce(is_deleted,false)=false");
            if (!string.IsNullOrWhiteSpace(filtro.Termo)) where.Add($"({Expr(c,"nome","coalesce(nome,'')","''")} ilike '%'||@Termo||'%' or login ilike '%'||@Termo||'%'" + (c.Contains("email") ? " or coalesce(email,'') ilike '%'||@Termo||'%'" : string.Empty) + ")");
            if (filtro.Ativo.HasValue) where.Add("ativo=@Ativo");
            if (filtro.Bloqueado.HasValue && c.Contains("bloqueado")) where.Add("coalesce(bloqueado,false)=@Bloqueado");
            var sql = select + (where.Count > 0 ? " where " + string.Join(" and ", where) : string.Empty) + $" order by {Expr(c,"nome","coalesce(nome,login)","login")} limit 100;";
            using var cn = _connectionFactory.CreateConnection();
            var rows = await cn.QueryAsync<UsuarioRow>(new CommandDefinition(sql, new { Termo = filtro.Termo?.Trim(), filtro.Ativo, filtro.Bloqueado }, cancellationToken: ct)).ConfigureAwait(false);
            return rows.Select(x => new UsuarioListItemViewModel { Id=x.Id, Nome=x.Nome, Login=x.Login, EmailMascarado=MaskEmail(x.Email), Tenant="Global", Perfil=x.Perfil, Ativo=x.Ativo, Bloqueado=x.Bloqueado }).ToArray();
        }
        catch(Exception ex){ _logger.LogError(ex,"Falha ao listar usuários reais."); return Array.Empty<UsuarioListItemViewModel>(); }
    }

    public async Task<UsuarioDetalheViewModel?> ObterUsuarioAsync(long id, CancellationToken ct)
    {
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "usuario", ct).ConfigureAwait(false)) return null;
            var c = await ColumnsAsync("usuario", ct).ConfigureAwait(false);
            var sql = $"select id, {Expr(c,"nome","coalesce(nome,login)","login")} as Nome, login, {Expr(c,"email","coalesce(email,'')","''")} as Email, {Expr(c,"pessoa_id","pessoa_id","null")} as PessoaId, ativo, {Expr(c,"bloqueado","coalesce(bloqueado,false)","false")} as Bloqueado, {Expr(c,"deve_alterar_senha","coalesce(deve_alterar_senha,true)","true")} as DeveAlterarSenha, {Expr(c,"mfa_habilitado","coalesce(mfa_habilitado,false)","false")} as MfaHabilitado, 'Global' as Tenant from sigov.usuario where id=@Id" + (c.Contains("is_deleted") ? " and coalesce(is_deleted,false)=false" : string.Empty) + ";";
            using var cn=_connectionFactory.CreateConnection(); var u=await cn.QuerySingleOrDefaultAsync<UsuarioDetalheViewModel>(new CommandDefinition(sql,new{Id=id},cancellationToken:ct)).ConfigureAwait(false); if(u!=null) u.Status=u.Bloqueado?"Bloqueado":u.Ativo?"Ativo":"Inativo"; return u;
        }
        catch(Exception ex){ _logger.LogError(ex,"Falha ao obter usuário {Id}.", id); return null; }
    }

    public async Task<(bool Ok,string Mensagem,long? Id)> SalvarUsuarioAsync(UsuarioFormViewModel form, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(form.Login)) return (false,"Login é obrigatório.",form.Id);
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov", "usuario", ct).ConfigureAwait(false)) return (false,"Tabela sigov.usuario indisponível; usuário não foi persistido.",form.Id);
            var c = await ColumnsAsync("usuario", ct).ConfigureAwait(false);
            if (c.Contains("email") && string.IsNullOrWhiteSpace(form.Email)) return (false,"E-mail é obrigatório neste ambiente.", form.Id);
            using var cn=_connectionFactory.CreateConnection();
            var dupSql = "select count(*) from sigov.usuario where " + (c.Contains("is_deleted") ? "coalesce(is_deleted,false)=false and " : string.Empty) + "(@Id is null or id<>@Id) and (lower(login)=lower(@Login)" + (c.Contains("email") ? " or lower(email)=lower(@Email)" : string.Empty) + ");";
            var dup=await cn.ExecuteScalarAsync<int>(new CommandDefinition(dupSql, new{form.Id, Login=form.Login.Trim(), Email=form.Email?.Trim()}, cancellationToken:ct)).ConfigureAwait(false);
            if(dup>0) return (false,"Login ou e-mail já cadastrado.",form.Id);
            if(form.Id.HasValue)
            {
                var set = new List<string>{"login=@Login", "ativo=@Ativo"};
                if(c.Contains("email")) set.Add("email=@Email"); if(c.Contains("pessoa_id")) set.Add("pessoa_id=@PessoaId"); if(c.Contains("bloqueado")) set.Add("bloqueado=@Bloqueado"); if(c.Contains("deve_alterar_senha")) set.Add("deve_alterar_senha=@DeveAlterarSenha"); if(c.Contains("mfa_habilitado")) set.Add("mfa_habilitado=@MfaHabilitado"); if(c.Contains("updated_at")) set.Add("updated_at=now()");
                await cn.ExecuteAsync(new CommandDefinition($"update sigov.usuario set {string.Join(',',set)} where id=@Id;", new { form.Id, Login=form.Login.Trim(), Email=form.Email?.Trim(), form.PessoaId, form.Ativo, form.Bloqueado, form.DeveAlterarSenha, form.MfaHabilitado }, cancellationToken:ct)).ConfigureAwait(false);
                await AuditarAsync("USUARIO_EDITAR","sigov.usuario",form.Id.Value,null,SafePayload(form),ct).ConfigureAwait(false); return (true,"Usuário atualizado com sucesso.",form.Id);
            }
            var cols = new List<string>{"login","ativo","senha_hash"}; var vals = new List<string>{"@Login","@Ativo","@Hash"};
            if(c.Contains("email")){cols.Add("email"); vals.Add("@Email");} if(c.Contains("pessoa_id")){cols.Add("pessoa_id"); vals.Add("@PessoaId");} if(c.Contains("bloqueado")){cols.Add("bloqueado"); vals.Add("@Bloqueado");} if(c.Contains("deve_alterar_senha")){cols.Add("deve_alterar_senha"); vals.Add("true");} if(c.Contains("mfa_habilitado")){cols.Add("mfa_habilitado"); vals.Add("@MfaHabilitado");} if(c.Contains("tipo_usuario")){cols.Add("tipo_usuario"); vals.Add("'OPERADOR'");} if(c.Contains("created_at")){cols.Add("created_at"); vals.Add("now()");}
            var hash = _passwordHashService.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition($"insert into sigov.usuario({string.Join(',',cols)}) values({string.Join(',',vals)}) returning id;", new{Login=form.Login.Trim(),Email=form.Email?.Trim(),form.PessoaId,form.Ativo,form.Bloqueado,form.MfaHabilitado,Hash=hash}, cancellationToken:ct)).ConfigureAwait(false);
            await AuditarAsync("USUARIO_CRIAR","sigov.usuario",id,null,SafePayload(form),ct).ConfigureAwait(false); return (true,"Usuário criado com senha temporária e troca obrigatória no próximo login.",id);
        }
        catch(Exception ex){ _logger.LogError(ex,"Falha ao salvar usuário."); return (false,"Não foi possível persistir o usuário. Nenhum sucesso foi simulado.",form.Id); }
    }

    public Task<bool> AlterarStatusUsuarioAsync(long id,bool ativo,CancellationToken ct)=>ExecutarUsuarioAsync(id, ativo?"USUARIO_ATIVAR":"USUARIO_INATIVAR", ativo, ct);
    public async Task<bool> ResetarSenhaAsync(long id,CancellationToken ct){ try{ var c=await ColumnsAsync("usuario",ct).ConfigureAwait(false); var set=new List<string>{"senha_hash=@Hash"}; if(c.Contains("deve_alterar_senha")) set.Add("deve_alterar_senha=true"); if(c.Contains("updated_at")) set.Add("updated_at=now()"); using var cn=_connectionFactory.CreateConnection(); var n=await cn.ExecuteAsync(new CommandDefinition($"update sigov.usuario set {string.Join(',',set)} where id=@Id;",new{Id=id,Hash=_passwordHashService.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant())},cancellationToken:ct)).ConfigureAwait(false); if(n>0) await AuditarAsync("USUARIO_RESET_SENHA","sigov.usuario",id,null,new{id},ct).ConfigureAwait(false); return n>0;}catch(Exception ex){_logger.LogError(ex,"Falha ao resetar senha."); return false;} }

    public async Task<IReadOnlyCollection<PerfilListItemViewModel>> ListarPerfisAsync(CancellationToken ct){ try{ if (!await _schemaInspector.TableExistsAsync("sigov", "perfil", ct).ConfigureAwait(false)) return Array.Empty<PerfilListItemViewModel>(); using var cn=_connectionFactory.CreateConnection(); return (await cn.QueryAsync<PerfilListItemViewModel>(new CommandDefinition("select id,codigo,nome,coalesce(descricao,'') as Descricao,ativo from sigov.perfil where coalesce(is_deleted,false)=false order by nome limit 100;", cancellationToken:ct)).ConfigureAwait(false)).ToArray(); }catch(Exception ex){_logger.LogWarning(ex,"Perfis indisponíveis; exibindo limitação honesta."); return Array.Empty<PerfilListItemViewModel>();}}
    public async Task<PerfilDetalheViewModel?> ObterPerfilAsync(long id, CancellationToken ct){ try{ if (!await _schemaInspector.TableExistsAsync("sigov", "perfil", ct).ConfigureAwait(false)) return null; using var cn=_connectionFactory.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<PerfilDetalheViewModel>(new CommandDefinition("select id,codigo,nome,coalesce(descricao,'') as Descricao,ativo from sigov.perfil where id=@Id and coalesce(is_deleted,false)=false;", new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);}catch(Exception ex){ _logger.LogError(ex,"Falha ao obter perfil {Id}.", id); return null; }}
    public async Task<bool> CriarPerfilAsync(PerfilFormViewModel form,CancellationToken ct){ try{ if (!await _schemaInspector.TableExistsAsync("sigov", "perfil", ct).ConfigureAwait(false)) return false; using var cn=_connectionFactory.CreateConnection(); var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.perfil(codigo,nome,descricao,ativo,created_at) values(@Codigo,@Nome,@Descricao,true,now()) returning id;", form,cancellationToken:ct)).ConfigureAwait(false); await AuditarAsync("PERFIL_CRIAR","sigov.perfil",id,null,form,ct).ConfigureAwait(false); return true;}catch(Exception ex){_logger.LogError(ex,"Falha ao criar perfil."); return false;}}
    public async Task<bool> AtualizarPerfilAsync(long id, PerfilFormViewModel form, CancellationToken ct){ try{ if (!await _schemaInspector.TableExistsAsync("sigov", "perfil", ct).ConfigureAwait(false)) return false; using var cn=_connectionFactory.CreateConnection(); var n = await cn.ExecuteAsync(new CommandDefinition("update sigov.perfil set codigo=@Codigo,nome=@Nome,descricao=@Descricao,updated_at=now() where id=@Id and coalesce(is_deleted,false)=false;", new { Id=id, form.Codigo, form.Nome, form.Descricao }, cancellationToken: ct)).ConfigureAwait(false); if (n > 0) await AuditarAsync("PERFIL_EDITAR","sigov.perfil",id,null,form,ct).ConfigureAwait(false); return n > 0;}catch(Exception ex){_logger.LogError(ex,"Falha ao editar perfil {Id}.", id); return false;}}
    public async Task<bool> AlterarStatusPerfilAsync(long id, bool ativo, CancellationToken ct){ try{ if (!await _schemaInspector.TableExistsAsync("sigov", "perfil", ct).ConfigureAwait(false)) return false; using var cn=_connectionFactory.CreateConnection(); var n = await cn.ExecuteAsync(new CommandDefinition("update sigov.perfil set ativo=@Ativo,updated_at=now() where id=@Id and coalesce(is_deleted,false)=false;", new { Id=id, Ativo=ativo }, cancellationToken: ct)).ConfigureAwait(false); if (n > 0) await AuditarAsync(ativo ? "PERFIL_ATIVAR" : "PERFIL_INATIVAR","sigov.perfil", id,null,new { id, ativo }, ct).ConfigureAwait(false); return n > 0;}catch(Exception ex){_logger.LogError(ex,"Falha ao alterar status do perfil {Id}.", id); return false;}}

    public async Task<PerfilPermissoesViewModel> ObterPermissoesPerfilAsync(long perfilId, CancellationToken ct)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov","perfil",ct).ConfigureAwait(false) || !await _schemaInspector.TableExistsAsync("sigov","permissao",ct).ConfigureAwait(false) || !await _schemaInspector.TableExistsAsync("sigov","perfil_permissao",ct).ConfigureAwait(false))
            return new PerfilPermissoesViewModel{PerfilId=perfilId, MensagemFallback="Estrutura sigov.perfil/permissao/perfil_permissao indisponível; permissões não serão simuladas."};
        using var cn=_connectionFactory.CreateConnection();
        var nome=await cn.ExecuteScalarAsync<string>(new CommandDefinition("select nome from sigov.perfil where id=@Id",new{Id=perfilId},cancellationToken:ct)).ConfigureAwait(false) ?? $"Perfil {perfilId}";
        var pc = await ColumnsAsync("permissao", ct).ConfigureAwait(false);
        var modulo = Expr(pc, "modulo", "coalesce(p.modulo,'Geral')", "'Geral'");
        var recurso = FirstExpr(pc, "p", "recurso", "chave", "codigo", "nome");
        var acao = Expr(pc, "acao", "coalesce(p.acao,'Visualizar')", "'Visualizar'");
        var chave = FirstExpr(pc, "p", "chave", "codigo", "nome");
        var rows=await cn.QueryAsync<PermissaoItemViewModel>(new CommandDefinition($"select p.id, {modulo} as Modulo, {recurso} as Recurso, {acao} as Acao, {chave} as Chave, (pp.permissao_id is not null) as Selecionada from sigov.permissao p left join sigov.perfil_permissao pp on pp.permissao_id=p.id and pp.perfil_id=@Id order by 2,3,4;",new{Id=perfilId},cancellationToken:ct)).ConfigureAwait(false);
        return new PerfilPermissoesViewModel{PerfilId=perfilId, PerfilNome=nome, Permissoes=rows.ToArray()};
    }

    public async Task<bool> SalvarPermissoesPerfilAsync(long perfilId, IEnumerable<long> permissaoIds, CancellationToken ct)
    {
        try
        {
            if (!await _schemaInspector.TableExistsAsync("sigov","perfil_permissao",ct).ConfigureAwait(false)) return false;
            var ids=permissaoIds.Distinct().ToArray(); using var cn=_connectionFactory.CreateConnection(); cn.Open(); using var tx=cn.BeginTransaction();
            var antes=(await cn.QueryAsync<long>(new CommandDefinition("select permissao_id from sigov.perfil_permissao where perfil_id=@PerfilId",new{PerfilId=perfilId},transaction:tx,cancellationToken:ct)).ConfigureAwait(false)).ToArray();
            await cn.ExecuteAsync(new CommandDefinition("delete from sigov.perfil_permissao where perfil_id=@PerfilId",new{PerfilId=perfilId},transaction:tx,cancellationToken:ct)).ConfigureAwait(false);
            foreach(var pid in ids) await cn.ExecuteAsync(new CommandDefinition("insert into sigov.perfil_permissao(perfil_id,permissao_id) values(@PerfilId,@Pid)",new{PerfilId=perfilId,Pid=pid},transaction:tx,cancellationToken:ct)).ConfigureAwait(false);
            tx.Commit(); await AuditarAsync("PERMISSOES_SALVAR","sigov.perfil_permissao",perfilId,new{permissoes=antes},new{permissoes=ids},ct).ConfigureAwait(false); return true;
        } catch(Exception ex){ _logger.LogError(ex,"Falha ao salvar permissões do perfil {PerfilId}.",perfilId); return false; }
    }

    public Task<bool> SalvarPermissoesAsync(CancellationToken ct) => Task.FromResult(false);

    private async Task<bool> ExecutarUsuarioAsync(long id,string acao,bool ativo,CancellationToken ct){ try{ var c=await ColumnsAsync("usuario",ct).ConfigureAwait(false); var set=new List<string>{"ativo=@Ativo"}; if(c.Contains("updated_at")) set.Add("updated_at=now()"); using var cn=_connectionFactory.CreateConnection(); var n=await cn.ExecuteAsync(new CommandDefinition($"update sigov.usuario set {string.Join(',',set)} where id=@Id;",new{Id=id,Ativo=ativo},cancellationToken:ct)).ConfigureAwait(false); if(n>0) await AuditarAsync(acao,"sigov.usuario",id,null,new{id,ativo},ct).ConfigureAwait(false); return n>0;}catch(Exception ex){_logger.LogError(ex,"Falha em ação crítica {Acao}.",acao); return false;}}
    private async Task<HashSet<string>> ColumnsAsync(string table, CancellationToken ct) => new((await _schemaInspector.GetColumnsAsync("sigov", table, ct).ConfigureAwait(false)).Select(x => x.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
    private static string Expr(HashSet<string> c,string col,string yes,string no)=>c.Contains(col)?yes:no;
    private static string FirstExpr(HashSet<string> c, string alias, params string[] cols)
    {
        var existing = cols.Where(c.Contains).Select(x => $"{alias}.{x}").ToArray();
        return existing.Length == 0 ? "''" : $"coalesce({string.Join(',', existing)})";
    }
    private static object SafePayload(UsuarioFormViewModel f)=>new{f.Id,f.Login,Email=MaskEmail(f.Email),f.PessoaId,f.Ativo,f.Bloqueado,f.DeveAlterarSenha,f.MfaHabilitado};
    private async Task AuditarAsync(string acao,string entidade,long id,object? antes,object? depois,CancellationToken ct){ try{ var h=_httpContextAccessor.HttpContext; long? uid=long.TryParse(h?.User.FindFirstValue(ClaimTypes.NameIdentifier),out var parsed)?parsed:null; await _auditTrail.RegistrarAsync(null,uid,acao,entidade,id.ToString(),antes,depois,h?.Connection.RemoteIpAddress?.ToString(),h?.Request.Headers.UserAgent.ToString(),h?.TraceIdentifier ?? Guid.NewGuid().ToString(),ct).ConfigureAwait(false);}catch(Exception ex){_logger.LogWarning(ex,"Auditoria best-effort falhou para {Acao}.",acao);} }
    private static string MaskEmail(string value) => LgpdMaskingHelper.MaskEmail(value);
    private sealed record UsuarioRow(long Id,string Nome,string Login,string Email,string Perfil,bool Ativo,bool Bloqueado);
}
