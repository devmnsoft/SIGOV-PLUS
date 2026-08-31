# Contratos e fiscalização — RC50.85

## Fluxo e controles

O contrato administrativo mantém contexto multi-esfera, fornecedor canônico, origem licitatória ou ata, órgão, unidades, objeto, valor, saldo e vigência. Fiscal e gestor são vínculos a pessoas reais; a camada de apresentação deve fornecê-los por seleção ou autocomplete, nunca por entrada manual de identificador.

Eventos distinguem ocorrência, aceite, recebimento provisório, recebimento definitivo e medição. Garantias validam percentual, valor e vigência. Aditivos têm justificativa, valor, percentual e estado de aprovação; apostilamentos registram motivo e responsável. Sanções exigem motivo, fundamento, período coerente e responsável.

## Integrações auditáveis

`contrato_vinculo_financeiro` armazena somente a referência de empenho, liquidação ou pagamento que já exista no módulo responsável. `contrato_vinculo_ged` mantém somente o vínculo de documento canônico. A migration não cria documento, saldo financeiro, medição de obra, entrada patrimonial ou estoque fictício.

## Limites

A ativação do bloqueio de contrato sem fiscal e gestor deve ser parametrizada por esfera e entidade na camada Application. Até essa camada consumir a nova estrutura, a ausência do schema/configuração deve falhar explicitamente; não deve haver fallback de sucesso.
