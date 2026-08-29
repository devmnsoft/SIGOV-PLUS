# EXP25 — GED360 / InovaGED Inteligente

## Entrega

- Modelo PostgreSQL idempotente para ciclo de vida digital e físico, protocolo, OCR, workflow, temporalidade, eliminação, LGPD e integração.
- 22 permissões persistidas e índices de contexto, busca, hash e idempotência.
- Dashboard responsivo com doze indicadores reais e navegação operacional.
- Listagem e busca full-text de documentos com filtros de status e sigilo.
- Rotas operacionais com estados vazios explícitos quando não existem registros.
- Scripts consolidados e manifest sincronizados pelo SHA-256 da migration.

## Critérios operacionais

OCR e assinatura não são simulados: filas e solicitações continuam rastreáveis enquanto os provedores não estiverem configurados. Eliminação física permanece bloqueada sem regra e auditoria. Vínculos documentais suportam módulos reais sem duplicar o documento.

## Dependências bloqueadas neste ambiente

`dotnet` e um PostgreSQL de validação precisam existir no ambiente de CI/homologação para build, aplicação transacional e smoke HTTP autenticado.
