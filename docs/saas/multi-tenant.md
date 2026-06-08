# PlantãoPro — multi-tenant

## Implementado nesta rodada
- Migration incremental `database/postgres/migrations/20260608120000_plantao_pro_white_label_b2b_launch.sql` com estrutura B2B, white label, planos, self-service, API keys, contratos, SLA, suporte, beta, go-to-market, telemetria e compatibilidade com schema `plantaopro`.
- Serviço `IWhiteLabelB2BLaunchService` registrado no DI com operações transacionais, auditoria por evento e validações básicas.
- APIs B2B retornando `ApiResponse<T>` para planos, self-service, white label, developer portal, API keys, assinatura, contratos, suporte, monitoramento, beta e go-to-market.
- Views MVC reais para planos públicos, cadastro self-service, white label, developer portal, assinatura, contratos, beta, parceiros, monitoramento e go-to-market.

## Fluxo operacional
1. Cliente acessa Planos Públicos e compara os limites comerciais.
2. Cliente inicia o cadastro em SelfService, aceita termos/LGPD e escolhe o plano.
3. A solicitação registra aceite, status e evento de telemetria para onboarding/provisionamento.
4. Admin cliente configura white label, publica e usa Developer Portal/API keys conforme plano.
5. Contratos/SLA, suporte B2B, beta e monitoramento ficam rastreáveis no sistema.

## Pendências reais
- Executar migration em ambiente PostgreSQL de homologação.
- Conectar provisionamento automático completo do tenant/usuário admin ao fluxo self-service quando o ambiente de banco estiver disponível.
- Validar upload físico de imagens com armazenamento definitivo definido por ambiente.
