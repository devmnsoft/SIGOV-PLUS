# Relatório Ciclo 1 · Sprint 2 — Educação Avançada (#4 máquina de estados + #6 escopo institucional)

Data: 2026-10-10/11 · Commit validado: `5a8e770c` (trilha Sprint 2+3 no mesmo commit) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

- **#4 Máquina de estados por recurso**: matriz de transições por FAMÍLIA de status multi-recurso
  (`EducacaoStatusMaquina`, regra 24: nada municipal hardcoded), recusa nomeada `TRANSICAO_INVALIDA` (409)
  e no-op idempotente auditado para transição ao próprio estado; origem em estado desconhecido preserva o
  comportamento legado como exceção documentada (regra 13).
- **Guarda de versão otimista**: escrita com `coalesce(updated_at,created_at)=@VersaoAtual`;
  conflito vira `CONFLITO_VERSAO` (409) sem sobrescrever silenciosamente; contrato expõe a versão efetiva
  (`coalesce(updated_at,created_at)` como `updatedAt`) para o cliente reenviar.
- **Falhas explícitas**: `REGISTRO_NAO_ENCONTRADO` (404) e `RECURSO_INDISPONIVEL` (404) —
  schema ausente nunca vira sucesso simulado nem lista vazia em comando de status.
- **#6 Escopo institucional**: antes de gravar qualquer vínculo, escola e ano letivo do comando são
  validados contra o tenant e a entidade do contexto autenticado:
  - escola inexistente → `ESCOLA_INEXISTENTE` (404);
  - escola de outra entidade → `ESCOLA_FORA_DO_ESCOPO` (409);
  - ano letivo ausente para o tenant/entidade → `ANO_LETIVO_INEXISTENTE` (422);
  - comando de status sobre registro de outra entidade → `CONTEXTO_INSTITUCIONAL_DIVERGENTE` (409).
  Regra 24 respeitada: a entidade é o escopo; multi-esfera (municipal/estadual/federal).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `src/Sigov.Application/Educacao/Avancada/EducacaoStatusMaquina.cs` | novo (commitado no WIP): máquina de estados + exceções nomeadas |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | novo: exceções de escopo institucional (#6) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | `AlterarStatusAsync` recebe `versaoAtual`; DTO expõe `UpdatedAt` |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | validação de escopo em `CriarAsync`; leitura `(status,entidade)` + transição validada + guarda de versão em `AlterarStatusAsync`; `updatedAt` = `coalesce(updated_at,created_at)` nas consultas |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | `ExecutarStatusAsync`/`ExecutarCriarAsync` mapeiam falhas nomeadas p/ HTTP explícito |
| `src/Sigov.Api/Controllers/Educacao{Merenda,Biblioteca,Indicadores,TransporteEscolar}Controller.cs` | endpoints `status` aceitam `updatedAt`; criações passam por `ExecutarCriarAsync` |
| `scripts/c1_tier2_e2e.ps1` | blocos E5 (máquina de estados + versão) e E6 (escopo institucional, fixture dinâmico de entidades/escolas fictícias) + limpeza ampliada |
| `tests/Sigov.UnitTests/PagedResultTests.cs` | testes da matriz de estados (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | asserções estáticas #4/#6 atualizadas |

Sem migration nesta fatia: schema existente reutilizado (regras 6–8 intactas; os 6 consolidados não alterados).

## Gates executados

- Build Release `-warnaserror`: **0w/0e** (dois cortes: antes e depois das correções de Dapper/coalesce).
- Suítes (reexecução limpa de 2026-10-11, timeouts estendidos `VSTEST_CONNECTION_TIMEOUT=600` e
  `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=1800`): Unit **521/521** · Integration
  **160/160** · Api **158/158** (0 ignorados; total 839/839). Contagem final compartilhada com a
  Sprint 3 (mesmo corte de binários; o Integration inclui as asserções estáticas da Sprint 3).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **25/25 PASS** (ts=2026-10-11T00:24:23Z),
  fixture fictício `c1e26-%` do tenant 910001 aplicado e removido ao final (resíduo zero).

## Evidência Tier-2 ao vivo (rodada 25/25)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E1 | Dashboard: totais sobre o conjunto completo (120 linhas fictícias) | `total=120 ativos=60 pendentes=20 alertas=20 recentes=6` |
| E2 | CSV: exportação integral, BOM, escape e proteção de fórmula | `linhas_csv=122` (cabeçalho+120+1 quebra embutida), BOM e fórmula íntegros |
| E3 | Schema ausente (probe de rename transitório) | diagnóstico operacional explícito em vez de lista vazia |
| E4 | Auditoria append-only de transição | 2 entradas `acao=STATUS` com `de/para/motivo`, anterior preservada |
| E5.1 | Registro criado em RASCUNHO | 200, id retornado |
| E5.2 | PENDENTE → ENCERRADA | **409 `TRANSICAO_INVALIDA`** (pendente só ativa/permanece/cancela) |
| E5.3 | RASCUNHO → ATIVA | 200 (transição canônica) |
| E5.4 | `updatedAt` exposto no contrato | valor presente e igual à escrita no banco |
| E5.5 | Guarda de versão com versão atual | 200 e banco atualizado (ENCERRADA) |
| E5.6 | Reenvio com versão obsoleta | **409 `CONFLITO_VERSAO`** (sem sobrescrita silenciosa) |
| E5.7 | TERMINAL → ATIVA | **409** (terminal não reabre) |
| E5.8 | Status sobre id inexistente | **404 `REGISTRO_NAO_ENCONTRADO`** nomeado, não 500 |
| E6.1 | Escola inexistente (`escolaId=9999999999`) | **404 `ESCOLA_INEXISTENTE`** |
| E6.2 | Ano letivo ausente (garantido ausente p/ tenant) | **422 `ANO_LETIVO_INEXISTENTE`** |
| E6.3 | Escola da MESMA entidade do contexto | 200 (aceita; sem falso positivo) |
| E6.4 | Escola de OUTRA entidade | **409 `ESCOLA_FORA_DO_ESCOPO`** (vínculo não cruza entidades) |
| CX | Limpeza do fixture | `restantes=0` |

## Defeitos runtime descobertos na prova ao vivo e corrigidos (sem máscara)

1. `Dapper.ExecuteScalarAsync<(string?,long)>` não mapeia tupla multi-coluna
   (`SqlMapper.Parse` tenta converter a 1ª coluna para o tuple → 500 em toda transição) →
   substituído por `QueryFirstOrDefaultAsync<(string? Status,long LinhaEntidade)>`.
2. Contrato de `updatedAt` saía nulo em registros recém-criados (`updated_at` NULL) quebrando a
   guarda de versão no cliente → select expõe agora `coalesce(updated_at,created_at)`, idêntico ao
   lado da escrita (`@VersaoAtual::timestamptz`).

## Resíduos declarados (regra 13)

- A validação de escopo cobre os recursos do repositório avançado (matriz #1–#7 do Ciclo 1);
  o núcleo escolar (matrícula/turma/frequência em `EducacaoRepository`) valida vínculos na sua própria sprint.
- Escola inativa (`ativo=false`) não bloqueia vínculo novo nesta fatia; decisão de política pendente.
- Ano letivo é verificado por existência no tenant/entidade; vigência por data (início/fim) não é
  condição do comando avançado.
- Ambiente (não mascarado): nesta máquina o boot do host ASP.NET Core chega a levar vários minutos
  (suspeita de scan AV nos binários recém-compilados) e o vstest estourava timeout de conexão/host;
  as reexecuções usam timeouts estendidos e o driver Tier-2 tem readiness próprio.
