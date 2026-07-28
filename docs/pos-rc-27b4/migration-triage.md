# Triagem de migrations

A migration `20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql` criava diretamente `ix_protocolo_movimento_status` sobre `sigov.protocolo_movimento(status)`. Schemas históricos podem não possuir essa coluna. Todos os 76 índices da migration agora passam pelo helper temporário, que verifica tabela e todas as colunas, emite `NOTICE` ao ignorar e não inventa colunas. O checksum anterior foi preservado em `knownChecksums` e uma pós-condição booleana valida objetos essenciais.
