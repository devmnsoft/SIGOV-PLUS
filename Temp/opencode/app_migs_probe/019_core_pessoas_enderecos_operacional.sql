-- Etapa 2 - Pessoa e Endereço: reforços operacionais, índices e permissões granulares.
create unique index if not exists ux_pessoa_tenant_documento_ativo
    on sigov.pessoa (tenant_id, documento)
    where documento is not null and is_deleted = false;

create index if not exists idx_pessoa_tenant_nome
    on sigov.pessoa (tenant_id, nome)
    where is_deleted = false;

create index if not exists idx_pessoa_tenant_tipo
    on sigov.pessoa (tenant_id, tipo_pessoa)
    where is_deleted = false;

create index if not exists idx_endereco_tenant_pessoa
    on sigov.endereco (tenant_id, pessoa_id)
    where is_deleted = false;

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
    ('core', 'pessoas', 'visualizar', 'core.pessoas.visualizar', 'Visualizar pessoas e endereços', true),
    ('core', 'pessoas', 'criar', 'core.pessoas.criar', 'Criar pessoas', true),
    ('core', 'pessoas', 'editar', 'core.pessoas.editar', 'Editar pessoas', true),
    ('core', 'pessoas', 'excluir', 'core.pessoas.excluir', 'Excluir pessoas', true),
    ('core', 'enderecos', 'gerenciar', 'core.enderecos.gerenciar', 'Gerenciar endereços de pessoas', true),
    ('core', 'pessoas', 'exportar', 'core.exportar', 'Exportar cadastro de pessoas', true)
on conflict do nothing;
