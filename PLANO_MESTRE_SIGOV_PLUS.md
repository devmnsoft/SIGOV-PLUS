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
- [ ] RC50.68C — troca auditada de contexto (tenant/entidade/exercício/unidade). A implementação
  foi integrada no PR #267, porém sua promoção permanece bloqueada pela homologação e pelos gates
  de banco/CI da RC50.68C-R2; o secret administrativo `SIGOV_CI_DB_PASSWORD` ainda precisa ser
  confirmado/configurado e os workflows precisam ser reexecutados.
- [ ] RC50.68D — dashboard operacional do SuperAdmin.

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
- [ ] A promoção da RC50.68C permanece pendente da RC50.68C-R2; a RC50.68D não foi iniciada.
