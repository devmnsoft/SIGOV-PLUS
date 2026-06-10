# Financeiro integrado

A evolução Pós-Build 07 adiciona a base do módulo `financeiro_empresarial` para empresas privadas, comércio, indústria, serviços e preparação para integrações públicas futuras sem implementar SIAFIC completo, boleto real, PIX real ou CNAB real.

## Fluxo financeiro

1. A origem operacional (venda, PDV, OS, contrato, compra, produção ou assinatura SaaS) informa forma de pagamento, vencimento, competência, natureza e centro de custo.
2. Operações a prazo geram contas a receber ou contas a pagar.
3. Operações à vista geram movimento financeiro de entrada ou saída.
4. Baixas parciais mantêm saldo aberto e status `PARCIAL` quando a configuração do tenant permite.
5. Baixas totais encerram o título como `RECEBIDA` ou `PAGA`.
6. Cancelamentos não excluem dados; apenas mudam status e preservam histórico.
7. Estornos geram movimento contrário e mantêm trilha de auditoria com `correlation_id`.

## Segurança, tenant e LGPD

Todas as tabelas operacionais financeiras possuem `tenant_id`. Listagens não devem vazar dados sensíveis e ações críticas exigem permissão específica, módulo contratado e auditoria.
