-- RC-SAAS-AUT Etapa B - Corretiva: normalizacao canonica de modulo/recurso/acao das chaves
-- saas.superadmin.* (dashboard.visualizar, dashboard.exportar, dashboard.administrar e
-- autorizacao.administrar) e da chave saas.plataforma.administrar.
-- Root cause: reexecucao parcial do script completo consolidado aplicou a regra de derivacao
-- %admin% da migration 013 (recurso = split_part(chave,'.',1); acao = split_part(chave,'.',2))
-- e parou antes das secoes de normalizacao da migration 01, deixando as linhas com valores
-- grossos ('saas','saas','<parte>'), incompativeis com os tuplos canonicos do avaliador
-- persistente ('saas','saas.superadmin.dashboard'|'saas.superadmin.autorizacao'|'plataforma',acao),
-- o que quebra as pos-condicoes 20261005090000 (01) e 20261005120000 (04).
-- Regra 8: correcao em migration nova e idempotente; migrations publicadas nao sao alteradas.

update sigov.permissao
   set modulo = 'saas',
       recurso = 'plataforma',
       acao = 'administrar',
       updated_at = now()
 where modulo = 'saas' and chave = 'saas.plataforma.administrar';

update sigov.permissao
   set modulo = 'saas',
       recurso = 'saas.superadmin.dashboard',
       acao = 'visualizar',
       updated_at = now()
 where modulo = 'saas' and chave = 'saas.superadmin.dashboard.visualizar';

update sigov.permissao
   set modulo = 'saas',
       recurso = 'saas.superadmin.dashboard',
       acao = 'exportar',
       updated_at = now()
 where modulo = 'saas' and chave = 'saas.superadmin.dashboard.exportar';

update sigov.permissao
   set modulo = 'saas',
       recurso = 'saas.superadmin.autorizacao',
       acao = 'administrar',
       updated_at = now()
 where modulo = 'saas' and chave = 'saas.superadmin.autorizacao.administrar';

update sigov.permissao
   set modulo = 'saas',
       recurso = 'saas.superadmin.dashboard',
       acao = 'administrar',
       updated_at = now()
 where modulo = 'saas' and chave = 'saas.superadmin.dashboard.administrar';

do $$
declare
    v_ok integer;
begin
    select count(*) into v_ok
    from sigov.permissao
    where modulo = 'saas' and ativo and not is_deleted and (
        (chave = 'saas.plataforma.administrar' and recurso = 'plataforma' and acao = 'administrar')
     or (chave = 'saas.superadmin.dashboard.visualizar' and recurso = 'saas.superadmin.dashboard' and acao = 'visualizar')
     or (chave = 'saas.superadmin.dashboard.exportar' and recurso = 'saas.superadmin.dashboard' and acao = 'exportar')
     or (chave = 'saas.superadmin.autorizacao.administrar' and recurso = 'saas.superadmin.autorizacao' and acao = 'administrar')
     or (chave = 'saas.superadmin.dashboard.administrar' and recurso = 'saas.superadmin.dashboard' and acao = 'administrar')
    );
    if v_ok <> 5 then
        raise exception 'RC-SAAS-AUT normalizacao superadmin/plataforma: apenas % de 5 chaves canonicas ativas', v_ok;
    end if;
end $$;
