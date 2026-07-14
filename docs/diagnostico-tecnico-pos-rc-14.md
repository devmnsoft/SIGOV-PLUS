# Diagnóstico técnico Pós-RC 14

Gerado para a branch `codex/pos-rc-14-correcao-runtime-produto-final` após análise local do último histórico Git disponível, workflows, scripts, controllers, views, serviços Dapper, migrations, seeds e módulos operacionais.

## Matriz de diagnóstico

| Área | Arquivo | Problema | Tipo | Risco | Correção | Status |
|---|---|---|---|---|---|---|
| Smoke | `scripts/smoke-test-sigov.ps1` | Resumo e evidências usavam string literal sem interpolação. | CI/CD | Relatório falso e bloqueio estático. | Uso de strings interpoladas, JSON com contadores reais e evidência Pós-RC 14. | Corrigido |
| CI | `.github/workflows/ci.yml` | Etapa `Wait health` tinha chave `run` duplicada e artefatos Pós-RC 13. | CI/CD | YAML inválido ou evidência obsoleta. | Remoção do `run` duplicado e atualização de artefatos Pós-RC 14. | Corrigido |
| Kanban | `src/Sigov.Web/Controllers/OperationalTransversalController.cs` | POST não estava protegido no controller inteiro e não persistia OS/propostas. | Segurança/Funcional | Mudança de status sem autorização consistente. | Controller autorizado, antiforgery, carregamento Dapper para OS/propostas, auditoria e fallback honesto para tarefas. | Corrigido |
| Kanban | `src/Sigov.Web/Views/Kanban/Index.cshtml` | View exibia colunas estáticas e aviso de ausência de cards reais. | UX/Funcional | Quadro não operacional. | View tipada com colunas por tipo, cards reais quando disponíveis, filtros, detalhe e botão de status. | Corrigido |
| Lote | `src/Sigov.Api/Controllers/EnterpriseModulesController.cs` | Contagem de falhas por reflection. | Runtime/Auditoria | Resultado frágil e falso sucesso parcial. | Resultado por item tipado, contagem forte, 409 total e 207 parcial. | Corrigido |
| Importação | `src/Sigov.Api/Controllers/EnterpriseModulesController.cs` | Preview valida arquivo, mas confirmação precisa evidenciar relatório/correlação. | Funcional/Auditoria | Sucesso sem rastreabilidade. | Confirm mantém validação, retorna relatório JSON, evento e rejeições por linha. | Parcial honesto |
| Anexos | `src/Sigov.Api/Controllers/EnterpriseModulesController.cs` | Storage/GED pode estar indisponível. | Runtime/LGPD | Expor path ou falso download. | Mantido fallback 503 honesto para download/visualização e endpoints auditáveis. | Parcial honesto |
| Release | `scripts/go-live-check.ps1`, `scripts/package-release.ps1` | Referências Pós-RC 13. | Documentação/CI/CD | Pacote incompleto para Pós-RC 14. | Incluídos documentos Pós-RC 14 e evidências atualizadas. | Corrigido |
| Dashboard/Central/Busca/Agenda/SLA | `src/Sigov.Web/Services/Operational/*` | Dados reais dependem de schemas opcionais. | Funcional | Cards demonstrativos sem fonte podem confundir. | Mantido padrão de fonte real/fallback honesto na documentação e smoke. | Parcial honesto |

## Pendências honestas

- Homologação runtime completa depende de ambiente com Docker, PostgreSQL, `dotnet` SDK e provedor GED/storage configurado.
- Tarefas no Kanban não simulam persistência quando `sigov.tarefa` não estiver disponível.
- Anexos retornam 503 para download/visualização quando o provedor GED/storage não está configurado, sem expor caminho físico.
