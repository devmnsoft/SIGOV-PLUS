# Fechamento FUNC20

## Entrega
- Migration idempotente, manifest e cinco scripts consolidados sincronizados.
- Repositório Dapper/Npgsql, contratos, controller MVC, ViewModels, dashboard, CRUDs, navegação, auditoria e nove famílias de CSV.
- 23 permissões persistidas e aplicadas por policy.
- Formulários responsivos com ValidationSummary, mensagens por campo, antiforgery e relacionamentos exclusivamente por dropdown.

## Validações
Instrumentos conciliam valores e vigência; projetos, metas e etapas controlam datas, conclusão e execução; financeiro exige valores positivos e pagamento datado; prestações controlam período, envio e aprovação; diligências controlam prazo e resposta. Constraints PostgreSQL repetem as invariantes críticas.

## Comandos e bloqueios
Os comandos finais (`dotnet restore`, `dotnet build`, validação JSON, validação PostgreSQL e smoke) são registrados no PR. A aplicação local da migration fica `BLOCKED` quando não houver servidor PostgreSQL configurado em `ConnectionStrings__DefaultConnection`; não são usadas credenciais ou bancos substitutos.
