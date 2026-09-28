-- Seed institucional e de usuários demo para a jornada de aprovações de compras
-- (desenvolvimento/homologação). Idempotente, com dados fictícios/LGPD-safe e
-- multi-esfera (municipal, estadual e federal). Nenhum segredo literal neste
-- arquivo: os usuários demo reaproveitam o hash de senha do administrador
-- local (mesma senha de validação do ambiente); sem esse administrador o seed
-- falha explicitamente em vez de inventar credenciais.
-- Usuários demo (apenas ambiente de validação):
--   compras.demo.analista -> Analista de Compras (permissões do módulo + alçada de 50.000,00)
--   compras.demo.gestor   -> Gestor de Compras (fila de aprovações + alçada de 250.000,00)

do $$
declare
  tenant_municipal uuid := 'b0000001-0000-4000-8000-000000000001';
  tenant_estadual uuid := 'b0000001-0000-4000-8000-000000000002';
  tenant_federal uuid := 'b0000001-0000-4000-8000-000000000003';
  tenant_admin uuid := 'b0000001-0000-4000-8000-000000000004';
  sub_analista uuid := md5('sigov:usuario:101')::uuid;
  sub_gestor uuid := md5('sigov:usuario:102')::uuid;
  senha_hash text;
  p_dashboard bigint;
  p_requisicoes_visualizar bigint;
  p_requisicoes_criar bigint;
  p_requisicoes_editar bigint;
  p_requisicoes_enviar bigint;
  p_aprovacoes_visualizar bigint;
  p_aprovacoes_aprovar bigint;
  p_relatorios bigint;
  p_configuracao bigint;
