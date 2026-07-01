# Schema report operacional local

Validação local não executada neste ambiente porque `docker` não está disponível. Use `scripts/schema-report-operacional.ps1` para consultar `information_schema.columns` no schema `sigov` e atualizar este arquivo com as tabelas operacionais encontradas.

Tabelas-alvo: protocolo, processo, tramite, protocolo_movimento, protocolo_anexo, documento, ged_documento, ged_pasta, pasta, documento_versao, arquivo, ocr_fila, contribuinte, imovel, debito, guia, divida_ativa, contrato, contrato_aditivo, contrato_fiscal, contrato_documento, processo_juridico, parecer_juridico, prazo_juridico, audiencia_juridica, conta_pagar, conta_receber, caixa_movimento, categoria_financeira e auditoria_evento.
