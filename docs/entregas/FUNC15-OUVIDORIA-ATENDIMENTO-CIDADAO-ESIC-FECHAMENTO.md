# Fechamento FUNC15

## Entrega
Módulo funcional integrado nas camadas Application, Infrastructure e Web, com Dapper/Npgsql, PostgreSQL, RBAC persistido, 17 tabelas, dashboard, fluxos operacionais, auditoria e dez exportações CSV. Migration e cinco scripts consolidados foram sincronizados.

## Validações e comandos
- `dotnet build sigov.sln --no-restore`
- `python -m json.tool database/postgres/migrations/manifest.json`
- `psql -v ON_ERROR_STOP=1 -f database/postgres/migrations/20260825090000_func15_ouvidoria_atendimento_esic.sql` quando PostgreSQL estiver disponível.

## Segurança/LGPD
Segregação institucional obrigatória, autorização fail-closed, mascaramento sigiloso, consentimento, exclusão lógica, queries parametrizadas, CSRF, logs correlacionáveis, auditoria transacional e neutralização de fórmulas CSV.

## Bloqueios
Resultados efetivamente observados na validação final devem constar no PR. InovaGED, GED e Protocolo permaneceram intocados.
