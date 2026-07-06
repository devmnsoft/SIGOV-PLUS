# SIGOV PLUS — API, Mobile/Offline, Assinatura, BI avançado e Integrações

## Escopo da sprint
Esta sprint profissionaliza a camada externa dos fluxos já existentes do SIGOV PLUS, sem criar módulos aleatórios e sem simular integrações oficiais. O foco é expor endpoints versionados, governar API keys por tenant, preparar webhooks, mobile/offline, assinatura eletrônica simples, validação pública, BI operacional, observabilidade, segurança e LGPD.

## Endpoints criados
- `GET/POST /api/v1/protocolos`, `GET /api/v1/protocolos/{id}`, `POST /api/v1/protocolos/{id}/tramitar`.
- `GET/POST /api/v1/documentos`, `GET /api/v1/documentos/{id}`.
- `GET/POST /api/v1/tarefas`, `POST /api/v1/tarefas/{id}/concluir`.
- `GET /api/v1/notificacoes`, `POST /api/v1/notificacoes/{id}/marcar-lida`.
- `GET /api/v1/fluxos`, `GET /api/v1/fluxos/{id}`.
- `POST /api/v1/mobile/sync/pull`, `POST /api/v1/mobile/sync/push`, `POST /api/v1/mobile/evidencias`, `GET /api/v1/mobile/roteiros`, `GET /api/v1/mobile/tarefas`.
- `GET/POST /api/v1/assinaturas`, `GET /api/v1/assinaturas/{id}`.
- `GET /api/v1/bi/indicadores`.

## Eventos e webhooks
Eventos suportados: `protocolo.criado`, `protocolo.tramitado`, `documento.criado`, `documento.assinado`, `tarefa.criada`, `tarefa.concluida`, `contrato.criado`, `obra.medicao_registrada`, `manifestacao.recebida`, `chamado.aberto`, `sla.vencido`.

Webhooks administrativos ficam em `/Integracoes/Webhooks`, com secret por configuração, HMAC SHA-256 quando possível, status de entrega, retry e logs. Payloads devem ser mínimos e sem dados pessoais completos.

## API keys
API keys são planejadas por `tenant_id`, integração e escopos. O token completo só pode aparecer na criação/rotação; depois, apenas prefixo. O hash é SHA-256 e a validação usa comparação em tempo constante. Escopos mínimos: protocolos, documentos, tarefas, notificações, webhooks, mobile, assinaturas e BI.

## Mobile/offline
A API mobile aceita payload versionado para pull/push. Offline real depende das tabelas `campo_dispositivo`, `campo_roteiro`, `campo_coleta`, `campo_evidencia` e `campo_sincronizacao`; sem schema/storage, a resposta assume fallback honesto e não finge sincronização.

## Assinatura e validação pública
Assinatura eletrônica simples é identificada como simples. Gov.br/ICP-Brasil permanecem como providers configuráveis; não há simulação de ICP-Brasil. A validação pública em `/ValidarDocumento` prepara código/hash/QR Code futuro sem expor documentos sigilosos, CPF ou CNPJ.

## BI avançado
`/Bi/Fluxos` e `/api/v1/bi/indicadores` consolidam indicadores de protocolos, GED, contratos, obras, suporte, portal e financeiro. Quando não houver tabela real, a tela/API deve explicitar fallback; gráficos demonstrativos devem ser identificados como demonstração.

## Integrações oficiais
Conectores planejados: Gov.br, ICP/provider de assinatura, SMTP/E-mail, WhatsApp configurável, Storage, OCR, SIAFIC/e-Sfinge/SICONFI, eSocial, Portal da Transparência e webhooks externos. Sem configuração real, status é “Não configurado”. Segredos são mascarados.

## LGPD e auditoria
APIs públicas não expõem CPF/CNPJ completo, tokens, secrets ou payloads sensíveis completos. Operações críticas devem registrar auditoria com tenant, usuário/API key, endpoint, correlationId e data.

## Limitações honestas
- Não há simulação de integração oficial.
- Não há simulação de ICP-Brasil.
- Offline real só é efetivo com schema e storage.
- Persistência de API keys/webhooks/logs depende das tabelas listadas no schema report.
