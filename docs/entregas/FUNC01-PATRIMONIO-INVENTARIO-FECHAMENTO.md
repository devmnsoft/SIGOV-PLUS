# Fechamento FUNC01 — Patrimônio e Inventário

## Estado

**FUNC01 entregue em código, pendente dos gates de runtime/banco descritos abaixo.** A implementação substitui a antiga tela decorativa/fallback por jornadas persistentes Dapper em MVC e API, dashboard real, inventário e auditoria transacional.

A RC50.68 permanece **BLOCKED** por ambiente/CI. Nenhuma promoção foi declarada. A RC50.69 permanece não iniciada/não promovida; FUNC01 é uma trilha funcional paralela.

## Banco e compatibilidade

A migration `20260824120000_func01_patrimonio_inventario.sql` cria o contrato bigint do módulo. Quando encontra as antigas tabelas patrimoniais com PK UUID, preserva-as com sufixo `_legado_uuid`, sem conversão ou descarte, e cria o contrato canônico. FKs auxiliares só são adicionadas quando tabela e tipo são compatíveis. Categorias e permissões são seeds idempotentes e fictícios/estruturais.

## Critérios cobertos

- cadastro, edição, movimentação e baixa de bem;
- inventário geral ou filtrado, conferência, divergência e fechamento;
- dashboard com agregações reais;
- permissões persistidas e negação por padrão;
- auditoria na mesma transação de cada escrita;
- CSV com minimização e proteção contra formula injection;
- telas responsivas Bootstrap, estados vazios, badges, confirmação e validação;
- migration, manifest e scripts consolidados sincronizados.

## Validação

Os comandos e resultados executados nesta entrega constam na mensagem final do PR. Gates indisponíveis devem permanecer **BLOCKED**, nunca PASS inferido. Em ambiente seguro com PostgreSQL 16, aplicar e reaplicar `script_completop.sql` com `ON_ERROR_STOP=1`, e executar o build runtime .NET 10.
