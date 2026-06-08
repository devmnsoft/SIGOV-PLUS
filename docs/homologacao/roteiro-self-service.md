# Roteiro de homologação — self-service PlantãoPro B2B

1. Abrir `/PlanosPublicos`.
2. Abrir `/PlanosPublicos/Comparar`.
3. Abrir `/SelfService`.
4. Preencher razão social, CNPJ, responsável, e-mail, plano e aceites.
5. Confirmar toast de cadastro recebido.
6. Validar registros em `sigov.b2b_cadastro_cliente_solicitacoes` e `sigov.b2b_cadastro_cliente_aceites`.
7. Validar evento `SELF_SERVICE_CADASTRO_SOLICITADO` em `sigov.b2b_telemetria_eventos`.

## Pendências reais
- Homologar com PostgreSQL ativo e usuário de e-mail real para convite do admin cliente.
