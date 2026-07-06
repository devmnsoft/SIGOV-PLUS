# LGPD — API, Mobile e Webhooks

## Princípios
Minimização, finalidade, necessidade, transparência, segurança e prestação de contas.

## API
CPF/CNPJ completos não são retornados sem base legal e permissão explícita. Respostas públicas usam dados mínimos e mascarados.

## Mobile/offline
Payload mobile não deve carregar dados sensíveis desnecessários. Evidências exigem finalidade, retenção e storage controlado.

## Webhooks
Webhooks enviam evento, ids técnicos, status e links controlados, nunca dossiês completos com dados pessoais.

## Logs/outbox
Tokens, secrets e documentos pessoais não devem ser salvos em logs/outbox. Auditoria registra uso de dados pessoais com tenant, usuário/API key e finalidade.

## Dados sensíveis
Saúde, assistência social e dados de menores exigem proteção reforçada, acesso mínimo e revisão de retenção.
