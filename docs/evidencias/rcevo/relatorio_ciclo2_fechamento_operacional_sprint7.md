# Relatório Ciclo 2 · Sprint 7 — Mapa da frente administrativa (contratos de auditoria, sem implementação)

Data: 2026-10-13 · Trilha: commit `fbf17587` (Ciclo 2 S6) + commit desta sprint (contrato/migration/manifest/consolidados/teste/docs) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

**Auditoria funcional escrita** de Compras (Lei 14.133/2021) → Licitação → Contrato → Fornecimento →
Recebimento → Estoque/Patrimônio → Obrigação financeira, com dependências (Jurídico, Convênios, Obras),
e o catálogo de permissões da frente catalogado em seed idempotente — BR-7.1 a BR-7.8 do roadmap
`docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md`. Gate da S7: **documento de contratos
revisado + permissões da frente catalogadas (seed) + nenhuma implementação fora do catálogo**.
Regras aplicadas: 6–7 (migration/manifest/consolidados sincronizados), 11/12 (banco como autoridade),
13 (falha nomeada explícita), 14 (estender suítes existentes — sem nova classe), 19 (sintaxe dos
artefatos alterados) e 20 (RC própria). Regras 21–24: multi-esfera sem valor municipal hardcoded —
teto, margem ME/EPP, prazos e percentuais todos parametrizáveis por banco.

**Esta fatia NÃO implementa funcionalidade**: nenhum endpoint, view, domínio ou máquina de estados.
A migration é **apenas-de-dados** (catálogo de permissões) e o schema permanece intacto — apenas a
contagem do manifest muda (**232 → 233 entradas**).

## O que foi entregue

**1. Documento de contratos revisado** — `docs/CONTRATOS-FRENTE-ADMINISTRATIVA-C2S7-MULTIESFERA.md`:
fundamento legal (Lei 14.133/2021 arts. 6º/18–29/30–57/100–116/106/128/130 + LC 123/2006), escopo
multi-esfera (municípios/estados/União com as entidades de cada esfera), auditoria do estado atual
(tabelas existentes por fase na base RC50.85/FUNC03/Licitapro/almoxarifado/patrimônio/empenho/convenio/
obras/jurídico), os 8 contratos de ato (BR-7.1 a BR-7.8) com atos/invariantes/erros nomeados/permissão
exigida/sprint executora (S8: demanda→contrato; S9: fornecimento→obrigação financeira; S10:
dependências convênio/obra/jurídico), catálogo de permissões e tabela de parâmetros obrigatórios
(margem ME/EPP default 5%, teto consulta prévia vigente, aditivo quantitativo por tipo de objeto,
prorrogação art. 106 paramétrica, prazo de substituição default 5 dias úteis, contrapartida por
instrumento).

**2. Migration apenas-de-dados idempotente** — `database/postgres/migrations/20261013130000_evo_c2s7_frente_administrativa_permissoes.sql`:
- **25 chaves novas** no módulo `compras` em `sigov.permissao` — uma chave canônica por ato da frente
  (demanda criar/aprovar/reabrir/visualizar; licitação abrir/modalidade/habilitação/lances/documentar
  preferência; adjudicação/homologação executar; contrato aditivo criar+aprovar/prorrogar/rescindir/
  fiscalizar execução; fornecimento criar/visualizar; recebimento aprovar/rejeitar; fatura vincular;
  obrigação visualizar/liquidar; contrapartida registrar; auditoria visualizar) — insert `where not
  exists` (sem conversão destrutiva, regra 5);
- **vínculo dos perfis SUPERADMIN ativos e não excluídos** (1 por tenant) em `sigov.perfil_permissao`
  com `on conflict do update` (padrão FUNC03 — ajuste documentado, ver defeito abaixo);
- **pós-condição explícita `C2S7-POSTCOND`** (regra 13): falha nomeada se faltar qualquer chave
  (≠25), se não existir perfil SUPERADMIN ativo ou se os vínculos ≠ N_perfis×25.

**3. Manifest + consolidados** — entry `20261013130000` (checksum SHA-256, dependência
`20261013120000`, `applyAutomatically`, `postConditionSql`) → manifest **233 entradas**; consolidados
regenerados pelo gerador oficial e **verificados** (`scripts/generate-script-completop.ps1
-IncludeDevelopmentSeed` + `-Verify` — ambos exit 0: 229 migrations incluídas, 4 excluídas — herdado).

