# RC50.67 — relatório de homologação integrada real

Data UTC: 2026-08-20. **Decisão: NÃO APTO / P0 ambiental.** Este host não possui runtime .NET nem cliente PostgreSQL; portanto nenhum resultado estático foi promovido a homologação funcional.

## 1–7. Escopo, módulos e massa

1. **Relatórios analisados:** RC50.57, 58, 59, 60, 61, 62, 63, 64 e 66, inventário/checklist RC50.66 e os documentos de gate/bloqueios de produção.
2. **Módulos homologados em runtime:** nenhum neste host. Os 37 grupos exigidos foram preservados no plano/inventário; foram encontradas também estruturas empresariais e Assistência Social.
3. **Módulos com pendência:** todos exigem o smoke autenticado; integrações oficiais e operações avançadas já classificadas como preparatórias continuam pendentes.
4. **Não regressão:** preservação e contratos foram verificados estaticamente; ausência de regressão runtime não foi declarada.
5. **Carga criada:** `seed_homologacao_funcional.sql`, idempotente, protegida por ambiente, reutiliza a guarda administrativa e a massa documental existente, habilita módulos e publica marcadores outbox por domínio.
6. **Usuários/perfis:** admin e superadmin canônicos, Gestor Fazenda, Funcionário Financeiro, Gestor Educação, Professor, Gestor Saúde, ACS, Atendimento, Auditor, Gestor Agro, Técnico Rural e Operador Patrulha. Hash PBKDF2 canônico; e-mails `invalid.local`; nenhum segredo em artifact.
7. **Dados por módulo:** protocolo/documento/workflow/API key fictícios e eventos idempotentes para Tributário, Financeiro, Saneamento, Educação, Saúde, Processos, GED, RH, Folha, Compras, Licitações, Contratos, Almoxarifado, Patrimônio, Frotas, Obras, Agro, Geo e transversais. A criação de registros setoriais profundos permanece a cargo das services oficiais para evitar SQL incompatível com o schema canônico.

## 8–18. Resultados executáveis

8. **Banco:** bloqueado antes do apply: `psql` ausente.
9. **Seed:** bloqueada pelo mesmo P0; não se afirma aplicação ou segunda execução idempotente.
10. **Build:** bloqueado: `dotnet` ausente. O `sigov.runtime.slnf` já inclui API, Web, Worker, Application, Domain e Infrastructure e não inclui projetos de teste.
11. **API:** não iniciada.
12. **Web:** não iniciada.
13. **Worker:** não iniciado; falha ambiental controlada pelo runner.
14. **Dashboards:** manifesto cobre Minha Central, catálogo/status e Agro; não executados.
15. **Menus:** 6.260 ocorrências de controllers/actions e 1.921 referências Web foram inventariadas; ausência de 404 depende do runtime.
16. **Endpoints:** manifesto cobre health, transversais, catálogo/status e Agro/Geo; nenhum probe HTTP executado após o bloqueio.
17. **Agro/Geo:** JS válido e contratos de dashboard/mapa/camadas preservados; CRUD e integração transversal aguardam runtime.
18. **GeoJSON:** endpoint inventariado; privacidade, validade e auditoria aguardam chamada autenticada.

## 19–23. Segurança, LGPD, auditoria e erros

19. **Permissões:** grants da seed são segregados por módulo/perfil; SuperAdmin permanece na guarda canônica. Escopos turma/microárea/tenant exigem jornada autenticada.
20. **LGPD:** massa usa identidades fictícias, domínios inválidos e payload outbox com `pii=false`; artifact não grava cookie, token, connection string nem senha.
21. **Auditoria:** contratos existentes e requisito de exportação auditada foram preservados; comprovação GeoJSON aguarda runtime.
22. **Erros corrigidos nesta sprint:** ausência de seed funcional única; ausência de runners Linux/Windows; ausência de manifesto HTTP e artifact sanitizado; ausência dos documentos RC50.67.
23. **Erros pendentes:** aplicação dupla da seed, build, inicialização dos três processos, login e jornadas por perfil, todos os probes e dados setoriais profundos.

## 24–26. Priorização

- **P0:** ferramentas `dotnet`, `psql`, `pg_dump`, `pg_restore` e `pwsh` ausentes; banco/build/runtime não comprovados. Também é P0 qualquer 404 crítico, 500 de dashboard, 501 essencial, vazamento de tenant/PII ou falha de idempotência quando o gate for executado.
- **P1:** completar massa setorial pelas services canônicas; provar botões/transições, perfis operacionais, exportações e integrações internas.
- **P2:** integrações oficiais externas, evidência visual e refinamentos de experiência após gate verde.

## 27–30. Comandos, artifacts, ambiente e RC50.68

27. **Comandos:** comandos Git da fase 0; varreduras `rg`; versões de ferramentas; `./scripts/homologacao/homologacao-funcional.sh`; manifest/índices/rotas; `bash -n`; JSON; três `node --check`; buscas de `SELECT *`, raw literals, `.TotalCount` e 501. `dotnet clean/restore/build`, apply e gates runtime não puderam iniciar por ausência das ferramentas.
28. **Artifacts:** `artifacts/homologacao/homologacao-20260820T100456Z.jsonl`, sanitizado, registra `BLOCKED` e a ferramenta encontrada primeiro (`dotnet`). Em host equipado, logs separados de banco, seed, build, API, Web e Worker são produzidos.
29. **Ferramentas ausentes exatamente:** `dotnet`, `psql`, `pg_dump`, `pg_restore`, `pwsh`. `node v20.20.2` está disponível.
30. **RC50.68:** executar o runner Linux e `./scripts/smoke-production-like.sh`; no Windows executar `.\\scripts\\prod-gate-local.ps1` e `.\\scripts\\homologacao\\homologacao-funcional.ps1`; aplicar a seed duas vezes; fornecer tokens/credenciais efêmeras ao runner sem persistir segredos; fechar somente falhas reproduzidas.

## Validações estáticas

Manifest e JSON válidos; conflitos: zero em 611 rotas API; validadores de migrations retornaram sucesso com 53/129/7 avisos conservadores históricos; JS Agro CRUD, Agro Geo e UI passou em `node --check`; zero `SELECT *`, raw literal C# e `.TotalCount`. A busca ampla por `501` encontrou apenas o código PostgreSQL `42501` numa view de diagnóstico, não um HTTP 501. Nenhuma classe, fixture, mock, pasta ou projeto de teste foi criado nesta sprint.
