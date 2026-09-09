-- Compatibilidade forward-only para o catálogo canônico de permissões.
-- Desde a POS-RC36B, `chave` é a identidade natural e possui índice único.
-- A restrição histórica (modulo,recurso,acao) impede chaves distintas para a
-- mesma ação contextual (por exemplo, seletor/unidade/exercício visualizar).
alter table if exists sigov.permissao
    drop constraint if exists uk_sigov_permissao_modulo_recurso_acao;
