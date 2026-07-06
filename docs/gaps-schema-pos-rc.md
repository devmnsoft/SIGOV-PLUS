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
