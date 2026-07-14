# Manual administrativo SIGOV PLUS Pós-RC 14

- Execute migrations PostgreSQL em ordem e aplique `pos_rc_homologacao_demo.sql` apenas em ambiente permitido.
- Valide tenant obrigatório, perfis, permissões, API Key, webhooks, outbox e auditoria antes de homologar.
- Rode `scripts/smoke-test-sigov.ps1`, `scripts/go-live-check.ps1` e `scripts/package-release.ps1 -Version 1.0.0-rc-final`.
- Revise artifacts: test-results, docker logs, schema report, smoke MD/JSON, go-live, pacote de release e evidências Pós-RC 14.
- Configure storage/GED antes de liberar download/visualização real de anexos.
- Não rode seeds demo em produção sem flag explícita.
