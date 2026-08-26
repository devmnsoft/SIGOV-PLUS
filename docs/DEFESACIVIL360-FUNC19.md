# DefesaCivil360 — expansão do FUNC19

## Escopo

O DefesaCivil360 permanece no FUNC19 e usa MVC/Razor, Dapper/Npgsql e PostgreSQL. A expansão cobre risco e cenários, população vulnerável, contingência e rotas, resposta a ocorrências, equipes, abrigos e ocupação, recursos vinculáveis a patrimônio/frotas, embarcações, estoque, doações, fontes identificadas, alertas, comunicação e evidência transversal.

## Segurança e contexto

As tabelas exigem `tenant_id`, `entity_id`/`entidade_id` e `exercicio_id`. O repositório parametriza valores e limita leitura e escrita ao contexto autenticado. Permissões `DEFESA_CIVIL_*` são resolvidas pela autorização persistente e falham fechadas. Vínculos são escolhidos em listas e revalidados no servidor.

População vulnerável e ocupação de abrigos guardam apenas vínculo com a pessoa canônica. A identificação individual requer `DEFESA_CIVIL_DADO_SENSIVEL_VIEW`; exportação individual requer `DEFESA_CIVIL_DADO_SENSIVEL_EXPORT` e auditoria. Relatórios de abrigo são agregados e não publicam identificação pessoal.

## Integrações

- evidências referenciam `evidencia_transversal`; não existe upload simulado;
- ocorrências podem referenciar ordens do Fiscaliza360, sem alterar autos ou sanções;
- recursos e embarcações aceitam os identificadores canônicos de patrimônio/frotas;
- comunicação registra outbox, publicação e erro técnico sanitizado;
- fonte meteorológica/hidrológica sem adaptador real fica explicitamente `INDISPONIVEL`.

## Regras e rotas

Constraints impedem quantidades, capacidade, vagas, estoque ou doação negativos, validam coordenadas e períodos e limitam severidade, nível e status. O ponto de entrada é `/DefesaCivil`; `/Defesa` continua como alias compatível. CSVs neutralizam fórmulas e exigem `DEFESA_CIVIL_RELATORIO_EXPORT`.
