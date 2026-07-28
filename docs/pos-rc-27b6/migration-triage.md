# Triagem de migrations

A migration `20260721120000` agora descobre colunas por `information_schema` e monta o backfill dinamicamente. O checksum anteriormente publicado foi preservado em `knownChecksums` e o manifest valida a nulabilidade canônica por `postConditionSql`.
