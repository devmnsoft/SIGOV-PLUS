-- Sonda de aceite RC-EVO-C (ramo APLICADA do sidecar docker): tabela efemera
-- criada dentro da transacao oficial; sera removida apos a validacao.
create table if not exists sigov.evo_c_probe_aplicada (
    id int primary key,
    criado_em timestamptz not null default now()
);
insert into sigov.evo_c_probe_aplicada (id) values (1) on conflict do nothing;
