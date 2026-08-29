# CORR25 — fechamento técnico do GED360 / InovaGED

## Base e objetivo

Fechamento corretivo executado sobre a branch local `work`, pois este checkout não possui o remote `origin`. O trabalho permanece restrito ao módulo documental existente e não cria outro módulo de negócio.

## Correções fechadas

- As consultas de dashboard, listagem e busca permanecem parametrizadas por `tenant_id` e agora ocultam `RESTRITO` e `SIGILOSO` quando `GED_DOCUMENTO_SENSIVEL_VIEW` não está efetivamente concedida (fail-closed).
- Filtros de status e sigilo são normalizados contra listas permitidas antes da materialização Dapper.
- Cada área GED exige sua permissão persistida específica; auditoria, relatório, OCR/revisão, temporalidade, tramitação e eliminação não herdam mais uma permissão documental genérica.
- A migration corretiva `20260829160000_corr25_ged360_integridade_lgpd.sql` acrescenta validações idempotentes de SHA-256, datas de empréstimo, conclusão de assinatura e estados de aprovação/execução de eliminação.
- Índices impedem duplicidade ativa de hash principal, item importado e busca salva por usuário.
- Trigger de banco bloqueia execução de lote sem aprovação prévia, com hold jurídico/auditoria ou item não elegível.
- Migration, manifest, baselines de produção e baselines com seeds fictícias foram regenerados e sincronizados.

## Telas e rotas revisadas

Foram revisadas as rotas existentes de dashboard, documentos/busca e todas as áreas agrupadas sob `/GED`. Dashboard, filtros, indicadores, badges, tabela responsiva e estados vazios utilizam `ged360.css`, sem CSS inline. As áreas sem operação materializada continuam declarando explicitamente a dependência de dados/configuração real; não simulam OCR, assinatura, arquivo, QR ou resultado operacional.

## Permissões revisadas

As 22 permissões GED persistidas pela EXP25 permanecem a fonte de autoridade. CORR25 reforça o uso específico de `GED_IMPORTACAO_MANAGE`, `GED_OCR_VIEW`, `GED_OCR_REVIEW`, `GED_CLASSIFICACAO_MANAGE`, `GED_TEMPORALIDADE_MANAGE`, `GED_PROTOCOLO_VIEW`, `GED_TRAMITACAO_MANAGE`, `GED_WORKFLOW_MANAGE`, `GED_ASSINATURA_MANAGE`, `GED_ACERVO_VIEW`, `GED_EMPRESTIMO_MANAGE`, `GED_ELIMINACAO_VIEW`, `GED_AUDITORIA_VIEW`, `GED_RELATORIO_EXPORT` e `GED_DOCUMENTO_SENSIVEL_VIEW`.

## Limites técnicos

- OCR continua pendente até existir motor real configurado; nenhum texto é inventado.
- Assinatura só pode ser concluída com provedor, hash e data verificáveis.
- As áreas operacionais ainda não materializadas apresentam estado vazio honesto, não um CRUD fictício.
- Não havia `dotnet`, `psql`, PowerShell nem servidor web em execução neste ambiente. Assim, build, aplicação PostgreSQL e smoke HTTP autenticado devem ser repetidos na CI/homologação.

## Validação e Git

Executados: inspeção de Git, gate estático `scripts/validate-rc50-80.py`, validação JSON/checksums, comparação dos seis scripts completos, buscas Razor/GED e inspeção estática de rotas. Não houve conflito Git.

Bloqueios do ambiente:

- `BLOCKED: comando dotnet build não executado porque dotnet não está disponível no PATH.`
- `BLOCKED: comando psql -v ON_ERROR_STOP=1 -f script_completo.sql não executado porque psql não está disponível no PATH.`
- `BLOCKED: comando smoke HTTP autenticado das rotas GED360 não executado porque dotnet não está disponível no PATH para iniciar a aplicação.`
- `BLOCKED: comando git fetch origin não executado porque o remote origin não existe no checkout.`
- `BLOCKED: comando git pull --rebase origin main não executado porque o remote origin não existe no checkout.`
- `BLOCKED: comando git push origin codex/corr25-fechamento-ged360-inovaged não executado porque o remote origin não existe no checkout.`
- `BLOCKED: comando abertura de PR não executado porque o remote origin não existe no checkout.`
- `BLOCKED: comando merge do PR não executado porque o remote origin não existe no checkout.`
- `BLOCKED: comando git pull final origin main não executado porque o remote origin não existe no checkout.`
