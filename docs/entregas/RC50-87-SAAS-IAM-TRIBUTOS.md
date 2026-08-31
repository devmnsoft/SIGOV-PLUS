# Entrega RC50.87 — SaaS, IAM e Tributos

## Entregue

- Migration idempotente com clientes SaaS multi-esfera, planos, contratação, cobranças, bloqueios e auditoria.
- IAM por tenant com documento protegido, evento de login sanitizado, perfis, permissões e sessões revogáveis.
- Núcleo tributário contextualizado e controles de lançamento, pagamento, baixa, parcelamento, dívida ativa e certidão.
- Catálogo mínimo de 30 permissões MNSOFT, cliente e Tributos.
- Baselines SQL e manifesto sincronizados; documentação operacional e limites atualizados.

## Integrações reais e limites

Não foi simulada integração bancária, cartorial ou judicial. A RC reutiliza as superfícies SaaS e tributárias já conectadas a Dapper; novos aliases de navegação e fluxos completos serão liberados quando conectados ao mesmo avaliador persistido, sem tela meramente decorativa.

## Validação e bloqueios

Os comandos executados e seus resultados constam no relatório do commit/PR. Validação contra PostgreSQL real fica condicionada a `ConnectionStrings__DefaultConnection`. BASE LOCAL utilizada porque `origin/main` não estava disponível.

## Pendências conhecidas registradas

- As telas legadas `SaasConfiguracao/Modulos` e `SaasConfiguracao/Parametros` ainda solicitam `tenant_id`; sua correção exige um seletor autorizado abastecido pelo repositório e não pode ser substituída por catálogo hardcoded.
- Os cadastros tributários especializados não consolidados nesta migration permanecem nas estruturas legadas; a convergência deve ser corretiva, aditiva e acompanhada de serviços Dapper transacionais.
- Build, smoke HTTP e aplicação PostgreSQL não foram executados neste contêiner por ausência, respectivamente, do SDK `dotnet`, de aplicação compilável e de cliente/servidor `psql` configurado.
