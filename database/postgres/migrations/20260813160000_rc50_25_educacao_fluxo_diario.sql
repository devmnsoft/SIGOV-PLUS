-- RC50.25: confirmação de matrícula e frequência diária com situação explícita.
-- Idempotente, restrito ao schema sigov e compatível com Database=postgres.
alter table sigov.diario_frequencia
    add column if not exists situacao varchar(20) not null default 'PRESENTE';

update sigov.diario_frequencia
set situacao = case when presente then 'PRESENTE' else 'FALTA' end
where situacao is null or situacao not in ('PRESENTE', 'FALTA', 'JUSTIFICADA', 'ABONADA');

alter table sigov.diario_frequencia
    drop constraint if exists ck_diario_frequencia_situacao;
alter table sigov.diario_frequencia
    add constraint ck_diario_frequencia_situacao
    check (situacao in ('PRESENTE', 'FALTA', 'JUSTIFICADA', 'ABONADA'));

create unique index if not exists ux_diario_frequencia_aluno_data_componente
    on sigov.diario_frequencia
       (tenant_id, entidade_id, turma_id, aluno_id, data_aula, coalesce(componente_curricular, ''))
    where is_deleted = false;

alter table sigov.matricula
    drop constraint if exists ck_matricula_status_rc50_25;
alter table sigov.matricula
    add constraint ck_matricula_status_rc50_25
    check (status in ('PENDENTE', 'ATIVA', 'CONFIRMADA', 'TRANSFERIDA', 'CANCELADA', 'ENCERRADA'));

create unique index if not exists ux_matricula_aluno_ano_em_curso
    on sigov.matricula (tenant_id, entidade_id, aluno_id, ano_letivo_id)
    where is_deleted = false and ativo = true
      and status in ('PENDENTE', 'ATIVA', 'CONFIRMADA');

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values ('educacao', 'matricula', 'confirmar', 'educacao.matricula.confirmar', 'Confirmar matrícula', true)
on conflict do nothing;
