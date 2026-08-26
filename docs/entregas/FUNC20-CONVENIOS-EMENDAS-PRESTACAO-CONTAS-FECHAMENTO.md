# Fechamento técnico — FUNC20

## Entrega CORR20

- Fluxos MVC/Razor navegáveis para os 16 recursos, dashboard, relatórios e auditoria.
- Relacionamentos exclusivamente por seletores carregados do PostgreSQL e filtrados pelo contexto.
- Validação server-side de status, obrigatoriedade, números, datas, percentuais e transições críticas.
- Bloqueio de decisão final de prestação com diligências pendentes e auditoria semântica das ações.
- Dashboard real com valores global, repassado, contrapartida e executado, vencimentos, prestações e diligências.
- Listas responsivas com filtros, datas, paginação, estado vazio, badges e confirmação de exclusão.
- CSV autorizado, filtrado e protegido contra formula injection.
- Migration corretiva com constraints e índices, registrada no manifesto e nos consolidados.

## Validação desta entrega

- `dotnet build --no-restore`: **BLOCKED: comando dotnet build --no-restore não executado porque o SDK dotnet não está instalado no ambiente.**
- `psql --version`: registrar como BLOCKED se o cliente PostgreSQL não estiver disponível.
- Manifesto JSON, hashes e igualdade dos blocos consolidados: validados por comandos locais documentados no PR.
- Rotas MVC/Razor: conferidas estaticamente no controller e nas views; execução depende do SDK e de PostgreSQL configurado por `ConnectionStrings__DefaultConnection`.
