# Jornadas Enterprise Pós-RC 10

## Comercial

Proposta aprovada pode gerar pedido; proposta reprovada é bloqueada. Pedido confirmado pode gerar OS; pedido cancelado é bloqueado para confirmação e geração de OS. O backend responde conflito para regra de negócio violada e 503 quando o schema real está indisponível.

## Ordem de Serviço

OS pode ser agendada, iniciada, pausada, receber checklist/apontamento, consumir peça e concluir. Consumo usa estoque real e bloqueia saldo negativo quando `PermitirSaldoNegativo` não está explícito.

## Estoque

Produtos geram saldo inicial. Movimentos de entrada, saída e ajuste atualizam saldo e geram auditoria. CSV usa dados mascarados e células sanitizadas.

## Industrial

Planos de manutenção geram OS preventiva. Medidores aceitam leitura. Paradas/falhas são tratadas no CRUD Enterprise e auditadas conforme o fluxo operacional.
