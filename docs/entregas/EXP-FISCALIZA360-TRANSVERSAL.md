# Entrega EXP-FISCALIZA360-TRANSVERSAL

## Resultado

Foi implementada a base transversal reutilizável de fiscalização de campo, sem alterar GED/Protocolo nem avançar módulos fora do escopo. Inclui schema idempotente, permissões, Dapper parametrizado, isolamento contextual, auditoria, rotas MVC/Razor, formulários validados e relatórios CSV.

## Artefatos

- Banco: onze tabelas `fiscalizacao_*`, integração com `evidencia_transversal` e `sincronizacao_outbox`.
- MVC: `/Fiscalizacao` e todas as subrotas previstas no ciclo.
- Segurança: policies persistidas, antiforgery em POST, transições server-side e seleção de vínculos sem IDs manuais.
- FUNC13/FUNC14/FUNC18/FUNC19: resolução dos registros fiscalizados nos repositórios oficiais de Obras, Ambiental, Trânsito e Defesa.

## Comandos e bloqueios

Os comandos finais de build, geração/verificação dos scripts, análise estática de formulários e rotas são executados antes do commit e reportados no resumo da PR.

BLOCKED: sincronização externa não executada porque o repositório não contém adaptador/worker oficial para o destino de campo; nenhum sucesso é simulado.

- `dotnet build --no-restore`: BLOCKED: comando dotnet build --no-restore não executado porque o SDK dotnet não está instalado no ambiente.
- `pwsh -NoProfile -File scripts/validate-script-completop.ps1`: BLOCKED: comando pwsh -NoProfile -File scripts/validate-script-completop.ps1 não executado porque o PowerShell não está instalado no ambiente.
- `psql`: BLOCKED: comando psql não executado porque o cliente PostgreSQL não está instalado e não há conexão de banco configurada no ambiente.
- `git diff --check`, validação JSON e buscas estáticas de IDs, validação, antiforgery e rotas: concluídos sem erro.
