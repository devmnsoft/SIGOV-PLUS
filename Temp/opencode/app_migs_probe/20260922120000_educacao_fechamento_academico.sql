-- Fechamento acadêmico versionado: prévia/concorrência, snapshot imutável e reabertura auditada.
set search_path to sigov;

alter table sigov.educacao_diario_fechamento
    add column if not exists versao integer,
    add column if not exists token_conferencia varchar(64),
    add column if not exists retifica_fechamento_id bigint references sigov.educacao_diario_fechamento(id);

-- Numera uma única vez o histórico legado, na ordem em que foi efetivamente criado.
with legado as (
  select id, row_number() over(partition by tenant_id,diario_id order by created_at,id)::integer as versao_calculada
  from sigov.educacao_diario_fechamento where versao is null
)
update sigov.educacao_diario_fechamento f set versao=legado.versao_calculada from legado where legado.id=f.id;
alter table sigov.educacao_diario_fechamento alter column versao set default 1;
alter table sigov.educacao_diario_fechamento alter column versao set not null;

-- Interrompe a migration se dados já versionados forem incompatíveis.
do $$
begin
  if exists (
    select 1 from sigov.educacao_diario_fechamento
    group by tenant_id, diario_id, versao having count(*) > 1
  ) then
    raise exception 'Fechamentos acadêmicos possuem versões duplicadas; saneamento explícito é obrigatório.';
  end if;
end $$;

create unique index if not exists ux_educacao_diario_fechamento_versao
    on sigov.educacao_diario_fechamento(tenant_id, diario_id, versao);
create index if not exists ix_educacao_diario_fechamento_retificacao
    on sigov.educacao_diario_fechamento(tenant_id, retifica_fechamento_id)
    where retifica_fechamento_id is not null;

insert into sigov.permissao(modulo,recurso,acao,chave,descricao,ativo,is_deleted)
values
 ('educacao','diario','conferir','educacao.diario.conferir','Conferir lançamentos e prévia do período',true,false),
 ('educacao','diario','fechar','educacao.diario.fechar','Confirmar fechamento acadêmico do período',true,false),
 ('educacao','diario','reabrir','educacao.diario.reabrir','Reabrir período acadêmico com justificativa',true,false),
 ('educacao','boletim','emitir','educacao.boletim.emitir','Emitir boletim vinculado ao fechamento',true,false)
on conflict(modulo,chave) do update set recurso=excluded.recurso,acao=excluded.acao,descricao=excluded.descricao,ativo=true,is_deleted=false;
