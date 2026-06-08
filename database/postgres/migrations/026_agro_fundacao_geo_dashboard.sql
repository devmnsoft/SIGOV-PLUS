-- Agro e Desenvolvimento Rural - fundação, georreferenciamento estrutural e dashboard inicial.
-- Schema único sigov; sem PostGIS obrigatório; Dapper/PostgreSQL-ready.

insert into sigov.modulo_saas (codigo, nome, descricao, categoria, ordem, rota_base, icone, ativo)
values ('agro', 'Agro e Desenvolvimento Rural', 'Fundação do módulo Agro com georreferenciamento estrutural e dashboard inicial.', 'Políticas públicas', 160, '/Agro/Dashboard', 'bi-tree', true)
on conflict (codigo) do update set
    nome = excluded.nome,
    descricao = excluded.descricao,
    categoria = excluded.categoria,
    rota_base = excluded.rota_base,
    icone = excluded.icone,
    ativo = true;

insert into sigov.tenant_modulo (tenant_id, modulo_saas_id, habilitado, contratado, ativo)
select t.id, m.id, true, true, true
  from sigov.tenant t
  join sigov.modulo_saas m on m.codigo = 'agro'
 where t.ativo = true and t.is_deleted = false
on conflict (tenant_id, modulo_saas_id) do nothing;

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
('agro','dashboard','visualizar','agro.dashboard.visualizar','Visualizar dashboard Agro.',true),
('agro','geo','visualizar','agro.geo.visualizar','Visualizar camadas e feições geográficas do Agro.',true),
('agro','geo','criar','agro.geo.criar','Criar camadas e feições geográficas do Agro.',true),
('agro','geo','editar','agro.geo.editar','Editar camadas e feições geográficas do Agro.',true),
('agro','geo','excluir','agro.geo.excluir','Excluir camadas e feições geográficas do Agro.',true),
('agro','geo','exportar','agro.geo.exportar','Exportar dados geográficos do Agro em GeoJSON.',true)
on conflict (modulo, recurso, acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.feature_flag_def (codigo, nome, descricao, modulo, ativo)
values
('agro.dashboard','Dashboard Agro','Habilita o dashboard inicial do módulo Agro.','agro',true),
('agro.geo','Georreferenciamento Agro','Habilita camadas e feições geográficas estruturais do Agro.','agro',true),
('agro.exportacao_geojson','Exportação GeoJSON Agro','Habilita exportação estrutural GeoJSON do Agro.','agro',true)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, modulo=excluded.modulo, ativo=true;

insert into sigov.tenant_feature_flag (tenant_id, feature_flag_def_id, habilitado, ativo)
select t.id, f.id, true, true
  from sigov.tenant t
  join sigov.feature_flag_def f on f.modulo = 'agro'
 where t.ativo = true and t.is_deleted = false
on conflict (tenant_id, feature_flag_def_id) do nothing;

create table if not exists sigov.agro_geo_camada (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    codigo varchar(80) not null,
    nome varchar(250) not null,
    tipo_camada varchar(80) not null,
    descricao text null,
    publica boolean not null default false,
    estilo_json jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint uk_agro_geo_camada_tenant_entidade_codigo unique (tenant_id, entidade_id, codigo),
    constraint ck_agro_geo_camada_tipo check (tipo_camada in ('PRODUTORES','PROPRIEDADES','TALHOES','CULTURAS','ESTRADAS','PONTOS_CRITICOS','FEIRAS','AGROINDUSTRIAS','OCORRENCIAS','OUTROS'))
);

create unique index if not exists ux_agro_geo_camada_tenant_entidade_codigo on sigov.agro_geo_camada (tenant_id, coalesce(entidade_id, 0), codigo) where is_deleted = false;

create table if not exists sigov.agro_geo_feicao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    camada_id bigint not null references sigov.agro_geo_camada(id),
    origem_tabela varchar(150) null,
    origem_id bigint null,
    nome varchar(250) not null,
    tipo_geometria varchar(40) not null,
    latitude numeric(12,8) null,
    longitude numeric(12,8) null,
    geojson jsonb null,
    propriedades_json jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_agro_geo_feicao_tipo check (tipo_geometria in ('POINT','LINESTRING','POLYGON','MULTIPOLYGON','GEOJSON')),
    constraint ck_agro_geo_feicao_latitude check (latitude is null or (latitude >= -90 and latitude <= 90)),
    constraint ck_agro_geo_feicao_longitude check (longitude is null or (longitude >= -180 and longitude <= 180)),
    constraint ck_agro_geo_feicao_coordenadas_pares check ((latitude is null and longitude is null) or (latitude is not null and longitude is not null))
);

create table if not exists sigov.agro_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    tipo_evento varchar(150) not null,
    origem varchar(150) null,
    origem_id bigint null,
    payload jsonb not null default '{}'::jsonb,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create index if not exists idx_agro_geo_camada_tenant_codigo on sigov.agro_geo_camada (tenant_id, codigo) where is_deleted = false;
create index if not exists idx_agro_geo_feicao_tenant_camada on sigov.agro_geo_feicao (tenant_id, camada_id) where is_deleted = false;
create index if not exists idx_agro_geo_feicao_tenant_origem on sigov.agro_geo_feicao (tenant_id, origem_tabela, origem_id) where is_deleted = false;
create index if not exists idx_agro_evento_tenant_tipo on sigov.agro_evento (tenant_id, tipo_evento);
create index if not exists idx_agro_evento_created_at on sigov.agro_evento (created_at);

create or replace view sigov.vw_agro_dashboard as
with chaves as (
    select tenant_id, entidade_id from sigov.agro_geo_camada where is_deleted = false
    union
    select tenant_id, entidade_id from sigov.agro_geo_feicao where is_deleted = false
    union
    select tenant_id, entidade_id from sigov.agro_evento
), camadas as (
    select tenant_id, entidade_id, count(*)::bigint total_camadas from sigov.agro_geo_camada where is_deleted = false group by tenant_id, entidade_id
), feicoes as (
    select tenant_id, entidade_id, count(*)::bigint total_feicoes from sigov.agro_geo_feicao where is_deleted = false group by tenant_id, entidade_id
), eventos as (
    select tenant_id, entidade_id, count(*)::bigint total_eventos from sigov.agro_evento group by tenant_id, entidade_id
)
select k.tenant_id,
       k.entidade_id,
       coalesce(c.total_camadas, 0)::bigint as total_camadas,
       coalesce(f.total_feicoes, 0)::bigint as total_feicoes,
       coalesce(e.total_eventos, 0)::bigint as total_eventos,
       0::bigint as total_produtores,
       0::bigint as total_propriedades,
       0::bigint as total_visitas,
       0::bigint as total_servicos_maquina,
       0::bigint as total_pontos_criticos
  from chaves k
  left join camadas c on c.tenant_id = k.tenant_id and c.entidade_id is not distinct from k.entidade_id
  left join feicoes f on f.tenant_id = k.tenant_id and f.entidade_id is not distinct from k.entidade_id
  left join eventos e on e.tenant_id = k.tenant_id and e.entidade_id is not distinct from k.entidade_id;
