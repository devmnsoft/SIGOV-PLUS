# Cidadão360 — EXP04

## Escopo entregue

O Cidadão360 amplia o atendimento oficial sem criar cadastro paralelo de pessoas. O portal MVC/Razor oferece home, busca e catálogo publicados, detalhe humanizado, abertura autenticada, comprovante, área de solicitações e consulta pública protegida pelo par protocolo/código verificador. A administração reutiliza as telas reais de Atendimento Cidadão, Ouvidoria, agendas, satisfação e base de conhecimento.

O fluxo persiste com Dapper/Npgsql, queries parametrizadas e contexto obrigatório `tenant_id`/`entidade_id`. A pessoa vem do vínculo autenticado (`pessoa_id`); não é solicitada por identificador manual. Criação e consulta geram auditoria sanitizada em `atendimento_auditoria`. Listagens não retornam CPF, telefone ou e-mail.

## Banco e regras

A migration `20260827100000_exp04_cidadao360_portal_servicos.sql` cria configuração, catálogo, requisitos/campos, solicitação, anexos, histórico, mensagens, agenda/fila, avaliação, preferências, vínculo de autenticação, FAQ e pesquisa. Ela vincula as tabelas oficiais `pessoa`, `protocolo_atendimento`, `processo_digital`, `ouvidoria_manifestacao` e `documento_gerado`.

Checks controlam status, categoria, canal, prioridade, escala, visibilidade, identificação/anonimato e coerência temporal. Índices cobrem contexto, serviço, pessoa, workflow, protocolo, status, prazo, unidade, canal e datas. O índice de agenda impede duplicidade ativa do cidadão/serviço/horário. As 19 permissões `CIDADAO_*` são persistidas e concedidas idempotentemente ao SUPERADMIN.

## Rotas e experiência

- Públicas: `/Cidadao`, `/Cidadao/Portal`, `/Cidadao/Servicos`, detalhe, `/Cidadao/Protocolo`, Ouvidoria e FAQ.
- Cidadão autenticado: `/Cidadao/Solicitar`, `/Cidadao/MinhasSolicitacoes`, agendamentos, atendimento e avaliações.
- Administração: dashboard, catálogo/configuração e Ouvidoria sob `/Cidadao/Admin`.

O CSS dedicado é mobile-first, sem estilos inline, com hero, busca, cards, stepper, badges, comprovante, timeline, estados vazios, tabela responsiva e indicadores.

## Integrações e limites técnicos

O catálogo é fonte persistida; não existem catálogos artificiais. O fluxo liga-se por FKs aos contratos reais, mas a conversão automática para `ProtocoloAtendimento`/`ProcessoDigital` depende da configuração de fluxo e permanece trabalho administrativo explícito. Upload binário não foi simulado: a estrutura registra somente metadados e hash quando o adaptador GED real a preencher. E-mail/painel usam a infraestrutura oficial quando acionados; SMS, WhatsApp e Gov.br permanecem não configurados sem adaptador real.

As telas administrativas de categorias e configuração reutilizam o cadastro persistente atual; formulários especializados de edição dinâmica, bloqueios de agenda e relatórios adicionais deverão evoluir sobre essas tabelas, sempre sobre dados persistidos.

## Validação e bloqueios

Comandos executados constam em `docs/entregas/EXP04-CIDADAO360.md`. A atualização da `main` ficou bloqueada porque o checkout fornecido não possui remote nem branch `main`; a base local já contém o merge #315 do Ativos360.
