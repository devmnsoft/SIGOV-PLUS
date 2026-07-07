# API Keys e Webhooks SIGOV

## API Keys

A API v1 exige `X-Api-Key` e `X-Tenant-Id`. A chave é comparada por SHA-256 em tempo constante contra `sigov.api_key.api_key_hash`. Os escopos são carregados de `sigov.api_key_escopo`.

Escopos mínimos: `protocolos.read`, `protocolos.write`, `documentos.read`, `documentos.write`, `tarefas.read`, `tarefas.write`, `notificacoes.read`, `webhooks.manage`, `mobile.sync`, `assinaturas.read`, `assinaturas.write`, `bi.read`.

## Webhooks

Eventos suportados: `protocolo.criado`, `protocolo.tramitado`, `documento.criado`, `documento.assinado`, `tarefa.criada`, `tarefa.concluida`, `contrato.criado`, `obra.medicao_registrada`, `manifestacao.recebida`, `chamado.aberto`, `sla.vencido`.

## Pós-RC 03 — homologação Web real

- **Funcional real:** Protocolo e GED Web passam a acionar serviços Dapper para `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.protocolo_anexo`, `sigov.portal_validacao_documento` e `sigov.outbox_evento` quando o schema existe.
- **Parcial:** Dashboard, Minha Central, Busca e Relatórios mantêm fallback honesto e devem priorizar dados reais detectados no schema.
- **Em implantação/fallback:** PDF/DOCX da POC, OCR, ICP-Brasil e Gov.br não são simulados.
- **Dependente de provedor:** envio externo de webhook e validações oficiais dependem de infraestrutura configurada.
- **Não disponível:** exposição de path físico de storage e dados pessoais completos em listagens/exports.
