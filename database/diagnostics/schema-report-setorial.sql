select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'educacao_aluno','educacao_escola','educacao_turma','educacao_matricula','educacao_frequencia','educacao_boletim','educacao_avaliacao','educacao_transporte_rota','educacao_merenda_cardapio','educacao_biblioteca_livro',
    'saude_paciente','saude_unidade','saude_atendimento','saude_agenda','saude_procedimento','saude_acs','saude_visita_domiciliar','saude_familia','saude_domicilio',
    'saneamento_consumidor','saneamento_ligacao','saneamento_hidrometro','saneamento_leitura','saneamento_fatura','saneamento_ordem_servico','saneamento_rede_gis',
    'social_familia','social_pessoa','social_atendimento','social_beneficio','social_visita',
    'agro_produtor','agro_propriedade','agro_programa','agro_servico','portal_servico','portal_solicitacao','ouvidoria_manifestacao','campo_roteiro','campo_coleta','campo_evidencia','gis_camada','gis_geometria','auditoria_evento'
  )
order by table_name, ordinal_position;
