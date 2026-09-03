-- Correção aditiva para instalações que aplicaram a RC50.85 antes das correções
-- de tipo das FKs de fornecedor e do índice de fiscalização contratual.
-- A migration publicada 20260831230000 e o seu histórico permanecem imutáveis.

DO $$
DECLARE
    tabela text;
    tipo_fornecedor text;
BEGIN
    IF to_regclass('sigov.compras_fornecedor') IS NULL THEN
        RAISE EXCEPTION 'Schema de Compras incompleto: sigov.compras_fornecedor não existe';
    END IF;

    SELECT a.atttypid::regtype::text
      INTO tipo_fornecedor
      FROM pg_attribute a
     WHERE a.attrelid = 'sigov.compras_fornecedor'::regclass
       AND a.attname = 'id'
       AND NOT a.attisdropped;

    IF tipo_fornecedor <> 'uuid' THEN
        RAISE EXCEPTION 'Schema de Compras incompatível: compras_fornecedor.id possui tipo %, esperado uuid', tipo_fornecedor;
    END IF;

    FOREACH tabela IN ARRAY ARRAY[
        'compras_fornecedor_cotacao', 'compras_proposta', 'compras_habilitacao',
        'compras_recurso', 'contrato_administrativo', 'contrato_sancao'
    ] LOOP
        IF to_regclass('sigov.' || tabela) IS NULL THEN
            RAISE EXCEPTION 'Schema de Compras incompleto: sigov.% não existe', tabela;
        END IF;

        IF NOT EXISTS (
            SELECT 1
              FROM pg_attribute a
             WHERE a.attrelid = to_regclass('sigov.' || tabela)
               AND a.attname = 'fornecedor_id'
               AND a.atttypid = 'uuid'::regtype
               AND NOT a.attisdropped
        ) THEN
            RAISE EXCEPTION 'Schema de Compras incompatível: sigov.%.fornecedor_id não é uuid', tabela;
        END IF;

        IF NOT EXISTS (
            SELECT 1
              FROM pg_constraint c
             WHERE c.conrelid = to_regclass('sigov.' || tabela)
               AND c.contype = 'f'
               AND c.confrelid = 'sigov.compras_fornecedor'::regclass
               AND c.conkey = ARRAY[(
                   SELECT a.attnum
                     FROM pg_attribute a
                    WHERE a.attrelid = to_regclass('sigov.' || tabela)
                      AND a.attname = 'fornecedor_id'
                      AND NOT a.attisdropped
               )]::smallint[]
               AND c.confkey = ARRAY[(
                   SELECT a.attnum
                     FROM pg_attribute a
                    WHERE a.attrelid = 'sigov.compras_fornecedor'::regclass
                      AND a.attname = 'id'
                      AND NOT a.attisdropped
               )]::smallint[]
               AND c.convalidated
        ) THEN
            RAISE EXCEPTION 'FK de fornecedor ausente ou não validada em sigov.%', tabela;
        END IF;
    END LOOP;
END $$;

-- A definição legada de contrato_fiscal antecede a coluna que representa a
-- vigência da designação no domínio de Compras. A RC50.85 usa CREATE TABLE IF
-- NOT EXISTS e, portanto, não acrescenta a coluna em instalações já existentes.
ALTER TABLE sigov.contrato_fiscal
    ADD COLUMN IF NOT EXISTS ativo boolean NOT NULL DEFAULT true;

CREATE INDEX IF NOT EXISTS ix_contrato_fiscal_ativo
    ON sigov.contrato_fiscal (tenant_id, contrato_id, ativo);
