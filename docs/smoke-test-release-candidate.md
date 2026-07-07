# Smoke test Release Candidate — Pós-RC 03

Classificação: **Parcial / pendente de execução no ambiente atual**.

Execute `pwsh -NoProfile -File scripts/smoke-test-sigov.ps1` após `docker compose up -d` para validar rotas Web, health checks, API v1 sem key (401) e API v1 com key válida.
