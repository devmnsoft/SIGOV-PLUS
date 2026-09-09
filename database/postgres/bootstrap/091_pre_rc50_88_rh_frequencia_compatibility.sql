-- Compatibilidade forward-only da frequência FUNC12 com o contrato RH360.
-- O vínculo legado permanece preservado; servidor_id é acrescentado sem
-- inventar associação quando não houver origem autoritativa persistida.
alter table if exists sigov.rh_frequencia
    add column if not exists servidor_id bigint;
