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

### Matriz de continuidade (revisão 2026-09-16)

| Jornada | Implementação existente | Lacuna confirmada | Mudança desta revisão | Evidência automatizada |
|---|---|---|---|---|
| Contexto e cadastros | `escola`, `ano_letivo`, `turma`, `aluno`, `matricula`, `professor_turma` e autorização do serviço canônico | A interface ainda permite alguns identificadores livres em vez de seletores pesquisáveis | Nenhuma estrutura paralela foi criada; os lançamentos continuam referenciando os cadastros canônicos | `EducacaoModuleSmokeTests.Repository_Usa_Dapper_Parametrizado_Tenant_Entidade_Auditoria_Outbox` |
| Frequência | API, serviço Dapper, matrícula e ano letivo | Presença era o valor padrão; justificativa ausente era fabricada; atribuição do professor não era verificada no `INSERT` | Estado inicial agora é `NAO_LANCADO`; lançamento exige estado explícito, justificativa real, matrícula vigente, data no ano letivo e atribuição turma/componente | `EducacaoApiTests.Frequencia_Nao_Assume_Presenca_Nem_Inventa_Justificativa` e smoke de repositório |
| Avaliação e resultado | `avaliacao`, `nota`, endpoints e auditoria em `educacao_evento` | Escala/peso tinham padrão oculto; nota dependia principalmente de constraint | Escala e peso passam a ser explícitos; avaliação valida atribuição/data/estado e resultado valida elegibilidade, avaliação aberta e limite | smoke de repositório |
| Cálculo e boletim | Consulta de boletim | A consulta inventava corte de 60% e média aritmética, sem política acadêmica persistida | Removidas inferências: zero continua valor lançado, ausência vira `NAO_LANCADO` e a média permanece indisponível até existir política versionada | `EducacaoModuleSmokeTests.Boletim_Nao_Inventa_Regra_De_Aprovacao_Sem_Politica` |
| Fechamento/reabertura | Diário Bloco 3 verifica conteúdo/frequência e grava histórico com justificativa | Ainda não existe política acadêmica versionada nem snapshot publicável de resultados; a conferência não cobre todos os alunos elegíveis | Mantido como parcial e sem declarar fechamento acadêmico completo | testes existentes de estrutura do Bloco 3 |
| Painel, Minha Central e relatórios | dashboard e exportações básicas | Contagens ainda não oferecem todo o recorte escola/ano/permissão; não há integração completa com Minha Central | Sem mudança nesta revisão; não foi criada tabela paralela de tarefas | pendência registrada |
| Ingresso e vagas | Pré-matrícula simples e criação direta de matrícula | Não havia rascunho, transições validadas, concorrência otimista, oferta/reserva nem conversão idempotente | A migration `20260916210000` acrescenta versão, oferta, pendência e origem; serviço/API validam estados, motivos, validade e última vaga; matrícula pode aguardar turma e a enturmação tem vigência própria | `EducacaoModuleSmokeTests.Ingresso_E_Vagas_Preserva_Concorrencia_Origem_E_Enturmacao_Opcional` |

### Regras e fontes adotadas

* Ano letivo é obtido de `ano_letivo.data_inicio/data_fim`; a data corrente do servidor não substitui esse intervalo nos lançamentos.
* Elegibilidade de frequência deriva da matrícula na turma e da data da matrícula. A atribuição deriva de `professor_turma` e do componente informado.
* Escala máxima e peso são dados explícitos da avaliação. Este documento **não** define média mínima, percentual mínimo de frequência, precisão, arredondamento, recuperação ou equivalência de conceitos.
* Pré-matrícula e análise não ocupam capacidade. Ofertas `OFERTADA` ou `ACEITA`, ainda válidas, reservam capacidade; a conversão consome somente a oferta de origem e a restrição única torna a repetição segura. A oferta é contabilizada no escopo escola/ano/série/turno e a alocação em turma permanece uma etapa separada quando a turma ainda não foi informada.
* Não há classificação automática: pontuação preexistente não decide a fila. Aprovação, indeferimento, complementação e cancelamento são decisões administrativas autorizadas, versionadas e, quando afetam o solicitante, exigem motivo.
* Na ausência de política acadêmica persistida e versionada, média e situação final ficam indisponíveis. Isso é impedimento funcional, não zero nem aprovação implícita.

### Pendências deliberadamente não declaradas como concluídas

1. Modelar política acadêmica multi-esfera versionada, incluindo escala numérica/conceitual, precisão, arredondamento, recuperação e publicação, mediante decisão de produto.
2. Unificar o diário base e o diário Bloco 3 sem migração destrutiva, preservando históricos já publicados.
3. Completar conferência atômica, snapshot do fechamento/publicação, correção versionada e concorrência otimista.
4. Integrar os agregados autorizados à Minha Central e concluir relatórios/impressão responsivos.
5. Validar navegador e PostgreSQL 16 quando esses runtimes estiverem disponíveis. Compras e Estoque permanecem fora desta revisão.

GED/InovaGED foi explicitamente adiado para a etapa final. FUNC05 não promove a RC50.68, que continua **BLOCKED** por runtime/CI/PostgreSQL oficiais, e não inicia nem marca a RC50.69.
