# Gaps de schema dos módulos

Atualizado em 2026-07-06.

## Tabelas críticas esperadas

- Prioridade 1: `workflow`, `workflow_etapa`, `workflow_instancia`, `workflow_historico`, `tarefa`, `notificacao`, `notificacao_usuario`, `agenda_prazo`, `evento_operacional`, `outbox_evento`.
- Prioridade 2: `protocolo`, `protocolo_movimento`, `protocolo_anexo`, `documento`, `ged_pasta`, `arquivo`, `contrato`, `contrato_fiscal`, `contrato_documento`, `compra_solicitacao`, `licitacao`.
- Prioridade 3: `patrimonio_bem`, `patrimonio_movimento`, `patrimonio_inventario`, `obra`, `obra_medicao`, `obra_diario`, `obra_foto`.

## Gaps a confirmar localmente

Execute `scripts/schema-report-consolidacao-modulos.ps1` para preencher `docs/schema-report-consolidacao-modulos-local.md`. Sem esse relatório, qualquer salvamento novo deve permanecer em fallback honesto.

## Regras para próxima migration

- Criar apenas objetos ausentes com `CREATE TABLE IF NOT EXISTS`.
- Adicionar `tenant_id`, `created_at`, `updated_at`, `is_deleted` quando fizer sentido operacional.
- Criar índices por `tenant_id`, `status`, datas e entidades vinculadas.
- Não renomear, apagar ou alterar tipo de coluna existente sem validação explícita.
