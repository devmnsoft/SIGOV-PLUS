# FUNC19 — Defesa Civil, Guarda Municipal e Segurança Pública Municipal

## Escopo

O módulo `/Defesa` fornece dashboard operacional, agentes, equipes e integrantes, recursos/viaturas, áreas de risco, ocorrências, acionamentos, vistorias, abrigos, atendimentos emergenciais, rondas, ordens de serviço, notificações, planos de contingência, auditoria e exportações CSV. A persistência usa PostgreSQL, Npgsql, Dapper e chaves `bigint identity`; não usa Entity Framework.

## Banco e isolamento

A migration `20260825130000_func19_defesa_civil_guarda_municipal.sql` cria as quinze tabelas `sigov.defesa_*`, índices, chaves estrangeiras, unicidades e checks operacionais. Toda consulta e alteração exige `tenant_id` e `entity_id`; relacionamentos são revalidados no mesmo contexto antes da gravação. Exclusões são lógicas e geram evento em `defesa_auditoria`.

Os scripts consolidados de produção, desenvolvimento e compatibilidade, bem como o manifest com SHA-256, incluem a mesma migration idempotente.

## Permissões

As policies persistidas abrangem `DEFESA_DASHBOARD_VIEW`, pares `VIEW`/`MANAGE` para agentes, equipes, recursos, áreas de risco, ocorrências, acionamentos, vistorias, abrigos, atendimentos, rondas, ordens, notificações e planos, além de `DEFESA_RELATORIO_EXPORT` e `DEFESA_AUDITORIA_VIEW`. O banco continua sendo a fonte de autoridade.

## Rotas e telas

As telas MVC/Razor reais ficam sob `/Defesa`: `Dashboard`, `Agentes`, `Equipes`, `Recursos`, `AreasRisco`, `Ocorrencias`, `Acionamentos`, `Vistorias`, `Abrigos`, `Atendimentos`, `Rondas`, `OrdensServico`, `Notificacoes`, `PlanosContingencia`, `Relatorios` e `Auditoria`. Todas as operações POST usam antiforgery, validam `ModelState`, reconstroem opções em falha e auditam a escrita.

Nenhum relacionamento é digitado como ID. Agente, equipe, área, ocorrência, abrigo e vistoria são apresentados por rótulos humanos em selects; integrantes de equipe usam lista de checkboxes. O valor numérico existe apenas internamente após a seleção.

## Regras e LGPD

As validações cobrem cronologia, coordenadas, capacidades, composição familiar, fechamento/conclusão, detalhe de área crítica, plano ativo e vínculos operacionais. Ocorrências fechadas não aceitam novo acionamento e abrigos inativos ou lotados não aceitam atendimento. Dados de CPF/documento e contato são limitados às finalidades do módulo, isolados por contexto e auditados. Não há reconhecimento facial, ranking de cidadãos ou vigilância automatizada.

## Dashboard e relatórios

Todos os cards executam agregações reais no PostgreSQL: ocorrências abertas/críticas/fechadas no mês, acionamentos, vistorias, áreas críticas, abrigos e ocupação, rondas, ordens e notificações. Os nove CSVs aceitam filtros reais de período/status/busca, respeitam contexto e neutralizam células iniciadas por `=`, `+`, `-` ou `@`.

## Validação executada

- `jq empty database/postgres/migrations/manifest.json`.
- validações estáticas de sincronismo, antiforgery, relacionamentos e ausência de campos de ID editáveis.
- `dotnet restore` e `dotnet build`: **BLOCKED**, pois `dotnet` não está instalado no ambiente.
- aplicação local da migration: **BLOCKED**, pois `psql` não está instalado no ambiente.
- smoke manual servido: **BLOCKED**, pois depende do runtime .NET e do PostgreSQL ausentes.

## Fechamento CORR19

O fechamento acrescenta leitura contextual para detalhes e edição, atualização transacional com auditoria, conversão explícita dos tipos PostgreSQL e catálogo fechado de status/campos. A identificação técnica gerada deixou de ser solicitada nos recursos sem número de negócio. A migration corretiva `20260825131000_corr19_defesa_indices.sql` preserva a migration publicada e adiciona índices idempotentes para os filtros operacionais e unicidade de patrimônio ativo.
