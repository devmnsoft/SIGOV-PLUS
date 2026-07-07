# Checklist Segurança e LGPD — Pós-RC 05

| Item | Status esperado | Evidência |
|---|---|---|
| API key em logs | Não deve aparecer completa | Revisar logs API/Worker/Web e smoke mascarado |
| API key em banco | Não salvar texto claro | Validar hash/metadata, nunca segredo completo |
| Webhook secret | Não exibir após criação | Validar tela/API de webhooks |
| CPF/CNPJ/e-mail/telefone | Mascarar em busca/relatórios públicos | Validar Busca Global e CSV |
| `storage_path` | Não expor em CSV/API pública | Validar relatórios e endpoints públicos |
| Documento restrito | Bloquear sem permissão | Teste usuário comum |
| `tenant_id` | Aplicar em consultas | Revisar SQL e testes multi-tenant |
| Permissões | Aplicar botões/actions/API | Teste usuário comum/admin |
| Erros | Não exibir stacktrace ao usuário | Testar erro controlado em Production |
| Swagger Production | Proteger/desabilitar | `SwaggerEnabledInProduction=false` |
| CORS Production | Não abrir `*` | Revisar configuração Production |
| Rate limit API v1 | Ativo | Testes de rate limit/API |
| Headers de segurança | Ativos | Testes de headers |
| CorrelationId | Presente em logs | Revisar logs com requisição smoke |

## Pendências honestas

A validação local automática não foi executada neste container por ausência de `dotnet`, `docker` e `pwsh`. A verificação real deve ocorrer no GitHub Actions e no ambiente de homologação.
