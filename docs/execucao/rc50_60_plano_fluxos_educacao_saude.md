# RC50.60 — plano dos fluxos de Educação e Saúde

## Inventário executável

- **Educação:** API `EducacaoControllers`, Bloco 3 e controllers avançados de transporte, merenda, biblioteca e indicadores; Web `EducacaoController` e controllers especializados; `EducacaoService`, `EducacaoBloco3Service` e serviços avançados; repositórios Dapper `EducacaoRepository`, Bloco 3 e avançado.
- **Saúde:** API `SaudeControllers`, ACS, operação e retaguarda; Web `SaudeController`, ACS, vacinação, farmácia, regulação, e-SUS, SLA e suporte; `SaudeService` e serviços avançados; repositórios Dapper `SaudeRepository` e avançado.
- **Tabelas:** o núcleo existente cobre escola, aluno, responsável, matrícula, turma, frequência/diário, transporte, merenda, biblioteca, unidade, paciente, profissional, ACS, domicílio, visita, vacinação, farmácia e regulação. As migrations RC50.48/49 cobrem frentes avançadas.
- **Endpoints principais:** CRUDs canônicos em `/api/educacao/*` e `/api/saude/*`, além das superfícies avançadas. A busca estática não encontrou endpoint 501 no escopo.
- **Views e botões:** dashboards, grids, formulários e modais existem nos diretórios `Views/Educacao` e `Views/Saude`; rotas precisam de smoke autenticado para homologação definitiva. Nenhum botão foi removido para ocultar pendência.

## Estado e lacunas tratados

As regras existentes já usam persistência Dapper parametrizada, filtros de tenant/entidade, licenciamento modular, permissões e auditoria de mutações/acessos pessoais. Esta entrega adiciona proteção modular à API canônica de Educação, auditoria de negativas, justificativas obrigatórias para cancelar/transferir matrícula, validação mínima de frequência, visita, vacina, dispensação e regulação, índices concorrentes e o catálogo granular solicitado.

Continuam preparatórias as integrações oficiais Educacenso, FUNDEB, e-SUS/SISAB e provedores externos. Escopo por escola, turma, unidade, equipe e microárea depende das concessões territoriais existentes e deve ser provado com usuários reais. Os perfis necessários são os templates setoriais criados na migration RC50.60, além de SuperAdmin, AdminTenant e Auditor existentes.
