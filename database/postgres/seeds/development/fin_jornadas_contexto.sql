-- =============================================================================
-- SIGOV PLUS · Seed de desenvolvimento/homologação — contexto das jornadas
-- financeiras (empenho → liquidação → pagamento; lançamento → arrecadação;
-- conferência). EXCLUSIVO PARA DEVELOPMENT: dados fictícios, idempotente,
-- sem remoção física, sem pessoas reais, documentos reais ou credenciais.
-- Nomenclatura multi-esfera: catálogos neutros, válidos para esfera municipal,
-- estadual ou federal.
-- =============================================================================
do $fin_ctx$
declare
    v_tenant_id bigint;
    v_entidade_id bigint;
    v_exercicio_id bigint;
    v_pessoa_id bigint;
    v_fonte_id bigint;
    v_orgao_id bigint;
    v_programa_id bigint;
    v_acao_id bigint;
    v_natureza_despesa_id bigint;
    v_natureza_receita_id bigint;
    v_dotacao_id bigint;
    v_receita_id bigint;
begin
    if upper(coalesce(current_setting('sigov.environment', true), 'DEVELOPMENT')) <> 'DEVELOPMENT' then
        raise exception 'fin_jornadas_contexto.sql somente pode ser executado em Development';
    end if;

    select t.id into v_tenant_id from sigov.tenant t where t.slug = 'sigov-local' and not t.is_deleted limit 1;
    if v_tenant_id is null then
        raise notice 'fin_jornadas_contexto: tenant de desenvolvimento (slug sigov-local) não encontrado; seed ignorado.';
        return;
    end if;

    select e.id into v_entidade_id from sigov.entidade e where e.tenant_id = v_tenant_id and e.cnpj = '00000000000000' and not e.is_deleted limit 1;
    select x.id into v_exercicio_id from sigov.exercicio x where x.entidade_id = v_entidade_id and x.ano = extract(year from current_date)::int and not x.is_deleted limit 1;
    if v_entidade_id is null or v_exercicio_id is null then
        raise notice 'fin_jornadas_contexto: entidade principal ou exercício corrente não encontrados no tenant %; seed ignorado.', v_tenant_id;
        return;
    end if;

    -- Fornecedor fictício (pessoa jurídica) usado nos empenhos de homologação.
    insert into sigov.pessoa(tenant_id,entidade_id,exercicio_id,tipo_pessoa,nome,nome_social,documento,observacao,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,'J','Fornecedor Fictício de Homologação S.A.','Fornecedor Fictício de Homologação S.A.','00000000000191','Registro fictício para homologação financeira',true,false)
    on conflict (tenant_id,documento) where documento is not null and is_deleted = false do nothing;
    select id into v_pessoa_id from sigov.pessoa where tenant_id = v_tenant_id and documento = '00000000000191' and tipo_pessoa = 'J' order by is_deleted,id limit 1;

    insert into sigov.pessoa_juridica(tenant_id,entidade_id,exercicio_id,pessoa_id,cnpj,razao_social,nome_fantasia,ativo,is_deleted)
    select v_tenant_id,v_entidade_id,v_exercicio_id,v_pessoa_id,'00.000.000/0001-91','Fornecedor Fictício de Homologação S.A.','Fornecedor Fictício de Homologação',true,false
    where not exists(select 1 from sigov.pessoa_juridica pj where pj.pessoa_id = v_pessoa_id);

    -- Fonte de recurso fictícia.
    insert into sigov.fonte_recurso(tenant_id,entidade_id,exercicio_id,codigo,nome,descricao,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,'9900','Recurso não vinculado — homologação fictícia','Fonte fictícia para as jornadas de homologação financeira',true,false)
    on conflict (tenant_id,entidade_id,exercicio_id,codigo) do nothing;
    select id into v_fonte_id from sigov.fonte_recurso where tenant_id = v_tenant_id and entidade_id = v_entidade_id and exercicio_id = v_exercicio_id and codigo = '9900' order by is_deleted,id limit 1;

    -- Órgão/unidade orçamentária fictícia (nome neutro multi-esfera).
    insert into sigov.orgao_unidade_orcamentaria(tenant_id,entidade_id,exercicio_id,codigo,nome,sigla,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,'ORG-HOMO-01','Unidade Orçamentária Fictícia de Homologação','UOH',true,false)
    on conflict (tenant_id,entidade_id,exercicio_id,codigo) do nothing;
    select id into v_orgao_id from sigov.orgao_unidade_orcamentaria where tenant_id = v_tenant_id and entidade_id = v_entidade_id and exercicio_id = v_exercicio_id and codigo = 'ORG-HOMO-01' order by is_deleted,id limit 1;

    -- Programa e ação fictícios.
    insert into sigov.programa(tenant_id,entidade_id,exercicio_id,codigo,nome,objetivo,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,'PRG-HOM01','Programa Fictício de Serviços Administrativos','Programa fictício para homologação da execução orçamentária',true,false)
    on conflict (tenant_id,entidade_id,exercicio_id,codigo) do nothing;
    select id into v_programa_id from sigov.programa where tenant_id = v_tenant_id and entidade_id = v_entidade_id and exercicio_id = v_exercicio_id and codigo = 'PRG-HOM01' order by is_deleted,id limit 1;

    insert into sigov.acao(tenant_id,entidade_id,exercicio_id,programa_id,codigo,nome,tipo_acao,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,v_programa_id,'ACT-HOM01','Ação Fictícia de Manutenção Administrativa','ACAO',true,false)
    on conflict (tenant_id,entidade_id,exercicio_id,codigo) do nothing;
    select id into v_acao_id from sigov.acao where tenant_id = v_tenant_id and entidade_id = v_entidade_id and exercicio_id = v_exercicio_id and codigo = 'ACT-HOM01' order by is_deleted,id limit 1;

    -- Naturezas fictícias (estrutura clássica multi-esfera, valores ilustrativos).
    insert into sigov.natureza_despesa(tenant_id,entidade_id,exercicio_id,codigo,nome,categoria,grupo,elemento,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,'3.3.90.39','Outras Despesas de Custeio','DESPESA_CORRENTE','APLICACAO_DIRETA','CUSTEIO',true,false)
    on conflict (tenant_id,entidade_id,exercicio_id,codigo) do nothing;
    select id into v_natureza_despesa_id from sigov.natureza_despesa where tenant_id = v_tenant_id and entidade_id = v_entidade_id and exercicio_id = v_exercicio_id and codigo = '3.3.90.39' order by is_deleted,id limit 1;

    insert into sigov.natureza_receita(tenant_id,entidade_id,exercicio_id,codigo,nome,categoria,origem,especie,ativo,is_deleted)
    values(v_tenant_id,v_entidade_id,v_exercicio_id,'1.1.1.1.1.00.00.00','Impostos Fictícios de Circulação','RECEITA_TRIBUTARIA','IMPOSTOS','IMPOSTO_FICTICIO',true,false)
    on conflict (tenant_id,entidade_id,exercicio_id,codigo) do nothing;
    select id into v_natureza_receita_id from sigov.natureza_receita where tenant_id = v_tenant_id and entidade_id = v_entidade_id and exercicio_id = v_exercicio_id and codigo = '1.1.1.1.1.00.00.00' order by is_deleted,id limit 1;

    -- Dotação orçamentária fictícia (1.000.000,00) para as jornadas de despesa.
    insert into sigov.orcamento_despesa(tenant_id,entidade_id,exercicio_id,orgao_unidade_orcamentaria_id,programa_id,acao_id,natureza_despesa_id,fonte_recurso_id,dotacao_inicial,suplementacoes,reducoes,reservado,empenhado,liquidado,pago,ativo,is_deleted)
    select v_tenant_id,v_entidade_id,v_exercicio_id,v_orgao_id,v_programa_id,v_acao_id,v_natureza_despesa_id,v_fonte_id,1000000.00,0,0,0,0,0,0,true,false
    where not exists(select 1 from sigov.orcamento_despesa od
        where od.tenant_id=v_tenant_id and od.entidade_id=v_entidade_id and od.exercicio_id=v_exercicio_id
          and od.programa_id=v_programa_id and od.acao_id=v_acao_id
          and od.natureza_despesa_id=v_natureza_despesa_id and od.fonte_recurso_id=v_fonte_id and not od.is_deleted);
    select id into v_dotacao_id from sigov.orcamento_despesa od
        where od.tenant_id=v_tenant_id and od.entidade_id=v_entidade_id and od.exercicio_id=v_exercicio_id
          and od.programa_id=v_programa_id and od.acao_id=v_acao_id
          and od.natureza_despesa_id=v_natureza_despesa_id and od.fonte_recurso_id=v_fonte_id and not od.is_deleted
        order by od.id limit 1;

    -- Previsão orçamentária fictícia (500.000,00) para as jornadas de receita.
    insert into sigov.orcamento_receita(tenant_id,entidade_id,exercicio_id,natureza_receita_id,fonte_recurso_id,previsao_inicial,previsao_atualizada,lancado,arrecadado,ativo,is_deleted)
    select v_tenant_id,v_entidade_id,v_exercicio_id,v_natureza_receita_id,v_fonte_id,500000.00,500000.00,0,0,true,false
    where not exists(select 1 from sigov.orcamento_receita ore
        where ore.tenant_id=v_tenant_id and ore.entidade_id=v_entidade_id and ore.exercicio_id=v_exercicio_id
          and ore.natureza_receita_id=v_natureza_receita_id and ore.fonte_recurso_id=v_fonte_id and not ore.is_deleted);
    select id into v_receita_id from sigov.orcamento_receita ore
        where ore.tenant_id=v_tenant_id and ore.entidade_id=v_entidade_id and ore.exercicio_id=v_exercicio_id
          and ore.natureza_receita_id=v_natureza_receita_id and ore.fonte_recurso_id=v_fonte_id and not ore.is_deleted
        order by ore.id limit 1;

    raise notice 'fin_jornadas_contexto: pronto. tenant=% entidade=% exercicio=% fornecedor=% dotacao=% previsao=%',
        v_tenant_id, v_entidade_id, v_exercicio_id, v_pessoa_id, v_dotacao_id, v_receita_id;
end
$fin_ctx$;
