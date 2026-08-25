# FUNC18 — Trânsito, Mobilidade Urbana e Fiscalização de Transporte

## Escopo funcional
O módulo oferece dashboard operacional, cadastros de agentes, condutores, veículos e infrações, ciclo de autos/notificações/recursos/julgamentos, ocorrências, sinalização, intervenções, rede de transporte (linhas, rotas e pontos), autorizações, vistorias, credenciais, auditoria e exportação CSV.

## Persistência e isolamento
A migration `20260825120000_func18_transito_mobilidade_fiscalizacao.sql` cria as 19 tabelas `sigov.transito_*` solicitadas, PKs `bigint identity`, FKs, índices documentais, unicidades e checks de datas, coordenadas e valores. Todo acesso do repositório usa `tenant_id` e `entity_id`; exclusão é lógica e as escritas relevantes geram `transito_auditoria`. PostgreSQL/Npgsql/Dapper são usados sem Entity Framework.

## Rotas e permissões
As rotas começam em `/Transito`, com Dashboard, Agentes, Condutores, Veiculos, Infracoes, Autos, Notificacoes, Recursos, Julgamentos, Ocorrencias, Sinalizacao, Intervencoes, Linhas, Rotas, Pontos, Autorizacoes, Vistorias, Credenciais, Relatorios e Auditoria. As 29 permissões `TRANSITO_*` da especificação são persistidas e também registradas no catálogo de policies.

## Formulários e regras
Relacionamentos nunca são digitados como IDs: são selects carregados do banco e exibem matrícula/nome, CPF/CNH, placa/modelo, código/descrição, número/resumo ou protocolo/requerente. Rotas oferecem seleção de linha e pontos. Há antiforgery, ValidationSummary, validação unobtrusive, DataAnnotations e mensagens úteis. Regras adicionais existem no banco e servidor para justificativas, datas, multas, coordenadas, reprovação e cancelamentos.

## Dashboard e relatórios
Os nove indicadores consultam dados reais do contexto. CSVs cobrem autos, notificações, recursos, ocorrências, sinalização, autorizações, vistorias e credenciais, aplicando filtros e proteção contra fórmulas.

## Validação
Comandos previstos: `dotnet restore`, `dotnet build`, parse do manifesto com Python, verificação de checksum e, quando `psql`/conexão local estiverem disponíveis, aplicação transacional da migration. Não foram criados mocks, seeds ou classes de teste. O módulo GED/Protocolo não foi alterado.
