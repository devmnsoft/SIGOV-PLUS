# Auditoria de paridade PostgreSQL — 2026-09-22

## 1–5. Escopo, defeito, correção, implementação e estado parcial

Foram relidos o `README.md`, as regras do repositório, o status real dos módulos,
o changelog, o manifesto, a governança das migrations, o gerador PowerShell e os
scripts consolidados. A auditoria anterior já classificava conservadoramente todas
as áreas solicitadas como parciais, sem evidência runtime conjunta. Essa
classificação foi preservada.

O gate Bash validava o catálogo e os arquivos individuais, porém não detectava
divergência nos seis artefatos consolidados exigidos pelo repositório. O gate agora
confere os quatro aliases de produção, os dois artefatos de desenvolvimento, a
ordem, o checksum e o corpo normalizado de cada migration do baseline, as exclusões
e a concatenação determinística dos seeds fictícios. Nenhuma funcionalidade de
produto foi declarada concluída.

## 6–12. Regras, banco, UI e produto

- Regras de negócio consolidadas: nenhuma alterada nesta fatia de estabilização.
- Migrations criadas: nenhuma; não houve alteração de schema.
- Scripts atualizados: nenhum conteúdo SQL precisou ser regenerado; os seis
  artefatos passaram na comparação.
- Telas, menus, relatórios e dashboards: não alterados.
- Permissões, tenant, entidade e exercício: não alterados; continuam aguardando
  evidência runtime nos fluxos descritos no status oficial.

## 13–17. Testes e evidências

- `bash -n scripts/check-migration-catalog.sh`: **PASS**.
- `./scripts/check-migration-catalog.sh`: **PASS estático**, com 195 SQLs, 185
  entradas de manifesto, 181 migrations no baseline e dez órfãs governadas.
- `dotnet restore`, `dotnet build` e `dotnet test`: **BLOCKED**, pois o SDK .NET
  10.0.100 não está instalado no ambiente.
- Instalação limpa, reaplicação, upgrade e pós-condições: **BLOCKED**, pois
  PostgreSQL 16 e `psql` não estão instalados e não há connection string.
- Login, autorização, isolamento e navegação responsiva: **BLOCKED**, pois API/Web
  não podem iniciar sem runtime e banco; não foi produzida falsa evidência manual.

## 18–19. Riscos e próximo ciclo

O risco principal continua sendo confundir integridade textual com convergência do
banco. O gate novo detecta drift determinístico, mas não interpreta DDL nem substitui
o runner canônico. O próximo ciclo deve provisionar .NET 10.0.100 e PostgreSQL 16,
executar o Gate A completo e, somente depois, selecionar uma jornada vertical para
fechamento ponta a ponta.

## Matriz final

| Área | Funcionalidade | Estado anterior | Estado final | Evidência | Pendência |
|------|----------------|----------------|--------------|-----------|-----------|
| Banco | Catálogo de migrations | Parcial; arquivos individuais validados | Parcial; catálogo e baseline validados estaticamente | Gate Bash: 185 entradas, 181 no baseline, 10 exclusões governadas | Aplicar, reaplicar e validar upgrade no PostgreSQL 16 |
| Banco | Consolidados de produção | Não cobertos pelo gate Bash | Paridade estática validada | Quatro aliases idênticos; ordem, checksum e corpo conferidos | Equivalência semântica e execução real |
| Banco | Consolidados de desenvolvimento | Não cobertos pelo gate Bash | Paridade determinística validada | Dois aliases iguais ao baseline mais seeds autorizados | Executar somente em ambiente de desenvolvimento controlado |
| Produto | 18 áreas obrigatórias | Parcial ou não verificado | Inalterado; sem promoção indevida | `STATUS_REAL_MODULOS.md` | Gate A e homologação ponta a ponta |
| Runtime | Build, banco, login e navegação | Bloqueado | Bloqueado | Ausência explícita de SDK, PostgreSQL, credencial e navegador autenticado | Provisionar ambiente de homologação |
