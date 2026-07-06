# Patrimônio, Inventário e Obras — SIGOV PLUS

## Escopo da sprint
Base funcional e visual para Patrimônio, Inventário Patrimonial, Obras, Fiscalização, Diário de Obra, Medições e Relatório Fotográfico, conectando Contratos, Almoxarifado, RH e Financeiro/SIAFIC quando o schema físico existir.

## Tabelas previstas
`patrimonio_bem`, `patrimonio_grupo`, `patrimonio_localizacao`, `patrimonio_responsavel`, `patrimonio_movimento`, `patrimonio_inventario`, `patrimonio_inventario_item`, `patrimonio_baixa`, `patrimonio_depreciacao`, `obra`, `obra_contrato`, `obra_medicao`, `obra_diario`, `obra_foto`, `obra_ocorrencia`, `obra_fiscalizacao`, `obra_garantia`, além de `contrato`, `contrato_fiscal`, `almoxarifado_produto`, `almoxarifado_movimento`, `rh_servidor`, `rh_lotacao`, `empenho`, `liquidacao`, `pagamento`, `conta_pagar` e `auditoria_evento`.

## Fluxos
- Patrimônio: dashboard, bens, detalhe, cadastro/edição/baixa condicionados ao schema real, movimentações, localizações, responsáveis, depreciação e CSV seguro.
- Inventário: campanhas, itens, conclusão auditada, divergências e relatórios.
- Obras: dashboard, cadastro, medições, diário, fotos, fiscalização, relatórios e CSV.

## Integrações
- Contratos: vínculos com bens adquiridos, obras, fiscais, medições, documentos, vencimentos e aditivos.
- Almoxarifado: incorporação patrimonial, saída para obra e consumo futuro sem simular estoque.
- RH: responsáveis e fiscais usando servidores/lotações quando disponíveis, com dados pessoais minimizados.
- Financeiro/SIAFIC: valores contratados, medidos, pagos e saldo apenas quando empenho/liquidação/pagamento estiverem disponíveis.
- Mobile/Campo: rotas para inventário, obras, evidências e sincronização como preparação de campo.

## Permissões e auditoria
Permissões planejadas: visualizar, criar, editar, baixar, transferir, inventariar, medir, fiscalizar, anexar, exportar e auditar. Ações críticas registram auditoria quando `auditoria_evento` estiver disponível; em ambientes sem schema, o sistema informa fallback.

## LGPD
Listagens devem mascarar documentos e evitar exposição de CPF/matrícula/responsável completo. Detalhes exigem finalidade administrativa legítima.

## Relatórios e POC
Relatórios CSV são seguros e retornam mensagem honesta quando a tabela não existir. A POC deve exibir status Funcional, Parcial, Demonstrativo, Em implantação ou Indisponível, indicando se salva no banco e se usa fallback.

## Próximos passos
Homologar DDL não destrutiva, regras oficiais de tombamento, cálculo de depreciação, storage de fotos, API mobile/offline, integração SIAFIC e BI avançado.