begin
  senha_hash := (select u.senha_hash from sigov.usuario u where u.id = 1 and not u.is_deleted limit 1);
  if senha_hash is null then
    raise exception 'Seed de demo exige o usuario administrador local (id=1) para reaproveitar o hash de senha.';
  end if;

  -- Os ids das permissoes sao resolvidos pelo catalogo (modulo+recurso+acao):
  -- o id e identity e pode variar entre ambientes; a chave e a fonte de autoridade.
  select id into p_dashboard from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.dashboard' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_requisicoes_visualizar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.requisicoes' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_requisicoes_criar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.requisicoes' and acao='criar' and ativo and not is_deleted limit 1;
  select id into p_requisicoes_editar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.requisicoes' and acao='editar' and ativo and not is_deleted limit 1;
  select id into p_requisicoes_enviar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.requisicoes' and acao='enviar' and ativo and not is_deleted limit 1;
  select id into p_aprovacoes_visualizar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.aprovacoes' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_aprovacoes_aprovar from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.aprovacoes' and acao='aprovar' and ativo and not is_deleted limit 1;
  select id into p_relatorios from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.relatorios' and acao='visualizar' and ativo and not is_deleted limit 1;
  select id into p_configuracao from sigov.permissao where modulo='compras_empresariais' and recurso='compras_empresariais.configuracao' and acao='gerenciar' and ativo and not is_deleted limit 1;
  if p_dashboard is null or p_requisicoes_visualizar is null or p_requisicoes_criar is null
     or p_requisicoes_editar is null or p_requisicoes_enviar is null
     or p_aprovacoes_visualizar is null or p_aprovacoes_aprovar is null
     or p_relatorios is null or p_configuracao is null then
    raise exception 'Seed de demo exige as permissoes do modulo compras_empresariais (dashboard, requisicoes, aprovacoes, relatorios e configuracao) presentes no catalogo.';
  end if;

  -- Unidades organizacionais do nucleo (mesmo tenant da entidade; vínculo
  -- entidade preenchido após a criação para tolerar FKs circulares).
  insert into sigov.unidade_organizacional(id, tenant_id, entidade_id, nome) overriding system value values
    (9201, 1, null, 'Sede da Secretaria Municipal de Compras (Fictícia)'),
    (9202, 3, null, 'Sede da Secretaria de Estado da Administração (Fictícia)'),
    (9203, 4, null, 'Gabinete do Ministério Fictício da Gestão Pública')
    on conflict (id) do nothing;

  -- Entidades institucionais multi-esfera com configuração completa exigida
  -- pelo contexto institucional fail-closed (esfera, tipo, hierarquia,
  -- abrangência territorial e unidades gestora/executora presentes).
  insert into sigov.entidade(id, tenant_id, nome, cnpj, esfera_governo, tipo_entidade, unidade_gestora_id, unidade_executora_id, hierarquia_administrativa, abrangencia_territorial, uf, municipio, regiao_jurisdicao) overriding system value values
    (9101, 1, 'Secretaria Municipal de Compras de Fictópolis (Fictícia)', '11111111000191', 'municipal', 'secretaria_municipal', 9201, 9201, 'Prefeitura Municipal de Fictópolis - Secretaria Municipal de Compras', 'Município de Fictópolis/SP', 'SP', 'Fictópolis', null),
    (9102, 3, 'Secretaria de Estado da Administração de Fictilândia (Fictícia)', '22222222000192', 'estadual', 'secretaria_estadual', 9202, 9202, 'Governo do Estado de Fictilândia - Secretaria de Estado da Administração', 'Estado de Fictilândia/FG', 'FG', null, null),
    (9103, 4, 'Ministério Fictício da Gestão Pública', '33333333000193', 'federal', 'ministerio', 9203, 9203, 'Presidência da República - Ministério Fictício da Gestão Pública', 'Território Nacional', null, null, 'Nacional')
    on conflict (id) do nothing;

  update sigov.unidade_organizacional u
     set entidade_id = case u.id when 9201 then 9101 when 9202 then 9102 when 9203 then 9103 end
   where u.id in (9201, 9202, 9203);

  -- Vínculos tenant empresarial -> núcleo (um único vínculo ativo por tenant).
  -- O núcleo 5 (administração local) recebe vínculo, mas permanece SEM
  -- configuração institucional completa de propósito: contexto admin continua
  -- fail-closed como evidência viva da regra de falha explícita.
  insert into sigov.enterprise_tenant_mapping(id, core_tenant_id, enterprise_tenant_id, ativo) values
    (4101, 1, tenant_municipal, true),
    (4102, 3, tenant_estadual, true),
    (4103, 4, tenant_federal, true),
    (4104, 5, tenant_admin, true)
    on conflict (id) do nothing;

  -- Usuários demo (ids explícitos; coluna id sem default). Senha igual à do
  -- administrador local do ambiente de validação.
  insert into sigov.usuario(id, tenant_id, login, email, senha_hash, nome, tipo_usuario, ativo) overriding system value values
    (101, 1, 'compras.demo.analista', 'compras.demo.analista@demo.local', senha_hash, 'Analista de Compras (Demo Fictícia)', 'TENANT_USER', true),
    (102, 1, 'compras.demo.gestor', 'compras.demo.gestor@demo.local', senha_hash, 'Gestor de Compras (Demo Fictícia)', 'TENANT_USER', true)
    on conflict (id) do nothing;

  -- A criação de sessão persistente exige entidade_id resolvido; os usuários
  -- demo referenciam a entidade institucional municipal do núcleo 1.
  update sigov.usuario
     set entidade_id = 9101
   where id in (101, 102) and tenant_id = 1;

  -- Perfis demo (nivel derivado de codigo_externo garante papel no login).
  insert into sigov.perfil_acesso(id, tenant_id, nome, codigo_externo) overriding system value values
    (9001, 1, 'Analista de Compras (Demo Fictícia)', 'SERVIDOR'),
    (9002, 1, 'Gestor de Compras (Demo Fictícia)', 'COORDENADOR')
    on conflict (id) do nothing;

  insert into sigov.grupo_acesso(id, tenant_id, nome, codigo_externo) overriding system value values
    (9301, 1, 'Grupo Demo Analista de Compras', 'demo.compras.analista'),
    (9302, 1, 'Grupo Demo Gestor de Compras', 'demo.compras.gestor')
    on conflict (id) do nothing;

  insert into sigov.grupo_perfil(grupo_acesso_id, perfil_acesso_id, tenant_id) values
    (9301, 9001, 1),
    (9302, 9002, 1)
    on conflict (grupo_acesso_id, perfil_acesso_id) do nothing;

  insert into sigov.usuario_grupo(usuario_id, grupo_acesso_id, tenant_id) values
    (101, 9301, 1),
    (102, 9302, 1)
    on conflict (usuario_id, grupo_acesso_id) do nothing;

  -- Permissões do módulo (fonte: banco; alçada vive na linha de concessão).
  -- Analista: navega, cria/edita/envia requisições, vê fila/relatórios/configura
  -- e aprova até 50.000,00. Gestor: vê a fila e aprova até 250.000,00.
  insert into sigov.perfil_permissao(perfil_acesso_id, permissao_id, tenant_id, efeito, alcada_valor) values
    (9001, p_dashboard, 1, 'PERMITIR', null),
    (9001, p_requisicoes_visualizar, 1, 'PERMITIR', null),
    (9001, p_requisicoes_criar, 1, 'PERMITIR', null),
    (9001, p_requisicoes_editar, 1, 'PERMITIR', null),
    (9001, p_requisicoes_enviar, 1, 'PERMITIR', null),
    (9001, p_aprovacoes_visualizar, 1, 'PERMITIR', null),
    (9001, p_aprovacoes_aprovar, 1, 'PERMITIR', 50000.00),
    (9001, p_relatorios, 1, 'PERMITIR', null),
    (9001, p_configuracao, 1, 'PERMITIR', null),
    (9002, p_aprovacoes_visualizar, 1, 'PERMITIR', null),
    (9002, p_aprovacoes_aprovar, 1, 'PERMITIR', 250000.00)
    on conflict (perfil_acesso_id, permissao_id) do nothing;

  -- Técnicos demo no tenant empresarial municipal (responsáveis de devolução).
  insert into sigov.os_tecnico(id, tenant_id, usuario_id, nome, especialidade, status, created_by, updated_by) values
    ('c0000001-0000-4000-8000-000000000101', tenant_municipal, sub_analista, 'Técnico Fictício Um', 'Compras e Suprimentos', 'DISPONIVEL', 'seed', 'seed'),
    ('c0000001-0000-4000-8000-000000000102', tenant_municipal, sub_gestor, 'Técnico Fictício Dois', 'Gestão de Contratos', 'DISPONIVEL', 'seed', 'seed')
    on conflict (id) do nothing;

  -- Requisições demo em rascunho (UUIDs determinísticos) para percorrer a
  -- jornada: bloqueio sem política, aprovação em alçadas, devolução com novo
  -- ciclo e rejeição. Totais recalculados pelo servidor a partir dos itens.
  insert into sigov.compras_empresarial_requisicao(id, tenant_id, numero, solicitante_id, justificativa, valor_estimado, status, created_by, updated_by, correlation_id) values
    ('d0000001-0000-4000-8000-000000000001', tenant_municipal, 'RC-DEMO-0001', sub_analista, 'Requisição fictícia para validação da aprovação em duas alçadas (80 mil).', 80000.00, 'RASCUNHO', 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000002', tenant_municipal, 'RC-DEMO-0002', sub_analista, 'Requisição fictícia enviada antes da política ativa para evidenciar ciclo bloqueado (40 mil).', 40000.00, 'RASCUNHO', 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000003', tenant_municipal, 'RC-DEMO-0003', sub_analista, 'Requisição fictícia para validação de devolução, correção e novo ciclo (60 mil).', 60000.00, 'RASCUNHO', 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000004', tenant_municipal, 'RC-DEMO-0004', sub_analista, 'Requisição fictícia para validação de rejeição em alçada superior (70 mil).', 70000.00, 'RASCUNHO', 'seed', 'seed', 'seed-compras-aprovacoes')
    on conflict (id) do nothing;

  insert into sigov.compras_empresarial_requisicao_item(id, tenant_id, requisicao_id, ordem, tipo, descricao, unidade, quantidade, valor_estimado, created_by, updated_by, correlation_id) values
    ('d0000001-0000-4000-8000-000000000101', tenant_municipal, 'd0000001-0000-4000-8000-000000000001', 1, 'MATERIAL', 'Material de escritório fictício', 'UN', 10, 3000.00, 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000102', tenant_municipal, 'd0000001-0000-4000-8000-000000000001', 2, 'SERVICO', 'Serviço de suporte fictício', 'HORA', 5, 10000.00, 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000201', tenant_municipal, 'd0000001-0000-4000-8000-000000000002', 1, 'MATERIAL', 'Material didático fictício', 'UN', 8, 5000.00, 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000301', tenant_municipal, 'd0000001-0000-4000-8000-000000000003', 1, 'MATERIAL', 'Equipamento de laboratório fictício', 'UN', 12, 5000.00, 'seed', 'seed', 'seed-compras-aprovacoes'),
    ('d0000001-0000-4000-8000-000000000401', tenant_municipal, 'd0000001-0000-4000-8000-000000000004', 1, 'SERVICO', 'Serviço de consultoria fictício', 'HORA', 7, 10000.00, 'seed', 'seed', 'seed-compras-aprovacoes')
    on conflict (id) do nothing;
end $$;
