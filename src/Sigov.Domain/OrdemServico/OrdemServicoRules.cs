namespace Sigov.Domain.OrdemServico;

public enum OrdemServicoStatus { Rascunho, Aberta, EmTriagem, Agendada, EmDeslocamento, EmExecucao, Pausada, AguardandoCliente, AguardandoPeca, Concluida, Cancelada, Reaberta }
public enum OrdemServicoPrioridade { Baixa, Normal, Alta, Urgente, Critica }
public enum OrdemServicoOrigem { Manual, PedidoComercial, Contrato, ManutencaoPreventiva, Chamado, Garantia }

public static class OrdemServicoRules
{
    private static readonly IReadOnlyDictionary<OrdemServicoStatus, OrdemServicoStatus[]> Transicoes = new Dictionary<OrdemServicoStatus, OrdemServicoStatus[]>
    {
        [OrdemServicoStatus.Rascunho] = [OrdemServicoStatus.Aberta, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.Aberta] = [OrdemServicoStatus.EmTriagem, OrdemServicoStatus.Agendada, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.EmTriagem] = [OrdemServicoStatus.Agendada, OrdemServicoStatus.AguardandoCliente, OrdemServicoStatus.AguardandoPeca, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.Agendada] = [OrdemServicoStatus.EmDeslocamento, OrdemServicoStatus.EmExecucao, OrdemServicoStatus.AguardandoCliente, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.EmDeslocamento] = [OrdemServicoStatus.EmExecucao, OrdemServicoStatus.Pausada, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.EmExecucao] = [OrdemServicoStatus.Pausada, OrdemServicoStatus.AguardandoCliente, OrdemServicoStatus.AguardandoPeca, OrdemServicoStatus.Concluida, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.Pausada] = [OrdemServicoStatus.EmExecucao, OrdemServicoStatus.AguardandoCliente, OrdemServicoStatus.AguardandoPeca, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.AguardandoCliente] = [OrdemServicoStatus.Agendada, OrdemServicoStatus.EmExecucao, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.AguardandoPeca] = [OrdemServicoStatus.Agendada, OrdemServicoStatus.EmExecucao, OrdemServicoStatus.Cancelada],
        [OrdemServicoStatus.Concluida] = [OrdemServicoStatus.Reaberta],
        [OrdemServicoStatus.Reaberta] = [OrdemServicoStatus.EmTriagem, OrdemServicoStatus.Agendada, OrdemServicoStatus.EmExecucao],
        [OrdemServicoStatus.Cancelada] = []
    };

    public static void ValidarTransicao(OrdemServicoStatus atual, OrdemServicoStatus destino)
    {
        if (!Transicoes.TryGetValue(atual, out var permitidos) || !permitidos.Contains(destino))
            throw new InvalidOperationException($"Transição de {atual} para {destino} não permitida.");
    }
}
