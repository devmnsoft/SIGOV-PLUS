# Patrimônio360, Almoxarifado e Frotas — RC50.89

A RC50.89 consolida os cadastros oficiais FUNC01, FUNC02, FUNC04 e Ativos360 em uma navegação canônica, sem catálogos paralelos. Todas as operações persistentes continuam usando Dapper/Npgsql, contexto obrigatório de tenant e entidade e autorização proveniente do banco.

## Cobertura

- **Patrimônio360:** tombamento, localização, responsabilidade, movimentação auditada, baixa, inventário, depreciação decimal e imóveis multi-esfera.
- **Estoque público:** material, saldo não negativo, entrada, saída, requisição, atendimento, transferência conferida, lotes, validade e estoque crítico.
- **Frotas:** veículo, motorista vinculado, viagem, abastecimento, hodômetro, manutenção, multas e alerta documental.
- **Integrações:** as referências a contrato, empenho, pagamento, obra e documento são gravadas somente quando o registro oficial correspondente existe; não há simulação de integração.

## Segurança e operação

O escopo de cada consulta inclui `tenant_id` e `entidade_id`; exercício, esfera, órgão, unidade gestora e unidade executora são aplicados quando disponíveis. Dados de responsáveis e motoristas não são publicados nos relatórios de transparência. Exportações usam o sanitizador CSV oficial contra fórmulas. A migration `20260901090000_rc50_89_patrimonio_almoxarifado_frotas.sql` é aditiva e idempotente.

## Como usar as telas

Comece pelos dashboards, aplique os filtros institucionais e abra o cadastro pela listagem — nunca informe identificadores técnicos. Registre uma movimentação antes de alterar guarda/localização, confira saldo antes do atendimento e encerre viagens com data e hodômetro finais. Baixas, recusas e cancelamentos exigem justificativa e autorização específica.
