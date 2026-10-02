-- Seed fictício da jornada de cotações (Bloco B: cotação -> comparativo ->
-- seleção -> pedido). Desenvolvimento/homologação apenas. Idempotente, com
-- dados fictícios/LGPD-safe e multi-esfera (produtos nos quatro tenants demo:
-- municipal, estadual e federal). Nenhum segredo literal neste arquivo.
-- Permissões: Analista registra propostas e vê pedidos/recebimentos; Gestor
-- julga (seleciona/encerra) e acompanha — segregação de funções da jornada.
-- As ids das permissões são resolvidas pelo catálogo (módulo+recurso+ação);
-- se alguma faltar, o seed falha explicitamente em vez de inventar ids.

do $$
declare
  tenant_municipal uuid := 'b0000001-0000-4000-8000-000000000001';
  tenant_estadual uuid := 'b0000001-0000-4000-8000-000000000002';
  tenant_federal uuid := 'b0000001-0000-4000-8000-000000000003';
  tenant_admin uuid := 'b0000001-0000-4000-8000-000000000004';
  p_cot_visualizar bigint;
  p_cot_criar bigint;
  p_cot_enviar bigint;
  p_cot_julgar bigint;
  p_ped_visualizar bigint;
  p_for_visualizar bigint;
  p_for_criar bigint;
  p_rec_visualizar bigint;
  p_rec_registrar bigint;
  p_rec_inspecionar bigint;
