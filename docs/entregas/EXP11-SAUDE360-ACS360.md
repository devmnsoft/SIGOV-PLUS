# EXP11 — Saúde360 + ACS360

## Entrega

- Migration `20260829120000` aditiva e idempotente, com tabelas territoriais, sincronização, cadastros, produção, campo, e-SUS e vigilâncias.
- Permissões Saúde/ACS/Vigilâncias persistidas sem concessão automática.
- Rotas MVC/Razor Saúde360/ACS360 conectadas a APIs reais existentes.
- Formulários de domicílio, indivíduo e visita com antiforgery, resumo/validação por campo, lookups canônicos e GPS validado.
- Scripts completos e manifest sincronizados.

## Validação e bloqueios

Comandos previstos: `dotnet build sigov.sln --no-restore`, validação JSON do manifest, checksums, comparação dos blocos SQL e buscas estáticas de segurança. **BLOCKED:** o SDK `dotnet` não está instalado no ambiente. **BLOCKED:** validação executável PostgreSQL não é possível sem `psql`/instância configurada. **BLOCKED:** transmissão e-SUS/SISAB depende de layout oficial e credenciais ausentes.
