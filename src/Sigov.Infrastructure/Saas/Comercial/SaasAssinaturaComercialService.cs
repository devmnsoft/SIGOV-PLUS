using System.Data;
using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

/// <summary>
/// RC-SAAS-AUT Etapa B - implementação transacional das operações comerciais da família B.
/// A assinatura "melhor" do tenant (ativa primeiro, depois a mais recente) é travada com
/// <c>for update</c> para serializar disputas de mutação no mesmo tenant.
/// </summary>
public sealed class SaasAssinaturaComercialService : ISaasAssinaturaComercialService
{
    private readonly DapperContext _context;

    public SaasAssinaturaComercialService(DapperContext context) => _context = context;

    private sealed record AssinaturaLinha(long Id, long PlanoId, string Status, int PlanoOrdem, string PlanoCodigo);
    private sealed record AssinaturaLinhaBruta(long Id, long PlanoId, string Status, DateTime? CriadoEm);
    private sealed record PlanoDados(int Ordem, string Codigo);
    private sealed record PlanoLinha(long Id, string Codigo, int Ordem, string Periodicidade, string Moeda, decimal? Valor, int? LimiteUsuarios);

    public async Task<SaasComercialResultado> CriarAsync(long tenantId, long planoId, long usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0) return SaasComercialResultado.Falha("Tenant inválido.");
        using var connection = _context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var atual = await LockBestAsync(connection, tx, tenantId, cancellationToken).ConfigureAwait(false);
            if (atual is not null && (atual.Status == "ATIVA" || atual.Status == "SUSPENSA"))
                return SaasComercialResultado.Falha($"Cliente já possui assinatura em situação {atual.Status}; use reativação ou troca de plano.");

            var plano = await GetPlanoAsync(connection, tx, planoId, cancellationToken).ConfigureAwait(false);
            if (plano is null) return SaasComercialResultado.Falha("Plano inválido ou inativo.");

            var assinaturaId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                @"insert into sigov.saas_assinatura (tenant_id, plano_id, status, periodicidade, valor_contratado, moeda, usuarios_contratados, created_by, correlation_id)
values (@TenantId, @PlanoId, 'ATIVA', @Periodicidade, @Valor, @Moeda, @Usuarios, @UsuarioId, @CorrelationId)
returning id;",
                new
                {
                    TenantId = tenantId,
                    PlanoId = plano.Id,
                    Periodicidade = plano.Periodicidade,
                    Valor = plano.Valor,
                    Moeda = plano.Moeda,
                    Usuarios = plano.LimiteUsuarios ?? 1,
                    UsuarioId = usuarioId,
                    CorrelationId = Correlation(correlationId)
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await AtivarModulosAsync(connection, tx, tenantId, assinaturaId, plano.Id, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
            await RegistrarHistoricoAsync(connection, tx, tenantId, assinaturaId, null, plano.Id, "CRIAR", null, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, tx, tenantId, assinaturaId, "ASSINATURA_CRIADA", new { plano = plano.Codigo }, correlationId, cancellationToken).ConfigureAwait(false);

            tx.Commit();
            return SaasComercialResultado.Ok($"Assinatura {plano.Codigo} criada para o cliente.");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return SaasComercialResultado.Falha($"Falha ao criar a assinatura: {ex.Message}");
        }
    }

    public Task<SaasComercialResultado> UpgradeAsync(long tenantId, long novoPlanoId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default) =>
        TrocarPlanoAsync(tenantId, novoPlanoId, motivo, usuarioId, correlationId, upgrade: true, cancellationToken);

    public Task<SaasComercialResultado> DowngradeAsync(long tenantId, long novoPlanoId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default) =>
        TrocarPlanoAsync(tenantId, novoPlanoId, motivo, usuarioId, correlationId, upgrade: false, cancellationToken);

