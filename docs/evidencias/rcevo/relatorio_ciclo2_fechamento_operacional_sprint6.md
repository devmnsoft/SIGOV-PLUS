# Relatório Ciclo 2 · Sprint 6 — Fechamento Operacional de Educação: jornadas pelas telas reais (substituir a fachada estática)

Data: 2026-10-13 · Trilha: commit `5bef2410` (Ciclo 2 S5) + commit desta sprint (código front/back/driver/evidências/docs) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

As 7 áreas de Educação saem do shell estático para **views ligadas aos endpoints tipados, com KPI de origem verificável, ações que chamam API real, responsivo comprovado em viewport real, confirmação destrutiva com justificativa e acesso governado pelo banco** — BR-6.1 a BR-6.6 do roadmap `docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md` (+ regra 8 do complemento: relatórios/telas com origem verificável). Regras aplicadas: 11/12 (banco como autoridade das permissões e parâmetros), 13 (falha nomeada explícita, nada mascarado), 14 (estender suítes existentes), 19 (sintaxe dos artefatos alterados) e 20 (RC própria — P1, fora da RC50.68A).

**Esta fatia não altera schema** — manifest permanece **232 entradas** e os consolidados intocados (regras 6–7 satisfeitas sem alteração).

## O que foi implementado

**Camada Web (`Sigov.Web`):**

- **4 controllers de rota nova** com `[Authorize]` e sub-rotas por página — cada ação renderiza a view da área ou o estado nomeado "sem acesso" (`SemAcesso.cshtml`) quando `IMenuPermissionService.CanSeeModule(User,"educacao")` é falso (BR-6.6 — permissão do banco como autoridade, regra 11; perfil sem permissão vê estado nomeado, não 500):
  - `EducacaoMerendaController` (`Educacao/Merenda/{Dashboard|Produtos|Fornecedores|Estoque|Movimentos|Cardapios|Distribuicoes|Requisicoes|Consumo|Relatorios}`)
  - `EducacaoBibliotecaController` (`Educacao/Biblioteca/{Dashboard|Acervo|AcervoDetalhe|Exemplares|Emprestimos|Reservas|Atrasos|Digital|Relatorios}`)
  - `EducacaoTransporteEscolarController` (`Educacao/TransporteEscolar/{Dashboard|Rotas|RotaDetalhe|Veiculos|Motoristas|Alunos|Viagens|Ocorrencias|Custos|Relatorios}`)
  - `EducacaoIndicadoresController` (`Educacao/Indicadores/{Dashboard|Fundeb|Custos|Rateios|ProjecoesMatricula|Educacenso|EducacensoInconsistencias|AvaliacaoMagisterio|SerieHistorica|Relatorios}`)
- **6 views em `Views/Educacao/Avancada/`**: shell `_AreaShell.cshtml` data-driven (KPI grid com `.kpi-src` = métrica + endpoint + timestamp UTC, filtro de status no servidor, busca client-side, tabela `role=grid` com detalhe expansível, paginação, erro slot `role=alert`, empty state nomeado, exportação CSV, ações da página) + parciais por área consumidos via `PartialAsync("~/Views/Educacao/Avancada/_AreaShell.cshtml")` + `EduAreaModels.cs` (modelos).
- **`Views/Shared/_ValidationScriptsPartial.cshtml` restaurado** (defeito D5 abaixo).

**Camada API (`Sigov.Api`):** +4 GETs tipados que as telas reais consomem (mesmo padrão genérico `ListarAsync` dos demais recursos — sem novo contrato):

| Rota | Recurso |
|---|---|
| `GET /api/educacao/biblioteca/digital` | `educacao_biblioteca_digital_referencia` |
| `GET /api/educacao/transporte/alunos` | `educacao_transporte_aluno` |
| `GET /api/educacao/custos/rateios` | `educacao_custo_rateio` |
| `GET /api/educacao/educacenso/inconsistencias` | `educacao_educacenso_inconsistencia` |

CORS somente em dev: `Sigov:Security:CorsAllowedOrigins` += `http://localhost:5099` (`appsettings.Development.json` — produção intacta).

**Frontend:**

- Motor `wwwroot/js/modules/educacao-area.js`: configuração por área/página (apiBase tipado, definições de KPI com origem, colunas, ações); toda chamada via `sigovApi` (Bearer); KPI renderiza `fonte: GET <endpoint> · <ts UTC>`; **zero exibido como zero com a origem (zero ≠ indisponível)**; falha nomeada da API exibida verbatim no slot de erro; **toda ação chama a API real antes de notificar (BR-6.2 — zero toast falso)**; ações destrutivas usam `SigovConfirm.show` com **justificativa obrigatória** (BR-6.4); loading durante chamada; erro preserva estado do formulário.
- CSS `wwwroot/css/sigov-components.css`: `.edu-area` + media queries **≤360 / 361–767 / 768–1439 / ≥1440** + anel `:focus-visible` — componentes compartilhados, sem CSS inline por página (BR-6.3).

