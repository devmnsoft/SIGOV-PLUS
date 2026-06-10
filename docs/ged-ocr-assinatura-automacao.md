# SIGOV Pós-Build 09 — GED/OCR, Assinatura Digital e Automação

## Escopo entregue

O módulo documental evolui o SIGOV PLUS com GED completo, OCR simulado, assinatura digital simulada, contratos, protocolos eletrônicos, workflow, tramitação, histórico e auditoria multi-tenant.

## Banco de dados

A migration `20260610220000_pos_build_09_ged_ocr_assinatura_automacao.sql` cria estruturas idempotentes no schema `sigov`:

- `ged_documento`, `ged_anexo`, `ged_indice`, `ged_historico`, `ged_assinatura`, `ged_workflow`.
- `protocolo`, `contrato`, `fluxo_tramitacao`, `ocr_digitalizacao`.
- Seeds de tipos documentais, templates, workflow básico e pacote SaaS `GED_AUTOMACAO_PLUS`.
- Índices por tenant, tipo, data, status, metadados e tags.

## API

Base URL: `/api/ged`.

- `GET /dashboard`
- `GET /documentos`
- `POST /documentos`
- `POST /documentos/{id}/anexos`
- `GET /documentos/{id}/download`
- `POST /documentos/{id}/ocr`
- `POST /documentos/{id}/indices`
- `POST /documentos/{id}/assinaturas/simular`
- `POST /documentos/{id}/tramitar`
- `GET /documentos/{id}/historico`
- `GET /contratos`
- `POST /contratos`
- `GET /protocolos`
- `POST /protocolos`
- `GET /workflows`

## Permissões

- `ged.visualizar`, `ged.upload`, `ged.download`, `ged.indexar`, `ged.assinar`, `ged.tramitar`.
- `contrato.visualizar`, `contrato.criar`, `contrato.assinar`.
- `fluxo.visualizar`.
- `ocr.processar`.

ADMIN_GERAL e ADMIN_TENANT recebem todas por seed da migration.

## LGPD e auditoria

Cada mutação relevante grava histórico em `sigov.ged_historico` e auditoria em `sigov.auditoria_evento`, com `tenant_id`, usuário, IP, user-agent, payload JSON e `correlation_id`.
