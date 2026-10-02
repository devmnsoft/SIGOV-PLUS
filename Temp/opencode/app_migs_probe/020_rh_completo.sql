-- Módulo Recursos Humanos completo: cadastros, folha, ponto, férias, afastamentos, saúde ocupacional,
-- eSocial estrutural, portal do servidor, outbox de eventos e integração futura com Financeiro/SIAFIC.

create or replace function sigov.rh_touch_updated_at()
returns trigger language plpgsql as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

do $$
declare
    t text;
begin
    foreach t in array array[
        'servidor','cargo','lotacao','vinculo','folha','folha_evento','folha_lancamento','ponto','ferias',
        'afastamento','saude_ocupacional','esocial','portal_usuario','portal_acesso','rh_evento'
    ] loop
        execute format($fmt$
            create table if not exists sigov.%I (
                id bigint generated always as identity primary key,
                tenant_id bigint not null references sigov.tenant(id),
                dados jsonb not null default '{}'::jsonb,
                auditoria jsonb not null default '{}'::jsonb,
                ativo boolean not null default true,
                is_deleted boolean not null default false,
                created_at timestamptz not null default now(),
                created_by bigint null,
                updated_at timestamptz null,
                updated_by bigint null,
                deleted_at timestamptz null,
                deleted_by bigint null,
                correlation_id uuid null
            )
        $fmt$, t);
        execute format('create index if not exists idx_%s_tenant_ativo on sigov.%I (tenant_id, ativo) where is_deleted = false', t, t);
        execute format('create index if not exists idx_%s_dados_gin on sigov.%I using gin (dados)', t, t);
        execute format('drop trigger if exists trg_%s_touch on sigov.%I', t, t);
        execute format('create trigger trg_%s_touch before update on sigov.%I for each row execute function sigov.rh_touch_updated_at()', t, t);
        execute format('alter table sigov.%I enable row level security', t);
        execute format('drop policy if exists %I on sigov.%I', 'rls_' || t || '_tenant_isolation', t);
        execute format('create policy %I on sigov.%I using (tenant_id = sigov.current_tenant_id()) with check (tenant_id = sigov.current_tenant_id())', 'rls_' || t || '_tenant_isolation', t);
    end loop;
end $$;

create unique index if not exists ux_rh_servidor_matricula
    on sigov.servidor (tenant_id, (dados->>'matricula')) where is_deleted = false and dados ? 'matricula';
create unique index if not exists ux_rh_servidor_cpf
    on sigov.servidor (tenant_id, (dados->>'cpf')) where is_deleted = false and dados ? 'cpf';
create unique index if not exists ux_rh_cargo_codigo
    on sigov.cargo (tenant_id, (dados->>'codigo')) where is_deleted = false and dados ? 'codigo';
create unique index if not exists ux_rh_lotacao_codigo
    on sigov.lotacao (tenant_id, (dados->>'codigo')) where is_deleted = false and dados ? 'codigo';
create index if not exists idx_rh_vinculo_servidor on sigov.vinculo (tenant_id, ((dados->>'servidorId')::bigint)) where is_deleted = false and dados ? 'servidorId';
create index if not exists idx_rh_folha_competencia on sigov.folha (tenant_id, (dados->>'ano'), (dados->>'mes')) where is_deleted = false;
create index if not exists idx_rh_folha_lancamento_servidor on sigov.folha_lancamento (tenant_id, ((dados->>'servidorId')::bigint)) where is_deleted = false and dados ? 'servidorId';
create index if not exists idx_rh_ponto_servidor_data on sigov.ponto (tenant_id, ((dados->>'servidorId')::bigint), (dados->>'dataHora')) where is_deleted = false and dados ? 'servidorId';
create index if not exists idx_rh_ferias_servidor on sigov.ferias (tenant_id, ((dados->>'servidorId')::bigint)) where is_deleted = false and dados ? 'servidorId';
create index if not exists idx_rh_afastamento_servidor on sigov.afastamento (tenant_id, ((dados->>'servidorId')::bigint)) where is_deleted = false and dados ? 'servidorId';
create index if not exists idx_rh_evento_outbox on sigov.rh_evento (tenant_id, ((dados->>'publicado')::boolean)) where is_deleted = false;

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
    ('rh', 'registros', 'visualizar', 'rh.registros.visualizar', 'Visualizar cadastros e movimentos de Recursos Humanos', true),
    ('rh', 'registros', 'criar', 'rh.registros.criar', 'Criar cadastros e movimentos de Recursos Humanos', true),
    ('rh', 'registros', 'editar', 'rh.registros.editar', 'Editar cadastros e movimentos de Recursos Humanos', true),
    ('rh', 'registros', 'excluir', 'rh.registros.excluir', 'Excluir logicamente registros de Recursos Humanos', true),
    ('rh', 'dashboard', 'visualizar', 'rh.dashboard.visualizar', 'Visualizar dashboards de Recursos Humanos', true),
    ('rh', 'portal', 'visualizar', 'rh.portal.visualizar', 'Visualizar portal do servidor', true),
    ('rh', 'exportar', 'exportar', 'rh.exportar', 'Exportar dados de Recursos Humanos em CSV/JSON', true),
    ('rh', 'financeiro', 'integrar', 'rh.financeiro.integrar', 'Preparar integração de folha com Financeiro/SIAFIC', true)
on conflict do nothing;
