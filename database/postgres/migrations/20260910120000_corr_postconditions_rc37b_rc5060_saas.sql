-- Correção forward-only das pós-condições históricas RC37B/RC50.60/SaaS.
-- Preserva os contratos UUID renomeados, dados comerciais e o ledger publicado.

-- Templates de perfil são globais; toda atribuição efetiva permanece contextualizada
-- pelas colunas tenant_id de perfil_permissao, grupo_perfil e usuario_grupo.
alter table if exists sigov.perfil_acesso alter column tenant_id drop not null;

insert into sigov.permissao (modulo, chave, descricao)
select 'saude', 'saude.visita.registrar', 'Registrar visita'
where not exists (select 1 from sigov.permissao where chave='saude.visita.registrar');

alter table sigov.modulo_saas
    add column if not exists preco_mensal numeric(18,2),
    add column if not exists preco_anual numeric(18,2),
    add column if not exists taxa_implantacao numeric(18,2),
    add column if not exists moeda char(3) not null default 'BRL',
    add column if not exists limite_padrao_usuarios integer,
    add column if not exists limite_armazenamento_mb bigint,
    add column if not exists limite_requisicoes_mensais bigint,
    add column if not exists periodo_trial_dias integer not null default 0,
    add column if not exists recursos_incluidos jsonb not null default '[]'::jsonb,
    add column if not exists dependencias jsonb not null default '[]'::jsonb,
    add column if not exists incompatibilidades jsonb not null default '[]'::jsonb,
    add column if not exists status_comercial varchar(24) not null default 'ATIVO',
    add column if not exists versao varchar(30) not null default '1.0',
    add column if not exists disponivel_contratacao boolean not null default true,
    add column if not exists publico_alvo text;

do $$
begin
    if not exists (select 1 from pg_constraint where conrelid='sigov.modulo_saas'::regclass and conname='ck_modulo_saas_precos') then
        alter table sigov.modulo_saas add constraint ck_modulo_saas_precos
            check (coalesce(preco_mensal,0)>=0 and coalesce(preco_anual,0)>=0 and coalesce(taxa_implantacao,0)>=0) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid='sigov.modulo_saas'::regclass and conname='ck_modulo_saas_limites') then
        alter table sigov.modulo_saas add constraint ck_modulo_saas_limites
            check (coalesce(limite_padrao_usuarios,0)>=0 and coalesce(limite_armazenamento_mb,0)>=0 and coalesce(limite_requisicoes_mensais,0)>=0 and periodo_trial_dias>=0) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid='sigov.modulo_saas'::regclass and conname='ck_modulo_saas_status_comercial') then
        alter table sigov.modulo_saas add constraint ck_modulo_saas_status_comercial
            check (status_comercial in ('ATIVO','INATIVO','EM_BREVE','DESCONTINUADO')) not valid;
    end if;
end $$;

alter table sigov.tenant_modulo_contratado
    add column if not exists plano_codigo varchar(80),
    add column if not exists ciclo_cobranca varchar(12) not null default 'MENSAL',
    add column if not exists valor_tabela numeric(18,2),
    add column if not exists valor_contratado numeric(18,2),
    add column if not exists desconto_percentual numeric(7,4) not null default 0,
    add column if not exists moeda char(3) not null default 'BRL',
    add column if not exists trial_inicio date,
    add column if not exists trial_fim date,
    add column if not exists limites_contratados jsonb not null default '{}'::jsonb,
    add column if not exists recursos_adicionais jsonb not null default '[]'::jsonb,
    add column if not exists renovacao_automatica boolean not null default true,
    add column if not exists cancelamento_agendado_para date,
    add column if not exists motivo_status text,
    add column if not exists usuario_responsavel_id bigint;

do $$
begin
    if exists (select 1 from pg_constraint where conrelid='sigov.tenant_modulo_contratado'::regclass and conname='ck_tenant_modulo_contratado_status') then
        alter table sigov.tenant_modulo_contratado drop constraint ck_tenant_modulo_contratado_status;
    end if;
    if not exists (select 1 from pg_constraint where conrelid='sigov.tenant_modulo_contratado'::regclass and conname='ck_tenant_modulo_contratado_status_v2') then
        alter table sigov.tenant_modulo_contratado add constraint ck_tenant_modulo_contratado_status_v2
            check (status in ('DISPONIVEL','TRIAL','EM_IMPLANTACAO','CONTRATADO','HABILITADO','ATIVO','BETA','SUSPENSO','INADIMPLENTE','CANCELADO','EXPIRADO')) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid='sigov.tenant_modulo_contratado'::regclass and conname='ck_tenant_modulo_contratado_valores') then
        alter table sigov.tenant_modulo_contratado add constraint ck_tenant_modulo_contratado_valores
            check (coalesce(valor_tabela,0)>=0 and coalesce(valor_contratado,0)>=0 and desconto_percentual between 0 and 100) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid='sigov.tenant_modulo_contratado'::regclass and conname='ck_tenant_modulo_contratado_datas') then
        alter table sigov.tenant_modulo_contratado add constraint ck_tenant_modulo_contratado_datas
            check ((vigencia_fim is null or vigencia_inicio is null or vigencia_fim>=vigencia_inicio)
               and (trial_fim is null or trial_inicio is null or trial_fim>=trial_inicio)) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid='sigov.tenant_modulo_contratado'::regclass and conname='fk_tenant_modulo_contratado_responsavel') then
        alter table sigov.tenant_modulo_contratado add constraint fk_tenant_modulo_contratado_responsavel
            foreign key (usuario_responsavel_id) references sigov.usuario(id) not valid;
    end if;
