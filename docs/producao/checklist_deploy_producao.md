# Checklist de deploy

## Pré-deploy
- [ ] change/aprovação, janela, responsáveis e artefato Release/hash registrados.
- [ ] .NET Runtime compatível e PostgreSQL suportado; portas/firewall, DNS, TLS e proxy validados.
- [ ] secrets externos, conexão a `postgres`/schema `sigov`, CORS, Swagger, storage, logs e workers revisados.
- [ ] backup executado e restore recentemente ensaiado; rollback e artefato anterior disponíveis.
- [ ] validadores, migration em clone e build `-warnaserror` aprovados.

## Deploy e pós-deploy
- [ ] drenar tráfego/desligar workers; aplicar migrations uma vez com `ON_ERROR_STOP=1`.
- [ ] publicar API/Web, validar liveness/readiness, logs, migration e depois ativar worker.
- [ ] validar login/troca inicial de senha, MinhaCentral, ProjectStatus, Observabilidade, permissões, LGPD e auditoria.
- [ ] confirmar Swagger inacessível, headers/TLS/cookie, páginas críticas, exportação auditada e ausência de 404/500.
- [ ] executar smoke, arquivar resultado sanitizado e monitorar latência/erros/filas após liberar tráfego.

## Evidência automatizada RC50.54
- [ ] Workflow **SIGOV+ Production Gate** verde e artifacts baixados/inspecionados.
- [ ] Gate Windows executado em `C:\MNSOFT\SIGOV-PLUS`; admin/superadmin validados sem registrar credenciais.
- [ ] Nenhum `SKIP` obrigatório; logs e dumps não contêm senha/conexão completa.
