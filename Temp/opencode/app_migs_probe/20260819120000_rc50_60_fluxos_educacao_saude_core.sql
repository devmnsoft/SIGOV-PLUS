set search_path to sigov;

-- Catálogo funcional granular usado pelo backend e pelos menus.
insert into sigov.permissao (modulo, chave, descricao)
select p.modulo, p.chave, p.descricao
from (values
 ('educacao','educacao.escola.alterar','Alterar escola'),
 ('educacao','educacao.responsavel.visualizar','Visualizar responsável'),
 ('educacao','educacao.responsavel.criar','Criar responsável'),
 ('educacao','educacao.frequencia.lancar','Lançar frequência'),
 ('educacao','educacao.diario.lancar','Lançar diário'),
 ('educacao','educacao.transporte.visualizar','Visualizar transporte escolar'),
 ('educacao','educacao.transporte.gerenciar','Gerenciar transporte escolar'),
 ('educacao','educacao.merenda.visualizar','Visualizar merenda'),
 ('educacao','educacao.merenda.gerenciar','Gerenciar merenda'),
 ('educacao','educacao.merenda.movimentar_estoque','Movimentar estoque de merenda'),
 ('educacao','educacao.biblioteca.visualizar','Visualizar biblioteca'),
 ('educacao','educacao.biblioteca.gerenciar','Gerenciar biblioteca'),
 ('educacao','educacao.indicadores.visualizar','Visualizar indicadores educacionais'),
 ('educacao','educacao.relatorio.exportar','Exportar relatório educacional'),
 ('saude','saude.unidade.alterar','Alterar unidade de saúde'),
 ('saude','saude.paciente.alterar','Alterar paciente'),
 ('saude','saude.acs.gerenciar','Gerenciar ACS'),
 ('saude','saude.domicilio.visualizar','Visualizar domicílio'),
 ('saude','saude.domicilio.criar','Criar domicílio'),
 ('saude','saude.visita.visualizar','Visualizar visita'),
 ('saude','saude.visita.registrar','Registrar visita'),
 ('saude','saude.ocorrencia.registrar','Registrar ocorrência'),
 ('saude','saude.vacinacao.aplicar','Aplicar vacina'),
 ('saude','saude.farmacia.movimentar_estoque','Movimentar estoque da farmácia'),
 ('saude','saude.regulacao.movimentar','Movimentar regulação'),
 ('saude','saude.esus_preparatorio.visualizar','Visualizar lote e-SUS preparatório'),
 ('saude','saude.relatorio.exportar','Exportar relatório de saúde')
) p(modulo, chave, descricao)
where not exists (
 select 1 from sigov.permissao atual
 where atual.modulo = p.modulo and atual.chave = p.chave
);

-- Perfis funcionais são templates globais; concessões continuam vinculadas ao tenant.
insert into sigov.perfil_acesso (nome, descricao, codigo_externo)
select p.nome, p.descricao, p.codigo
from (values
 ('Secretário de Educação','Gestão gerencial completa da Educação','SECRETARIO_EDUCACAO'),
 ('Coordenador Educação','Operação educacional no escopo autorizado','COORDENADOR_EDUCACAO'),
 ('Diretor Escolar','Gestão da escola autorizada','DIRETOR_ESCOLAR'),
 ('Professor','Diário e frequência das turmas vinculadas','PROFESSOR'),
 ('Secretaria Escolar','Cadastro, responsáveis e matrículas','SECRETARIA_ESCOLAR'),
 ('Merenda e Estoque','Cardápio, distribuição e estoque','MERENDA_ESTOQUE'),
 ('Bibliotecário','Acervo, empréstimos e reservas','BIBLIOTECARIO'),
 ('Secretário de Saúde','Gestão gerencial completa da Saúde','SECRETARIO_SAUDE'),
 ('Coordenador Saúde','Operação de unidade e equipe autorizadas','COORDENADOR_SAUDE'),
 ('Profissional de Saúde','Atendimentos clínicos autorizados','PROFISSIONAL_SAUDE'),
 ('ACS','Cadastros e visitas da microárea autorizada','ACS'),
 ('Farmácia','Estoque, lotes e dispensação','FARMACIA'),
 ('Regulação','Fila e solicitações regulatórias','REGULACAO')
) p(nome, descricao, codigo)
where not exists (
 select 1 from sigov.perfil_acesso atual
 where atual.codigo_externo = p.codigo and atual.is_deleted = false
);

-- Integridade concorrente: uma matrícula corrente por aluno/ano e um lançamento diário por turma/aluno/data.
alter table sigov.matricula
 add column if not exists tenant_id bigint,
 add column if not exists entidade_id bigint,
 add column if not exists aluno_id bigint,
 add column if not exists ano_letivo_id bigint,
 add column if not exists status varchar(40) not null default 'ATIVA',
 add column if not exists ativo boolean not null default true,
 add column if not exists is_deleted boolean not null default false;
alter table sigov.diario_frequencia
 add column if not exists tenant_id bigint,
 add column if not exists entidade_id bigint,
 add column if not exists turma_id bigint,
 add column if not exists aluno_id bigint,
 add column if not exists data_aula date,
 add column if not exists ativo boolean not null default true,
 add column if not exists is_deleted boolean not null default false;
create unique index if not exists ux_matricula_aluno_ano_ativa
 on sigov.matricula (tenant_id, entidade_id, aluno_id, ano_letivo_id)
 where is_deleted = false and ativo = true and status in ('ATIVA','CONFIRMADA');
create unique index if not exists ux_diario_frequencia_turma_aluno_data
 on sigov.diario_frequencia (tenant_id, entidade_id, turma_id, aluno_id, data_aula)
 where is_deleted = false and ativo = true;
