# Backup, restore e rollback

## Antes de migration
Exporte `PGPASSWORD` apenas na sessão e configure `SIGOV_DB_HOST`, `SIGOV_DB_PORT`, `SIGOV_DB_NAME=postgres`, `SIGOV_DB_USER` e `SIGOV_DB_SCHEMA=sigov`. Execute `./scripts/db/backup-sigov.sh`; guarde o `.dump`, hash, horário e operador em storage cifrado. Não registre senha.

## Ensaio de restore
Crie um **banco separado**, vazio e de acesso restrito (por exemplo `sigov_restore_20260819`). Aponte `SIGOV_DB_NAME` para ele, execute `restore-sigov.sh BACKUP.dump` e depois `verify-restore-sigov.sh`. Compare contagens críticas, última migration e login de homologação. PowerShell possui comandos equivalentes.

## Falha e rollback
1. interrompa deploy/workers e preserve logs, correlation id, SQLSTATE, migration e checksum;
2. não reaplique às cegas nem altere `schema_migrations`;
3. reverta a aplicação para o artefato imutável anterior;
4. se a mudança de banco for incompatível, restaure o dump em infraestrutura separada, valide e faça cutover aprovado;
5. reabra tráfego gradualmente e execute smoke.

Nunca drope schema em produção, marque migration manualmente, edite `schema_migrations` sem aprovação ou rode SQL destrutivo sem backup testado.
