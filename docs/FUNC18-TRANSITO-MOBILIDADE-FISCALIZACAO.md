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

## Fechamento CORR18 (25/08/2026)

A revisão identificou que o cadastro genérico aceitava qualquer nome de coluna formado por letras minúsculas e sublinhado. A persistência agora mantém uma **whitelist fechada por recurso**; recurso, tabela, colunas pesquisáveis e relacionamentos continuam provenientes apenas de especificações internas, nunca do conteúdo enviado pelo navegador. Relacionamentos são novamente conferidos por `tenant_id` e `entity_id` antes da gravação.

Os formulários oferecem agentes (matrícula, nome e CPF mascarado), condutores (nome, CPF mascarado e CNH), veículos (placa, modelo e proprietário), infrações, autos, notificações, recursos, linhas, pontos e autorizações em seletores abastecidos pelo PostgreSQL. O identificador é apenas o valor interno da opção; não existe caixa para digitação de ID. POSTs de gravação e exclusão usam antiforgery, e valores e opções são reconstruídos após erro de validação.

Foram reforçadas no servidor as regras de datas, coordenadas, valores não negativos, vínculos e justificativas de autos, notificações, recursos, julgamentos, ocorrências, sinalização, intervenções, rotas, autorizações, vistorias e credenciais. Os filtros de período agora chegam à consulta, e os oito CSV autorizados respeitam período, status, contexto e neutralizam fórmulas (`=`, `+`, `-` e `@`).

A migration corretiva idempotente `20260825121000_corr18_transito_validacoes_indices.sql` adiciona a regra cronológica de vistoria da sinalização e índices parciais usados pelo dashboard. A migration FUNC18 publicada não foi modificada; manifesto e seis scripts consolidados foram sincronizados.
