# Plano de migração .NET Pós-RC 15

Gerado em 2026-07-14. Este plano não executa migração automática; ele define cenários, riscos, validações e rollback para uma migração segura do SIGOV PLUS.

## Cenários comparados

| Cenário | Benefícios | Riscos | Pré-requisitos | Validação obrigatória | Decisão recomendada |
|---|---|---|---|---|---|
| A — manter .NET 6 temporariamente | Menor risco imediato; evita alteração simultânea de runtime e pacotes. | .NET 6 está fora de suporte; maior exposição a CVEs e incompatibilidades futuras. | Produção presa a runtime 6 e janela curta de sustentação. | Restore, build, test, Docker, smoke, go-live e pacote release no ambiente atual. | Aceitável apenas como transição curta e documentada. |
| B — migrar para .NET 8 LTS | LTS estável; menor salto técnico; ecossistema maduro. | Exige atualizar Microsoft.AspNetCore.* e Microsoft.Extensions.* para 8.x; pode ativar novos analyzers. | Branch própria, SDK/runtime Docker 8, pacote Microsoft 8.x, CI atualizado. | Build/test completos, Docker compose, migrations, seeds idempotentes, smoke E2E, go-live e rollback. | Melhor caminho de curto prazo após estabilizar Pós-RC 15. |
| C — migrar para .NET 10 LTS | Maior longevidade de suporte e modernização técnica. | Maior risco de breaking changes em SDK, analyzers, ASP.NET Core, bibliotecas e Docker. | Inventário de compatibilidade atualizado para todos os pacotes e ambiente de homologação dedicado. | Mesmo pacote de validação do cenário B, com testes regressivos ampliados e observabilidade. | Planejar depois do .NET 8 ou como trilha separada de plataforma. |

## Estratégia recomendada

1. Fechar Pós-RC 15 em `net6.0` sem falso sucesso, registrando limitações de ambiente e evidências reais.
2. Criar branch exclusiva para .NET 8 LTS.
3. Atualizar `TargetFramework`, imagens Docker e pacotes Microsoft para 8.x na mesma alteração controlada.
4. Manter Dapper e FluentValidation inicialmente, evitando upgrades desnecessários no primeiro corte.
5. Rodar `dotnet restore`, `dotnet build`, `dotnet test`, Docker compose, migrations, seed duas vezes, smoke E2E, go-live e package release.
6. Se qualquer etapa crítica falhar, reverter a branch de migração e manter produção no baseline anterior.

## Plano de rollback

- Não aplicar migração diretamente na branch de release.
- Preservar tag/commit do último Pós-RC 15 validado.
- Reverter `Directory.Build.props`, `Directory.Packages.props`, Dockerfiles, workflows e qualquer ajuste de API causado por breaking changes.
- Restaurar imagens Docker anteriores e connection strings sem alteração de schema destrutiva.
- Se migration nova for necessária na branch de migração, garantir script reversível antes de homologação.

## Critérios de aceite da migração

- Build sem warnings com `TreatWarningsAsErrors=true`.
- Testes unitários, integração e API verdes.
- Docker compose com PostgreSQL, API, Web e Worker saudáveis.
- Smoke E2E cobrindo login, Dashboard, Minha Central, Busca, Relatórios, Auditoria, Agenda, Kanban, Protocolo/GED e Enterprise.
- Go-live check e package release sem segredos, dumps, storage, certificados ou tokens.
- Evidências anexadas antes de declarar homologação.
