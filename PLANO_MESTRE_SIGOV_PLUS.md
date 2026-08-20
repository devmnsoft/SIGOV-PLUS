# Plano Mestre SIGOV PLUS

## Fundação concluída — RC50.68A
- [x] Fixar .NET 10/C# 14 e contrato único de conexão.
- [x] Remover `.env` do Git e segredos literais dos gates.
- [x] Persistir autorização com recurso+ação e escopos de tenant, entidade, exercício,
  unidade, vigência, alçada e negativa explícita, reutilizando o schema existente.
- [x] Entregar migration/manifest/scripts sincronizados e seed fictícia idempotente dos
  oito perfis institucionais.
- [x] Formalizar Definition of Done e relatório da release.

## Próximas releases (não implementar nesta RC)
- [x] RC50.68B — avaliador de autorização persistente, auditável e precedência de negativas.
- [x] RC50.68C — troca auditada de contexto (tenant/entidade/exercício/unidade), integrada nos
  PRs #267/#268. A implementação está entregue; a promoção conjunta da RC50.68 permanece
  bloqueada pelos gates externos de runtime, banco e CI registrados na RC50.68E.
- [x] RC50.68D — dashboard operacional do SuperAdmin entregue com leitura Dapper, autorização
  persistente, exports protegidos e estados explícitos para áreas opcionais. A promoção permanece
  condicionada aos gates de runtime, PostgreSQL e CI descritos no relatório da RC50.68E.
- [ ] RC50.68E-R2 — nova tentativa de fechamento executada sobre o merge do PR #270; promoção
  **BLOCKED** porque o runner não dispõe de .NET 10, PostgreSQL/psql, PowerShell ou actionlint e
  não possui `SIGOV_CI_DB_PASSWORD` nem autenticação GitHub. Evidências em `docs/RC50.68E-R2.md`.
- [ ] RC50.69 — ERP Serviços é a próxima macro-release e não foi iniciada. Só pode começar após
  a RC50.68 ficar sem pendência P0/P1.

## Regras permanentes
O banco é a fonte de autoridade para autorização e parametrização. Não são aceitos
mock/demo/fallback, segredo versionado, nova PK UUID ou drift entre migration, manifest
e scripts distribuíveis. Riscos P0/P1 bloqueiam a promoção da release.

## RC50.68B — avaliador persistente
- [x] Centralizar decisão fail-closed em avaliador Dapper parametrizado.
- [x] Aplicar vigência, escopos, alçada e precedência de `NEGAR`.
- [x] Converter serviços legados e handler Enterprise em adapters sem autoridade paralela.
- [x] Policies Web e diagnóstico da API consomem a mesma decisão persistente, sem autoridade em claims.
- [x] Trilha global de decisões e integridade/estado dos vínculos adicionadas por migration corretiva.
- [ ] Ampliação do CRUD administrativo legado permanece como risco residual: as telas atuais de perfis e
  permissões persistem dados, mas grupos e a matriz contextual ainda não cobrem integralmente todos os campos.
- [x] RC50.68C e RC50.68D foram implementadas; sua promoção produtiva integra o gate único da
  RC50.68E e não deve ser confundida com conclusão apenas documental.
