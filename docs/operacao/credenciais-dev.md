# Credenciais exclusivamente de desenvolvimento

Somente no ambiente local provisionado por `script_completo_dev.sql`:

- `admin` / `SigovDevLocal!2026`
- `superadmin` / `SigovSuperAdmin!2026`

Nunca reutilize essas senhas em homologação ou produção. Configure segredos por variáveis/secret store e não registre a connection string completa em logs ou artefatos.