## Defeitos corrigidos NA PRÓPRIA prova (sem máscara)

1. **Bloco de rede do mini-shim jQuery legado (classe de defeito)** — os módulos agro/saude/saas/educacao.* usam 100+ `$.getJSON`/`$.ajax` com caminhos relativos `/api/*` e sem bearer; na arquitetura dual-port (Web :5099 ↔ Api :5001) os loads caíam na origem errada (401/404). Fix centralizado em `wwwroot/lib/jquery/jquery.min.js`: restaurado o jQuery **3.7.1 real** em `wwwroot/lib/jquery/dist/jquery.min.js` (12 views o referenciavam em 404) + reescrita byte-exact do bloco de rede (`apiCall`/`apiTarget`/`tokenHeaders`/`ajaxChain`): **somente caminhos iniciando `/api/`** migram a origem para a API (dev: `w.Sigov_API_BASE_URL || (https→origin | http→http://localhost:5001)`) + bearer via `sigovApi.getAuthToken()`; demais rotas same-origin; `$.ajax` respeita `contentType`; `$.getJSON`/`$.post` devolvem chain done/fail/always idêntica. **Zero call sites alterados; comportamento de produção idêntico.**
2. **Dapper/alunos 500 (classe de defeito)** — `GET /api/educacao/alunos` falhava com `NotSupportedException: The member Comparer of type IEqualityComparer<1[System.String]] cannot be used as a parameter value` (`GridReader.ReadAsync` ← `EducacaoRepository.ListarAsync`). Root cause: a coluna morta `array[]::json[] as Responsaveis` contém o 1º `:` do cast isolado após `]`, que casa o `LegacyParameterPattern` do Dapper 2.1.35 → `filterParams=false` → reflexão completa no `Dictionary<string,object?>` cru (membro `Comparer`) durante o cálculo de identidade. Todas as outras ocorrências de `::` no repo passam por `ExecuteScalar`/`Execute` (exampleParameters ≠ null → branch KVP) — auditadas e seguras. Fix: coluna removida do `Select("aluno")` (não mapeada em `AlunoResumoResponse`; GridReader exige bijeção exata coluna×construtor no grid read; o detalhe via `QueryFirstOrDefault` permanece flexível e `AlunoDetalheResponse.Responsaveis` vira null — aceitável e documentado). Reproduzido deterministicamente em app console isolado (Dapper 2.1.35 + PG). **Regra registrada para as próximas sprints: SQL que chega a `QueryMultipleAsync` com parâmetros Dictionary cru não pode conter colon/pergunta isolados (casts `::`, `?`, tokens) — prefira `cast(x as t)` ou evite QueryMultiple.**
3. **Race do `SigovConfirm.show`** — `done(true)` era chamado depois de `modal.hide()`, perdendo a confirmação (estorno pela tela não resolvia o promise) — corrigido em `wwwroot/js/sigov-ui.js`.
4. **Hang de `/Auth/ApiToken` (mitigação nomeada)** — o ciclo token+loads podia tomar ~10s reais; timeout nomeado de **8s** em `getAuthToken` (`wwwroot/js/sigov.api.js`) → `null` + warn `SIGOV_APITOKEN_TIMEOUT`, com dedupe do `tokenPromise` limpo no `finally` (nenhum load em branco em cascata).
5. **`_ValidationScriptsPartial.cshtml` ausente** — parcial do template referenciado por **17 views** que nunca foi commitado (untracked em disco); após o rebuild Release o assembly pré-compilado passou a não o conter → **500 em toda página** que usa `<partial name="_ValidationScriptsPartial">` (incluindo as 4 áreas novas). Restaurado como parcial no-op documentado: os scripts (`jquery.validate.min.js` + `jquery.validate.unobtrusive.min.js`) já são carregados UMA VEZ globalmente em `_Layout.cshtml` L36–37 e nenhuma view usa `@layout` alternativo — incluir as tags no parcial causaria dupla inicialização do unobtrusive.
6. **Driver do gate E2E** — resíduo da jornada (acervo `C1S6-JORNADA-ACT` + movimentos/estoques de merenda que ficaram órfãos quando o produto era limpo por código) não estava coberto pelo cleanup e quebrava as contagens exatas E1/E2; cleanup idempotente estendido com o marcador `C1S6-JORNADA-%` (acervo+exemplares) + catch-all `not exists` de movimentos/estoques órfãos + **termos de contagem autoversificáveis** no total `restantes`. Corrigido também um comentário `#` dentro do bloco SQL do cleanup (não aceito pelo psql nesse contexto — virou `--`).