    private async Task<SaasComercialResultado> TrocarPlanoAsync(long tenantId, long novoPlanoId, string? motivo, long usuarioId, string correlationId, bool upgrade, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) return SaasComercialResultado.Falha("Tenant inválido.");
        using var connection = _context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var atual = await LockBestAsync(connection, tx, tenantId, cancellationToken).ConfigureAwait(false);
            if (atual is null || atual.Status != "ATIVA")
                return SaasComercialResultado.Falha("Cliente não possui assinatura ativa para alteração de plano.");
            if (atual.PlanoId == novoPlanoId)
                return SaasComercialResultado.Ok("Cliente já está no plano informado.");

            var novo = await GetPlanoAsync(connection, tx, novoPlanoId, cancellationToken).ConfigureAwait(false);
            if (novo is null) return SaasComercialResultado.Falha("Plano destino inválido ou inativo.");
            if (upgrade && novo.Ordem <= atual.PlanoOrdem)
                return SaasComercialResultado.Falha("O plano destino não é superior ao plano atual; use downgrade.");
            if (!upgrade && novo.Ordem >= atual.PlanoOrdem)
                return SaasComercialResultado.Falha("O plano destino não é inferior ao plano atual; use upgrade.");

            var antes = await GetModulosPlanoAsync(connection, tx, atual.PlanoId, cancellationToken).ConfigureAwait(false);
            var depois = await GetModulosPlanoAsync(connection, tx, novo.Id, cancellationToken).ConfigureAwait(false);
            var diff = PlanModuleDiff.Calcular(antes, depois);

            if (diff.Adicionados.Count > 0)
                await AtivarModulosAsync(connection, tx, tenantId, atual.Id, novo.Id, usuarioId, correlationId, cancellationToken, diff.Adicionados).ConfigureAwait(false);

            IReadOnlyCollection<string>? suspensos = null;
            if (diff.Removidos.Count > 0 && await BloqueioDowngradeAtivoAsync(connection, tx, tenantId, cancellationToken).ConfigureAwait(false))
            {
                await SuspenderModulosAsync(connection, tx, tenantId, atual.Id, diff.Removidos, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
                suspensos = diff.Removidos;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"update sigov.saas_assinatura set plano_id=@NovoPlanoId, valor_contratado=@Valor, moeda=@Moeda, periodicidade=@Periodicidade, usuarios_contratados=@Usuarios,
updated_at=now(), updated_by=@UsuarioId, correlation_id=@CorrelationId where id=@AssinaturaId;",
                new
                {
                    AssinaturaId = atual.Id,
                    NovoPlanoId = novo.Id,
                    Valor = novo.Valor,
                    Moeda = novo.Moeda,
                    Periodicidade = novo.Periodicidade,
                    Usuarios = novo.LimiteUsuarios ?? 1,
                    UsuarioId = usuarioId,
                    CorrelationId = Correlation(correlationId)
                }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            var acao = upgrade ? "UPGRADE" : "DOWNGRADE";
            var tipoEvento = upgrade ? "ASSINATURA_UPGRADED" : "ASSINATURA_DOWNGRADED";
            await RegistrarHistoricoAsync(connection, tx, tenantId, atual.Id, atual.PlanoId, novo.Id, acao, motivo, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, tx, tenantId, atual.Id, tipoEvento,
                new { planoAnterior = atual.PlanoCodigo, planoNovo = novo.Codigo, modulosSuspensos = suspensos ?? Array.Empty<string>() }, correlationId, cancellationToken).ConfigureAwait(false);

            tx.Commit();
            var mensagem = upgrade
                ? $"Upgrade para o plano {novo.Codigo} concluído."
                : $"Downgrade para o plano {novo.Codigo} concluído. Os dados dos módulos excedentes foram preservados.";
            return suspensos is { Count: > 0 }
                ? new SaasComercialResultado(true, $"{mensagem} Módulos excedentes suspensos: {string.Join(", ", suspensos)}.", suspensos)
                : SaasComercialResultado.Ok(mensagem);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return SaasComercialResultado.Falha($"Falha ao alterar o plano: {ex.Message}");
        }
    }

