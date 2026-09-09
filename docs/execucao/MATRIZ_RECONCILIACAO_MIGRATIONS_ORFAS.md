# Matriz de reconciliação de migrations órfãs — RC51.00

Data: 2026-09-09.

Esta matriz registra a decisão técnica sem reativar cegamente SQL órfão. A fonte executável continua sendo `database/postgres/migrations/manifest.json`; arquivos físicos preservados fora do manifest são memória técnica até prova runtime em PostgreSQL 16.

| Arquivo órfão | Objeto / bloco | Consumidor localizado | Estado atual | Migration sucessora / evidência | Ação RC51.00 |
|---|---|---|---|---|---|
| `20260813120000_rc50_24_educacao_rh_folha_produto_core.sql` | `educacao_*`, `folha_*`, catálogos produto/RH | Services e controllers históricos de Educação/RH/Folha | Parcialmente substituído | Migrations posteriores de Educação/RH/Folha constam no manifest; `dotnet test` cobre contratos estáticos existentes | Não ativada automaticamente; manter como órfã necessária até comparação runtime por objeto |
| `20260813120000_rc50_27_operacoes_inteligentes.sql` | alertas operacionais, fila de matrícula, simulação folha | Serviços de operação e dashboards históricos | Parcialmente substituído | Outbox/worker e dashboards operacionais atuais usam `sigov.outbox_evento` e tabelas posteriores | Não ativada; exige decomposição canônica sem colisão de versão |
| `20260813160000_rc50_25_educacao_fluxo_diario.sql` | diário/frequência/matrícula legado | Educação histórica | Parcialmente substituído | STATUS_REAL marca Educação parcial; testes preservados como contrato estático | Não ativada; aguarda sprint Educação |
| `20260813190000_rc50_28_gestao_avancada.sql` | workflows, diário de classe, RH/folha avançado | Workflow/RH/Educação históricos | Parcialmente substituído | Manifest possui blocos posteriores de workflow/outbox e módulos correlatos | Não ativada; objetos necessários devem virar migration nova em sprint própria |
| `20260813193000_rc50_29_parametros_modulos.sql` | `parametro_modulo*`, função `salvar_parametro_modulo` | SaaS/parametrização | Parcialmente substituído | RC51.00 reforça `modulo_saas` e `tenant_modulo_contratado` como fonte canônica | Não ativada; conciliar com catálogo SaaS canônico antes de executar |
| `20260813195500_rc50_30_governanca_executiva.sql` | notificações, preferências, qualidade de dados, assistente operacional | Dashboards e governança | Parcialmente substituído | Testes API/UI atuais passam após reclassificação de contrato | Não ativada; aguarda prova runtime por objeto |
| `20260813210000_rc50_31_bloco1_fechamento.sql` | fechamento Educação/RH/Folha, constraints e índices | Educação/RH/Folha históricos | Parcialmente substituído | Múltiplas migrations posteriores cobrem partes, sem equivalência completa comprovada | Não ativada; manter órfã necessária até matriz objeto-a-objeto com PostgreSQL 16 |
| `20260815120000_rc50_37_compras_licitacoes_bloco6_core.sql` | compras/licitações bloco 6 legado | Compras/LicitaPro | Incompatível | Correções posteriores bigint e pós-condições LicitaPro preservadas no manifest | Preservada fora de ativação |
| `20260815121000_rc50_37_contratos_bloco6_core.sql` | contratos bloco 6 legado | Contratos | Incompatível | Contratos atuais têm correções posteriores e dependências bigint | Preservada fora de ativação |
| `20260815122000_rc50_37_almoxarifado_patrimonio_bloco6_core.sql` | almoxarifado/patrimônio bloco 6 legado | Almoxarifado/Patrimônio | Incompatível | Contratos posteriores exigem modelo bigint/multi-esfera | Preservada fora de ativação |

## Revisão adicional RC51.00

`20260902000000_rc50_98_ged_workflow_branding_logo.sql` foi mantida no manifest como migration histórica, mas removida de `applyAutomatically` e `includeInBaseline`, porque cria schema físico `ged.*` e GED permanece fora da RC51.00. O SQL publicado não foi alterado. Os scripts consolidados foram regenerados com 170 migrations incluídas e 5 excluídas.

## Evidências locais

- `pwsh -NoProfile -File scripts/check-migration-governance.ps1 -StaticOnly`: `static=PASS`, `P0=BLOCKED` por exigir PostgreSQL 16 runtime.
- `pwsh -NoProfile -File scripts/generate-script-completop.ps1 -IncludeDevelopmentSeed`: scripts canônicos e dev sincronizados.
- `dotnet test tests/Sigov.UnitTests/Sigov.UnitTests.csproj --no-build`: 371/371.
- `dotnet test tests/Sigov.ApiTests/Sigov.ApiTests.csproj --no-build`: 100/100.
- `dotnet test tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj -p:EnableSigovIntegrationTests=true --no-restore`: 123/123.

## Bloqueio pendente

BLOCKED: banco vazio, reaplicação, upgrade legado e equivalência semântica completa não foram executados localmente porque o Docker Engine/PostgreSQL 16 local não está disponível neste host. A CI possui jobs com `postgres:16`; a execução remota deve ser usada para fechar o P0 runtime.
