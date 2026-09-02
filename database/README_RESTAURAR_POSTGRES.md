# Restaurar a base PostgreSQL completa — RC50.97

> **Somente local/desenvolvimento.** O pacote contém dados institucionais fictícios e uma credencial inicial que deve ser trocada no primeiro acesso. Não aplique os seeds em produção.

## Requisitos

PostgreSQL 16 ou superior e banco vazio em UTF-8. A aplicação usa `ConnectionStrings__DefaultConnection`.

Arquivos `.sql` são scripts em texto e devem ser executados com `psql` ou pelo Query Tool do pgAdmin. Arquivos `.backup` em formato custom devem ser restaurados com `pg_restore`; não use `pg_restore` para um arquivo `.sql`.

## psql

```bash
createdb -U postgres sigov_plus
psql -h localhost -p 5432 -U postgres -d sigov_plus -f database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql
```

No Windows, usando o runtime distribuído com o pgAdmin 4:

```powershell
"C:\Program Files\PostgreSQL\18\pgAdmin 4\runtime\psql.exe" --host localhost --port 5432 --username postgres --dbname sigov_plus --file "C:\MNSOFT\SIGOV-PLUS\database\SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql"
```

Para aplicar apenas os dados locais sobre uma base estrutural já atualizada:

```bash
psql -v ON_ERROR_STOP=1 -U postgres -d sigov_plus -f database/SIGOV_PLUS_PARAMETROS_SEEDS.sql
```

## pgAdmin

Crie um banco UTF-8 vazio, abra **Query Tool**, carregue `SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql` e execute todo o arquivo. Este é SQL plain; não use a tela de `Restore` destinada a formatos custom/tar.

## pg_restore

Se aparecer `pg_restore: input file does not appear to be a valid archive`, foi selecionado um arquivo `.sql`. Use `psql` ou o Query Tool do pgAdmin para SQL plain; use `pg_restore` ou **Restore** do pgAdmin exclusivamente para o arquivo `.backup` custom.


O artefato `.backup` somente existe quando `pg_dump` está instalado e há uma instância temporária validada. Quando disponível:

```bash
pg_restore -h localhost -p 5432 -U postgres -d sigov_plus --verbose database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.backup
```

No Windows:

```powershell
"C:\Program Files\PostgreSQL\18\pgAdmin 4\runtime\pg_restore.exe" --host localhost --port 5432 --username postgres --dbname sigov_plus --verbose "C:\MNSOFT\SIGOV-PLUS\database\SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.backup"
```

## Acesso local/dev

* Login/e-mail: `superadmin@mnsoft.local`
* A senha inicial não é documentada nem armazenada nos scripts de restauração; defina-a por um fluxo local seguro e force a troca no primeiro acesso.

A senha consta no banco somente como hash PBKDF2-SHA256, no formato real `SIGOV_PBKDF2_V1`, com 100.000 iterações. O pacote inclui tenants fictícios municipal, estadual e federal, planos SaaS, perfis base e associa todas as permissões persistidas ao perfil de Super Administrador.

## Smoke de restauração

```sql
select count(*) from information_schema.tables where table_schema='sigov';
select count(*) from sigov.modulo_saas where ativo and not is_deleted;
select count(*) from sigov.permissao where ativo and not is_deleted;
select slug, ambiente from sigov.tenant order by slug;
select email, deve_alterar_senha from sigov.usuario where email='superadmin@mnsoft.local';
select conname from pg_constraint where conname='ck_entidade_esfera_governo';
```

A restauração transacional dos seeds falha explicitamente se o schema oficial não estiver presente. O arquivo não elimina objetos existentes e usa `ON CONFLICT`/`NOT EXISTS` para reexecução segura.

## Automação

Os scripts `restore-sigov-plus.sh` e `restore-sigov-plus.ps1` aceitam as variáveis `PGHOST`, `PGPORT`, `PGUSER` e `PGDATABASE`, interrompem no primeiro erro e executam consultas mínimas de integridade. A senha do PostgreSQL deve vir de mecanismo seguro do cliente (por exemplo, prompt ou arquivo de senhas), nunca do script.
