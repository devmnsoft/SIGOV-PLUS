# Sala de Situação Municipal

A Sala de Situação reúne ocorrências, crises, obras, metas e alertas com origem rastreável. O registro possui responsáveis, prioridade, prazo, resultado e vínculo opcional a documento GED real.

Estados institucionais: `ABERTA`, `EM_MONITORAMENTO`, `RESOLVIDA` e `CANCELADA`. O banco impede encerramento sem resultado significativo. Itens registram ações e decisões no mesmo contexto de tenant e entidade; índices atendem filas por módulo, status, prioridade, prazo e responsável.

A rota `/Executivo/SalaSituacao` apresenta somente registros persistidos. O estado vazio orienta o operador, sem criar sala ou ocorrência artificial.
