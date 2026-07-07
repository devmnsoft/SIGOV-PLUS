# Pós-RC SIGOV PLUS

Documento operacional da homologação Pós-RC.

- Funcional real: schema tenant-aware, auditoria, correlação, LGPD e fallback honesto.
- Parcial: telas/APIs que ainda dependem de conexão de serviço devem declarar indisponibilidade sem simular persistência.
- Dependente de provedor/configuração: assinatura oficial, OCR e entregas HTTP externas.
- Não disponível: aprovação automática sem evidência real ou exposição de dados sensíveis sem máscara.

## Pós-RC 02 — persistência real operacional

- Funcional real: API v1 com API key/tenant/escopos, Protocolo e GED persistindo nas tabelas Pós-RC, Outbox worker consumindo sigov.outbox_evento.
- Parcial: telas MVC administrativas continuam com fallback honesto quando ação/formulário não possui todos os dados reais.
- Dependente de provedor: OCR, ICP/Gov.br e entrega externa oficial de webhooks.
- LGPD: respostas e logs não devem expor dados pessoais completos nem token claro.

