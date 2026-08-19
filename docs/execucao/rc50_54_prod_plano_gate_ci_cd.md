# RC50.54-PROD — plano do gate CI/CD

Data: 2026-08-19. Baseline: RC50.53-PROD.

## Herança e execução

A RC50.53 bloqueou corretamente banco limpo/parcial, build e runtime porque `dotnet`, `psql`, `pg_dump` e `pg_restore` estavam ausentes. O novo workflow transfere a prova para Ubuntu com .NET definido por `global.json` e PostgreSQL/PostGIS 16; executa validação estática, dois bancos isolados, build, API, Web, Worker, smoke, backup/restore e publica evidências sanitizadas. No Windows, `scripts/prod-gate-local.ps1` replica as etapas e preserva senha/conexão apenas no processo.

## Triagem e severidade

* **P0:** ferramenta obrigatória ausente ou `SKIP`; migration que falha limpa/parcialmente; índice com coluna não garantida; expressão de índice não imutável; build warning/erro; health/Swagger indisponível; 404/500 crítico; segredo em artifact.
* **P1:** warning conservador em migration histórica mitigada, prova autenticada de admin/superadmin e persistência funcional que requer operação local; precisa responsável/evidência antes da promoção.
* **P2:** falso positivo em comentário/SQL dinâmico já protegido e melhorias não bloqueantes.

Os históricos registrados na RC50.53 foram 49 avisos de índice parcial, 126 de colunas e 7 de `COALESCE`. A execução com PostgreSQL decide risco real; a classificação detalhada está no relatório de triagem.

## Critério “apto”

Todos os seis jobs devem estar verdes, sem P0, sem `SKIP` obrigatório, com artifacts sem segredo. Banco limpo e legado parcial devem convergir, build deve usar `-warnaserror`, runtimes e páginas críticas não podem falhar, e restore isolado deve ser verificável. Login real e fluxos autenticados continuam obrigatórios no Windows/homologação; sem essa evidência a decisão formal permanece **não apto**.