    public Task<SaasComercialResultado> SuspenderAsync(long tenantId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default) =>
        AlterarStatusAsync(tenantId, new[] { "ATIVA" }, "SUSPENSA", "SUSPENDER", "ASSINATURA_SUSPENSA", DataFimSql.Nula, motivo, usuarioId, correlationId, cancellationToken);

    public Task<SaasComercialResultado> ReativarAsync(long tenantId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default) =>
        AlterarStatusAsync(tenantId, new[] { "SUSPENSA" }, "ATIVA", "REATIVAR", "ASSINATURA_REATIVADA", DataFimSql.Nula, motivo, usuarioId, correlationId, cancellationToken);

    public Task<SaasComercialResultado> CancelarAsync(long tenantId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default) =>
        AlterarStatusAsync(tenantId, new[] { "ATIVA", "SUSPENSA" }, "CANCELADA", "CANCELAR", "ASSINATURA_CANCELADA", DataFimSql.MantemOuPreenche, motivo, usuarioId, correlationId, cancellationToken);

    private static class DataFimSql
    {
        public const string Nula = "data_fim=null";
        public const string MantemOuPreenche = "data_fim=coalesce(data_fim, @DataFim)";
    }

    private async Task<SaasComercialResultado> AlterarStatusAsync(
        long tenantId, string[] statusOrigem, string statusDestino, string acao, string tipoEvento,
        string dataFimSql, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) return SaasComercialResultado.Falha("Tenant inválido.");
        using var connection = _context.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var atual = await LockBestAsync(connection, tx, tenantId, cancellationToken).ConfigureAwait(false);
            if (atual is null)
                return SaasComercialResultado.Falha("Cliente não possui assinatura.");
            if (atual.Status == statusDestino)
                return SaasComercialResultado.Ok($"Assinatura já se encontra no status {statusDestino}.");
            if (!statusOrigem.Contains(atual.Status))
                return SaasComercialResultado.Falha($"Assinatura em status {atual.Status}; operação não aplicável.");

            DateOnly? dataFim = statusDestino == "CANCELADA" ? DateOnly.FromDateTime(DateTime.UtcNow) : null;
            await connection.ExecuteAsync(new CommandDefinition(
                $@"update sigov.saas_assinatura set status=@Status, {dataFimSql}, updated_at=now(), updated_by=@UsuarioId, correlation_id=@CorrelationId where id=@AssinaturaId;",
                new { AssinaturaId = atual.Id, Status = statusDestino, UsuarioId = usuarioId, CorrelationId = Correlation(correlationId), DataFim = dataFim },
                tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

            await RegistrarHistoricoAsync(connection, tx, tenantId, atual.Id, atual.PlanoId, atual.PlanoId, acao, motivo, usuarioId, correlationId, cancellationToken).ConfigureAwait(false);
            await RegistrarEventoAsync(connection, tx, tenantId, atual.Id, tipoEvento,
                new { statusOrigem = atual.Status, statusNovo = statusDestino, motivo }, correlationId, cancellationToken).ConfigureAwait(false);

            tx.Commit();
            return SaasComercialResultado.Ok($"Assinatura {statusDestino}. Os dados do cliente foram preservados.");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return SaasComercialResultado.Falha($"Falha ao alterar o status da assinatura: {ex.Message}");
        }
    }

