# Entrega RC50.84 — Módulos Estruturantes Multi-Esfera

## Evidências

- Migration idempotente com onze tabelas complementares de histórico/fluxo.
- Contexto obrigatório por tenant, entidade e esfera de governo.
- Índices por contexto, órgão, unidade, exercício, status e data.
- Integridade de domínio para esfera, tipo, status, prioridade, valores,
  percentuais, datas e UF.
- FKs para tenant e entidade e permissões persistidas por domínio.
- Baselines de produção e desenvolvimento sincronizadas com a migration.
- Manifesto com SHA-256 e pós-condição verificável.

## Gate desta cópia de trabalho

`dotnet build` ficou **BLOCKED** porque `dotnet` não está disponível no `PATH`.
A ausência da ferramenta não foi tratada como sucesso. As validações estáticas de
JSON, checksum, sincronismo e padrões SQL continuam obrigatórias.

## Critérios para implantação

1. Executar build e testes existentes com SDK 10.0.100.
2. Aplicar em PostgreSQL 16+ e confirmar a pós-condição do manifesto.
3. Validar permissões com usuários de cada esfera e unidade.
4. Fazer smoke das rotas estruturantes e exportações sob contexto autorizado.
5. Não publicar se houver divergência entre migration, manifesto e baselines.
