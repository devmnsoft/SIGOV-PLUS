-- Correção aditiva das lacunas encontradas pela validação final das migrations.
-- Não altera migrations publicadas nem o histórico de execução.

-- A FUNC04 define manutenção e ordem de serviço como objetos canônicos de
-- Frotas. Algumas bases importaram o histórico da FUNC04 sem essas relações.
-- A dependência é recomposta na ordem correta e as FKs opcionais de Compras só
-- são criadas quando o tipo legado bigint ainda é compatível.
create table if not exists sigov.frotas_manutencao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint not null,
    veiculo_id bigint not null references sigov.frotas_veiculo(id),
    tipo varchar(15) not null,
    data_abertura timestamptz not null,
    data_conclusao timestamptz,
    km_abertura numeric(18,2) not null,
    fornecedor_id bigint,
    contrato_id bigint,
    descricao text not null,
    valor_estimado numeric(18,2) not null default 0,
    valor_final numeric(18,2),
    status varchar(15) not null default 'ABERTA',
    observacao text,
    created_at timestamptz not null default now(),
    created_by bigint,
    updated_at timestamptz not null default now(),
    updated_by bigint,
    constraint ck_frotas_manut_tipo check (tipo in ('PREVENTIVA','CORRETIVA','REVISAO','PNEU','LAVAGEM','OUTRA')),
    constraint ck_frotas_manut_status check (status in ('ABERTA','EM_EXECUCAO','CONCLUIDA','CANCELADA')),
    constraint ck_frotas_manut_valores check (km_abertura >= 0 and valor_estimado >= 0 and (valor_final is null or valor_final >= 0)),
    constraint ck_frotas_manut_datas check (data_conclusao is null or data_conclusao >= data_abertura)
);

create index if not exists ix_frotas_manutencao
    on sigov.frotas_manutencao (tenant_id, entidade_id, veiculo_id, status, data_abertura desc);

create table if not exists sigov.frotas_ordem_servico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint not null,
    exercicio integer not null,
    numero varchar(80) not null,
    veiculo_id bigint not null references sigov.frotas_veiculo(id),
    manutencao_id bigint references sigov.frotas_manutencao(id),
    fornecedor_id bigint,
    data_abertura timestamptz not null,
    previsao_conclusao date,
    descricao text not null,
    status varchar(15) not null default 'ABERTA',
    created_at timestamptz not null default now(),
    created_by bigint,
    updated_at timestamptz not null default now(),
    updated_by bigint,
    constraint ck_frotas_os_status check (status in ('ABERTA','APROVADA','EM_EXECUCAO','CONCLUIDA','CANCELADA')),
    constraint ck_frotas_os_previsao check (previsao_conclusao is null or previsao_conclusao >= data_abertura::date)
);

create unique index if not exists ux_frotas_os_numero
    on sigov.frotas_ordem_servico (tenant_id, entidade_id, exercicio, numero);
create index if not exists ix_frotas_os
    on sigov.frotas_ordem_servico (tenant_id, entidade_id, veiculo_id, status, data_abertura desc);

do $$
declare
    origem text;
    destino text;
begin
    if to_regclass('sigov.compras_fornecedor') is not null then
        select a.atttypid::regtype::text into destino
          from pg_attribute a
         where a.attrelid=to_regclass('sigov.compras_fornecedor')
           and a.attname='id' and not a.attisdropped;

        foreach origem in array array['frotas_manutencao','frotas_ordem_servico'] loop
            if destino = 'bigint' and not exists (
                select 1 from pg_constraint c
                 where c.conrelid=to_regclass('sigov.' || origem)
                   and c.conname='fk_' || origem || '_fornecedor'
            ) then
                execute format(
                    'alter table sigov.%I add constraint %I foreign key (fornecedor_id) references sigov.compras_fornecedor(id)',
                    origem, 'fk_' || origem || '_fornecedor');
            end if;
        end loop;
    end if;
end $$;

do $$
begin
    if to_regclass('sigov.compras_licitapro_alerta') is null
       or not exists (
           select 1 from information_schema.columns
            where table_schema='sigov' and table_name='compras_licitapro_alerta'
              and column_name in ('tenant_id','entidade_id','status','vencimento_at')
            group by table_schema, table_name having count(*)=4
       ) then
        raise exception 'Schema LicitaPro incompleto: compras_licitapro_alerta ou colunas canônicas ausentes';
    end if;

    create index if not exists ix_clp_alerta_tenant_status_vencimento
        on sigov.compras_licitapro_alerta (tenant_id, entidade_id, status, vencimento_at);

    if to_regclass('sigov.compras_licitapro_fonte') is null
       or not exists (
           select 1 from information_schema.columns
            where table_schema='sigov' and table_name='compras_licitapro_fonte'
              and column_name in ('configurada','endpoint_url')
            group by table_schema, table_name having count(*)=2
       ) then
        raise exception 'Schema LicitaPro incompleto: compras_licitapro_fonte ou colunas canônicas ausentes';
    end if;

    if not exists (
        select 1
          from pg_constraint c
         where c.conrelid = to_regclass('sigov.compras_licitapro_fonte')
           and c.conname = 'ck_clp_fonte_endpoint_url'
    ) then
        alter table sigov.compras_licitapro_fonte
            add constraint ck_clp_fonte_endpoint_url
            check (not configurada or endpoint_url ~* '^https?://[^[:space:]]+$') not valid;
    end if;