    /// <summary>
    /// Trava todas as linhas de assinatura do tenant e, em seguida, escolhe a "melhor"
    /// (ativa primeiro, depois a mais recente) em memória sobre o conjunto travado.
    /// Atenção: a sentença FOR UPDATE é restrita à tabela saas_assinatura (sem JOIN, sem
    /// ORDER BY/LIMIT). No PG 16 / READ COMMITTED, quando a sentença de lock contém JOIN, o
    /// EvalPlanQual pode descartar a linha reobtida após aguardar o lock de outra transação
    /// e devolver conjunto vazio mesmo existindo linha commitada (reproduzido em teste de
    /// duas sessões; RC-SAAS-AUT GATE.G.upgrade_concorrente).
    /// </summary>
    private static async Task<AssinaturaLinha?> LockBestAsync(IDbConnection connection, IDbTransaction tx, long tenantId, CancellationToken ct)
    {
        var linhas = (await connection.QueryAsync<AssinaturaLinhaBruta>(new CommandDefinition(
            @"select a.id as Id, a.plano_id as PlanoId, upper(a.status) as Status, a.created_at as CriadoEm
from sigov.saas_assinatura a
where a.tenant_id=@TenantId
for update of a;",
            new { TenantId = tenantId }, tx, cancellationToken: ct)).ConfigureAwait(false)).ToList();

        var melhor = linhas
            .OrderBy(l => l.Status == "ATIVA" ? 0 : 1)
            .ThenByDescending(l => l.CriadoEm)
            .FirstOrDefault();
        if (melhor is null) return null;

        // Leitura normal do plano depois do lock (mesma semantica do join original: sem filtro de ativo).
        var plano = await connection.QuerySingleOrDefaultAsync<PlanoDados?>(new CommandDefinition(
            "select ordem as Ordem, codigo as Codigo from sigov.saas_plano where id=@PlanoId limit 1;",
            new { PlanoId = melhor.PlanoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new AssinaturaLinha(melhor.Id, melhor.PlanoId, melhor.Status, plano?.Ordem ?? 0, plano?.Codigo ?? string.Empty);
    }

    private static Task<PlanoLinha?> GetPlanoAsync(IDbConnection connection, IDbTransaction tx, long planoId, CancellationToken ct) =>
        connection.QuerySingleOrDefaultAsync<PlanoLinha>(new CommandDefinition(
            @"select id as Id, codigo as Codigo, ordem as Ordem, periodicidade as Periodicidade, moeda as Moeda,
preco_base as Valor, limite_usuarios as LimiteUsuarios
from sigov.saas_plano where id=@PlanoId and ativo limit 1;",
            new { PlanoId = planoId }, tx, cancellationToken: ct));

    private static async Task<IReadOnlyList<string>> GetModulosPlanoAsync(IDbConnection connection, IDbTransaction tx, long planoId, CancellationToken ct) =>
        (await connection.QueryAsync<string>(new CommandDefinition(
            "select modulo_codigo from sigov.saas_plano_modulo where plano_id=@PlanoId and incluso=true order by modulo_codigo;",
            new { PlanoId = planoId }, tx, cancellationToken: ct)).ConfigureAwait(false)).ToList();

    private static async Task AtivarModulosAsync(
        IDbConnection connection, IDbTransaction tx, long tenantId, long assinaturaId, long planoId,
        long usuarioId, string correlationId, CancellationToken ct, IReadOnlyCollection<string>? modulos = null)
    {
        var lista = modulos ?? (await GetModulosPlanoAsync(connection, tx, planoId, ct).ConfigureAwait(false)).ToList();
        if (lista.Count == 0) return;

        var correlation = Correlation(correlationId);
        await connection.ExecuteAsync(new CommandDefinition(
            @"insert into sigov.tenant_modulo_contratado (tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, ativo, created_by, correlation_id)
select @TenantId, m, 'ATIVO', current_date, current_date, true, @UsuarioId, @CorrelationId
from unnest(@Modulos) as m
on conflict (tenant_id, modulo_codigo) do update set status='ATIVO', ativo=true, updated_at=now(), updated_by=@UsuarioId, correlation_id=@CorrelationId;",
            new { TenantId = tenantId, UsuarioId = usuarioId, CorrelationId = correlation, Modulos = lista.ToArray() },
            tx, cancellationToken: ct)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            @"insert into sigov.saas_assinatura_modulo (tenant_id, assinatura_id, modulo_codigo, status, habilitado, vigencia_inicio, vigencia_fim)
select @TenantId, @AssinaturaId, m, 'ATIVO', true, current_date, null
from unnest(@Modulos) as m
on conflict (tenant_id, assinatura_id, modulo_codigo) do update set status='ATIVO', habilitado=true, vigencia_fim=null;",
            new { TenantId = tenantId, AssinaturaId = assinaturaId, Modulos = lista.ToArray() },
            tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    // As duas atualizações rodam na mesma conexão/tx: Npgsql não permite dois comandos
    // simultâneos por conexão, então a execução é sequencial (mesma transação).
    private static async Task SuspenderModulosAsync(
        IDbConnection connection, IDbTransaction tx, long tenantId, long assinaturaId,
        IReadOnlyCollection<string> modulos, long usuarioId, string correlationId, CancellationToken ct)
    {
        var lista = modulos.ToArray();
        var correlation = Correlation(correlationId);
        await connection.ExecuteAsync(new CommandDefinition(
            "update sigov.tenant_modulo_contratado set status='SUSPENSO', updated_at=now(), updated_by=@UsuarioId, correlation_id=@CorrelationId " +
            "where tenant_id=@TenantId and ativo=true and modulo_codigo=any(@Modulos);",
            new { TenantId = tenantId, UsuarioId = usuarioId, CorrelationId = correlation, Modulos = lista },
            tx, cancellationToken: ct)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            "update sigov.saas_assinatura_modulo set status='SUSPENSO', habilitado=false, vigencia_fim=current_date " +
            "where tenant_id=@TenantId and assinatura_id=@AssinaturaId and modulo_codigo=any(@Modulos);",
            new { TenantId = tenantId, AssinaturaId = assinaturaId, Modulos = lista },
            tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    private static async Task<bool> BloqueioDowngradeAtivoAsync(IDbConnection connection, IDbTransaction tx, long tenantId, CancellationToken ct)
    {
        // Política parametrizada (banco como autoridade): SAAS.DOWNGRADE_BLOQUEIO_NOVAS_ALOCACOES.
        // Ausência de configuração segue o padrão declarado (true = suspender excedentes).
        var valor = await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
            @"select coalesce(v.valor, p.valor_padrao)::text
from sigov.parametro_modulo p
left join sigov.parametro_modulo_valor v on v.parametro_id=p.id and v.tenant_id=@TenantId and not v.is_deleted
where p.modulo='SAAS' and p.codigo='DOWNGRADE_BLOQUEIO_NOVAS_ALOCACOES' and p.ativo and not p.is_deleted
limit 1;",
            new { TenantId = tenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return valor is null || string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Task RegistrarHistoricoAsync(
        IDbConnection connection, IDbTransaction tx, long tenantId, long assinaturaId, long? planoAnteriorId, long? planoNovoId,
        string acao, string? motivo, long usuarioId, string correlationId, CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(
            @"insert into sigov.saas_assinatura_historico (assinatura_id, tenant_id, plano_anterior_id, plano_novo_id, acao, motivo, usuario_id, correlation_id)
values (@AssinaturaId, @TenantId, @PlanoAnteriorId, @PlanoNovoId, @Acao, @Motivo, @UsuarioId, @CorrelationId);",
            new
            {
                AssinaturaId = assinaturaId,
                TenantId = tenantId,
                PlanoAnteriorId = planoAnteriorId,
                PlanoNovoId = planoNovoId,
                Acao = acao,
                Motivo = motivo,
                UsuarioId = usuarioId,
                CorrelationId = Correlation(correlationId)
            }, tx, cancellationToken: ct));

    private static Task RegistrarEventoAsync(
        IDbConnection connection, IDbTransaction tx, long tenantId, long assinaturaId, string tipoEvento, object payload, string correlationId, CancellationToken ct = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            @"insert into sigov.saas_evento (tenant_id, tipo_evento, origem, origem_id, payload, correlation_id)
values (@TenantId, @TipoEvento, 'SAAS_ADM', @AssinaturaId, @Payload::jsonb, @CorrelationId);",
            new { TenantId = tenantId, TipoEvento = tipoEvento, AssinaturaId = assinaturaId, Payload = JsonSerializer.Serialize(payload), CorrelationId = Guid.TryParse(correlationId, out var g) ? g : Guid.NewGuid() },
            tx, cancellationToken: ct));

    private static Guid Correlation(string? correlationId) => Guid.TryParse(correlationId, out var g) ? g : Guid.NewGuid();
}
