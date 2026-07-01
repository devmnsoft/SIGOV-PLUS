# Schema report local — Sprint SaaS comercial

A implementação consulta o schema em runtime com `IDatabaseSchemaInspector` antes de usar:

- `sigov.plano_saas`
- `sigov.assinatura_saas`
- `sigov.tenant`
- `sigov.tenant_modulo_contratado`
- `sigov.modulo_saas`
- `sigov.parametro_sistema`
- `sigov.notificacao`
- `sigov.usuario`

Quando tabela/coluna mínima não existe, a tela exibe fallback honesto e não simula gravação.
