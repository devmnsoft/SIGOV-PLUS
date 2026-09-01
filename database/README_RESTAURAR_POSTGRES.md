# Restaurar a base PostgreSQL completa — RC50.95

> **Somente local/desenvolvimento.** O pacote contém dados institucionais fictícios e uma credencial inicial que deve ser trocada no primeiro acesso. Não aplique os seeds em produção.

## Requisitos

PostgreSQL 16 ou superior e banco vazio em UTF-8. A aplicação usa `ConnectionStrings__DefaultConnection`.

## psql

```bash
createdb -U postgres sigov_plus
psql -v ON_ERROR_STOP=1 -U postgres -d sigov_plus -f database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql
```

Para aplicar apenas os dados locais sobre uma base estrutural já atualizada:

```bash
psql -v ON_ERROR_STOP=1 -U postgres -d sigov_plus -f database/SIGOV_PLUS_PARAMETROS_SEEDS.sql
```

## pgAdmin

Crie um banco UTF-8 vazio, abra **Query Tool**, carregue `SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql` e execute todo o arquivo. Este é SQL plain; não use a tela de `Restore` destinada a formatos custom/tar.

## pg_restore

O artefato `.backup` somente existe quando `pg_dump` está instalado e há uma instância temporária validada. Quando disponível:

```bash
pg_restore -U postgres -d sigov_plus database/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.backup
```

## Acesso local/dev

* Login/e-mail: `superadmin@mnsoft.local`
* Senha inicial: `Mns@2026!Trocar`
* Uso: exclusivamente local/dev; troca obrigatória no primeiro acesso.

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
