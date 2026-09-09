# RC50.99 — Fundação SaaS e Indústria

Data: 2026-09-09. Estado: PARCIAL / BLOCKED. Não é uma entrega candidata a PR.

## Resultado

O trabalho ficou corretamente restrito ao P0. A governança estática e o baseline foram estabilizados e exercitados em PostgreSQL 18 diagnóstico. O PostgreSQL 16 normativo não ficou disponível; portanto P1 SaaS, Administração SaaS, P2 Indústria e eventual avanço de Compras não foram iniciados nesta branch.

## Git e preservação

- Estado real inicial: branch `codex/p0-governanca-migrations`, HEAD `f6bb7f24df58620d3c898d3f28f8b460d8c366b1`, limpa e alinhada ao upstream.
- Branch criada após reconciliação: `codex/rc50-99-foundation-saas-industry`.
- O commit base já versionava milhares de artefatos `.vs`, `bin` e `obj`; eles foram preservados e não devem ser incluídos em commit desta fase.
- Nenhuma migration publicada foi editada. Não houve reset, stash, limpeza, force push, commit ou PR.

## Migrations

- Inventário: 184 SQLs, 174 entradas de manifesto e 10 órfãos classificados; gate estático PASS.
- O gerador sincroniza os sete artefatos consolidados e o wrapper `apply_all_required_migrations.sql` a partir do manifesto.
- Foram adicionadas compatibilidades forward-only 080–093 para contratos legados encontrados durante o ensaio vazio.
- `20260902010000_corr_compras_checksum_schema.sql`, publicada com validação UUID incompatível com o contrato canônico bigint, foi preservada e excluída de execução automática/baseline; correções bigint posteriores permanecem ativas.
- `20260903130000_corr_licitapro_postconditions_schema.sql`, publicada e com DDL de índice inválido no baseline atual, foi preservada e excluída de execução automática/baseline; `20260903173000_corr_licitapro_schema_history.sql` permanece como correção forward-only.
- PostgreSQL 18: banco vazio PASS, 170 versões registradas, segunda aplicação PASS pelo ledger.
- PostgreSQL 16, upgrade legado e equivalência: BLOCKED por indisponibilidade da instância oficial.

## Dez SQLs órfãos

| Arquivo | Classificação |
|---|---|
| `20260813120000_rc50_24_educacao_rh_folha_produto_core.sql` | necessária |
| `20260813120000_rc50_27_operacoes_inteligentes.sql` | necessária; colisão de versão com RC50.24 |
| `20260813160000_rc50_25_educacao_fluxo_diario.sql` | necessária |
| `20260813190000_rc50_28_gestao_avancada.sql` | necessária |
| `20260813193000_rc50_29_parametros_modulos.sql` | necessária |
| `20260813195500_rc50_30_governanca_executiva.sql` | necessária |
| `20260813210000_rc50_31_bloco1_fechamento.sql` | necessária |
| `20260815120000_rc50_37_compras_licitacoes_bloco6_core.sql` | incompatível (UUID versus bigint) |
| `20260815121000_rc50_37_contratos_bloco6_core.sql` | incompatível (UUID versus bigint) |
| `20260815122000_rc50_37_almoxarifado_patrimonio_bloco6_core.sql` | incompatível (UUID versus bigint) |

Todos permanecem fora do manifesto automático, com justificativa e checksum em `database/postgres/migration-governance.json`.

## Build, segurança e testes

- Restore locked e build Release com warnings como erros: PASS.
- UnitTests: 371/371 PASS.
- Swagger runtime: 11/11 PASS; `/swagger/v1/swagger.json` retornou 200.
- ApiTests: 88/100 PASS; 12 falhas de contratos estáticos históricos. Não foram reintroduzidos endpoints demo, senhas ou menus hardcoded para satisfazê-los.
- Endpoints anônimos existentes foram alinhados para negar acesso sem identidade; views com classes Bootstrap Icons foram convertidas para o componente `sigov-icon`.
- Login, revogação, dois tenants e fluxo industrial: não executados.

## Próximo passo

Restaurar uma instância descartável PostgreSQL 16, repetir aplicação/reaplicação, executar upgrade legado e comparação de schema. Só depois concluir P0.4 e liberar P1.
