# Gaps de schema — Pós-RC

## Resolvidos por migration não destrutiva
- API key, escopos e log de requisição para segurança real da API v1.
- Webhook configurável, entrega e outbox para processamento assíncrono.
- Protocolo, GED, workflow, tarefas, notificações e validação pública com `tenant_id`, auditoria e índices.

## Ainda dependente de implementação/provedor
- Assinatura oficial ICP/Gov.br: dependente de provedor real, sem simulação.
- OCR: dependente de provedor real, sem simulação de resultado oficial.
- PDF/DOCX avançado de POC: usar apenas infraestrutura real disponível.

## Fallback honesto obrigatório
Quando a tabela/provedor não estiver disponível em uma implantação legada, telas e APIs devem informar indisponibilidade operacional e não gravar dados fictícios.

## Pós-RC 02 — persistência real operacional

- Funcional real: API v1 com API key/tenant/escopos, Protocolo e GED persistindo nas tabelas Pós-RC, Outbox worker consumindo sigov.outbox_evento.
- Parcial: telas MVC administrativas continuam com fallback honesto quando ação/formulário não possui todos os dados reais.
- Dependente de provedor: OCR, ICP/Gov.br e entrega externa oficial de webhooks.
- LGPD: respostas e logs não devem expor dados pessoais completos nem token claro.