## Jornadas pela interface (BR-6.5) — usuário `hom.a.admin`, tenant 910001

Artefatos em `artifacts/c1_tier2_e2e/s6_jornadas/` (HTML renderizado pós-JS + medições):

| Jornada | Página | Evidência |
|---|---|---|
| J1 Merenda (`01_`) | `/Educacao/Merenda/Dashboard` | 4 KPIs com origem `GET api/educacao/merenda/dashboard`; produto criado pela API tipada (id 267 `C1S6-JORNADA-PROD`) → ABERTURA de estoque (mov 327) → ENTRADA (328) → **estorno pela tela com justificativa** → 200 `{"success":true,"data":329}` + toast capturado + linha ESTORNO visível; 404 `ESTOQUE_NAO_ABERTO` verbatim para produto pendurado (261) |
| J2 Biblioteca (`02_`,`03_`) | `/Educacao/Biblioteca/{Dashboard|Acervo}` | acervo via API tipada (id 7727 `C1S6-JORNADA-ACT` "Atlas de Geografia multi-esfera") → linha real no Acervo; Dashboard com estado vazio nomeado (zero ≠ indisponível) |
| J3 Transporte (`04_`) | `/Educacao/TransporteEscolar/Dashboard` | 4 KPIs com origem `GET api/educacao/transporte/dashboard` (zeros honestos com fonte) + empty state nomeado |
| J4 Fundeb (`05_`) | `/Educacao/Indicadores/Fundeb` | painel conformidade LC 114/2021 (aplicado 0% vs mínimo 70%, `emConformidade=false`, nota "sem aplicacoes no recorte") com origem `GET api/educacao/fundeb/conformidade` + 4 KPIs; ações Fechar/Reabrir competência presentes |
| Educacenso (`06_`) | `/Educacao/Indicadores/Educacenso` | painel BR-5.6 (lotes/inconsistências/gravidade/eventos/última sincronização) com origem `GET api/educacao/educacenso/dashboard` + ação "Registrar sincronização Educacenso" |
| Rateios (`07_`) | `/Educacao/Indicadores/Rateios` | 4 KPIs com origem + endpoint `custos/rateios` 200 na jornada API |
| J6 Matrícula (`08_`) | `/Educacao/Matriculas` (tela legado) | dropdown escola com **dado real** ("Escola Repro C2S1 (ATIVA)", id 214); grid com estado vazio honesto "Ainda não há matrículas neste contexto." (contagens do tenant: matricula/aluno/turma/ano_letivo = 0, escola = 1); erroSlot nulo; zero erros JS (depois do fix Dapper) |

**J5 detalhe — resultado documentado, não defeito:** a cadeia pessoa→aluno→curso→série→ano letivo→turma→matrícula foi tentada pela interface; `POST /api/educacao/cursos` retornou **403 "Usuário sem permissão para a operação de Educação."** (o perfil `hom.a.admin` não possui a permissão de escrita do recurso — banco como autoridade, regra 11) e `POST /anos-letivos` → 400 nomeado. A list da tela foi comprovada com dado real da escola (fix do defeito Dapper provado no ato).

## Responsivo e teclado (BR-6.3) — `09_responsivo_e_teclado.json`

Método: iframes same-origin de largura exata injetados na página (mecanismo **dev-only existente** `?embed=1` em `Program.cs` — em staging/produção `X-Frame-Options: DENY` permanece; o CSP padrão `frame-ancestors 'none'` bloqueia iframe sem o parâmetro, comportamento preservado). Cada iframe carregou o app completo e **renderizou KPIs reais** (consumo da API nas larguras exatas):

| Contexto | innerWidth efetivo | Grid KPI | Padding | Valor KPI | Toolbar | Busca |
|---|---|---|---|---|---|---|
| desktop (janela real) | 1000 px | 4 colunas (213.8px×4) | 20px | 26.4px | row | 340px |
| mobile (iframe 360) | 357 px | 1 coluna (292px) | 12px | 20.8px | row | 100% |
| tablet (iframe 768) | 765 px | 2 colunas (330px×2) | 20px | 26.4px | **column** | 340px |

