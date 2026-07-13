# Manual do Administrador Pós-RC 11

Documento de consolidação final para homologação comercial.

## Escopo operacional

- Usar tenant real resolvido por sessão/header; fallback demo somente fora de produção e identificado.
- Manter Protocolo, GED, Enterprise, Dashboard, Busca, Relatórios, API Key e Outbox integrados.
- Não expor CPF/CNPJ, e-mail, telefone, tokens, secrets ou paths completos sem permissão.
- Toda ação crítica deve confirmar, chamar rota real, auditar e informar falha parcial quando houver.

## Roteiro de validação

1. Login administrativo e usuário comum.
2. Abrir Minha Central, Dashboard, Busca e Relatórios.
3. Executar CRUD principal Enterprise com criar, editar, inativar, restaurar e CSV.
4. Validar ações operacionais por status.
5. Validar importação por prévia CSV e rejeição de colunas inválidas.
6. Validar lote sem seleção, lote parcial e auditoria.
7. Validar anexos GED/Enterprise com documento restrito bloqueado sem permissão.
8. Registrar evidência em smoke e checklist.

## Evidência esperada

- Build/test/smoke/go-live com data, comando e resultado.
- Prints ou logs sem dados sensíveis completos.
- Pendências classificadas como runtime, provedor ou evolução.
