# FUNC13 — Obras Públicas, Engenharia e Fiscalização

## Escopo entregue
Módulo MVC real em `/Obras`, persistido exclusivamente em PostgreSQL por Dapper/Npgsql, com dashboard, cadastro de obras, projetos versionáveis, orçamento e itens, cronograma e reprogramação, vínculo contratual por referência, medições e itens, diário, fiscalizações, ocorrências, ordens, convênios/repasses, metadados documentais, fila de integração financeira e auditoria.

## Banco
A migration `20260825070000_func13_obras_engenharia_fiscalizacao.sql` cria as 20 tabelas `obras_*` previstas. Todas usam PK identity bigint e segregação por tenant/entidade, auditoria temporal e exclusão lógica. Checks protegem status, valores, datas, percentuais e justificativas. Triggers bloqueiam orçamento aprovado e excesso no saldo do item medido.

## RBAC
Persistem-se e registram-se no catálogo as 26 permissões `OBRAS_*`: dashboard, visão/gestão de obra, projeto, orçamento, cronograma, medição, diário, fiscalização, ordem, convênio, documento e integração, além de homologação, exportação e auditoria.

## Rotas e operação
As rotas `/Obras`, `/Cadastro`, `/Projetos`, `/Orcamentos`, `/Cronogramas`, `/Medicoes`, `/DiarioObra`, `/Fiscalizacao`, `/Ocorrencias`, `/OrdensServico`, `/Convenios`, `/Documentos`, `/IntegracaoFinanceira`, `/Relatorios` e `/Auditoria` usam telas Razor próprias. Criação/alteração, homologação e exportação são auditadas com usuário, contexto, correlação e IP.

## Regras
Status críticos exigem justificativa; valores não podem ser negativos; datas são ordenadas; percentuais ficam entre 0 e 100; orçamento aprovado é imutável sem reprogramação; medição homologada é imutável e gera uma única pendência financeira; itens medidos não excedem saldo orçamentário. A validação de tenant e entidade é obrigatória em toda consulta.

## CSV
Há 12 relatórios: obras por status/localidade e unidade; orçamento; cronograma; medições no período e homologadas; diário; fiscalizações/não conformidades; ocorrências/ordens; convênios/repasses; integração financeira; auditoria. A saída neutraliza células iniciadas por operadores de planilha e registra exportação.

## Integrações
O vínculo administrativo aceita referência opcional ao contrato existente, sem criar contrato ou lançamento financeiro fictício. A integração Financeiro/SIAFIC é **preparada** pela fila `obras_integracao_financeira`; não há empenho, liquidação ou pagamento automático. Documentos são somente metadados: GED, InovaGED e Protocolo não integram este escopo.

## Limitações reais
Storage de fotos/documentos e consumo da fila pelo Financeiro não foram implementados. Checklist, equipe, equipamentos, ART/RRT e demais detalhes variáveis são preservados em JSONB validado como metadados operacionais, além das colunas relacionais centrais.
