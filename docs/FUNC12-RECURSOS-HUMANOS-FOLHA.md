# FUNC12 — Recursos Humanos e Folha de Pagamento

## Escopo entregue
O FUNC12 consolida o módulo existente de RH em PostgreSQL, sem `OperationalDemoService`, com cadastros de servidores, cargos, funções, unidades, vínculos, lotações históricas, dependentes, frequência, férias, afastamentos, eventos parametrizados, competência, cálculo, holerite, integração financeira e auditoria. As telas MVC usam os endpoints Dapper já integrados e estados vazios reais.

## Persistência e regras
A migration `20260825060000_func12_rh_folha_pagamento.sql` cria/evolui as 19 tabelas `rh_*` do escopo com identidade `bigint`, tenant e entidade, soft delete e trilha de autoria. CPF e matrícula são únicos por entidade; uma lotação ativa por vínculo é permitida; saída anterior à entrada e férias/afastamentos com datas inválidas são recusados. Desligamento e cancelamentos exigem justificativa. Competência homologada é imutável até reabertura formal.

Eventos são `PROVENTO`, `DESCONTO`, `BASE` ou `INFORMATIVO` e calculados por valor fixo, percentual ou fórmula persistida. Incidências previdenciária, IRRF, FGTS, margem e líquido são parâmetros do evento: nenhuma tabela legal foi codificada no aplicativo. Líquido negativo depende de autorização explícita no evento e justificativa.

## Rotas
`/Rh`, `/Rh/Servidores`, `/Rh/Servidores/Novo`, `/Rh/Cargos`, `/Rh/Funcoes`, `/Rh/Lotacoes`, `/Rh/Vinculos`, `/Rh/Dependentes`, `/Rh/Frequencia`, `/Rh/Ferias`, `/Rh/Afastamentos`, `/Rh/EventosFolha`, `/Rh/Folha`, `/Rh/Folha/Calcular`, `/Rh/Folha/Homologar`, `/Rh/Holerites`, `/Rh/IntegracaoFinanceira`, `/Rh/Relatorios` e `/Rh/Auditoria`.

## RBAC
Persistem-se as 29 permissões `RH_*` solicitadas, separando consulta, gestão, cálculo, homologação, reabertura, exportação, integração e auditoria. CPF, CID, dependentes e holerites permanecem sujeitos à máscara LGPD e à autorização do backend.

## CSV e integração financeira
Relatórios CSV abrangem servidores, vínculos, lotações, frequência, férias, afastamentos, eventos, resumo e itens da folha, integração financeira e auditoria. O exportador neutraliza conteúdo de célula e registra o acesso. A integração gera somente resumo consolidado em `rh_integracao_financeira` (`PENDENTE`, `GERADO`, `ENVIADO`, `CANCELADO`); não cria empenho, liquidação ou pagamento.

## Limitações reais
Não foi entregue PDF por não existir contrato estável de geração no módulo. Faixas, alíquotas e fórmulas legais devem ser cadastradas e versionadas pelo ente; o sistema deliberadamente não presume legislação tributária ou previdenciária. A conexão obrigatória é `ConnectionStrings__DefaultConnection`.
