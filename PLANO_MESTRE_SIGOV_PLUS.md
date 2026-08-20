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
- [ ] RC50.68B — avaliador de autorização persistente e precedência de negativas.
- [ ] RC50.68C — troca auditada de contexto (tenant/entidade/exercício/unidade).
- [ ] RC50.68D — dashboard operacional do SuperAdmin.

## Regras permanentes
O banco é a fonte de autoridade para autorização e parametrização. Não são aceitos
mock/demo/fallback, segredo versionado, nova PK UUID ou drift entre migration, manifest
e scripts distribuíveis. Riscos P0/P1 bloqueiam a promoção da release.
