# Tesouraria e conciliação — RC50.86

A tesouraria canônica mantém contas com agência e conta mascaradas, pagamentos vinculados a liquidações, retenções, ordens bancárias e transferências. A conciliação é manual ou assistida por arquivo/adaptador real: a aplicação não presume comunicação com banco.

## Fluxo e controles

1. pagamento pendente referencia liquidação e, quando exigido pela regra da entidade, conta bancária ativa;
2. ordem bancária referencia conta e pagamento reais;
3. conciliação compara `valor_extrato` e `valor_sistema` por item;
4. divergência permanece explícita até decisão autorizada e auditada;
5. cancelamentos não removem registros e devem preservar justificativa no fluxo de aplicação.

Valores bancários ou fiscais protegidos não devem aparecer em logs ou CSV. Exportações devem aplicar contexto, permissão `FINANCEIRO_RELATORIO_EXPORT`, mascaramento e neutralização de células iniciadas por `=`, `+`, `-`, `@`, tabulação ou retorno de carro.
