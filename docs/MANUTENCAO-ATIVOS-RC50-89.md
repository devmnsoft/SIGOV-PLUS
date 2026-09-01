# Manutenção de ativos — RC50.89

O endereço `/Manutencao` conduz ao fluxo real de ordens de serviço já persistido pelo módulo de frotas. As rotas de preventivas, corretivas, atendimentos e relatórios mantêm uma entrada única para bens, veículos, imóveis, equipamentos e unidades.

A tabela `manutencao_ordem_servico` implementa o fluxo aberta, triada, aprovada, em execução, aguardando peça, concluída, recusada e cancelada. Conclusão exige serviço realizado, responsável e data; recusa/cancelamento exige justificativa. A agenda preventiva exige periodicidade positiva. Evidência é apenas referência documental real.

## Como usar esta tela

Selecione o ativo nas listas institucionais, descreva a necessidade e defina prioridade. Após triagem e autorização, atualize o andamento; registre peças efetivamente consumidas. Na conclusão, informe responsável, data e descrição do serviço. Não registre oficina ou documento inexistente.
