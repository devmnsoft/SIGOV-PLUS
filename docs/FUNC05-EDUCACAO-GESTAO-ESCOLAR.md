# FUNC05 — Educação e Gestão Escolar

## Escopo entregue

A trilha FUNC05 integra Educação ao SIGOV PLUS com autoridade PostgreSQL e operações Dapper já existentes, sem catálogo em memória: escolas, alunos e múltiplos responsáveis, professores, anos/períodos letivos, séries/etapas e componentes, turmas/quadro de horários, matrícula/enturmação/transferência, i-Diário, avaliações, ocorrências, pré-matrícula e portal interno.

A migration `20260824230000_func05_educacao_gestao_escolar.sql` cria as 21 tabelas `sigov.educacao_*`, chaves `bigint identity`, escopo tenant/entidade, checks, índices e unicidades. Regras críticas também são defendidas no banco: escola ativa, vaga/turma aberta, matrícula ativa única, período/ano aberto e intervalo de nota. Operações críticas mantêm histórico/auditoria antes/depois.

## Segurança e LGPD

A autorização usa as 28 permissões persistidas `educacao.*` e falha fechada. Documentos e metadados sensíveis não são apresentados integralmente em listas/CSV. O vínculo `educacao_portal_vinculo` associa usuário existente a aluno/responsável; não existe autenticação paralela. Exportações exigem `educacao.exportar`, aplicam máscara e neutralização CSV.

## Rotas

MVC: `/Educacao`, `/Educacao/Escolas`, `/Educacao/Escolas/Nova`, `/Educacao/Alunos`, `/Educacao/Alunos/Novo`, `/Educacao/Alunos/Detalhe/{id}`, `/Educacao/Responsaveis`, `/Educacao/Professores`, `/Educacao/Professores/Novo`, `/Educacao/AnosLetivos`, `/Educacao/SeriesEtapas`, `/Educacao/Turmas`, `/Educacao/Turmas/Nova`, `/Educacao/Turmas/Detalhe/{id}`, `/Educacao/Matriculas`, `/Educacao/Matriculas/Nova`, `/Educacao/Frequencias`, `/Educacao/Frequencias/Lancar`, `/Educacao/Avaliacoes`, `/Educacao/Avaliacoes/LancarNotas`, `/Educacao/Ocorrencias`, `/Educacao/PreMatriculas`, `/Educacao/PreMatriculas/Nova` e `/Educacao/PortalAluno`.

API: dashboard, CRUD/listagens de escolas/alunos/professores/turmas/matrículas, frequência, avaliações/notas, pré-matrícula/conversão/indeferimento, boletim e exportações sob `/api/educacao`.

## Fluxos e regras

* Matrícula ativa é única por aluno/escola/ano/série; enturmação exige escola e turma abertas e saldo de vagas; cancelamento exige justificativa e transferência produz histórico.
* Frequência só aceita professor vinculado/perfil autorizado, data dentro do ano e período aberto; falta justificada exige motivo.
* Nota/conceito/parecer segue a metodologia da etapa, o intervalo da avaliação e bloqueios de fechamento; alterações são auditadas.
* Pré-matrícula recebe protocolo único; conversão transacional verifica vaga e cria matrícula, e ausência de vaga permite somente lista de espera.
* Dashboard e CSV consultam PostgreSQL no escopo corrente; informações pessoais são mascaradas.

GED/InovaGED foi explicitamente adiado para a etapa final. FUNC05 não promove a RC50.68, que continua **BLOCKED** por runtime/CI/PostgreSQL oficiais, e não inicia nem marca a RC50.69.
