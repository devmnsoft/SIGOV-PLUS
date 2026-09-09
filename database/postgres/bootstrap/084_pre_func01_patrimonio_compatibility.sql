-- Compatibilidade forward-only entre o patrimônio transversal legado e FUNC01.
-- As relações `patrimonio_bem` e `patrimonio_inventario` já existiam com um
-- contrato mínimo; por isso os CREATE TABLE IF NOT EXISTS publicados em FUNC01
-- não acrescentam as colunas usadas pelos índices e pelos fluxos atuais.

alter table if exists sigov.patrimonio_bem
    add column if not exists codigo_tombo varchar(80),
    add column if not exists codigo_anterior varchar(80),
    add column if not exists categoria_id bigint,
    add column if not exists tipo_bem varchar(80) not null default 'OUTRO',
    add column if not exists marca varchar(120),
    add column if not exists modelo varchar(120),
    add column if not exists numero_serie varchar(160),
    add column if not exists data_aquisicao date,
    add column if not exists valor_aquisicao numeric(18,2),
    add column if not exists valor_atual numeric(18,2),
    add column if not exists estado_conservacao varchar(30) not null default 'BOM',
    add column if not exists situacao varchar(30) not null default 'ATIVO',
    add column if not exists unidade_id bigint,
    add column if not exists setor_id bigint,
    add column if not exists responsavel_usuario_id bigint,
    add column if not exists observacao text,
    add column if not exists ativo boolean not null default true,
    add column if not exists created_by bigint,
    add column if not exists updated_by bigint,
    add column if not exists deleted_at timestamptz,
    add column if not exists deleted_by bigint;

update sigov.patrimonio_bem
set codigo_tombo = coalesce(nullif(codigo_tombo, ''), nullif(numero_tombamento, ''), 'LEGACY-BEM-' || id::text)
where codigo_tombo is null or codigo_tombo = '';

with duplicados as (
    select id, row_number() over(partition by tenant_id, codigo_tombo order by id) as ordem
    from sigov.patrimonio_bem
)
update sigov.patrimonio_bem bem
set codigo_tombo = bem.codigo_tombo || '-' || bem.id::text
from duplicados d
where d.id = bem.id and d.ordem > 1;

alter table if exists sigov.patrimonio_bem
    alter column codigo_tombo set not null;

create unique index if not exists ux_patrimonio_bem_tenant_tombo
    on sigov.patrimonio_bem(tenant_id, codigo_tombo);

alter table if exists sigov.patrimonio_inventario
    add column if not exists codigo varchar(80),
    add column if not exists data_abertura date not null default current_date,
    add column if not exists data_fechamento date,
    add column if not exists situacao varchar(20) not null default 'ABERTO',
    add column if not exists unidade_id bigint,
    add column if not exists responsavel_usuario_id bigint,
    add column if not exists created_by bigint,
    add column if not exists updated_by bigint;

update sigov.patrimonio_inventario
set codigo = coalesce(nullif(codigo, ''), 'LEGACY-INV-' || id::text)
where codigo is null or codigo = '';

alter table if exists sigov.patrimonio_inventario
    alter column codigo set not null;

create unique index if not exists ux_patrimonio_inventario_tenant_codigo
    on sigov.patrimonio_inventario(tenant_id, codigo);