end $$;

-- permissao possui PK identity e unicidade histórica em (modulo, chave). A
-- chave é, porém, o identificador canônico de autorização; por isso a carga
-- trata correspondências sem diferença de caixa antes de inserir.
with requeridas(modulo, chave, recurso, acao, descricao) as (values
    ('ativos',     'ATIVOS_DASHBOARD_VIEW',          'dashboard',  'visualizar',  'Visualizar dashboard de ativos'),
    ('cidadao360', 'CIDADAO_PORTAL_VIEW',             'portal',     'visualizar',  'Visualizar portal do cidadão'),
    ('sst360',     'SST_DASHBOARD_VIEW',               'dashboard',  'visualizar',  'Visualizar dashboard SST'),
    ('saude',      'ACS_DASHBOARD_VIEW',               'acs.dashboard', 'visualizar', 'Consultar ACS360'),
    ('ged360',     'GED_DASHBOARD_VIEW',               'dashboard',  'visualizar',  'Visualizar dashboard GED360'),
    ('ged360',     'GED_DOCUMENTO_SENSIVEL_VIEW',      'documento_sensivel', 'visualizar', 'Visualizar documento sensível GED360'),
    ('compras',    'COMPRAS_DASHBOARD_VIEW',           'dashboard',  'visualizar',  'Visualizar dashboard de compras e contratos'),
    ('saas',       'MNSOFT_SUPERADMIN_ACCESS',         'superadmin', 'acessar',     'Acesso Super Administração MNSOFT'),
    ('rc50.92',    'MEIO_AMBIENTE_DASHBOARD_VIEW',     'meio_ambiente.dashboard', 'visualizar', 'Visualizar dashboard de meio ambiente'),
    ('frotas',     'frotas.veiculo.visualizar',        'frotas.veiculo', 'visualizar', 'Visualizar veículos')
)
update sigov.permissao p
   set chave = r.chave,
       descricao = coalesce(nullif(p.descricao, ''), r.descricao),
       ativo = true,
       is_deleted = false,
       updated_at = now()
  from requeridas r
 where lower(p.chave) = lower(r.chave);

insert into sigov.permissao (modulo, chave, recurso, acao, descricao, ativo, is_deleted)
select r.modulo, r.chave, r.recurso, r.acao, r.descricao, true, false
  from (values
    ('ativos',     'ATIVOS_DASHBOARD_VIEW',          'dashboard',  'visualizar',  'Visualizar dashboard de ativos'),
    ('cidadao360', 'CIDADAO_PORTAL_VIEW',             'portal',     'visualizar',  'Visualizar portal do cidadão'),
    ('sst360',     'SST_DASHBOARD_VIEW',               'dashboard',  'visualizar',  'Visualizar dashboard SST'),
    ('saude',      'ACS_DASHBOARD_VIEW',               'acs.dashboard', 'visualizar', 'Consultar ACS360'),
    ('ged360',     'GED_DASHBOARD_VIEW',               'dashboard',  'visualizar',  'Visualizar dashboard GED360'),
    ('ged360',     'GED_DOCUMENTO_SENSIVEL_VIEW',      'documento_sensivel', 'visualizar', 'Visualizar documento sensível GED360'),
    ('compras',    'COMPRAS_DASHBOARD_VIEW',           'dashboard',  'visualizar',  'Visualizar dashboard de compras e contratos'),
    ('saas',       'MNSOFT_SUPERADMIN_ACCESS',         'superadmin', 'acessar',     'Acesso Super Administração MNSOFT'),
    ('rc50.92',    'MEIO_AMBIENTE_DASHBOARD_VIEW',     'meio_ambiente.dashboard', 'visualizar', 'Visualizar dashboard de meio ambiente'),
    ('frotas',     'frotas.veiculo.visualizar',        'frotas.veiculo', 'visualizar', 'Visualizar veículos')
  ) r(modulo, chave, recurso, acao, descricao)
 where not exists (
    select 1 from sigov.permissao p where lower(p.chave) = lower(r.chave)
 );

-- Mantém o contrato de concessão sistêmica já adotado pelos módulos recentes.
insert into sigov.perfil_permissao
    (perfil_acesso_id, permissao_id, efeito, ativo, is_deleted)
select pa.id, p.id, 'PERMITIR', true, false
  from sigov.perfil_acesso pa
 cross join sigov.permissao p
 where pa.codigo_externo = 'SUPERADMIN'
   and pa.sistemico and pa.ativo and not pa.is_deleted
   and p.chave in (
       'ATIVOS_DASHBOARD_VIEW', 'CIDADAO_PORTAL_VIEW', 'SST_DASHBOARD_VIEW',
       'ACS_DASHBOARD_VIEW', 'GED_DASHBOARD_VIEW', 'GED_DOCUMENTO_SENSIVEL_VIEW',
       'COMPRAS_DASHBOARD_VIEW', 'MNSOFT_SUPERADMIN_ACCESS',
       'MEIO_AMBIENTE_DASHBOARD_VIEW', 'frotas.veiculo.visualizar'
   )
   and p.ativo and not p.is_deleted
on conflict (perfil_acesso_id, permissao_id) do update
set efeito = 'PERMITIR', ativo = true, is_deleted = false;
