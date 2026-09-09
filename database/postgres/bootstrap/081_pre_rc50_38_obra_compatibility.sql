-- Compatibilidade forward-only para a tabela sigov.obra anterior à RC50.38.
-- A consolidação de módulos transversais já publicava a relação com um
-- contrato reduzido. A RC50.38 usa CREATE TABLE IF NOT EXISTS e, em seguida,
-- cria um índice sobre codigo sem garantir a coluna no caminho legado.
-- Mantê-la anulável preserva os registros existentes e a semântica do índice
-- parcial publicado, que considera apenas códigos informados.
alter table if exists sigov.obra
    add column if not exists codigo varchar(80);
