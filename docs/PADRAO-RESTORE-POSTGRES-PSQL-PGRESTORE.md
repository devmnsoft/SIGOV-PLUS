# Padrão de restauração PostgreSQL

O baseline do SIGOV PLUS é SQL plain, transacional, não destrutivo e destinado ao PostgreSQL 16+. Restaure `SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql` com `psql` (ou Query Tool do pgAdmin). Arquivos custom `.backup`, quando produzidos após restauração homologada, são restaurados com `pg_restore` (ou Restore do pgAdmin).

```bash
psql -h localhost -p 5432 -U postgres -d sigov_plus -f database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql
pg_restore -h localhost -p 5432 -U postgres -d sigov_plus --verbose database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.backup
```

A mensagem `input file does not appear to be a valid archive` indica, em geral, que SQL plain foi fornecido ao `pg_restore`. Não renomeie formatos. Use `ON_ERROR_STOP=1`, credenciais externas ao repositório e valide tabelas, módulos, permissões, parâmetros e o administrador local após a carga.
