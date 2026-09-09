-- Compatibilidade forward-only do cadastro Agro legado com FUNC11.
-- O documento continua derivado da pessoa já vinculada; ausência de documento
-- permanece explícita (NULL), sem geração de dado fictício como autoridade.
alter table if exists sigov.agro_produtor
    add column if not exists cpf_cnpj varchar(14),
    add column if not exists tipo_pessoa varchar(2),
    add column if not exists nome_razao_social varchar(180),
    add column if not exists telefone varchar(30),
    add column if not exists email varchar(180),
    add column if not exists endereco text,
    add column if not exists comunidade varchar(120),
    add column if not exists localidade varchar(120),
    add column if not exists caf_pronaf varchar(80),
    add column if not exists observacoes text;

update sigov.agro_produtor produtor
set cpf_cnpj = nullif(regexp_replace(coalesce(pessoa.documento, ''), '[^0-9]', '', 'g'), ''),
    nome_razao_social = coalesce(nullif(produtor.nome_razao_social, ''), nullif(pessoa.nome, '')),
    tipo_pessoa = coalesce(produtor.tipo_pessoa,
        case when length(regexp_replace(coalesce(pessoa.documento, ''), '[^0-9]', '', 'g')) = 14 then 'PJ'
             when length(regexp_replace(coalesce(pessoa.documento, ''), '[^0-9]', '', 'g')) = 11 then 'PF'
        end)
from sigov.pessoa pessoa
where pessoa.id = produtor.pessoa_id
  and (produtor.cpf_cnpj is null or produtor.nome_razao_social is null or produtor.tipo_pessoa is null);

alter table if exists sigov.agro_servico_maquina
    add column if not exists data_prevista date,
    add column if not exists data_executada date,
    add column if not exists horas numeric(10,2),
    add column if not exists custo_estimado numeric(14,2);

update sigov.agro_servico_maquina
set data_prevista = coalesce(data_prevista, data_agendada),
    data_executada = coalesce(data_executada, data_execucao),
    horas = coalesce(horas, horas_trabalhadas),
    custo_estimado = coalesce(custo_estimado, valor_estimado)
where data_prevista is null or data_executada is null or horas is null or custo_estimado is null;
