# RC50.95 — build e base PostgreSQL restaurável

A correção removeu inicializador de coleção aplicado indevidamente ao retorno de `ToDictionary`, isolou a expressão `switch` usada como argumento no LicitaPro e reestruturou o Razor do dashboard de Frotas para emissão inequívoca pelo gerador.

A base plain agrega o baseline oficial, a migration corretiva `20260901210000` e seeds locais idempotentes. Foram adicionados contexto multi-esfera à entidade, planos, quatro tenants institucionais fictícios, perfis e Super Administrador local com hash PBKDF2 e troca obrigatória.

## Validação e bloqueios

* `dotnet build`: **BLOCKED**, executável `dotnet` ausente no ambiente.
* `psql`: **BLOCKED**, cliente PostgreSQL ausente no ambiente; não foi possível restaurar banco temporário.
* `pg_dump`: **BLOCKED**, utilitário ausente; o `.backup` não foi gerado.
* JSON do manifest e checksums SHA-256 foram validados localmente.
