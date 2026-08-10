-- RC49.8: converge o contrato legado de protocolo antes dos índices/funções da RC46.
-- "exercicio" é o ano fiscal do número do protocolo (integer), não a FK exercicio_id.
do $$
begin
    if to_regclass('sigov.protocolo') is null then
        raise exception 'Contrato obrigatório ausente: sigov.protocolo';
    end if;
end $$;

alter table sigov.protocolo
    add column if not exists exercicio integer;

update sigov.protocolo
   set exercicio = coalesce(
       case when numero ~ '/[0-9]{4}$' then right(numero, 4)::integer end,
       extract(year from created_at)::integer,
       extract(year from current_date)::integer)
 where exercicio is null;

alter table sigov.protocolo
    alter column exercicio set default extract(year from current_date)::integer,
    alter column exercicio set not null;