As três branches reais aplicaram exatamente as regras CSS da sprint (≤360 / 361–767 / 768–1439). A branch ≥1440 mantém as mesmas 4 colunas + container centralizado `max-width:1400px` (regra provada por presença no CSS servido; a janela do host mediu 1000 px). Teclado: **211 elementos tabbable em ordem exata de DOM** (o foco segue sequencialmente); o anel de foco cobrindo botões/links/inputs/selects da área foi comprovado carregado na folha servida (regra #306 de `sigov-components.css`: `outline: 3px solid var(--sigov-focus,#2563eb)`); a área S6 expõe 6 controles tabbables sob essa regra. Nota declarada: o Chrome não aplica `:focus-visible` em foco programático (heurístico de modality exige evento de teclado confiável) — declarado, não mascarado.

## Suítes (estendidas, sem novas classes — regra 14)

- `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs`: fact existente `Repository_Usa_Dapper_Parametrizado…` estendido com asserções de regressão da S6 (`NotContain("array[]::json[]")` no select de aluno + `Contain("<select aluno de 7 colunas exato>")`).
- Execução final após todas as correções: **851 verdes (Unit 522 / Integration 171 / Api 158, 0 ignorados)**; build Release `-warnaserror` de `Sigov.sln` **0w/0e** a cada rodada com mudança de código + revalidação integral no fechamento.

## Gate E2E TIER2 — rodadas sem máscara

`scripts/c1_tier2_e2e.ps1` sobre apps reais (Web :5099 / Api :5100):

- rodada 1 = **332/2** — resíduo da jornada (acervo 7727 + produto 267/movs) quebrou as contagens exatas E1/E2 (121/122+1);
- rodada 2 = **333/1** — comentário `#` no bloco SQL do cleanup (psql) abortava o cleanup com `ON_ERROR_STOP` → CX falhou;
- rodada 3 = **320/14** — estado remanescente do cleanup interrompido da rodada 2 (emprestimos/reservas C1S7/C2S3) colidiu com a recriação dos fixtures (efeito cascata do mesmo defeito de driver);
- **corrida oficial = 334/334 PASS**, `fixture.removido restantes=0` agora autoversificável (inclui os termos `C1S6-JORNADA` e órfãos de merenda). Artefatos: `artifacts/c1_tier2_e2e/c1-tier2-e2e-result.json` / `c1-tier2-e2e-report.md`.

## Notas de ambiente e resíduos declarados (regra 13 — nada mascarado)

- **Screenshots BLOCKED (regra 18):** `browser.screenshot` do harness falha consistentemente ("Screenshot needs a visible tab"); a evidência BR-6.5 foi substituída de forma nomeada por HTML renderizado pós-JS das 7 áreas (arquivos 01–08), medições de estilo computado em viewports reais (09), estados de rede e toasts capturados por polling — sem screenshot.
- **Injeção de `Sigov:ApiBaseUrl` no `_Layout` adiada** (follow-up nomeado, candidato à S7): nenhuma view lê a key server-side; injetar o default commitado (`http://localhost:5001`) em página https poderia causar mixed content; os dois clientes (`sigov.api.js` + mini-shim) já implementam fallback idêntico e a ausência da key emite warning nomeado por página (comportamento esperado documentado).
- Herdado das sprints anteriores (sem alteração nesta): RAM crônica baixa do host (apps em shells dedicados + sonda de readiness), container `sigov-api` Restarting(139) legado contornado via API local, `rc50_25` órfã, `validate-script-completop` (seed `admin@sigov.local`), `p1_dev.schema_migrations` não registra as entradas 20261012xxxx, fila de reserva tamanho 1, custo/aluno projetado a consumir na S20.
- Fixtures da jornada (produto 267, movs 327–329, estoque, acervo 7727) são dados fictícios de homologação (regras 9/10) e o gate os limpa idempotentemente ao final de cada corrida.

## Jornada da fatia e gates

- **Jornada da fatia: IMPLEMENTADO + VALIDADO POR TESTES + EXECUTADO EM INTEGRAÇÃO + HOMOLOGADO PELA INTERFACE.**
- Gates: manifest **232 invariante** (sem alteração de schema — regras 6–7); build Release `-warnaserror` **0w/0e**; suítes **851 verdes (522/171/158)**; TIER2 E2E HTTP **334/334** com `restantes=0`; driver PS validado pelo parser (`SYNTAX OK`, regra 19) e JSONs alterados revalidados; logs temporários de depuração removidos antes do fechamento; RC própria desta sprint (regra 20 — P1, fora da RC50.68A).
- Próximo exato: **Ciclo 2 Sprint 7 — Mapa da frente administrativa (contratos de auditoria, sem implementação)** (BR-7.x do roadmap: Compras Lei 14.133/2021 → Licitação → Contrato → Fornecimento → Recebimento → Estoque/Patrimônio → Obrigação financeira). GED por último.