**4. Suíte estendida (regra 14)** — `SaasComercialTests` (classe existente, fact existente de contagem):
asserção do manifest 233→234 segmentos + comentário de trilha da entrada C2S7.

## Defeito encontrado NA PRÓPRIA prova (sem máscara)

**Filtro `pa.sistemico` herdado do padrão FUNC03 não casa com o estado de homologação** — a 1ª
aplicação da migration falhou com o erro nomeado `C2S7-POSTCOND: vinculos SUPERADMIN da frente
incompletos (0 <> 25)`: as 5 linhas `SUPERADMIN` ativas no banco têm `sistemico = false` (o flag é
inconfiável neste estado). Remediado e **documentado na própria migration**: o vínculo passa a usar a
autoridade correta — código externo `SUPERADMIN` ativo e não excluído (1 perfil por tenant) — e a
pós-condição passou a comparar contra `N_perfis × 25` dinamicamente (sem número fixo de tenants).
Aplicação posterior: **2 rodadas exit 0** (idempotência provada) e verificação direta no banco:
**25 chaves** + **125 vínculos = 5 perfis × 25 chaves**.

## Verificação ao vivo (PG16 `p1_dev`)

- Migration aplicada **2×** via psql `ON_ERROR_STOP=1` — exit 0 nas duas rodadas;
- Pós-condição do manifest: `chaves=25` e `vinculos=SUPERADMIN_ativos(5)×25=125`;
- Total `sigov.permissao` ativo pós-seed: 1736 (base anterior + 25);
- `scripts/generate-script-completop.ps1 -Verify`: "Scripts completos estão sincronizados com as
  migrations." (exit 0).

## Gates

- Build Release `-warnaserror` (`Sigov.sln`): **0w/0e**;
- Suítes: **851 verdes** (Unit 522 / Integration 171 / Api 158, 0 ignorados);
- TIER2 E2E HTTP (`scripts/c1_tier2_e2e.ps1` sobre apps reais Web :5099 / Api :5100/:5001):
  **334/334 PASS**, `fixture.removido restantes=0` — artefatos `artifacts/c1_tier2_e2e/c1-tier2-e2e-result.json` / `c1-tier2-e2e-report.md`;
- JSON alterado (`manifest.json`) revalidado (parse + generator `-Verify`); SQL idempotente aplicada
  2×; PS drivers validados pelo parser (regra 19); RC própria desta sprint (regra 20).

## Notas de ambiente e resíduos declarados (regra 13 — nada mascarado)

- Sem interface nesta fatia → sem jornada de navegador/screenshots (entrega é contrato + seed — gate
  da S7 explicitamente "sem implementação").
- Herdado das sprints anteriores (sem alteração nesta): RAM crônica baixa do host (apps em shells
  dedicados + sonda de readiness), container `sigov-api` Restarting(139) legado contornado via API
  local, `rc50_25` órfã, `validate-script-completop` (seed `admin@sigov.local`),
  `p1_dev.schema_migrations` não registra as entradas 20261012xxxx (incluindo a presente
  20261013xxxx — aplicação manual via psql, padrão das sprints EVO), custo/aluno projetado a consumir
  na S20.
- A família legacy UPPER_SNAKE (`LICITACAO_*`, `CONTRATO_*`, `COMPRAS_*`, `ATA_REGISTRO_PRECO_*`)
  permanece ativa e compatível — **não** há migração destrutiva para o formato pontilhado (regra 5);
  o documento de contratos define a reutilização das chaves existentes por ato.

## Jornada da fatia e gates

- **Jornada da fatia: CONTRATO REVISADO + CATÁLOGO CATALOGADO NO BANCO + VALIDADO AO VIVO (2× idempotente, pós-condições verdes, consolidados sincronizados).**
- Gates: manifest **233 entradas** (232 → 233, único delta — schema intacto; regras 6–7); build
  Release `-warnaserror` **0w/0e**; suítes 851 verdes; TIER2 E2E HTTP **334/334**; documento de
  contratos revisado + permissões da frente catalogadas (seed) + nenhuma implementação fora do
  catálogo (gate da S7 integralmente cumprido).
- Próximo exato: **Ciclo 2 Sprint 8 — Compras integradas (1/2): demanda → contratação → contrato**
  (BR-8.x do roadmap sobre os contratos BR-7.x desta sprint). GED por último.
