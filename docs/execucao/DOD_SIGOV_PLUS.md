# Definition of Done — SIGOV PLUS

## Plataforma e arquitetura
- SDK .NET 10 conforme `global.json`, C# 14 e build sem drift de versão.
- Camadas Domain/Application/Infrastructure/Api/Web preservadas.
- PostgreSQL 16+ e Dapper são a persistência oficial.

## Banco de dados
- Nova PK é `bigint identity`; UUID legado não sofre conversão destrutiva.
- Alteração possui migration idempotente e reaplicável.
- Manifest, checksums, scripts completo estrutural e dev são regenerados juntos.
- Seed é opt-in, fictícia, sem segredo e idempotente.
- Perfis, permissões, grupos, escopos e parâmetros persistidos são a autoridade.

## Execução segura
- O único contrato da aplicação é `ConnectionStrings__DefaultConnection`.
- Credenciais são injetadas pelo ambiente/secret store; `.env` nunca é commitado.
- Não há mock, demo, fallback silencioso ou senha literal em gate.
- Falta de SDK, PostgreSQL ou outra ferramenta produz resultado **BLOCKED**, nunca PASS.

## Evidências
- YAML/JSON/scripts validados com ferramentas disponíveis.
- Restore, build, testes existentes, aplicação dupla da migration/seed e smoke executados
  quando a infraestrutura existir, com resultado registrado.
- Nenhuma nova classe de teste é criada para satisfazer a entrega.
