using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.Tributario.TributarioAvancado;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Tributario;

public sealed class TributarioAvancadoRepository : ITributarioCarnesBoletosRepository, IPortalContribuinteRepository,
    ITributarioFiscalizacaoRepository, ITributarioNfseRepository, ITributarioCarnesBoletosService,
    ITributarioCarneEntregaService, ITributarioDamService, IPortalContribuinteCertidaoService,
    IPortalContribuinteGuiaService, IPortalContribuinteParcelamentoService, ITributarioIssqnService,
    ITributarioSimplesNacionalService, ITributarioAutoInfracaoService, ITributarioLivroEletronicoService,
    ITributarioDesifService, ITributarioNfseValidacaoService, ITributarioCarneArquivoService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.Ordinal)
    {
        "tributario_carne_emissao", "tributario_carne_producao", "tributario_carne_entrega", "tributario_dam", "tributario_boleto_preparatorio",
        "portal_contribuinte_solicitacao", "portal_contribuinte_certidao", "portal_contribuinte_guia_emitida", "portal_contribuinte_parcelamento_solicitacao", "portal_contribuinte_protocolo",
        "tributario_fiscalizacao_ordem", "tributario_fiscalizacao_diligencia", "tributario_fiscalizacao_notificacao", "tributario_fiscalizacao_auto_infracao", "tributario_fiscalizacao_defesa", "tributario_fiscalizacao_julgamento", "tributario_simples_divergencia", "tributario_iss_apuracao",
        "tributario_nfse_configuracao", "tributario_nfse_nota", "tributario_livro_eletronico", "tributario_desif_declaracao"
    };
    private readonly DapperContext _context;
    public TributarioAvancadoRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<TributarioRegistroDto>> ListarAsync(long tenantId, string recurso, int pagina, int tamanho, CancellationToken ct)
    {
        recurso = Recurso(recurso); pagina = Math.Max(1, pagina); tamanho = Math.Clamp(tamanho, 1, 100);
        var sql = $"select id as Id,codigo as Codigo,status as Status,tipo as Tipo,descricao as Descricao,valor as Valor,created_at as CreatedAt,dados::text as Dados from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false order by id desc limit @Tamanho offset @Offset; select count(1) from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false";
        using var c = _context.CreateConnection();
        using var m = await c.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Tamanho = tamanho, Offset = (pagina - 1) * tamanho }, cancellationToken: ct));
        var rows = (await m.ReadAsync<Row>()).Select(Mapear).ToList();
        return new PagedResult<TributarioRegistroDto>(rows, pagina, tamanho, await m.ReadSingleAsync<long>());
    }

    public async Task<TributarioRegistroDto?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        recurso = Recurso(recurso); using var c = _context.CreateConnection();
        var row = await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition($"select id as Id,codigo as Codigo,status as Status,tipo as Tipo,descricao as Descricao,valor as Valor,created_at as CreatedAt,dados::text as Dados,contribuinte_id as ContribuinteId,referencia_id as ReferenciaId from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false", new { TenantId = tenantId, Id = id }, cancellationToken: ct));
        return row is null ? null : Mapear(row);
    }

    public async Task<long> CriarAsync(TributarioAvancadoContext x, string recurso, TributarioOperacaoRequest r, CancellationToken ct)
    {
        recurso = Recurso(recurso); Validar(recurso, r);
        var dadosSanitizados = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (r.Dados != null)
        {
            foreach (var kvp in r.Dados)
            {
                if (kvp.Key is "tenant_id" or "usuario_id" or "permissoes" or "roles" or "is_deleted" or "auditoria" or "admin" or "bypass_rls" or "contribuinte_id")
                    continue;
                dadosSanitizados[kvp.Key] = kvp.Value;
            }
        }
        var contribuinteId = r.ReferenciaId;
        const string values = "(tenant_id,entidade_id,exercicio_id,contribuinte_id,referencia_id,codigo,tipo,status,descricao,justificativa,quantidade,valor,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@ContribuinteId,@ReferenciaId,@Codigo,@Tipo,@Status,@Descricao,@Justificativa,@Quantidade,@Valor,@Dados::jsonb,jsonb_build_object('acao','CRIAR','em',now()),@CorrelationId,@UsuarioId) returning id";
        using var c = _context.CreateConnection();
        return await c.ExecuteScalarAsync<long>(new CommandDefinition($"insert into sigov.{recurso}{values}", new { x.TenantId, x.EntidadeId, x.ExercicioId, ContribuinteId = contribuinteId, r.ReferenciaId, r.Codigo, r.Tipo, r.Status, r.Descricao, r.Justificativa, r.Quantidade, r.Valor, Dados = JsonSerializer.Serialize(dadosSanitizados), CorrelationId = Guid.TryParse(x.CorrelationId, out var g) ? g : Guid.NewGuid(), x.UsuarioId }, cancellationToken: ct));
    }

    public async Task<bool> AlterarStatusAsync(TributarioAvancadoContext x, string recurso, long id, string status, string? justificativa, CancellationToken ct)
    {
        recurso = Recurso(recurso);
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Status é obrigatório.");
        if ((status is "CANCELADO" or "CANCELADA" or "INDEFERIDO" or "INDEFERIDA") && string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Cancelamento ou indeferimento exige justificativa.");
        if (recurso == "tributario_carne_entrega" && status == "NOVA_TENTATIVA" && string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Nova tentativa exige motivo.");
        
        using var c = _context.CreateConnection();
        var atual = await c.QuerySingleOrDefaultAsync<string>($"select status from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false", new { x.TenantId, Id = id });
        if (atual is null) return false;
        if (atual is "CANCELADO" or "CANCELADA" or "CONCLUIDO" or "CONCLUIDA" && status != atual)
        {
            throw new InvalidOperationException($"Não é permitido alterar o status de um registro finalizado ({atual}).");
        }

        return await c.ExecuteAsync(new CommandDefinition($"update sigov.{recurso} set status=@Status,justificativa=coalesce(@Justificativa,justificativa),updated_at=now(),updated_by=@UsuarioId,correlation_id=@CorrelationId,auditoria=auditoria||jsonb_build_object('acao','STATUS','status_anterior',@Atual,'status_novo',@Status,'em',now()) where tenant_id=@TenantId and id=@Id and is_deleted=false", new { x.TenantId, Id = id, Status = status, Justificativa = justificativa, x.UsuarioId, Atual = atual, CorrelationId = Guid.TryParse(x.CorrelationId, out var g) ? g : Guid.NewGuid() }, cancellationToken: ct)) > 0;
    }

    public async Task<TributarioDashboardDto> DashboardAsync(long tenantId, string recurso, CancellationToken ct)
    {
        recurso = Recurso(recurso); using var c = _context.CreateConnection();
        var k = await c.QuerySingleAsync<Kpi>(new CommandDefinition($"select count(1) as Total,count(1) filter(where status in ('RASCUNHO','PENDENTE','ABERTA','EM_PRODUCAO')) as Pendentes,count(1) filter(where status in ('CONCLUIDO','ENTREGUE','EMITIDA','VALIDADO')) as Concluidos,count(1) filter(where prazo_at<now() and status not in ('CONCLUIDO','ENTREGUE','CANCELADO')) as Alertas from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false", new { TenantId = tenantId }, cancellationToken: ct));
        var recentes = await ListarAsync(tenantId, recurso, 1, 6, ct); return new(k.Total, k.Pendentes, k.Concluidos, k.Alertas, recentes.Items);
    }

    public async Task<byte[]> GerarCsvAsync(long tenantId, long emissaoId, CancellationToken ct)
    {
        using var c = _context.CreateConnection();
        var existe = await c.ExecuteScalarAsync<bool>(
            "select exists(select 1 from sigov.tributario_carne_emissao where tenant_id=@TenantId and id=@EmissaoId and is_deleted=false)",
            new { TenantId = tenantId, EmissaoId = emissaoId });
        if (!existe) throw new KeyNotFoundException("Emissão de carnê não encontrada para o tenant informado.");

        var rows = await c.QueryAsync<(long Id, string? Codigo, string Status)>(new CommandDefinition(
            "select id as Id,codigo as Codigo,status as Status from sigov.tributario_carne_item where tenant_id=@TenantId and referencia_id=@EmissaoId and is_deleted=false order by id",
            new { TenantId = tenantId, EmissaoId = emissaoId }, cancellationToken: ct));

        var csv = new StringBuilder("id;codigo;status\n");
        foreach (var row in rows)
        {
            csv.Append(row.Id).Append(';');
            csv.Append(SanitizarCsvCampo(row.Codigo)).Append(';');
            csv.Append(SanitizarCsvCampo(row.Status)).Append('\n');
        }
        return new UTF8Encoding(true).GetBytes(csv.ToString());
    }

    public static string SanitizarCsvCampo(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;
        var texto = valor;
        if (texto.Length > 0 && (texto[0] == '=' || texto[0] == '+' || texto[0] == '-' || texto[0] == '@' || texto[0] == '\t' || texto[0] == '\r'))
        {
            texto = "'" + texto;
        }
        if (texto.Contains(';') || texto.Contains('"') || texto.Contains('\n') || texto.Contains('\r'))
        {
            return $"\"{texto.Replace("\"", "\"\"")}\"";
        }
        return texto;
    }

    public async Task<IReadOnlyList<ContribuinteAutorizadoInfo>> ObterContribuintesAutorizadosAsync(long tenantId, long usuarioId, string? userEmail, CancellationToken ct)
    {
        using var c = _context.CreateConnection();
        var sqlAcesso = @"
            select a.contribuinte_id as ContribuinteId, coalesce(c.inscricao, a.codigo, cast(a.contribuinte_id as text)) as Inscricao, c.nome as Nome, a.tipo as TipoVinculo
            from sigov.portal_contribuinte_acesso a
            join sigov.contribuinte c on c.id = a.contribuinte_id and c.tenant_id = a.tenant_id and c.ativo = true and not c.is_deleted
            where a.tenant_id = @TenantId
              and a.ativo = true
              and not a.is_deleted
              and a.status in ('ATIVO', 'VALIDADO', 'HOMOLOGADO', 'DEFERIDO')
              and a.status not in ('REVOGADO', 'CANCELADO', 'EXPIRADO', 'SUSPENSO')
              and (a.prazo_at is null or a.prazo_at >= now())
              and a.contribuinte_id is not null
              and (
                a.referencia_id = @UsuarioId
                or (a.dados->>'usuario_id')::bigint = @UsuarioId
                or exists (
                    select 1 from sigov.usuario u2
                    join sigov.pessoa p2 on p2.id = u2.pessoa_id and p2.tenant_id = @TenantId and not p2.is_deleted
                    where u2.id = @UsuarioId and not u2.is_deleted and p2.documento is not null and (a.dados->>'documento_representante' = p2.documento or a.dados->>'cpf_representante' = p2.documento)
                )
                or (a.created_by = @UsuarioId and a.tipo in ('PROCURADOR', 'REPRESENTANTE', 'AUTORIZADO', 'DELEGADO'))
              )";
        var acessos = (await c.QueryAsync<ContribuinteAutorizadoInfo>(new CommandDefinition(sqlAcesso, new { TenantId = tenantId, UsuarioId = usuarioId }, cancellationToken: ct))).ToList();

        var sqlDireto = @"
            select c.id as ContribuinteId, c.inscricao as Inscricao, c.nome as Nome, 'TITULAR' as TipoVinculo
            from sigov.usuario u
            join sigov.pessoa p on p.id = u.pessoa_id and p.tenant_id = @TenantId and p.ativo = true and not p.is_deleted
            join sigov.contribuinte c on c.documento = p.documento and c.tenant_id = @TenantId and c.ativo = true and not c.is_deleted
            where u.id = @UsuarioId
              and u.ativo = true
              and not u.is_deleted
              and p.documento is not null";
        var diretos = (await c.QueryAsync<ContribuinteAutorizadoInfo>(new CommandDefinition(sqlDireto, new { TenantId = tenantId, UsuarioId = usuarioId }, cancellationToken: ct))).ToList();

        return acessos.Concat(diretos)
            .GroupBy(x => x.ContribuinteId)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<ContribuinteAutorizadoInfo?> BuscarContribuintePorCodigoAsync(long tenantId, string codigo, CancellationToken ct)
    {
        using var c = _context.CreateConnection();
        var sql = @"
            select id as ContribuinteId, inscricao as Inscricao, nome as Nome, 'ADMINISTRATIVO' as TipoVinculo
            from sigov.contribuinte
            where tenant_id = @TenantId
              and (inscricao = @Codigo or documento = @Codigo or (id = @IdNumeric and @IdNumeric > 0))
              and ativo = true
            limit 1";
        long.TryParse(codigo, out var idNumeric);
        return await c.QuerySingleOrDefaultAsync<ContribuinteAutorizadoInfo>(new CommandDefinition(sql, new { TenantId = tenantId, Codigo = codigo.Trim(), IdNumeric = idNumeric }, cancellationToken: ct));
    }

    public async Task<PagedResult<ContribuinteDebitoDto>> ListarDebitosAsync(long tenantId, long contribuinteId, int pagina, int tamanho, CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);
        var offset = (pagina - 1) * tamanho;
        using var c = _context.CreateConnection();

        var sql = @"
            select id as Id, cast(id as text) as Numero, origem_tipo as Tipo, valor_original as ValorOriginal, valor_atualizado as ValorAtualizado, data_vencimento as Vencimento, status as Status
            from sigov.parcela
            where tenant_id = @TenantId
              and contribuinte_id = @ContribuinteId
              and status in ('ABERTA', 'VENCIDA', 'PARCELADA')
            order by data_vencimento asc, id asc
            limit @Tamanho offset @Offset;

            select count(1)
            from sigov.parcela
            where tenant_id = @TenantId
              and contribuinte_id = @ContribuinteId
              and status in ('ABERTA', 'VENCIDA', 'PARCELADA');
        ";
        using var m = await c.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ContribuinteId = contribuinteId, Tamanho = tamanho, Offset = offset }, cancellationToken: ct));
        var items = (await m.ReadAsync<ContribuinteDebitoDto>()).ToList();
        var total = await m.ReadSingleAsync<long>();
        return new PagedResult<ContribuinteDebitoDto>(items, pagina, tamanho, total);
    }

    public async Task<PagedResult<ContribuintePagamentoDto>> ListarPagamentosAsync(long tenantId, long contribuinteId, int pagina, int tamanho, CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);
        var offset = (pagina - 1) * tamanho;
        using var c = _context.CreateConnection();

        var sql = @"
            select id as Id, codigo_baixa as CodigoBaixa, forma_pagamento as FormaPagamento, valor_pago as ValorPago, data_pagamento as DataPagamento, status as Status
            from sigov.arrecadacao
            where tenant_id = @TenantId
              and contribuinte_id = @ContribuinteId
              and status = 'CONFIRMADA'
            order by data_pagamento desc, id desc
            limit @Tamanho offset @Offset;

            select count(1)
            from sigov.arrecadacao
            where tenant_id = @TenantId
              and contribuinte_id = @ContribuinteId
              and status = 'CONFIRMADA';
        ";
        using var m = await c.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ContribuinteId = contribuinteId, Tamanho = tamanho, Offset = offset }, cancellationToken: ct));
        var items = (await m.ReadAsync<ContribuintePagamentoDto>()).ToList();
        var total = await m.ReadSingleAsync<long>();
        return new PagedResult<ContribuintePagamentoDto>(items, pagina, tamanho, total);
    }

    public async Task<CertidaoValidacaoPublicaDto?> ValidarCertidaoPublicaAsync(long? tenantId, string codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Trim().Length < 4) return null;
        using var c = _context.CreateConnection();
        var sql = @"
            select id, codigo, tipo, status, prazo_at, created_at, dados::text as dados_json
            from sigov.portal_contribuinte_certidao
            where (@TenantId is null or tenant_id = @TenantId)
              and (codigo = @Codigo or (id = @IdNumeric and @IdNumeric > 0))
              and is_deleted = false
            order by id desc
            limit 1";
        long.TryParse(codigo, out var idNumeric);
        var row = await c.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(sql, new { TenantId = tenantId, Codigo = codigo.Trim(), IdNumeric = idNumeric }, cancellationToken: ct));
        if (row is null) return null;

        string status = (string)row.status ?? "EMITIDA";
        DateTimeOffset createdAt = (DateTimeOffset)row.created_at;
        DateTimeOffset? prazoAt = row.prazo_at is null ? null : (DateTimeOffset?)row.prazo_at;
        DateOnly? validadeAte = prazoAt.HasValue ? DateOnly.FromDateTime(prazoAt.Value.Date) : null;

        bool revogada = string.Equals(status, "REVOGADA", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "CANCELADA", StringComparison.OrdinalIgnoreCase);
        bool expirada = validadeAte.HasValue && validadeAte.Value < DateOnly.FromDateTime(DateTime.UtcNow);
        bool autentica = !revogada && !expirada && (status == "EMITIDA" || status == "VALIDADA" || status == "CONCLUIDA");

        string mensagem = revogada ? "Certidão revogada pela autoridade fiscal." :
                         expirada ? "Certidão expirada." :
                         autentica ? "Certidão autêntica e válida." : $"Certidão com situação: {status}.";

        return new CertidaoValidacaoPublicaDto(
            Codigo: (string)row.codigo ?? codigo,
            Tipo: (string)row.tipo ?? "CERTIDAO_NEGATIVA",
            Status: status,
            Autentica: autentica,
            Mensagem: mensagem,
            EmitidaEm: createdAt,
            ValidadeAte: validadeAte
        );
    }

    public async Task<TributarioDashboardDto> DashboardAutoatendimentoAsync(long tenantId, long[] contribuinteIds, long usuarioId, CancellationToken ct)
    {
        using var c = _context.CreateConnection();
        var sql = @"
            select count(1) as Total,
                   count(1) filter(where status in ('RASCUNHO','PENDENTE','ABERTA','SOLICITADA','EM_ANALISE')) as Pendentes,
                   count(1) filter(where status in ('CONCLUIDO','ENTREGUE','EMITIDA','VALIDADO','DEFERIDA')) as Concluidos,
                   count(1) filter(where prazo_at < now() and status not in ('CONCLUIDO','ENTREGUE','CANCELADO','DEFERIDA','INDEFERIDA')) as Alertas
            from sigov.portal_contribuinte_protocolo
            where tenant_id = @TenantId
              and is_deleted = false
              and (contribuinte_id = any(@ContribuinteIds) or created_by = @UsuarioId)";
        var k = await c.QuerySingleAsync<Kpi>(new CommandDefinition(sql, new { TenantId = tenantId, ContribuinteIds = contribuinteIds, UsuarioId = usuarioId }, cancellationToken: ct));

        var sqlRecentes = @"
            select id as Id, codigo as Codigo, status as Status, tipo as Tipo, descricao as Descricao, valor as Valor, created_at as CreatedAt, dados::text as Dados
            from sigov.portal_contribuinte_protocolo
            where tenant_id = @TenantId
              and is_deleted = false
              and (contribuinte_id = any(@ContribuinteIds) or created_by = @UsuarioId)
            order by id desc
            limit 6";
        var rows = (await c.QueryAsync<Row>(new CommandDefinition(sqlRecentes, new { TenantId = tenantId, ContribuinteIds = contribuinteIds, UsuarioId = usuarioId }, cancellationToken: ct))).Select(Mapear).ToList();
        return new TributarioDashboardDto(k.Total, k.Pendentes, k.Concluidos, k.Alertas, rows);
    }

    public async Task<PagedResult<TributarioRegistroDto>> ListarSolicitacoesAutoatendimentoAsync(long tenantId, long[] contribuinteIds, long usuarioId, int pagina, int tamanho, CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);
        var offset = (pagina - 1) * tamanho;
        using var c = _context.CreateConnection();

        var sql = @"
            select id as Id, codigo as Codigo, status as Status, tipo as Tipo, descricao as Descricao, valor as Valor, created_at as CreatedAt, dados::text as Dados
            from sigov.portal_contribuinte_solicitacao
            where tenant_id = @TenantId
              and is_deleted = false
              and (contribuinte_id = any(@ContribuinteIds) or created_by = @UsuarioId)
            order by id desc
            limit @Tamanho offset @Offset;

            select count(1)
            from sigov.portal_contribuinte_solicitacao
            where tenant_id = @TenantId
              and is_deleted = false
              and (contribuinte_id = any(@ContribuinteIds) or created_by = @UsuarioId);
        ";
        using var m = await c.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ContribuinteIds = contribuinteIds, UsuarioId = usuarioId, Tamanho = tamanho, Offset = offset }, cancellationToken: ct));
        var rows = (await m.ReadAsync<Row>()).Select(Mapear).ToList();
        var total = await m.ReadSingleAsync<long>();
        return new PagedResult<TributarioRegistroDto>(rows, pagina, tamanho, total);
    }

    private static void Validar(string recurso, TributarioOperacaoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Status)) throw new ArgumentException("Status é obrigatório.");
        if (r.Codigo != null && r.Codigo.Length > 120) throw new ArgumentException("Código excede o limite de 120 caracteres.");
        if (r.Tipo != null && r.Tipo.Length > 80) throw new ArgumentException("Tipo excede o limite de 80 caracteres.");
        if (r.Descricao != null && r.Descricao.Length > 4000) throw new ArgumentException("Descrição excede o limite de 4000 caracteres.");
        if (r.Justificativa != null && r.Justificativa.Length > 2000) throw new ArgumentException("Justificativa excede o limite de 2000 caracteres.");
        if (r.Quantidade.HasValue && r.Quantidade.Value < 0) throw new ArgumentException("Quantidade não pode ser negativa.");
        if (r.Valor.HasValue && r.Valor.Value < 0) throw new ArgumentException("Valor não pode ser negativo.");

        if (recurso == "tributario_nfse_nota" && (!r.Valor.HasValue || r.Valor <= 0)) throw new ArgumentException("NFS-e preparatória exige valor positivo.");
        if (recurso == "tributario_fiscalizacao_auto_infracao" && (string.IsNullOrWhiteSpace(r.Descricao) || !r.Valor.HasValue || r.Valor <= 0)) throw new ArgumentException("Auto de infração exige fundamento, descrição e valor.");
    }
    private static string Recurso(string recurso) => Recursos.Contains(recurso) ? recurso : throw new ArgumentException("Recurso tributário inválido.");
    private static TributarioRegistroDto Mapear(Row r) => new(r.Id, r.Codigo, r.Status, r.Tipo, r.Descricao, r.Valor, r.CreatedAt, JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Dados) ?? new(), r.ContribuinteId, r.ReferenciaId);
    private sealed record Row(long Id, string? Codigo, string Status, string? Tipo, string? Descricao, decimal? Valor, DateTimeOffset CreatedAt, string Dados, long? ContribuinteId = null, long? ReferenciaId = null);
    private sealed record Kpi(long Total, long Pendentes, long Concluidos, long Alertas);
}
