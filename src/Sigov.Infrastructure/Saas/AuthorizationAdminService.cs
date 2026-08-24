using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.SuperAdmin;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class AuthorizationAdminService(DapperContext context) : IAuthorizationAdminService
{
    public async Task<AuthorizationAdminSnapshot> ListAsync(AuthorizationAdminFilter filter, CancellationToken ct)
    {
        using var connection = context.CreateConnection();
        var p = new { Search = string.IsNullOrWhiteSpace(filter.Search) ? null : $"%{filter.Search.Trim()}%", filter.TenantId, filter.IncludeInactive };
        using var rows = await connection.QueryMultipleAsync(new CommandDefinition(ListSql, p, cancellationToken: ct));
        return new(
            (await rows.ReadAsync<AuthorizationCatalogItem>()).AsList(), (await rows.ReadAsync<AuthorizationCatalogItem>()).AsList(),
            (await rows.ReadAsync<AuthorizationCatalogItem>()).AsList(), (await rows.ReadAsync<AuthorizationCatalogItem>()).AsList(),
            (await rows.ReadAsync<AuthorizationLinkItem>()).AsList(), (await rows.ReadAsync<AuthorizationLinkItem>()).AsList(),
            (await rows.ReadAsync<AuthorizationLinkItem>()).AsList());
    }

    public async Task<AuthorizationAdminResult> SaveCatalogAsync(string kind, AuthorizationCatalogCommand command, long actor, string correlationId, CancellationToken ct)
    {
        kind = kind.Trim().ToLowerInvariant();
        if (kind is not ("perfil" or "grupo" or "permissao")) return Fail("Catálogo inválido.");
        var maximumCodeLength = kind == "permissao" ? 150 : 100;
        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name) || command.Code.Trim().Length > maximumCodeLength || command.Name.Trim().Length > 150)
            return Fail("Código e nome são obrigatórios e devem respeitar os limites do cadastro.");
        if (kind == "permissao" && command.Code.Split('.', StringSplitOptions.RemoveEmptyEntries).Length < 3)
            return Fail("A chave da permissão deve usar modulo.recurso.acao.");
        using var connection = context.CreateConnection(); connection.Open(); using var tx = connection.BeginTransaction();
        var before = command.Id is null ? null : await connection.QuerySingleOrDefaultAsync<object>(new CommandDefinition(
            kind == "perfil" ? "select * from sigov.perfil_acesso where id=@Id" : kind == "grupo" ? "select * from sigov.grupo_acesso where id=@Id" : "select * from sigov.permissao where id=@Id",
            new { command.Id }, tx, cancellationToken: ct));
        var parts = command.Code.Trim().Split('.', 3);
        var sql = kind switch
        {
            "perfil" => command.Id is null ? "insert into sigov.perfil_acesso(codigo_externo,nome,descricao,ativo,created_by) values(@Code,@Name,@Description,@Active,@Actor)" : "update sigov.perfil_acesso set codigo_externo=@Code,nome=@Name,descricao=@Description,ativo=@Active,updated_at=now(),updated_by=@Actor where id=@Id and not is_deleted",
            "grupo" => command.Id is null ? "insert into sigov.grupo_acesso(codigo_externo,nome,descricao,ativo,created_by) values(@Code,@Name,@Description,@Active,@Actor)" : "update sigov.grupo_acesso set codigo_externo=@Code,nome=@Name,descricao=@Description,ativo=@Active,updated_at=now(),updated_by=@Actor where id=@Id and not is_deleted",
            _ => command.Id is null ? "insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,created_by) values(@Module,@Code,@Resource,@Action,@Description,@Active,@Actor)" : "update sigov.permissao set modulo=@Module,chave=@Code,recurso=@Resource,acao=@Action,descricao=@Description,ativo=@Active,updated_at=now(),updated_by=@Actor where id=@Id and not is_deleted"
        };
        var changed = await connection.ExecuteAsync(new CommandDefinition(sql, new { command.Id, Code=command.Code.Trim(), Name=command.Name.Trim(), Description=string.IsNullOrWhiteSpace(command.Description)?command.Name.Trim():command.Description.Trim(), command.Active, Actor=actor, Module=parts[0], Resource=parts.Length>1?parts[1]:parts[0], Action=parts.Length>2?parts[2]:"acessar" }, tx, cancellationToken: ct));
        if (changed != 1) { tx.Rollback(); return Fail("Registro não encontrado ou não alterado."); }
        await Audit(connection, tx, $"{kind}.salvar", kind, command.Id?.ToString(), actor, correlationId, before, command, ct); tx.Commit();
        return Ok($"{char.ToUpperInvariant(kind[0])}{kind[1..]} salvo e auditado.");
    }

    public async Task<AuthorizationAdminResult> SaveLinkAsync(AuthorizationLinkCommand command, long actor, string correlationId, CancellationToken ct)
    {
        var kind = command.Kind.Trim().ToLowerInvariant();
        if (kind is not ("usuario-grupo" or "grupo-perfil" or "perfil-permissao") || command.LeftId <= 0 || command.RightId <= 0) return Fail("Vínculo inválido.");
        if (command.ValidFrom.HasValue && command.ValidTo.HasValue && command.ValidTo <= command.ValidFrom) return Fail("A vigência final deve ser posterior à inicial.");
        var effect = (command.Effect ?? "PERMITIR").Trim().ToUpperInvariant();
        if (kind == "perfil-permissao" && effect is not ("PERMITIR" or "NEGAR")) return Fail("Efeito deve ser PERMITIR ou NEGAR.");
        if (command.ApprovalLimit < 0) return Fail("A alçada não pode ser negativa.");
        using var connection = context.CreateConnection(); connection.Open(); using var tx = connection.BeginTransaction();
        var (table,left,right) = Link(kind);
        var before = await connection.QuerySingleOrDefaultAsync<object>(new CommandDefinition($"select * from sigov.{table} where {left}=@LeftId and {right}=@RightId", command, tx, cancellationToken: ct));
        var extraColumns = kind == "perfil-permissao" ? ",efeito,alcada_valor,justificativa,updated_at" : "";
        var extraValues = kind == "perfil-permissao" ? ",@Effect,@ApprovalLimit,@Justification,now()" : "";
        var extraUpdate = kind == "perfil-permissao" ? ",efeito=excluded.efeito,alcada_valor=excluded.alcada_valor,justificativa=excluded.justificativa,updated_at=now()" : "";
        var sql = $"insert into sigov.{table}({left},{right},tenant_id,entidade_id,exercicio_id,unidade_id,vigencia_inicio,vigencia_fim,ativo,is_deleted,created_by{extraColumns}) values(@LeftId,@RightId,@TenantId,@EntityId,@FiscalYearId,@UnitId,@ValidFrom,@ValidTo,@Active,false,@Actor{extraValues}) on conflict({left},{right}) do update set tenant_id=excluded.tenant_id,entidade_id=excluded.entidade_id,exercicio_id=excluded.exercicio_id,unidade_id=excluded.unidade_id,vigencia_inicio=excluded.vigencia_inicio,vigencia_fim=excluded.vigencia_fim,ativo=excluded.ativo,is_deleted=false{extraUpdate}";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { command.LeftId, command.RightId, command.TenantId, EntityId=command.EntityId, FiscalYearId=command.FiscalYearId, UnitId=command.UnitId, command.ValidFrom, command.ValidTo, command.Active, Effect=effect, command.ApprovalLimit, command.Justification, Actor=actor }, tx, cancellationToken: ct));
        await Audit(connection, tx, $"{kind}.salvar", table, $"{command.LeftId}:{command.RightId}", actor, correlationId, before, command, ct); tx.Commit();
        return Ok("Vínculo contextual salvo e auditado.");
    }

    public async Task<AuthorizationAdminResult> ChangeStatusAsync(string kind, long leftId, long? rightId, bool active, bool delete, long actor, string correlationId, CancellationToken ct)
    {
        kind=kind.Trim().ToLowerInvariant(); if (leftId<=0) return Fail("Identificador inválido.");
        using var connection=context.CreateConnection(); connection.Open(); using var tx=connection.BeginTransaction();
        string table, where;
        if (kind is "perfil" or "grupo" or "permissao") { table=kind=="perfil"?"perfil_acesso":kind=="grupo"?"grupo_acesso":"permissao"; where="id=@LeftId"; }
        else { if (!rightId.HasValue) return Fail("Identificador do vínculo obrigatório."); var link=Link(kind); table=link.Table; where=$"{link.Left}=@LeftId and {link.Right}=@RightId"; }
        var before=await connection.QuerySingleOrDefaultAsync<object>(new CommandDefinition($"select * from sigov.{table} where {where}",new{LeftId=leftId,RightId=rightId},tx,cancellationToken:ct));
        if(before is null) return Fail("Registro não encontrado.");
        var sql=$"update sigov.{table} set ativo=@Active,is_deleted=@Delete"+(kind is "perfil" or "grupo" or "permissao"?",updated_at=now(),updated_by=@Actor,deleted_at=case when @Delete then now() else null end,deleted_by=case when @Delete then @Actor else null end":"")+$" where {where}";
        await connection.ExecuteAsync(new CommandDefinition(sql,new{LeftId=leftId,RightId=rightId,Active=active&&!delete,Delete=delete,Actor=actor},tx,cancellationToken:ct));
        await Audit(connection,tx,$"{kind}.{(delete?"excluir":active?"ativar":"inativar")}",table,$"{leftId}:{rightId}",actor,correlationId,before,new{active,delete},ct); tx.Commit(); return Ok(delete?"Exclusão lógica concluída.":active?"Registro ativado.":"Registro inativado.");
    }

    private static (string Table,string Left,string Right) Link(string kind)=>kind switch { "usuario-grupo"=>("usuario_grupo","usuario_id","grupo_acesso_id"), "grupo-perfil"=>("grupo_perfil","grupo_acesso_id","perfil_acesso_id"), "perfil-permissao"=>("perfil_permissao","perfil_acesso_id","permissao_id"), _=>throw new ArgumentException("Tipo de vínculo inválido.",nameof(kind)) };
    private static Task Audit(System.Data.IDbConnection c,System.Data.IDbTransaction tx,string operation,string entity,string? key,long actor,string correlation,object? before,object after,CancellationToken ct)=>c.ExecuteAsync(new CommandDefinition("insert into sigov.autorizacao_admin_auditoria(operacao,entidade,registro_chave,usuario_id,correlation_id,antes,depois) values(@Operation,@Entity,@Key,@Actor,@Correlation,cast(@Before as jsonb),cast(@After as jsonb))",new{Operation=operation,Entity=entity,Key=key,Actor=actor,Correlation=correlation,Before=before is null?null:JsonSerializer.Serialize(before),After=JsonSerializer.Serialize(after)},tx,cancellationToken:ct));
    private static AuthorizationAdminResult Ok(string message)=>new(true,message); private static AuthorizationAdminResult Fail(string message)=>new(false,message);

    private const string ListSql = """
select id,coalesce(codigo_externo,id::text) code,nome name,descricao description,ativo active from sigov.perfil_acesso where not is_deleted and (@IncludeInactive or ativo) and (@Search is null or nome ilike @Search or codigo_externo ilike @Search) order by nome limit 500;
select id,coalesce(codigo_externo,id::text) code,nome name,descricao description,ativo active from sigov.grupo_acesso where not is_deleted and (@IncludeInactive or ativo) and (@Search is null or nome ilike @Search or codigo_externo ilike @Search) order by nome limit 500;
select id,chave code,coalesce(descricao,chave) name,descricao description,ativo active from sigov.permissao where not is_deleted and (@IncludeInactive or ativo) and (@Search is null or chave ilike @Search or descricao ilike @Search) order by modulo,recurso,acao limit 1000;
select u.id,u.login code,coalesce(pe.nome,u.login) name,null::text description,u.ativo active
  from sigov.usuario u left join sigov.pessoa pe on pe.id=u.pessoa_id and not pe.is_deleted
 where not u.is_deleted and (@IncludeInactive or u.ativo)
   and (@TenantId is null or exists (select 1 from sigov.usuario_grupo ug where ug.usuario_id=u.id and ug.tenant_id=@TenantId and not ug.is_deleted))
   and (@Search is null or u.login ilike @Search or pe.nome ilike @Search)
 order by coalesce(pe.nome,u.login) limit 500;
select 'usuario-grupo' kind,ug.usuario_id leftid,coalesce(pe.nome,u.login) leftname,ug.grupo_acesso_id rightid,g.nome rightname,ug.tenant_id tenantid,ug.entidade_id entityid,ug.exercicio_id fiscalyearid,ug.unidade_id unitid,ug.vigencia_inicio validfrom,ug.vigencia_fim validto,null effect,null approvallimit,ug.ativo active,ug.is_deleted deleted from sigov.usuario_grupo ug join sigov.usuario u on u.id=ug.usuario_id left join sigov.pessoa pe on pe.id=u.pessoa_id and not pe.is_deleted join sigov.grupo_acesso g on g.id=ug.grupo_acesso_id where (@IncludeInactive or (ug.ativo and not ug.is_deleted)) and (@TenantId is null or ug.tenant_id is null or ug.tenant_id=@TenantId) and (@Search is null or u.login ilike @Search or pe.nome ilike @Search or g.nome ilike @Search) order by coalesce(pe.nome,u.login),g.nome limit 1000;
select 'grupo-perfil' kind,gp.grupo_acesso_id leftid,g.nome leftname,gp.perfil_acesso_id rightid,p.nome rightname,gp.tenant_id tenantid,gp.entidade_id entityid,gp.exercicio_id fiscalyearid,gp.unidade_id unitid,gp.vigencia_inicio validfrom,gp.vigencia_fim validto,null effect,null approvallimit,gp.ativo active,gp.is_deleted deleted from sigov.grupo_perfil gp join sigov.grupo_acesso g on g.id=gp.grupo_acesso_id join sigov.perfil_acesso p on p.id=gp.perfil_acesso_id where (@IncludeInactive or (gp.ativo and not gp.is_deleted)) and (@TenantId is null or gp.tenant_id is null or gp.tenant_id=@TenantId) order by g.nome,p.nome limit 1000;
select 'perfil-permissao' kind,pp.perfil_acesso_id leftid,pa.nome leftname,pp.permissao_id rightid,p.chave rightname,pp.tenant_id tenantid,pp.entidade_id entityid,pp.exercicio_id fiscalyearid,pp.unidade_id unitid,pp.vigencia_inicio validfrom,pp.vigencia_fim validto,pp.efeito effect,pp.alcada_valor approvallimit,pp.ativo active,pp.is_deleted deleted from sigov.perfil_permissao pp join sigov.perfil_acesso pa on pa.id=pp.perfil_acesso_id join sigov.permissao p on p.id=pp.permissao_id where (@IncludeInactive or (pp.ativo and not pp.is_deleted)) and (@TenantId is null or pp.tenant_id is null or pp.tenant_id=@TenantId) order by pa.nome,p.chave limit 2000;
""";
}
