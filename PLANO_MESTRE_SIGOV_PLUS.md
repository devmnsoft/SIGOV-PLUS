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
- [ ] RC50.68E-R4 — execução da esteira tentada no candidato
  `f6da64e3b756640b1322e7d0b8a3e506f7c92311`, mas **BLOCKED** antes do dispatch porque o ambiente
  não possui autenticação GitHub para confirmar o repository secret `SIGOV_CI_DB_PASSWORD` nem
  consultar Actions. Nenhum gate foi marcado PASS e ainda é obrigatória uma run integralmente verde.
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
- [x] RC50.68F-R2 estabiliza SQL Dapper, contratos e tela do CRUD, sem alterar a promoção bloqueada.
- [x] RC50.68F fecha o risco residual do CRUD administrativo: SuperAdmin administra perfis, grupos,
  permissões e os três vínculos da matriz com escopos, vigência, efeito, alçada, estado e auditoria.
- [ ] A promoção continua **BLOCKED** pelo gate de CI real da RC50.68E-R4; esta conclusão funcional
  não inicia a RC50.69 nem substitui execução verde da esteira.
- [x] RC50.68C e RC50.68D foram implementadas; sua promoção produtiva integra o gate único da
  RC50.68E e não deve ser confundida com conclusão apenas documental.
