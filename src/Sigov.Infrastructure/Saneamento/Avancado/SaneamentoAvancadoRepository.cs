using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.Health;
using Sigov.Application.Saneamento.Avancado;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saneamento.Avancado;

public sealed class SaneamentoAvancadoRepository : ISaneamentoComercialRepository, ISaneamentoComercialService, ISaneamentoLigacaoService, ISaneamentoHidrometroService, ISaneamentoAtendimentoService, ISaneamentoFaturamentoRepository, ISaneamentoFaturamentoService, ISaneamentoLeituraService, ISaneamentoFaturaService, ISaneamentoArrecadacaoService, ISaneamentoInadimplenciaService, ISaneamentoOperacaoRepository, ISaneamentoOperacaoService, ISaneamentoOrdemServicoService, ISaneamentoCorteReligacaoService, ISaneamentoVazamentoService, ISaneamentoEquipeService, ISaneamentoGisQualidadeRepository, ISaneamentoGisQualidadeService, ISaneamentoGisService, ISaneamentoLaboratorioService, ISaneamentoQualidadeService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.Ordinal)
    {
        "saneamento_consumidor", "saneamento_imovel", "saneamento_ligacao", "saneamento_hidrometro", "saneamento_tarifa", "saneamento_atendimento",
        "saneamento_rota_leitura", "saneamento_leitura", "saneamento_faturamento_lote", "saneamento_fatura", "saneamento_pagamento", "saneamento_inadimplencia", "saneamento_parcelamento",
        "saneamento_equipe", "saneamento_ordem_servico", "saneamento_corte", "saneamento_religacao", "saneamento_vazamento", "saneamento_vistoria",
        "saneamento_unidade_operacional", "saneamento_ponto_gis", "saneamento_rede", "saneamento_trecho_rede", "saneamento_laboratorio_parametro", "saneamento_laboratorio_ponto_coleta", "saneamento_laboratorio_amostra", "saneamento_laboratorio_ensaio", "saneamento_qualidade_alerta"
    };
    private readonly DapperContext _context;
    private readonly IDatabaseObjectInspector _inspector;
    public SaneamentoAvancadoRepository(DapperContext context, IDatabaseObjectInspector inspector) { _context = context; _inspector = inspector; }

    public async Task<PagedResult<SaneamentoAvancadoRegistroDto>> ListarAsync(long tenantId, string recurso, SaneamentoAvancadoFiltro filtro, CancellationToken ct)
    {
        recurso = Recurso(recurso); var pagina = Math.Max(1, filtro.Pagina); var tamanho = Math.Clamp(filtro.Tamanho, 1, 100);
        if (!await EstruturaDisponivelAsync(recurso, ct).ConfigureAwait(false)) return PagedResult<SaneamentoAvancadoRegistroDto>.Empty(pagina, tamanho);
        var sql = $"select id as Id,codigo as Codigo,numero as Numero,tipo as Tipo,status as Status,descricao as Descricao,valor as Valor,created_at as CreatedAt from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false and (@Status is null or status=@Status) and (@Tipo is null or tipo=@Tipo) order by id desc limit @Tamanho offset @Offset;select count(1) from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false and (@Status is null or status=@Status) and (@Tipo is null or tipo=@Tipo)";
        using var connection = _context.CreateConnection(); using var result = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, filtro.Status, filtro.Tipo, Tamanho = tamanho, Offset = (pagina - 1) * tamanho }, cancellationToken: ct));
        return new((await result.ReadAsync<SaneamentoAvancadoRegistroDto>()).ToList(), pagina, tamanho, await result.ReadSingleAsync<long>());
    }
    public async Task<SaneamentoAvancadoRegistroDto?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        recurso = Recurso(recurso); if (!await EstruturaDisponivelAsync(recurso, ct).ConfigureAwait(false)) return null;
        using var connection = _context.CreateConnection(); return await connection.QuerySingleOrDefaultAsync<SaneamentoAvancadoRegistroDto>(new CommandDefinition($"select id as Id,codigo as Codigo,numero as Numero,tipo as Tipo,status as Status,descricao as Descricao,valor as Valor,created_at as CreatedAt from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false", new { TenantId = tenantId, Id = id }, cancellationToken: ct));
    }
    public async Task<long> CriarAsync(SaneamentoAvancadoContext contexto, string recurso, SaneamentoAvancadoOperacaoRequest request, CancellationToken ct)
    {
        recurso = Recurso(recurso); Validar(recurso, request); var correlationId = Guid.TryParse(contexto.CorrelationId, out var parsed) ? parsed : Guid.NewGuid();
        var dados = (request.Dados ?? new Dictionary<string, object?>()).ToDictionary(x => x.Key, x => x.Value) { ["consumidorId"] = request.ConsumidorId, ["ligacaoId"] = request.LigacaoId, ["hidrometroId"] = request.HidrometroId, ["unidadeOperacionalId"] = request.UnidadeOperacionalId, ["ordemServicoId"] = request.OrdemServicoId, ["referenciaId"] = request.ReferenciaId, ["dataReferencia"] = request.DataReferencia, ["competencia"] = request.Competencia, ["latitude"] = request.Latitude, ["longitude"] = request.Longitude, ["quantidade"] = request.Quantidade };
        using var connection = _context.CreateConnection();
        var sql = $"insert into sigov.{recurso}(tenant_id,entidade_id,consumidor_id,ligacao_id,hidrometro_id,unidade_operacional_id,ordem_servico_id,codigo,numero,tipo,status,descricao,data_referencia,valor,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ConsumidorId,@LigacaoId,@HidrometroId,@UnidadeOperacionalId,@OrdemServicoId,@Codigo,@Numero,@Tipo,@Status,@Descricao,@DataReferencia,@Valor,jsonb_build_object('acao','CRIAR','em',now(),'usuario',@UsuarioId,'dados',cast(@Dados as jsonb)),@CorrelationId,@UsuarioId) returning id";
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { contexto.TenantId, contexto.EntidadeId, request.ConsumidorId, request.LigacaoId, request.HidrometroId, request.UnidadeOperacionalId, request.OrdemServicoId, request.Codigo, request.Numero, request.Tipo, request.Status, request.Descricao, request.DataReferencia, request.Valor, Dados = JsonSerializer.Serialize(dados), CorrelationId = correlationId, contexto.UsuarioId }, cancellationToken: ct));
    }
    public async Task<bool> AlterarStatusAsync(SaneamentoAvancadoContext contexto, string recurso, long id, string status, string? justificativa, CancellationToken ct)
    {
        recurso = Recurso(recurso); ValidarTransicao(recurso, status, justificativa); var correlationId = Guid.TryParse(contexto.CorrelationId, out var parsed) ? parsed : Guid.NewGuid();
        using var connection = _context.CreateConnection(); var afetadas = await connection.ExecuteAsync(new CommandDefinition($"update sigov.{recurso} set status=@Status,updated_at=now(),updated_by=@UsuarioId,correlation_id=@CorrelationId,auditoria=auditoria||jsonb_build_object('acao','STATUS','status',@Status,'justificativa',@Justificativa,'em',now()) where tenant_id=@TenantId and id=@Id and is_deleted=false", new { contexto.TenantId, Id = id, Status = status, Justificativa = justificativa, contexto.UsuarioId, CorrelationId = correlationId }, cancellationToken: ct)); return afetadas > 0;
    }
    public async Task<SaneamentoAvancadoDashboardDto> DashboardAsync(long tenantId, string recurso, CancellationToken ct)
    {
        var pagina = await ListarAsync(tenantId, recurso, new SaneamentoAvancadoFiltro(Tamanho: 8), ct); return new(pagina.TotalItems, pagina.Items.LongCount(x => x.Status is "ATIVO" or "ATIVA" or "EMITIDA"), pagina.Items.LongCount(x => x.Status.Contains("PENDENTE", StringComparison.Ordinal) || x.Status is "ABERTA" or "COLETADA"), pagina.Items.LongCount(x => x.Status.Contains("ALERTA", StringComparison.Ordinal) || x.Status is "REPROVADA" or "VENCIDA"), pagina.Items.Sum(x => x.Valor ?? 0), pagina.Items);
    }
    public async Task<byte[]> ExportarCsvAsync(long tenantId, string recurso, CancellationToken ct)
    {
        var pagina = await ListarAsync(tenantId, recurso, new SaneamentoAvancadoFiltro(Tamanho: 100), ct); var csv = new StringBuilder("id;codigo;numero;tipo;status;valor;criado_em\n"); foreach (var item in pagina.Items) csv.Append(item.Id).Append(';').Append(Limpar(item.Codigo)).Append(';').Append(Limpar(item.Numero)).Append(';').Append(Limpar(item.Tipo)).Append(';').Append(item.Status).Append(';').Append(item.Valor).Append(';').Append(item.CreatedAt).Append('\n'); return Encoding.UTF8.GetBytes(csv.ToString());
    }
    private async Task<bool> EstruturaDisponivelAsync(string recurso, CancellationToken ct)
    {
        if (!await _inspector.TableExistsAsync("sigov", recurso, ct).ConfigureAwait(false)) return false; foreach (var coluna in new[] { "id", "tenant_id", "codigo", "numero", "tipo", "status", "descricao", "valor", "created_at", "is_deleted" }) if (!await _inspector.ColumnExistsAsync("sigov", recurso, coluna, ct).ConfigureAwait(false)) return false; return true;
    }
    private static void Validar(string recurso, SaneamentoAvancadoOperacaoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status)) throw new ArgumentException("Status é obrigatório.");
        if ((request.Latitude.HasValue && !request.Longitude.HasValue) || (!request.Latitude.HasValue && request.Longitude.HasValue) || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180) throw new ArgumentException("Coordenadas geográficas inválidas.");
        if (recurso == "saneamento_consumidor" && string.IsNullOrWhiteSpace(request.Descricao)) throw new ArgumentException("Consumidor exige nome ou razão social.");
        if (recurso == "saneamento_ligacao" && (!request.ConsumidorId.HasValue || !request.ReferenciaId.HasValue || string.IsNullOrWhiteSpace(request.Tipo))) throw new ArgumentException("Ligação exige consumidor, imóvel e categoria.");
        if (recurso == "saneamento_hidrometro" && (!request.LigacaoId.HasValue || string.IsNullOrWhiteSpace(request.Numero))) throw new ArgumentException("Hidrômetro exige ligação e número de série.");
        if (recurso == "saneamento_leitura" && (!request.LigacaoId.HasValue || !request.HidrometroId.HasValue || !request.Competencia.HasValue || request.Quantidade < 0)) throw new ArgumentException("Leitura exige ligação, hidrômetro, competência e valor não negativo.");
        if (recurso == "saneamento_ordem_servico" && (string.IsNullOrWhiteSpace(request.Tipo) || string.IsNullOrWhiteSpace(request.Descricao))) throw new ArgumentException("Ordem de serviço exige tipo, local e prioridade/desfecho.");
        if (recurso == "saneamento_laboratorio_amostra" && (!request.DataReferencia.HasValue || string.IsNullOrWhiteSpace(request.Descricao))) throw new ArgumentException("Amostra exige ponto de coleta, data e responsável.");
    }
    private static void ValidarTransicao(string recurso, string status, string? justificativa)
    {
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Novo status é obrigatório."); if ((status is "CANCELADA" or "CANCELADO" or "REPROVADA") && string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Cancelamento ou reprovação exige justificativa."); if (recurso == "saneamento_ordem_servico" && status == "CONCLUIDA" && string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Conclusão da OS exige desfecho.");
    }
    private static string Recurso(string recurso) => Recursos.Contains(recurso) ? recurso : throw new ArgumentException("Recurso de Saneamento Avançado inválido.");
    private static string Limpar(string? valor) => (valor ?? string.Empty).Replace(";", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