begin
  select id into p_cot_visualizar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.cotacoes' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_cot_criar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.cotacoes' and acao='criar' and ativo and not is_deleted limit 1;
  select id into p_cot_enviar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.cotacoes' and acao='enviar' and ativo and not is_deleted limit 1;
  select id into p_cot_julgar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.cotacoes' and acao='julgar' and ativo and not is_deleted limit 1;
  select id into p_ped_visualizar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.pedidos' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_for_visualizar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.fornecedores' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_for_criar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.fornecedores' and acao='criar' and ativo and not is_deleted limit 1;
  select id into p_rec_visualizar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.recebimentos' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_rec_registrar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.recebimentos' and acao='registrar' and ativo and not is_deleted limit 1;
  select id into p_rec_inspecionar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.recebimentos' and acao='inspecionar' and ativo and not is_deleted limit 1;
  if p_cot_visualizar is null or p_cot_criar is null or p_cot_enviar is null or p_cot_julgar is null
     or p_ped_visualizar is null or p_for_visualizar is null or p_for_criar is null
     or p_rec_visualizar is null or p_rec_registrar is null or p_rec_inspecionar is null then
    raise exception 'Seed de cotação exige as permissões do módulo compras_empresariais (cotacoes, pedidos, fornecedores e recebimentos) presentes no catálogo.';
  end if;

  -- Catálogo fictício de produtos para as linhas das requisições demo, nos
  -- quatro tenants (municipal, estadual, federal e administração local).
  insert into sigov.estoque_produto(id, tenant_id, sku, nome, unidade, estoque_minimo, permite_saldo_negativo, ativo) values
    ('e0000001-0000-4000-8000-000000000101', tenant_municipal, 'DEMO-LIC-SW', 'Licença de software corporativo (fictícia)', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000102', tenant_municipal, 'DEMO-SUP-HORA', 'Serviço de suporte fictício', 'HORA', 0, false, true),
    ('e0000001-0000-4000-8000-000000000103', tenant_municipal, 'DEMO-MAT-ESC', 'Material de escritório fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000104', tenant_municipal, 'DEMO-DID-UN', 'Material didático fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000201', tenant_estadual, 'DEMO-LIC-SW', 'Licença de software corporativo (fictícia)', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000202', tenant_estadual, 'DEMO-SUP-HORA', 'Serviço de suporte fictício', 'HORA', 0, false, true),
    ('e0000001-0000-4000-8000-000000000203', tenant_estadual, 'DEMO-MAT-ESC', 'Material de escritório fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000204', tenant_estadual, 'DEMO-DID-UN', 'Material didático fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000301', tenant_federal, 'DEMO-LIC-SW', 'Licença de software corporativo (fictícia)', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000302', tenant_federal, 'DEMO-SUP-HORA', 'Serviço de suporte fictício', 'HORA', 0, false, true),
    ('e0000001-0000-4000-8000-000000000303', tenant_federal, 'DEMO-MAT-ESC', 'Material de escritório fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000304', tenant_federal, 'DEMO-DID-UN', 'Material didático fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000401', tenant_admin, 'DEMO-LIC-SW', 'Licença de software corporativo (fictícia)', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000402', tenant_admin, 'DEMO-SUP-HORA', 'Serviço de suporte fictício', 'HORA', 0, false, true),
    ('e0000001-0000-4000-8000-000000000403', tenant_admin, 'DEMO-MAT-ESC', 'Material de escritório fictício', 'UN', 0, false, true),
    ('e0000001-0000-4000-8000-000000000404', tenant_admin, 'DEMO-DID-UN', 'Material didático fictício', 'UN', 0, false, true)
    on conflict (id) do nothing;

  -- Almoxarifados fictícios (o recebimento exige um almoxarifado ativo por
  -- tenant); criado em todos os tenants demo para não limitar a jornada ao
  -- município.
  insert into sigov.estoque_almoxarifado(id, tenant_id, nome, ativo) values
    ('a0000001-0000-4000-8000-000000000201', tenant_municipal, 'Almoxarifado central (fictício)', true),
    ('a0000001-0000-4000-8000-000000000202', tenant_estadual, 'Almoxarifado central (fictício)', true),
    ('a0000001-0000-4000-8000-000000000203', tenant_federal, 'Almoxarifado central (fictício)', true),
    ('a0000001-0000-4000-8000-000000000204', tenant_admin, 'Almoxarifado central (fictício)', true)
    on conflict (id) do nothing;

  -- Fornecedores fictícios para os convites da jornada. Nascem RASCUNHO
  -- (estado canônico da criação via API); o guard de cotação exige apenas
  -- ausência de BLOQUEADO/SUSPENSO. O terceiro nasce BLOQUEADO para exercitar
  -- o filtro de elegibilidade na tela. Somente o tenant municipal demo recebe
  -- fornecedores, pois é a esfera da jornada de homologação.
  insert into sigov.compras_empresarial_fornecedor(
    id, tenant_id, codigo, tipo_pessoa, documento_hash, documento_mascarado,
    razao_social, nome_fantasia, categoria, porte, condicao_pagamento,
    prazo_medio, observacoes, nome, email, telefone, ativo, status, score,
    created_by, updated_by, correlation_id
  ) values
    ('f9000001-0000-4000-8000-000000000101', tenant_municipal, 'FORN-DEMO-2026-0001', 'J',
     encode(digest(regexp_replace('11222333000181','[^0-9]','','g'),'sha256'),'hex'), '***.***.***/0001-**',
     'Alfa Materiais de Escritório Fictício LTDA', null, 'Materiais de escritório',
     'EMPRESA_DE_PEQUENO PORTE', 'À vista', 7,
     'Fornecedor fictício para jornada de cotação.',
     'Alfa Materiais de Escritório Fictício LTDA', 'a***@demo.local', '00 00000-0000', true, 'RASCUNHO', 55,
     'seed', 'seed', 'seed-compras-cotacao'),
    ('f9000001-0000-4000-8000-000000000102', tenant_municipal, 'FORN-DEMO-2026-0002', 'J',
     encode(digest(regexp_replace('44555666000177','[^0-9]','','g'),'sha256'),'hex'), '***.***.***/0001-**',
     'Beta Serviços e Suprimentos Fictícios S.A.', null, 'Suprimentos e serviços',
     'EMPRESA_DE_MEDIO_PORTE', '30 dias', 5,
     'Fornecedor fictício para jornada de cotação.',
     'Beta Serviços e Suprimentos Fictícios S.A.', 'b***@demo.local', '00 00000-0000', true, 'RASCUNHO', 42,
     'seed', 'seed', 'seed-compras-cotacao'),
    ('f9000001-0000-4000-8000-000000000103', tenant_municipal, 'FORN-DEMO-2026-0003', 'J',
     encode(digest(regexp_replace('99888777000165','[^0-9]','','g'),'sha256'),'hex'), '***.***.***/0001-**',
     'Gama Fornecimentos Fictícios ME', null, 'Fornecimentos diversos',
     'MICROEMPRESA', 'À vista', 10,
     'Fornecedor fictício bloqueado para exercitar a regra de elegibilidade.',
     'Gama Fornecimentos Fictícios ME', 'g***@demo.local', '00 00000-0000', false, 'BLOQUEADO', 10,
     'seed', 'seed', 'seed-compras-cotacao')
    on conflict (id) do nothing;

  insert into sigov.compras_empresarial_historico(tenant_id, aggregate_type, aggregate_id, acao, created_by, correlation_id)
  select tenant_municipal, 'FORNECEDOR', f.id, 'CRIADO', 'seed', 'seed-compras-cotacao'
  from sigov.compras_empresarial_fornecedor f
  where f.tenant_id = tenant_municipal and f.codigo in ('FORN-DEMO-2026-0001','FORN-DEMO-2026-0002','FORN-DEMO-2026-0003')
    and not exists (
      select 1 from sigov.compras_empresarial_historico h
      where h.aggregate_type = 'FORNECEDOR' and h.aggregate_id = f.id and h.acao = 'CRIADO'
    );

  -- Concessões aos perfis demo (núcleo 1, como no seed institucional):
  -- Analista (9001) elabora cotações e registra propostas; Gestor (9002)
  -- julga (seleção/encerramento) e acompanha.
  insert into sigov.perfil_permissao(perfil_acesso_id, permissao_id, tenant_id, efeito, alcada_valor) values
    (9001, p_cot_visualizar, 1, 'PERMITIR', null),
    (9001, p_cot_criar, 1, 'PERMITIR', null),
    (9001, p_cot_enviar, 1, 'PERMITIR', null),
    (9001, p_ped_visualizar, 1, 'PERMITIR', null),
    (9001, p_for_visualizar, 1, 'PERMITIR', null),
    (9001, p_for_criar, 1, 'PERMITIR', null),
    (9001, p_rec_visualizar, 1, 'PERMITIR', null),
    (9001, p_rec_registrar, 1, 'PERMITIR', null),
    (9001, p_rec_inspecionar, 1, 'PERMITIR', null),
    (9002, p_cot_visualizar, 1, 'PERMITIR', null),
    (9002, p_cot_julgar, 1, 'PERMITIR', null),
    (9002, p_ped_visualizar, 1, 'PERMITIR', null),
    (9002, p_for_visualizar, 1, 'PERMITIR', null)
    on conflict (perfil_acesso_id, permissao_id) do nothing;
end $$;
