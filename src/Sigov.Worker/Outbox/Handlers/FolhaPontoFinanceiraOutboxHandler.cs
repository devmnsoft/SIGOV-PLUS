using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Financeiro;
using Sigov.Application.Parameters;
using Sigov.Domain.Rh;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Worker.Outbox;

namespace Sigov.Worker.Outbox.Handlers;

/// <summary>
/// RC-EVO-RH §9: consome RH_FOLHA_PONTO_FINANCEIRO e materializa o documento real no Financeiro —
/// um empenho por integração, SEM liquidação nem pagamento automático. RC-EVO-B §5: o tipo do
/// empenho vem do parâmetro FOLHA TIPO_EMPENHO_INTEGRACAO_PONTO (padrão ORDINARIO) e a data de
/// emissão é a congelada no payload na publicação — o retry não troca a data do documento.
/// O consumidor só age com regras suficientes (ORCAMENTO_DESPESA_FOLHA_ID + FORNECEDOR_FOLHA_ID no
/// módulo FOLHA) e com o payload assinado conferido (payloadHash); falhas definitivas nomeadas
/// (config/payload/checksum/saldo) viram dead-letter rastreável (FALHOU) — o administrador corrige
/// o parâmetro e reabre a mensagem. Transitórias mantêm o retry com backoff. Retry é idempotente
/// pela chave compartilhada com o producer (outbox) e o escopo 'empenho.criar' da idempotência financeira.
/// </summary>
public sealed class FolhaPontoFinanceiraOutboxHandler : DefaultOutboxHandler
{
    private readonly IModuleParameterService _parametros;
    private readonly IEmpenhoRepository _empenhos;
    private readonly DapperContext _context;
    private readonly ILogger<FolhaPontoFinanceiraOutboxHandler> _logger;

    public FolhaPontoFinanceiraOutboxHandler(IModuleParameterService parametros, IEmpenhoRepository empenhos, DapperContext context, ILogger<FolhaPontoFinanceiraOutboxHandler> logger)
    {
        _parametros = parametros;
        _empenhos = empenhos;
        _context = context;
        _logger = logger;
    }

    // Ordinal exato: "RH_FOLHA_PONTO_FINANCEIRO" contém "financeiro", mas nenhum handler anterior
    // casa com ele (os anteriores buscam "Financeira"/"Integracao"/"Tributario"/"WebhookEnviado").
    public override bool CanHandle(string tipoEvento) => string.Equals(tipoEvento, FolhaPontoFinanceiraRegras.TipoEvento, StringComparison.Ordinal);

    public override async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // RC-EVO-B §5: taxonomia de erros — falhas nomeadas das regras (payload inválido/sem checksum,
        // configuração insuficiente, sem proventos, exercício inativo) são DEFINITIVAS e viram
        // dead-letter rastreável; exceções de banco/transporte seguem comuns e mantêm backoff.
        FolhaPontoFinanceiraRegras.FolhaPontoFinanceiraPayload payload;
        FolhaPontoFinanceiraRegras.EmpenhoPlano plano;
        int ano;
        string tipoEmpenho;
        try
        {
            payload = FolhaPontoFinanceiraRegras.Parse(message.Payload);
            // Integridade antes de qualquer efeito: payload publicado é assinado (payloadHash);
            // divergência significa corrosão/alteração entre publicar e consumir — nada é registrado.
            FolhaPontoFinanceiraRegras.VerificarIntegridade(payload);

            var parametros = await _parametros.ListAsync(message.TenantId, "FOLHA", cancellationToken).ConfigureAwait(false);
            var orcamento = FolhaPontoFinanceiraRegras.InterpretarParametroLong(parametros.FirstOrDefault(p => string.Equals(p.Code, FolhaPontoFinanceiraRegras.ParametroOrcamento, StringComparison.OrdinalIgnoreCase))?.ValueJson);
            var fornecedor = FolhaPontoFinanceiraRegras.InterpretarParametroLong(parametros.FirstOrDefault(p => string.Equals(p.Code, FolhaPontoFinanceiraRegras.ParametroFornecedor, StringComparison.OrdinalIgnoreCase))?.ValueJson);
            // Tipo do empenho vem do parâmetro FOLHA TIPO_EMPENHO_INTEGRACAO_PONTO; ausente → padrão
            // documentado ORDINARIO (não é valor inventado). Data de emissão: congelada no payload.
            tipoEmpenho = FolhaPontoFinanceiraRegras.InterpretarTipoEmpenho(parametros.FirstOrDefault(p => string.Equals(p.Code, FolhaPontoFinanceiraRegras.ParametroTipoEmpenho, StringComparison.OrdinalIgnoreCase))?.ValueJson);

            plano = FolhaPontoFinanceiraRegras.ConstruirEmpenho(payload, orcamento, fornecedor);
            ano = await CarregarExercicioAnoAsync(message.TenantId, payload.EntidadeId, payload.ExercicioId, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new OutboxPermanentFailureException(ex.Message, ex);
        }

        var request = new EmpenhoCreateRequest(
            plano.OrcamentoDespesaId,
            payload.DataEmissao,
            plano.FornecedorPessoaId,
            plano.Historico,
            tipoEmpenho,
            plano.Itens.Select(i => new EmpenhoItemRequest(i.Descricao, i.Quantidade, i.ValorUnitario)).ToList(),
            plano.Observacoes);

        var chave = FolhaPontoFinanceiraRegras.ChaveIntegracao(message.TenantId, payload.IntegracaoId);
        var resultado = await _empenhos.CriarAsync(message.TenantId, payload.EntidadeId, payload.ExercicioId, ano, request, payload.UsuarioId, chave, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Empenho de ponto registrado. TenantId={TenantId} IntegracaoId={IntegracaoId} EmpenhoId={EmpenhoId} Replay={Replay}",
            message.TenantId, payload.IntegracaoId, resultado.DocumentoId, resultado.Replay);
    }

    // Ano de exercício ativo no momento do consumo (autoridade: banco). Ausente/inativo → falha nomeada EXERCICIO_AUSENTE.
    private async Task<int> CarregarExercicioAnoAsync(long tenantId, long entidadeId, long exercicioId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var ano = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "select coalesce(max(ano), 0) from sigov.exercicio where tenant_id=@TenantId and entidade_id=@EntidadeId and id=@ExercicioId and ativo and not is_deleted;",
            new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId }, cancellationToken: ct)).ConfigureAwait(false);
        if (ano <= 0)
        {
            throw new InvalidOperationException($"{FolhaPontoFinanceiraRegras.FalhaExercicioAusente}: exercício {exercicioId.ToString(System.Globalization.CultureInfo.InvariantCulture)} inexistente ou inativo para a entidade {entidadeId.ToString(System.Globalization.CultureInfo.InvariantCulture)}; corrija o exercício antes de reprocessar.");
        }
        return ano;
    }
}
