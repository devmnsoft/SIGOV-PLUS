# Comércio varejista e atacadista avançado

A evolução Pós-Build 05 aprofunda o ecossistema comercial do SIGOV PLUS para varejo, atacado e serviços, mantendo multi-tenancy, Dapper, LGPD, auditoria e módulos contratáveis.

## Módulos avulsos

- `comercial`: clientes, orçamentos, pedidos e vendas.
- `comercio_varejo`: fluxo de balcão, PDV e vendas rápidas.
- `comercio_atacado`: pedidos B2B, tabelas de preço, separação e conferência.
- `pdv`: ponto de venda web inicial, não fiscal.
- `caixa`: abertura, suprimento, sangria e fechamento.
- `estoque_compras`: integração opcional para saldos e movimentos.
- `financeiro_empresarial`: contas a receber inicial.
- `ordem_servico`: geração de OS a partir de pedidos/serviços.

## Fluxo varejo

1. Abrir caixa.
2. Registrar venda balcão ou PDV.
3. Adicionar itens ativos.
4. Registrar recebimentos até cobrir o total.
5. Finalizar a venda.
6. Baixar estoque quando integrado.
7. Cancelar com estorno quando necessário.

## Fluxo atacado

1. Cadastrar cliente e tabela de preço.
2. Criar orçamento ou pedido.
3. Aprovar orçamento e gerar pedido.
4. Confirmar pedido e reservar estoque.
5. Separar/conferir.
6. Faturar e gerar conta a receber inicial.

## LGPD e auditoria

Listagens de clientes mascaram documento, e-mail e telefone. Alterações geram eventos como `CLIENTE_CRIADO`, `PEDIDO_CONFIRMADO`, `VENDA_FINALIZADA` e `CONTA_RECEBER_GERADA`.
