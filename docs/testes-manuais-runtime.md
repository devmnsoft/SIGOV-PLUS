# Testes manuais de runtime

Use este checklist após subir o ambiente real com Docker Compose.

## Checklist funcional

- [ ] Login: abrir `/Auth/Login`, autenticar, sair e verificar auditoria.
- [ ] Dashboard: conferir KPIs reais ou fallback honesto e atalhos executivos.
- [ ] Minha Central: conferir ações recomendadas, módulos liberados, pendências e atividades.
- [ ] Usuários: listar, validar máscara de dados pessoais e ações críticas.
- [ ] Perfis: listar/criar/editar apenas se schema real permitir.
- [ ] Permissões: listar e conferir fallback sem stacktrace.
- [ ] Tenants: validar campos opcionais e unicidade de slug/código quando existirem.
- [ ] Módulos: ativar/inativar somente com persistência real.
- [ ] Parâmetros: filtrar, validar tipos `string`, `int`, `decimal`, `bool`, `json`, `date`, editar valor e restaurar padrão se `valor_padrao` existir.
- [ ] Protocolo: conferir dashboard/listagem/detalhe/fallback sem simular cadastro.
- [ ] GED: conferir storage, aviso LGPD e fallback de OCR/upload.
- [ ] Tributário: conferir listagens, filtros, CSV e máscara de documentos.
- [ ] Relatórios: baixar CSVs e confirmar ausência de dados sensíveis abertos.
- [ ] POC: confirmar status Funcional/Parcial/Demonstração/Em implantação/Indisponível por item.
- [ ] Health: confirmar Web, API, PostgreSQL, migrations, storage e worker com status real ou Não monitorado.
- [ ] Console: abrir DevTools e confirmar ausência de erro JS próprio.
- [ ] Mobile: validar 1366px, 768px e 390px sem sidebar sobrepor conteúdo.

## Evidências esperadas

- Print do `docker compose ps`.
- Logs `sigov-web`, `sigov-api`, `sigov-worker`, `sigov-db-migrations`, `sigov-postgres` sem loop de erro.
- Resultado de `docs/schema-report-local.md` gerado por `scripts/schema-report.ps1`.
- Tabela de smoke tests atualizada em `docs/runtime-smoke-tests.md` com status codes reais.
