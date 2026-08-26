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

## Fechamento CORR19

A CORR19 consolidou as telas expandidas sobre as tabelas `defesa_civil_*`, eliminando a mistura de identificadores do legado. Áreas, cenários, planos, rotas, ocorrências, respostas, equipes, abrigos, recursos, estoque, doações, alertas, fontes, comunicações, evidências e ocupação usam seletores contextuais e revalidação no servidor. Pessoas aparecem mascaradas no seletor e não são emitidas nos CSVs operacionais.

A migration corretiva `20260826210000_corr19_defesacivil360_integridade.sql` acrescenta domínios de status, unicidade contextual e gatilhos fail-closed para vínculo entre tenant/entidade/exercício, publicação de plano/alerta, encerramento com evidência, sobreposição de equipe/recurso e lotação do abrigo. Saídas de estoque continuam protegidas pelo saldo não negativo; movimentações devem atualizar o saldo na mesma transação.

### Operação e validação

- conexão: exclusivamente `ConnectionStrings__DefaultConnection`;
- publicação e exportação: permissões persistidas `DEFESA_CIVIL_*`;
- ausência de SDK, PostgreSQL ou credenciais é `BLOCKED`, nunca sucesso simulado;
- smoke autenticado exige banco migrado, usuário real e claims de tenant/entidade.
