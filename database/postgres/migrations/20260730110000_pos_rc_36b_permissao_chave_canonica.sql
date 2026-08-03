-- Pós-RC 36B: torna a chave funcional da permissão globalmente canônica.
--
-- A tabela sigov.permissao não possui tenant_id. entidade_id e exercicio_id são
-- metadados opcionais do cadastro, enquanto `chave` é o identificador utilizado
-- pelas policies. Por isso a unicidade correta é global por chave.
--
-- Em bases históricas pode haver a mesma chave em módulos diferentes. O vencedor
-- é escolhido de forma determinística, privilegiando o registro ativo, não
-- excluído e mais completo. Os vínculos de perfil são consolidados antes da
-- remoção dos registros redundantes.

drop table if exists pg_temp.permissao_chave_canonica;
create temporary table permissao_chave_canonica as
select id as canonical_id, chave
from (
    select
        p.id,
        p.chave,
        row_number() over (
            partition by p.chave
            order by
                p.is_deleted asc,
                p.ativo desc,
                ((p.descricao is not null)::int
                 + (p.codigo_externo is not null)::int
                 + (p.observacao is not null)::int
                 + (p.entidade_id is not null)::int
                 + (p.exercicio_id is not null)::int) desc,
                p.updated_at desc nulls last,
                p.created_at desc,
                p.id desc
        ) as position
    from sigov.permissao p
    where nullif(btrim(p.chave), '') is not null
) ranked
where position = 1;

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, created_at)
select pp.perfil_acesso_id, canonical.canonical_id, min(pp.created_at)
from sigov.perfil_permissao pp
join sigov.permissao duplicate on duplicate.id = pp.permissao_id
join permissao_chave_canonica canonical on canonical.chave = duplicate.chave
where pp.permissao_id <> canonical.canonical_id
group by pp.perfil_acesso_id, canonical.canonical_id
on conflict (perfil_acesso_id, permissao_id) do nothing;

delete from sigov.perfil_permissao pp
using sigov.permissao duplicate, permissao_chave_canonica canonical
where pp.permissao_id = duplicate.id
  and canonical.chave = duplicate.chave
  and duplicate.id <> canonical.canonical_id;

delete from sigov.permissao duplicate
using permissao_chave_canonica canonical
where duplicate.chave = canonical.chave
  and duplicate.id <> canonical.canonical_id;

create unique index if not exists permissao_chave_uidx
    on sigov.permissao (chave);

drop table if exists pg_temp.permissao_chave_canonica;
