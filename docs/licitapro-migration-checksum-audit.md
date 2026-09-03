# Auditoria do checksum da migration 20260903130000

A normalização oficial remove o BOM inicial, converte CRLF e CR para LF e calcula SHA-256 sobre UTF-8.

- `d9c59ec` (`CORR: corrigir pós-condições finais do LicitaPro`) contém a primeira versão legítima do arquivo, com 4.687 bytes normalizados e checksum `2ee4b77413f755230ad1bdaef456893c1f5f045866ea436e78d388a0b4f18364`.
- `096c4e6` (`CORR: alinhar pós-condições finais do LicitaPro`) substituiu o conteúdo pelo arquivo atual, com 1.959 bytes normalizados e checksum `c237332d2878958e55a6a535208c77ded73521be5c805a52e06a01493b347a6b`.

A origem foi recuperada com `git log --all -- <arquivo>`, `git reflog --all` e `git show <commit>:<arquivo>`. O primeiro valor é aceito apenas como `knownChecksums`; o runner não reexecuta a migration publicada nem altera o valor armazenado em `sigov.schema_migrations`. A reparação do estado físico pertence exclusivamente à migration aditiva `20260903173000_corr_licitapro_schema_history.sql`.
