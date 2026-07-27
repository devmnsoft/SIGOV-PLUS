# Triagem de migrations

A migration transversal agora cria índices somente quando tabela e todas as colunas escalares existem. O checksum anterior permanece em `knownChecksums`, e o contrato final foi isolado na migration aditiva `20260727160000_pos_rc_27b3_operacional_canonico.sql`, sem `DROP COLUMN`.