end $$;

create index if not exists ix_tenant_modulo_contratado_vigencia
    on sigov.tenant_modulo_contratado(tenant_id,status,vigencia_inicio,vigencia_fim) where ativo;
create index if not exists ix_tenant_modulo_contratado_renovacao
    on sigov.tenant_modulo_contratado(cancelamento_agendado_para) where ativo and cancelamento_agendado_para is not null;

create table if not exists sigov.tenant_modulo_contratado_historico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    tenant_modulo_contratado_id bigint not null references sigov.tenant_modulo_contratado(id),
    modulo_codigo varchar(80) not null,
    operacao varchar(12) not null,
    status_anterior varchar(40),
    status_novo varchar(40) not null,
    dados_anteriores jsonb,
    dados_novos jsonb not null,
    usuario_id bigint,
    correlation_id uuid,
    ocorrido_at timestamptz not null default now(),
    constraint ck_tenant_modulo_historico_operacao check (operacao in ('CRIACAO','ALTERACAO'))
);
create index if not exists ix_tenant_modulo_historico_tenant_data
    on sigov.tenant_modulo_contratado_historico(tenant_id,ocorrido_at desc);

create or replace function sigov.fn_tenant_modulo_contrato_auditar() returns trigger language plpgsql as $$
begin
    insert into sigov.tenant_modulo_contratado_historico
        (tenant_id,tenant_modulo_contratado_id,modulo_codigo,operacao,status_anterior,status_novo,dados_anteriores,dados_novos,usuario_id,correlation_id)
    values
        (new.tenant_id,new.id,new.modulo_codigo,case when tg_op='INSERT' then 'CRIACAO' else 'ALTERACAO' end,
         case when tg_op='UPDATE' then old.status end,new.status,case when tg_op='UPDATE' then to_jsonb(old) end,to_jsonb(new),
         coalesce(new.usuario_responsavel_id,new.updated_by,new.created_by),new.correlation_id);
    return new;
end $$;

do $$
begin
    if not exists (select 1 from pg_trigger where tgrelid='sigov.tenant_modulo_contratado'::regclass and tgname='trg_tenant_modulo_contrato_auditar' and not tgisinternal) then
        create trigger trg_tenant_modulo_contrato_auditar after insert or update on sigov.tenant_modulo_contratado
            for each row execute function sigov.fn_tenant_modulo_contrato_auditar();
    end if;
end $$;

create or replace function sigov.fn_tenant_modulo_historico_imutavel() returns trigger language plpgsql as $$
begin
    raise exception 'Histórico de contratação de módulo é imutável';
end $$;

do $$
begin
    if not exists (select 1 from pg_trigger where tgrelid='sigov.tenant_modulo_contratado_historico'::regclass and tgname='trg_tenant_modulo_historico_imutavel' and not tgisinternal) then
        create trigger trg_tenant_modulo_historico_imutavel before update or delete on sigov.tenant_modulo_contratado_historico
            for each row execute function sigov.fn_tenant_modulo_historico_imutavel();
    end if;
end $$;

-- Compatibilidade unidirecional: toda mudança na autoridade atualiza a estrutura legada.
create or replace function sigov.fn_tenant_modulo_compatibilizar() returns trigger language plpgsql as $$
declare v_modulo_id bigint;
begin
    select id into v_modulo_id from sigov.modulo_saas where codigo=new.modulo_codigo and ativo and not is_deleted limit 1;
    if v_modulo_id is null then return new; end if;
    insert into sigov.tenant_modulo(tenant_id,modulo_saas_id,habilitado,contratado,inicio_at,fim_at,configuracoes,ativo,created_by,updated_by,correlation_id)
    values(new.tenant_id,v_modulo_id,new.status in ('TRIAL','EM_IMPLANTACAO','CONTRATADO','HABILITADO','ATIVO','BETA'),
           new.status not in ('DISPONIVEL','CANCELADO','EXPIRADO'),coalesce(new.vigencia_inicio,current_date)::timestamptz,
           new.vigencia_fim::timestamptz,new.parametros_json,new.ativo,new.created_by,new.updated_by,new.correlation_id)
    on conflict(tenant_id,modulo_saas_id) do update set
        habilitado=excluded.habilitado,contratado=excluded.contratado,inicio_at=excluded.inicio_at,fim_at=excluded.fim_at,
        configuracoes=excluded.configuracoes,ativo=excluded.ativo,updated_at=now(),updated_by=excluded.updated_by,correlation_id=excluded.correlation_id;
    return new;
end $$;

do $$
begin
    if not exists (select 1 from pg_trigger where tgrelid='sigov.tenant_modulo_contratado'::regclass and tgname='trg_tenant_modulo_compatibilizar' and not tgisinternal) then
        create trigger trg_tenant_modulo_compatibilizar after insert or update on sigov.tenant_modulo_contratado
            for each row execute function sigov.fn_tenant_modulo_compatibilizar();
    end if;
end $$;

drop trigger if exists trg_tenant_modulo_contrato_auditar on sigov.tenant_modulo_contratado;
create trigger trg_tenant_modulo_contrato_auditar after insert or update on sigov.tenant_modulo_contratado
    for each row execute function sigov.fn_tenant_modulo_contrato_auditar();

drop trigger if exists trg_tenant_modulo_compatibilizar on sigov.tenant_modulo_contratado;
create trigger trg_tenant_modulo_compatibilizar after insert or update on sigov.tenant_modulo_contratado
    for each row execute function sigov.fn_tenant_modulo_compatibilizar();
