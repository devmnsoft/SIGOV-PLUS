# Runtime smoke tests — Sprint SaaS comercial

Data: 2026-07-01.

## Validação inicial

- `dotnet restore`: não executado no host porque o SDK `dotnet` não está instalado (`dotnet: command not found`).
- `dotnet build`: pendente pelo mesmo motivo.
- Docker/runtime HTTP: pendente nesta estação até SDK/imagens locais ficarem disponíveis.

## Rotas incluídas no checklist final

- `/Saas/Planos` com catálogo real via `sigov.plano_saas` ou demonstrativo honesto.
- `/Saas/Assinaturas` com fallback “em implantação” quando `sigov.assinatura_saas` não existir.
- `/Marketplace` com catálogo de módulos e aviso de persistência necessária para contratação.
- `/Notificacoes` com tabela real ou recomendações derivadas.
- `/Busca?q=admin` com inspeção schema-safe e máscara básica.
- `/Portal` com autoatendimento e fallback honesto para suporte/faturas.
