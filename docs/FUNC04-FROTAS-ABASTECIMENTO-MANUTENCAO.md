# FUNC04 — Frotas, Abastecimento e Manutenção

## Fluxos entregues

O módulo persiste veículos e vínculo opcional a `patrimonio_bem`, motoristas, utilização com saída/retorno, abastecimento com total calculado, manutenção e estado do veículo, ordens de serviço e documentos. Quilometragem regressiva é recusada. Uma utilização exige veículo ativo, motorista ativo e CNH válida, e a unique parcial impede dois usos abertos.

A manutenção aberta coloca o veículo em `EM_MANUTENCAO`; a conclusão só o ativa quando não existe outra manutenção aberta. Ao concluir uma OS, cada peça/material vinculado bloqueia o saldo de `almoxarifado_estoque`, recusa saldo insuficiente e grava saída e vínculo da movimentação na mesma transação. Contratos e fornecedores de FUNC03 e o bem de FUNC01 são referenciados, sem duplicar recebimento nem criar patrimônio incompleto. Abastecimento sem um contrato seguro de consumo de estoque não gera baixa implícita.

## Persistência, segurança e LGPD

A migration `20260824220000_func04_frotas_abastecimento_manutencao.sql` cria `frotas_veiculo`, `frotas_motorista`, `frotas_utilizacao`, `frotas_abastecimento`, `frotas_manutencao` e histórico, `frotas_ordem_servico`, itens e histórico, `frotas_documento` e `frotas_auditoria`. Todas usam PK bigint, tenant/entidade, checks, índices e unicidades idempotentes. Escritas Dapper são parametrizadas e auditam antes/depois. CPF é mascarado nas consultas; não há exportação de dado sensível completo.

As 22 permissões `frotas.*` da migration cobrem dashboard, visualizar/criar/editar veículos e motoristas, visualizar/criar/finalizar utilização, abastecimento, manutenção, OS, documentos e exportação. Web e API consultam o avaliador persistente e falham fechadas.

## Rotas

MVC: `/Frotas`, `/Frotas/Veiculos`, novo/editar/detalhe, `/Frotas/Motoristas`, novo, `/Frotas/Utilizacoes`, nova/finalizar, `/Frotas/Abastecimentos`, novo, `/Frotas/Manutencoes`, nova, `/Frotas/OrdensServico`, nova/detalhe e `/Frotas/Documentos`.

API: `GET dashboard/veiculos/motoristas`; `POST veiculos/motoristas/utilizacoes/abastecimentos/manutencoes/ordens-servico`; finalizar utilização e aprovar/concluir/cancelar OS sob `/api/frotas`.

## Indicadores e operação

O dashboard calcula no banco veículos ativos/indisponíveis, CNHs e documentos vencidos/a vencer em 30 dias, abastecimentos e custo do mês e manutenções abertas. Estado vazio retorna zero/lista vazia real. Configure somente `ConnectionStrings__DefaultConnection` e aplique via `psql -v ON_ERROR_STOP=1`.

FUNC04 não promove release: RC50.68 continua **BLOCKED** pelo ambiente/CI/runtime/PostgreSQL oficiais e RC50.69 não foi iniciada.
