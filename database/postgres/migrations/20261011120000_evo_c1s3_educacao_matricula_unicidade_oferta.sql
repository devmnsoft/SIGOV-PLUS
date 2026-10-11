-- ============================================================
-- RC-EVO Ciclo 1 Sprint 3 (ciclo escolar): integridade de matricula
-- por oferta e contrato de frequencia com situacao explicita.
--
-- (a) UNICIDADE DE MATRICULA VIGENTE POR ALUNO x TURMA (OFERTA)
--     sigov.matricula (021_educacao_base.sql) garantia apenas a
--     unicidade de numero_matricula; nada impedia duas matrulas
--     vigentes ('ATIVA'/'CONFIRMADA') do mesmo aluno na mesma turma,
--     isto e, duas ocupacoes da mesma oferta sem auditoria. O
--     enturmamento/conversao reserva vaga por update-first, mas o
--     invariante final precisa estar garantido no banco, nao apenas
--     no caminho C#. Indice parcial: turma_id NULL nao conflita
--     (matrulas ainda nao enturmadas nunca se bloqueiam); o conflito
--     aparece no enturmamento/conversao que tenta duplicar a oferta.
--
-- (b) COLUNA SITUACAO EM sigov.diario_frequencia (CORRETIVA)
--     O arquivo de migration 20260813160000_rc50_25_educacao_fluxo_diario.sql
--     esta orfafo do manifest.json (inventario P0.1-A: arquivos nao
--     registrados nunca rodam no runner) e sua coluna 'situacao' nao
--     existe nas instalacoes limpas; o contrato C# (FrequenciaCreateRequest.Status,
--     insert e listagem de diario_frequencia) referencia essa coluna
--     e falharia em runtime (regra 13: falha explicita, sem sucesso
--     simulado). Correcao por migration nova (regra 8: nao alterar
--     publicada): adiciona a coluna idempotente, faz backfill a
--     partir de 'presente' e impoe o check canônico das quatro
--     situacoes (PRESENTE/FALTA/JUSTIFICADA/ABONADA).
--
-- (c) REMOCAO DO INDICE ESTREITO LEGADO ux_diario_frequencia_turma_aluno_data
--     criado por 20260819120000 (rc50_60) sobre (tenant,entidade,turma,
--     aluno,data) SEM componente_curricular: ele proibia registrar mais
--     de um componente por dia no mesmo aluno/turma, contradizendo o
--     indice canônico com componente (ux_frequencia_turma_aluno_data_componente
--     do 021 e o contrato que exige componente). Remocao idempotente;
--     o indice com componente segue garantindo a unicidade real.
--
-- Regras: 6/7/8 (migration nova idempotente, consolidados sincronizados
-- por migration, nao por edicao de publicada); 13 (grupos duplicados
-- preexistentes em matricula FALHAM EXPLICITAMENTE em vez de mascarar);
-- 24 (multi-esfera: chaves sempre com tenant_id + entidade_id, sem
-- comportamento municipal hardcoded).
-- ============================================================

do $evo_c1s3$
declare
    v_duplas bigint;
begin
    select count(*) into v_duplas
      from (select 1
              from sigov.matricula m
             where m.is_deleted = false
               and m.status in ('ATIVA', 'CONFIRMADA')
             group by m.tenant_id, m.entidade_id, m.aluno_id, m.turma_id
            having count(*) > 1) d;

    if v_duplas > 0 then
        raise exception
            'Unicidade de matricula por oferta: % grupo(s) (tenant, entidade, aluno, turma) com mais de uma matricula vigente preexistem; resolver os duplicados antes de aplicar o indice.',
            v_duplas;
    end if;
end
$evo_c1s3$;

create unique index if not exists ux_matricula_tenant_aluno_turma_vigencia
    on sigov.matricula (tenant_id, entidade_id, aluno_id, turma_id)
   where is_deleted = false and status in ('ATIVA', 'CONFIRMADA');

alter table sigov.diario_frequencia
    add column if not exists situacao varchar(20) not null default 'PRESENTE';

update sigov.diario_frequencia
   set situacao = case when presente then 'PRESENTE' else 'FALTA' end
 where situacao is null
    or situacao not in ('PRESENTE', 'FALTA', 'JUSTIFICADA', 'ABONADA');

alter table sigov.diario_frequencia
    drop constraint if exists ck_diario_frequencia_situacao;

alter table sigov.diario_frequencia
    add constraint ck_diario_frequencia_situacao
        check (situacao in ('PRESENTE', 'FALTA', 'JUSTIFICADA', 'ABONADA'));

drop index if exists sigov.ux_diario_frequencia_turma_aluno_data;
