using System.Data;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Sigov.Application.Frotas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Frotas;

public sealed class FrotasService(NpgsqlConnectionFactory factory, ILogger<FrotasService> logger) : IFrotasService
{
    public async Task<FrotasDashboardDto> DashboardAsync(long t, long e, CancellationToken c)
    {
        const string sql = """
            select
                count(*) filter (where status = 'ATIVO') as VeiculosAtivos,
                count(*) filter (where status = 'EM_MANUTENCAO') as VeiculosEmManutencao,
                count(*) filter (where status in ('INATIVO', 'BAIXADO')) as VeiculosIndisponiveis
            from sigov.frotas_veiculo
            where tenant_id = @t and entidade_id = @e;

            select
                count(*) filter (where validade_cnh < current_date) as CnhVencida,
                count(*) filter (where validade_cnh between current_date and current_date + 30) as CnhAVencer
            from sigov.frotas_motorista
            where tenant_id = @t and entidade_id = @e and status = 'ATIVO';

            select
                count(*) filter (where data_vencimento < current_date and status = 'VIGENTE') as DocumentosVencidos,
                count(*) filter (where data_vencimento between current_date and current_date + 30 and status = 'VIGENTE') as DocumentosAVencer
            from sigov.frotas_documento
            where tenant_id = @t and entidade_id = @e;

            select
                count(*) as AbastecimentosMes,
                coalesce(sum(valor_total), 0) as CustoAbastecimento
            from sigov.frotas_abastecimento
            where tenant_id = @t and entidade_id = @e and data_abastecimento >= date_trunc('month', current_date);

            select
                count(*) filter (where status in ('ABERTA', 'EM_EXECUCAO')) as ManutencoesAbertas,
                coalesce(sum(coalesce(valor_final, valor_estimado)), 0) as CustoManutencao
            from sigov.frotas_manutencao
            where tenant_id = @t and entidade_id = @e and (status in ('ABERTA', 'EM_EXECUCAO') or data_conclusao >= date_trunc('month', current_date));

            select count(*) as UsosAbertos
            from sigov.frotas_utilizacao
            where tenant_id = @t and entidade_id = @e and status = 'ABERTO';

            select count(*) as OrdensAbertas
            from sigov.frotas_ordem_servico
            where tenant_id = @t and entidade_id = @e and status in ('ABERTA', 'APROVADA', 'EM_EXECUCAO');
            """;

        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(Cmd(sql, new { t, e }, c));
        dynamic v = await m.ReadSingleAsync();
        dynamic d = await m.ReadSingleAsync();
        dynamic doc = await m.ReadSingleAsync();
        dynamic a = await m.ReadSingleAsync();
        dynamic mt = await m.ReadSingleAsync();
        dynamic u = await m.ReadSingleAsync();
        dynamic o = await m.ReadSingleAsync();

        var ultimasUtil = (await UtilizacoesAsync(t, e, new(Tamanho: 5), c)).Take(5).ToList();
        var ultimasOrd = (await OrdensAsync(t, e, new(Tamanho: 5), c)).Take(5).ToList();

        return new FrotasDashboardDto(
            (long)v.veiculosativos,
            (long)v.veiculosemmanutencao,
            (long)d.cnhvencida,
            (long)d.cnhavencer,
            (long)doc.documentosvencidos,
            (long)doc.documentosavencer,
            (long)a.abastecimentosmes,
            (decimal)a.custoabastecimento + (decimal)mt.customanutencao,
            (long)mt.manutencoesabertas,
            ultimasUtil,
            ultimasOrd,
            (long)v.veiculosindisponiveis,
            (long)u.usosabertos,
            (long)o.ordensabertas
        );
    }

    public Task<IReadOnlyList<VeiculoDto>> VeiculosAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<VeiculoDto>("""
            select
                v.id, v.placa, v.marca, v.modelo, v.tipo,
                v.combustivel_principal as CombustivelPrincipal,
                v.quilometragem_atual as QuilometragemAtual,
                v.horimetro_atual as HorimetroAtual,
                v.status, v.unidade_responsavel as UnidadeResponsavel,
                v.bem_patrimonial_id as BemPatrimonialId,
                v.renavam, v.chassi,
                v.ano_fabricacao as AnoFabricacao,
                v.ano_modelo as AnoModelo,
                v.observacoes,
                b.descricao as BemPatrimonialDescricao,
                b.numero_patrimonio as BemPatrimonialNumero
            from sigov.frotas_veiculo v
            left join sigov.patrimonio_bem b on b.id = v.bem_patrimonial_id and b.tenant_id = v.tenant_id and b.entidade_id = v.entidade_id
            where v.tenant_id = @t and v.entidade_id = @e
              and (@status is null or v.status = @status)
              and (@busca is null or v.placa ilike '%' || @busca || '%' or v.modelo ilike '%' || @busca || '%' or v.marca ilike '%' || @busca || '%')
            order by v.placa
            limit @lim
            """, t, e, f, c);

    public async Task<VeiculoDto?> VeiculoAsync(long t, long e, long id, CancellationToken c) =>
        (await List<VeiculoDto>("""
            select
                v.id, v.placa, v.marca, v.modelo, v.tipo,
                v.combustivel_principal as CombustivelPrincipal,
                v.quilometragem_atual as QuilometragemAtual,
                v.horimetro_atual as HorimetroAtual,
                v.status, v.unidade_responsavel as UnidadeResponsavel,
                v.bem_patrimonial_id as BemPatrimonialId,
                v.renavam, v.chassi,
                v.ano_fabricacao as AnoFabricacao,
                v.ano_modelo as AnoModelo,
                v.observacoes,
                b.descricao as BemPatrimonialDescricao,
                b.numero_patrimonio as BemPatrimonialNumero
            from sigov.frotas_veiculo v
            left join sigov.patrimonio_bem b on b.id = v.bem_patrimonial_id and b.tenant_id = v.tenant_id and b.entidade_id = v.entidade_id
            where v.tenant_id = @t and v.entidade_id = @e and v.id = @id
            """, new { t, e, id }, c)).SingleOrDefault();

    public async Task<long> CriarVeiculoAsync(long t, long u, string corr, VeiculoInput i, CancellationToken c)
    {
        ValidateVehicle(i);
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(c);

        if (i.BemPatrimonialId.HasValue)
            await ValidarVinculoBemPatrimonialAsync(cn, tx, t, i.EntidadeId, null, i.BemPatrimonialId.Value, c);

        const string q = """
            insert into sigov.frotas_veiculo(
                tenant_id, entidade_id, placa, renavam, chassi, marca, modelo,
                ano_fabricacao, ano_modelo, tipo, combustivel_principal,
                quilometragem_atual, horimetro_atual, status, unidade_responsavel,
                bem_patrimonial_id, observacoes, created_by
            ) values (
                @t, @EntidadeId, upper(trim(@Placa)), @Renavam, @Chassi, @Marca, @Modelo,
                @AnoFabricacao, @AnoModelo, @Tipo, @CombustivelPrincipal,
                @QuilometragemAtual, @HorimetroAtual, @Status, @UnidadeResponsavel,
                @BemPatrimonialId, @Observacoes, @u
            ) returning id
            """;

        var id = await cn.ExecuteScalarAsync<long>(Cmd(q, Merge(i, new { t, u }), tx, c));
        await Audit(cn, tx, t, i.EntidadeId, "VEICULO", id, "CRIAR", null, i, u, corr, c);
        await tx.CommitAsync(c);
        return id;
    }

    public async Task EditarVeiculoAsync(long t, long u, string corr, long id, VeiculoInput i, CancellationToken c)
    {
        ValidateVehicle(i);
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(c);

        var before = await One(cn, tx, "select * from sigov.frotas_veiculo where id=@id and tenant_id=@t and entidade_id=@e for update", new { id, t, e = i.EntidadeId }, c);
        if (before is null) throw new KeyNotFoundException("Veículo não encontrado.");
        if (i.QuilometragemAtual < (decimal)before.quilometragem_atual)
            throw new InvalidOperationException("Quilometragem regressiva não é permitida.");

        if (i.BemPatrimonialId.HasValue)
            await ValidarVinculoBemPatrimonialAsync(cn, tx, t, i.EntidadeId, id, i.BemPatrimonialId.Value, c);

        const string q = """
            update sigov.frotas_veiculo set
                placa = upper(trim(@Placa)), renavam = @Renavam, chassi = @Chassi,
                marca = @Marca, modelo = @Modelo, ano_fabricacao = @AnoFabricacao,
                ano_modelo = @AnoModelo, tipo = @Tipo, combustivel_principal = @CombustivelPrincipal,
                quilometragem_atual = @QuilometragemAtual, horimetro_atual = @HorimetroAtual,
                status = @Status, unidade_responsavel = @UnidadeResponsavel,
                bem_patrimonial_id = @BemPatrimonialId, observacoes = @Observacoes,
                updated_at = now(), updated_by = @u
            where id = @id and tenant_id = @t and entidade_id = @EntidadeId
            """;

        await cn.ExecuteAsync(Cmd(q, Merge(i, new { t, u, id }), tx, c));
        await Audit(cn, tx, t, i.EntidadeId, "VEICULO", id, "EDITAR", before, i, u, corr, c);
        await tx.CommitAsync(c);
    }

    public Task<IReadOnlyList<MotoristaDto>> MotoristasAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<MotoristaDto>("""
            select
                id, nome,
                case when length(cpf) = 11 then left(cpf, 3) || '.***.***-' || right(cpf, 2) else '***' end as CpfMascarado,
                cnh, categoria_cnh as CategoriaCnh, validade_cnh as ValidadeCnh,
                telefone, email, vinculo_lotacao as VinculoLotacao, status, observacoes
            from sigov.frotas_motorista
            where tenant_id = @t and entidade_id = @e
              and (@status is null or status = @status)
              and (@busca is null or nome ilike '%' || @busca || '%' or cnh ilike '%' || @busca || '%')
            order by nome
            limit @lim
            """, t, e, f, c);

    public Task<long> CriarMotoristaAsync(long t, long u, string corr, MotoristaInput i, CancellationToken c)
    {
        var cpf = Digits(i.Cpf);
        if (cpf.Length != 11 || string.IsNullOrWhiteSpace(i.Nome) || string.IsNullOrWhiteSpace(i.Cnh))
            throw new ArgumentException("Nome, CPF válido (11 dígitos) e CNH são obrigatórios.");

        const string q = """
            insert into sigov.frotas_motorista(
                tenant_id, entidade_id, nome, cpf, cnh, categoria_cnh, validade_cnh,
                telefone, email, vinculo_lotacao, status, observacoes, created_by
            ) values (
                @t, @EntidadeId, @Nome, @cpf, @Cnh, @CategoriaCnh, @ValidadeCnh,
                @Telefone, @Email, @VinculoLotacao, @Status, @Observacoes, @u
            ) returning id
            """;

        return Insert(q, t, u, corr, Merge(i, new { cpf }), "MOTORISTA", c);
    }

    public Task<IReadOnlyList<UtilizacaoDto>> UtilizacoesAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<UtilizacaoDto>("""
            select
                x.id, x.veiculo_id as VeiculoId, v.placa,
                x.motorista_id as MotoristaId, m.nome as MotoristaNome,
                x.saida_em as SaidaEm, x.retorno_em as RetornoEm,
                x.km_saida as KmSaida, x.km_retorno as KmRetorno,
                x.destino, x.finalidade, x.status
            from sigov.frotas_utilizacao x
            join sigov.frotas_veiculo v on v.id = x.veiculo_id
            join sigov.frotas_motorista m on m.id = x.motorista_id
            where x.tenant_id = @t and x.entidade_id = @e
              and (@status is null or x.status = @status)
              and (@veiculoId is null or x.veiculo_id = @veiculoId)
              and (@motoristaId is null or x.motorista_id = @motoristaId)
            order by x.saida_em desc
            limit @lim
            """, new { t, e, status = f.Status, veiculoId = f.VeiculoId, motoristaId = f.MotoristaId, lim = Math.Clamp(f.Tamanho, 1, 500) }, c);

    public async Task<long> CriarUtilizacaoAsync(long t, long u, string corr, UtilizacaoInput i, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(IsolationLevel.Serializable, c);

        var v = await One(cn, tx, "select * from sigov.frotas_veiculo where id=@id and tenant_id=@t and entidade_id=@e for update", new { id = i.VeiculoId, t, e = i.EntidadeId }, c)
            ?? throw new KeyNotFoundException("Veículo não encontrado.");

        var vStatus = (string)v.status;
        if (vStatus == "EM_MANUTENCAO")
            throw new InvalidOperationException("Veículo em manutenção não pode iniciar utilização.");
        if (vStatus is "INATIVO" or "BAIXADO")
            throw new InvalidOperationException("Veículo indisponível ou baixado.");
        if (vStatus != "ATIVO")
            throw new InvalidOperationException($"Veículo com status '{vStatus}' não está apto para utilização.");

        if (i.KmSaida < (decimal)v.quilometragem_atual)
            throw new InvalidOperationException($"Quilometragem de saída ({i.KmSaida}) não pode ser inferior à quilometragem atual do veículo ({(decimal)v.quilometragem_atual}).");

        var usoAberto = await One(cn, tx, "select id from sigov.frotas_utilizacao where tenant_id=@t and entidade_id=@e and veiculo_id=@vid and status='ABERTO'", new { t, e = i.EntidadeId, vid = i.VeiculoId }, c);
        if (usoAberto is not null)
            throw new InvalidOperationException("Este veículo já possui uma utilização em aberto. Conclua a utilização anterior antes de iniciar uma nova.");

        var m = await One(cn, tx, "select * from sigov.frotas_motorista where id=@id and tenant_id=@t and entidade_id=@e", new { id = i.MotoristaId, t, e = i.EntidadeId }, c)
            ?? throw new KeyNotFoundException("Motorista não encontrado.");

        if ((string)m.status != "ATIVO")
            throw new InvalidOperationException("Motorista suspenso ou inativo.");

        var validadeCnh = ((DateTime)m.validade_cnh).Date;
        if (validadeCnh < DateTime.UtcNow.Date)
            throw new InvalidOperationException($"Motorista com CNH vencida em {validadeCnh:dd/MM/yyyy}. Utilização bloqueada.");

        const string q = """
            insert into sigov.frotas_utilizacao(
                tenant_id, entidade_id, veiculo_id, motorista_id, unidade_solicitante,
                saida_em, km_saida, destino, finalidade, observacao, created_by
            ) values (
                @t, @EntidadeId, @VeiculoId, @MotoristaId, @UnidadeSolicitante,
                @SaidaEm, @KmSaida, @Destino, @Finalidade, @Observacao, @u
            ) returning id
            """;

        var id = await cn.ExecuteScalarAsync<long>(Cmd(q, Merge(i, new { t, u }), tx, c));
        await Audit(cn, tx, t, i.EntidadeId, "UTILIZACAO", id, "CRIAR", null, i, u, corr, c);
        await tx.CommitAsync(c);
        return id;
    }

    public async Task FinalizarUtilizacaoAsync(long t, long e, long u, string corr, long id, decimal kmRetorno, DateTimeOffset retornoEm, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(c);

        var x = await One(cn, tx, "select * from sigov.frotas_utilizacao where id=@id and tenant_id=@t and entidade_id=@e for update", new { id, t, e }, c)
            ?? throw new KeyNotFoundException("Utilização não encontrada.");

        if ((string)x.status != "ABERTO")
            throw new InvalidOperationException("Esta utilização já foi finalizada ou cancelada.");

        var kmSaida = (decimal)x.km_saida;
        if (kmRetorno < kmSaida)
            throw new InvalidOperationException($"Quilometragem de retorno ({kmRetorno}) não pode ser menor que a de saída ({kmSaida}).");

        var saidaEm = (DateTimeOffset)x.saida_em;
        if (retornoEm < saidaEm)
            throw new InvalidOperationException("Data/hora de retorno não pode ser anterior à data/hora de saída.");

        const string q = """
            update sigov.frotas_utilizacao set
                status = 'FINALIZADO',
                km_retorno = @kmRetorno,
                retorno_em = @retornoEm,
                updated_at = now(),
                updated_by = @u
            where id = @id and tenant_id = @t and entidade_id = @e;

            update sigov.frotas_veiculo set
                quilometragem_atual = greatest(quilometragem_atual, @kmRetorno),
                updated_at = now(),
                updated_by = @u
            where id = @vid and tenant_id = @t and entidade_id = @e;
            """;

        await cn.ExecuteAsync(Cmd(q, new { id, kmRetorno, retornoEm, u, vid = (long)x.veiculo_id, t, e }, tx, c));
        await Audit(cn, tx, t, e, "UTILIZACAO", id, "FINALIZAR", x, new { kmRetorno, retornoEm }, u, corr, c);
        await tx.CommitAsync(c);
    }

    public Task<IReadOnlyList<AbastecimentoDto>> AbastecimentosAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<AbastecimentoDto>("""
            select
                a.id, a.veiculo_id as VeiculoId, v.placa,
                a.data_abastecimento as DataAbastecimento,
                a.km_atual as KmAtual,
                a.tipo_combustivel as TipoCombustivel,
                a.litros, a.valor_unitario as ValorUnitario,
                a.valor_total as ValorTotal,
                a.documento_fiscal as DocumentoFiscal
            from sigov.frotas_abastecimento a
            join sigov.frotas_veiculo v on v.id = a.veiculo_id
            where a.tenant_id = @t and a.entidade_id = @e
              and (@veiculoId is null or a.veiculo_id = @veiculoId)
            order by a.data_abastecimento desc
            limit @lim
            """, new { t, e, veiculoId = f.VeiculoId, lim = Math.Clamp(f.Tamanho, 1, 500) }, c);

    public Task<long> CriarAbastecimentoAsync(long t, long u, string corr, AbastecimentoInput i, CancellationToken c)
    {
        if (i.Litros <= 0 || i.ValorUnitario < 0)
            throw new ArgumentException("Quantidade de litros deve ser maior que zero e valor unitário não pode ser negativo.");

        return VehicleWrite(t, u, corr, i.EntidadeId, i.VeiculoId, i.KmAtual, "ABASTECIMENTO", i, async (cn, tx) =>
        {
            const string q = """
                insert into sigov.frotas_abastecimento(
                    tenant_id, entidade_id, veiculo_id, motorista_id, fornecedor_id, contrato_id,
                    data_abastecimento, km_atual, tipo_combustivel, litros, valor_unitario,
                    documento_fiscal, observacao, created_by
                ) values (
                    @t, @EntidadeId, @VeiculoId, @MotoristaId, @FornecedorId, @ContratoId,
                    @DataAbastecimento, @KmAtual, @TipoCombustivel, @Litros, @ValorUnitario,
                    @DocumentoFiscal, @Observacao, @u
                ) returning id
                """;

            return await cn.ExecuteScalarAsync<long>(Cmd(q, Merge(i, new { t, u }), tx, c));
        }, c, i.Litros > 0 && i.ValorUnitario >= 0);
    }

    public Task<IReadOnlyList<ManutencaoDto>> ManutencoesAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<ManutencaoDto>("""
            select
                m.id, m.veiculo_id as VeiculoId, v.placa, m.tipo,
                m.data_abertura as DataAbertura, m.data_conclusao as DataConclusao,
                m.km_abertura as KmAbertura, m.descricao,
                m.valor_estimado as ValorEstimado, m.valor_final as ValorFinal,
                m.status, m.observacao
            from sigov.frotas_manutencao m
            join sigov.frotas_veiculo v on v.id = m.veiculo_id
            where m.tenant_id = @t and m.entidade_id = @e
              and (@status is null or m.status = @status)
              and (@veiculoId is null or m.veiculo_id = @veiculoId)
            order by m.data_abertura desc
            limit @lim
            """, new { t, e, status = f.Status, veiculoId = f.VeiculoId, lim = Math.Clamp(f.Tamanho, 1, 500) }, c);

    public async Task<long> CriarManutencaoAsync(long t, long u, string corr, ManutencaoInput i, CancellationToken c)
    {
        if (i.ValorEstimado < 0) throw new ArgumentException("Valor estimado não pode ser negativo.");

        return await VehicleWrite(t, u, corr, i.EntidadeId, i.VeiculoId, i.KmAbertura, "MANUTENCAO", i, async (cn, tx) =>
        {
            const string q = """
                insert into sigov.frotas_manutencao(
                    tenant_id, entidade_id, veiculo_id, tipo, data_abertura, km_abertura,
                    fornecedor_id, contrato_id, descricao, valor_estimado, observacao, created_by
                ) values (
                    @t, @EntidadeId, @VeiculoId, @Tipo, @DataAbertura, @KmAbertura,
                    @FornecedorId, @ContratoId, @Descricao, @ValorEstimado, @Observacao, @u
                ) returning id
                """;

            var id = await cn.ExecuteScalarAsync<long>(Cmd(q, Merge(i, new { t, u }), tx, c));
            await cn.ExecuteAsync(Cmd("update sigov.frotas_veiculo set status='EM_MANUTENCAO', updated_at=now(), updated_by=@u where id=@id and tenant_id=@t and entidade_id=@e", new { id = i.VeiculoId, t, e = i.EntidadeId, u }, tx, c));
            return id;
        }, c, i.ValorEstimado >= 0);
    }

    public async Task ConcluirManutencaoAsync(long t, long e, long u, string corr, long id, decimal valor, CancellationToken c)
    {
        if (valor < 0) throw new ArgumentException("Valor final não pode ser negativo.");

        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(c);

        var x = await One(cn, tx, "select * from sigov.frotas_manutencao where id=@id and tenant_id=@t and entidade_id=@e for update", new { id, t, e }, c)
            ?? throw new KeyNotFoundException("Manutenção não encontrada.");

        if ((string)x.status is "CONCLUIDA" or "CANCELADA")
            throw new InvalidOperationException("Esta manutenção já foi finalizada ou cancelada.");

        var vid = (long)x.veiculo_id;

        const string q = """
            update sigov.frotas_manutencao set
                status = 'CONCLUIDA', valor_final = @valor, data_conclusao = now(),
                updated_by = @u, updated_at = now()
            where id = @id and tenant_id = @t and entidade_id = @e;

            insert into sigov.frotas_manutencao_historico(
                tenant_id, entidade_id, manutencao_id, status_anterior, status_novo, usuario_id, correlation_id
            ) values (
                @t, @e, @id, @status, 'CONCLUIDA', @u, @corr
            );

            update sigov.frotas_veiculo set
                status = 'ATIVO', updated_at = now(), updated_by = @u
            where id = @vid and tenant_id = @t and entidade_id = @e
              and not exists (
                  select 1 from sigov.frotas_manutencao
                  where veiculo_id = @vid and id <> @id and tenant_id = @t and entidade_id = @e and status in ('ABERTA', 'EM_EXECUCAO')
              )
              and not exists (
                  select 1 from sigov.frotas_ordem_servico
                  where veiculo_id = @vid and tenant_id = @t and entidade_id = @e and status in ('ABERTA', 'APROVADA', 'EM_EXECUCAO')
              );
            """;

        await cn.ExecuteAsync(Cmd(q, new { id, t, e, u, valor, corr, status = (string)x.status, vid }, tx, c));
        await Audit(cn, tx, t, e, "MANUTENCAO", id, "CONCLUIR", x, new { valor }, u, corr, c);
        await tx.CommitAsync(c);
    }

    public Task<IReadOnlyList<OrdemServicoDto>> OrdensAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<OrdemServicoDto>("""
            select
                o.id, o.exercicio, o.numero, o.veiculo_id as VeiculoId, v.placa,
                o.data_abertura as DataAbertura, o.previsao_conclusao as PrevisaoConclusao,
                o.descricao, o.status,
                coalesce(sum(i.quantidade * i.valor_unitario), 0) as ValorTotal,
                count(i.id)::int as Itens
            from sigov.frotas_ordem_servico o
            join sigov.frotas_veiculo v on v.id = o.veiculo_id
            left join sigov.frotas_ordem_servico_item i on i.ordem_servico_id = o.id
            where o.tenant_id = @t and o.entidade_id = @e
              and (@status is null or o.status = @status)
              and (@veiculoId is null or o.veiculo_id = @veiculoId)
            group by o.id, v.placa
            order by o.data_abertura desc
            limit @lim
            """, new { t, e, status = f.Status, veiculoId = f.VeiculoId, lim = Math.Clamp(f.Tamanho, 1, 500) }, c);

    public async Task<OrdemServicoDto?> OrdemAsync(long t, long e, long id, CancellationToken c) =>
        (await List<OrdemServicoDto>("""
            select
                o.id, o.exercicio, o.numero, o.veiculo_id as VeiculoId, v.placa,
                o.data_abertura as DataAbertura, o.previsao_conclusao as PrevisaoConclusao,
                o.descricao, o.status,
                coalesce(sum(i.quantidade * i.valor_unitario), 0) as ValorTotal,
                count(i.id)::int as Itens
            from sigov.frotas_ordem_servico o
            join sigov.frotas_veiculo v on v.id = o.veiculo_id
            left join sigov.frotas_ordem_servico_item i on i.ordem_servico_id = o.id
            where o.tenant_id = @t and o.entidade_id = @e and o.id = @id
            group by o.id, v.placa
            """, new { t, e, id }, c)).SingleOrDefault();

    public async Task<OrdemServicoDetalheDto?> ObterOrdemDetalheAsync(long t, long e, long id, CancellationToken c)
    {
        var os = await OrdemAsync(t, e, id, c);
        if (os is null) return null;

        await using var cn = factory.CreateConnection();

        const string qHeader = """
            select
                o.fornecedor_id as FornecedorId,
                f.nome as FornecedorNome,
                o.manutencao_id as ManutencaoId,
                v.modelo as VeiculoModelo
            from sigov.frotas_ordem_servico o
            join sigov.frotas_veiculo v on v.id = o.veiculo_id
            left join sigov.compras_fornecedor f on f.id = o.fornecedor_id
            where o.id = @id and o.tenant_id = @t and o.entidade_id = @e
            """;

        dynamic? extra = await cn.QuerySingleOrDefaultAsync(Cmd(qHeader, new { id, t, e }, c));

        const string qItens = """
            select
                i.id, i.descricao, i.tipo, i.quantidade, i.valor_unitario as ValorUnitario,
                (i.quantidade * i.valor_unitario) as ValorTotal,
                i.material_id as MaterialId, m.codigo as MaterialCodigo, m.descricao as MaterialDescricao,
                i.almoxarifado_id as AlmoxarifadoId, l.nome as AlmoxarifadoNome,
                i.contrato_id as ContratoId,
                i.movimentacao_almoxarifado_id as MovimentacaoAlmoxarifadoId,
                coalesce(s.quantidade, 0) as SaldoEstoqueDisponivel
            from sigov.frotas_ordem_servico_item i
            left join sigov.almoxarifado_material m on m.id = i.material_id and m.tenant_id = i.tenant_id and m.entidade_id = i.entidade_id
            left join sigov.almoxarifado_local l on l.id = i.almoxarifado_id and l.tenant_id = i.tenant_id and l.entidade_id = i.entidade_id
            left join sigov.almoxarifado_estoque s on s.material_id = i.material_id and s.almoxarifado_id = i.almoxarifado_id and s.tenant_id = i.tenant_id and s.entidade_id = i.entidade_id
            where i.ordem_servico_id = @id and i.tenant_id = @t and i.entidade_id = @e
            order by i.id
            """;

        var rawItens = (await cn.QueryAsync<dynamic>(Cmd(qItens, new { id, t, e }, c))).AsList();

        var itens = new List<OrdemItemDetalheDto>();
        bool todosComSaldo = true;
        string? motivoBloqueio = null;

        foreach (var r in rawItens)
        {
            var tipo = (string)r.tipo;
            var qtd = (decimal)r.quantidade;
            var saldo = (decimal)r.saldoestoquedisponivel;
            bool temSaldo = tipo == "SERVICO" || saldo >= qtd;

            if (!temSaldo)
            {
                todosComSaldo = false;
                motivoBloqueio ??= $"Peça '{r.descricao}' sem saldo suficiente no almoxarifado (necessário: {qtd:0.####}, disponível: {saldo:0.####}).";
            }

            itens.Add(new OrdemItemDetalheDto(
                (long)r.id,
                (string)r.descricao,
                tipo,
                qtd,
                (decimal)r.valorunitario,
                (decimal)r.valortotal,
                (long?)r.materialid,
                (string?)r.materialcodigo,
                (string?)r.materialdescricao,
                (long?)r.almoxarifadoid,
                (string?)r.almoxarifadonome,
                (long?)r.stratoid,
                (long?)r.movimentacaoalmoxarifadoid,
                saldo,
                temSaldo
            ));
        }

        return new OrdemServicoDetalheDto(
            os,
            (string?)extra?.fornecedornome,
            (long?)extra?.fornecedorid,
            (long?)extra?.manutencaoid,
            (string?)extra?.veiculomodelo,
            itens,
            todosComSaldo,
            motivoBloqueio
        );
    }

    public async Task<long> CriarOrdemAsync(long t, long u, string corr, OrdemServicoInput i, CancellationToken c)
    {
        if (i.Itens.Count == 0 || i.Itens.Any(x => x.Quantidade <= 0 || x.ValorUnitario < 0 || (x.Tipo != "SERVICO" && x.MaterialId is null)))
            throw new ArgumentException("A OS exige itens válidos. Peças e materiais exigem vínculo com material ativo do almoxarifado.");

        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(c);

        var v = await One(cn, tx, "select * from sigov.frotas_veiculo where id=@id and tenant_id=@t and entidade_id=@e", new { id = i.VeiculoId, t, e = i.EntidadeId }, c)
            ?? throw new KeyNotFoundException("Veículo não encontrado.");

        if ((string)v.status is "INATIVO" or "BAIXADO")
            throw new InvalidOperationException("Veículo indisponível ou baixado para abertura de nova OS.");

        if (i.FornecedorId.HasValue)
        {
            var fornAtivo = await cn.ExecuteScalarAsync<bool>(Cmd(
                "select exists(select 1 from sigov.compras_fornecedor where id=@id and tenant_id=@t and entidade_id=@e and status='ATIVO')",
                new { id = i.FornecedorId, t, e = i.EntidadeId }, tx, c));
            if (!fornAtivo)
                throw new InvalidOperationException("Fornecedor selecionado inexistente, suspenso ou inativo.");
        }

        // Validação de peças: devem ser de CONSUMO
        foreach (var it in i.Itens.Where(x => x.Tipo != "SERVICO"))
        {
            var mat = await One(cn, tx, "select tipo_material, ativo from sigov.almoxarifado_material where id=@m and tenant_id=@t and entidade_id=@e", new { m = it.MaterialId, t, e = i.EntidadeId }, c);
            if (mat is null || !(bool)mat.ativo)
                throw new InvalidOperationException($"Material {it.MaterialId} inativo ou inexistente no Almoxarifado.");
            if ((string)mat.tipo_material != "CONSUMO")
                throw new InvalidOperationException("Peças para manutenção de frota devem ser exclusivamente materiais de CONSUMO. Materiais permanentes não são elegíveis como peças de OS.");
        }

        const string q = """
            insert into sigov.frotas_ordem_servico(
                tenant_id, entidade_id, exercicio, numero, veiculo_id, manutencao_id,
                fornecedor_id, data_abertura, previsao_conclusao, descricao, created_by
            ) values (
                @t, @EntidadeId, @Exercicio, @Numero, @VeiculoId, @ManutencaoId,
                @FornecedorId, @DataAbertura, @PrevisaoConclusao, @Descricao, @u
            ) returning id
            """;

        var id = await cn.ExecuteScalarAsync<long>(Cmd(q, Merge(i, new { t, u }), tx, c));

        foreach (var x in i.Itens)
        {
            const string qItem = """
                insert into sigov.frotas_ordem_servico_item(
                    tenant_id, entidade_id, ordem_servico_id, descricao, tipo,
                    quantidade, valor_unitario, material_id, almoxarifado_id, contrato_id
                ) values (
                    @t, @e, @id, @Descricao, @Tipo,
                    @Quantidade, @ValorUnitario, @MaterialId, @AlmoxarifadoId, @ContratoId
                )
                """;
            await cn.ExecuteAsync(Cmd(qItem, Merge(x, new { t, e = i.EntidadeId, id }), tx, c));
        }

        await Audit(cn, tx, t, i.EntidadeId, "ORDEM_SERVICO", id, "CRIAR", null, i, u, corr, c);
        await tx.CommitAsync(c);
        return id;
    }

    public async Task AlterarOrdemAsync(long t, long e, long u, string corr, long id, string acao, string? justificativa, CancellationToken c)
    {
        var acaoNorm = acao?.Trim().ToLowerInvariant();
        var novo = acaoNorm switch
        {
            "aprovar" => "APROVADA",
            "cancelar" => "CANCELADA",
            "concluir" => "CONCLUIDA",
            _ => throw new ArgumentException($"Ação '{acao}' inválida. Use aprovar, concluir ou cancelar.")
        };

        if (novo == "CANCELADA" && string.IsNullOrWhiteSpace(justificativa))
            throw new ArgumentException("Justificativa formal é obrigatória para cancelamento de Ordem de Serviço.");

        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(IsolationLevel.Serializable, c);

        var os = await One(cn, tx, "select * from sigov.frotas_ordem_servico where id=@id and tenant_id=@t and entidade_id=@e for update", new { id, t, e }, c)
            ?? throw new KeyNotFoundException("Ordem de Serviço não encontrada.");

        var statusAtual = (string)os.status;
        if (statusAtual == "CANCELADA")
            throw new InvalidOperationException("Esta Ordem de Serviço já está cancelada.");
        if (statusAtual == "CONCLUIDA")
            throw new InvalidOperationException("Esta Ordem de Serviço já está concluída.");

        if (novo == "APROVADA" && statusAtual != "ABERTA")
            throw new InvalidOperationException($"Apenas OS com status 'ABERTA' pode ser aprovada. Status atual: '{statusAtual}'.");

        if (novo == "CONCLUIDA" && statusAtual is not ("APROVADA" or "EM_EXECUCAO"))
            throw new InvalidOperationException($"Apenas OS 'APROVADA' ou 'EM_EXECUCAO' pode ser concluída. Status atual: '{statusAtual}'.");

        if (novo == "CONCLUIDA")
        {
            var items = (await cn.QueryAsync<dynamic>(Cmd(
                "select * from sigov.frotas_ordem_servico_item where ordem_servico_id=@id for update",
                new { id }, tx, c))).AsList();

            if (items.Count == 0)
                throw new InvalidOperationException("OS sem itens não pode ser concluída.");

            foreach (var x in items.Where(x => (string)x.tipo != "SERVICO"))
            {
                if (x.material_id is null || x.almoxarifado_id is null)
                    throw new InvalidOperationException("Peça/material sem almoxarifado de origem definido.");

                var matId = (long)x.material_id;
                var almoxId = (long)x.almoxarifado_id;
                var qtd = (decimal)x.quantidade;

                var mat = await One(cn, tx, "select tipo_material, ativo from sigov.almoxarifado_material where id=@m and tenant_id=@t and entidade_id=@e", new { m = matId, t, e }, c);
                if (mat is null || !(bool)mat.ativo)
                    throw new InvalidOperationException($"Material {matId} inativo ou inexistente.");
                if ((string)mat.tipo_material != "CONSUMO")
                    throw new InvalidOperationException($"Material {matId} não é de CONSUMO. Peças de frota devem ser itens de consumo.");

                var estoque = await One(cn, tx, """
                    select id, quantidade from sigov.almoxarifado_estoque
                    where tenant_id=@t and entidade_id=@e and material_id=@m and almoxarifado_id=@a
                    for update
                    """, new { t, e, m = matId, a = almoxId }, c);

                if (estoque is null || (decimal)estoque.quantidade < qtd)
                {
                    var disponivel = estoque is null ? 0m : (decimal)estoque.quantidade;
                    throw new InvalidOperationException($"Saldo insuficiente no almoxarifado para a peça '{x.descricao}'. Necessário: {qtd:0.####}, Disponível: {disponivel:0.####}. Conclusão bloqueada.");
                }

                var antes = (decimal)estoque.quantidade;
                var depois = antes - qtd;

                await cn.ExecuteAsync(Cmd("update sigov.almoxarifado_estoque set quantidade=@depois, updated_at=now() where id=@eid", new { depois, eid = (long)estoque.id }, tx, c));

                const string qMov = """
                    insert into sigov.almoxarifado_movimentacao(
                        tenant_id, entidade_id, almoxarifado_id, material_id, tipo, motivo,
                        quantidade, valor_unitario, documento_origem, saldo_antes, saldo_depois,
                        usuario_id, correlation_id
                    ) values (
                        @t, @e, @a, @m, 'SAIDA', 'CONSUMO',
                        @q, @vu, @doc, @antes, @depois,
                        @u, @corr
                    ) returning id
                    """;

                var movId = await cn.ExecuteScalarAsync<long>(Cmd(qMov, new
                {
                    t, e, a = almoxId, m = matId, q = qtd,
                    vu = (decimal)x.valor_unitario,
                    doc = $"FROTAS-OS-{os.numero}",
                    antes, depois, u, corr
                }, tx, c));

                await cn.ExecuteAsync(Cmd("update sigov.frotas_ordem_servico_item set movimentacao_almoxarifado_id=@movId where id=@iid", new { movId, iid = (long)x.id }, tx, c));
            }
        }

        const string qUpdateOs = """
            update sigov.frotas_ordem_servico set
                status = @novo, updated_at = now(), updated_by = @u
            where id = @id and tenant_id = @t and entidade_id = @e;

            insert into sigov.frotas_ordem_servico_historico(
                tenant_id, entidade_id, ordem_servico_id, status_anterior, status_novo, justificativa, usuario_id, correlation_id
            ) values (
                @t, @e, @id, @statusAtual, @novo, @justificativa, @u, @corr
            );
            """;

        await cn.ExecuteAsync(Cmd(qUpdateOs, new { id, t, e, u, corr, novo, statusAtual, justificativa }, tx, c));

        // Se concluiu a OS, reativa o veículo caso não haja nenhuma outra manutenção ou OS aberta
        if (novo == "CONCLUIDA")
        {
            var vid = (long)os.veiculo_id;
            const string qReativa = """
                update sigov.frotas_veiculo set
                    status = 'ATIVO', updated_at = now(), updated_by = @u
                where id = @vid and tenant_id = @t and entidade_id = @e
                  and not exists (
                      select 1 from sigov.frotas_manutencao
                      where veiculo_id = @vid and tenant_id = @t and entidade_id = @e and status in ('ABERTA', 'EM_EXECUCAO')
                  )
                  and not exists (
                      select 1 from sigov.frotas_ordem_servico
                      where veiculo_id = @vid and id <> @id and tenant_id = @t and entidade_id = @e and status in ('ABERTA', 'APROVADA', 'EM_EXECUCAO')
                  );
                """;
            await cn.ExecuteAsync(Cmd(qReativa, new { vid, t, e, id, u }, tx, c));
        }

        await Audit(cn, tx, t, e, "ORDEM_SERVICO", id, novo, os, new { novo, justificativa }, u, corr, c);
        await tx.CommitAsync(c);
    }

    public Task<IReadOnlyList<DocumentoDto>> DocumentosAsync(long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<DocumentoDto>("""
            select
                d.id, d.veiculo_id as VeiculoId, v.placa, d.tipo, d.numero,
                d.data_emissao as DataEmissao, d.data_vencimento as DataVencimento,
                case when d.status = 'VIGENTE' and d.data_vencimento < current_date then 'VENCIDO' else d.status end as status,
                d.observacao
            from sigov.frotas_documento d
            join sigov.frotas_veiculo v on v.id = d.veiculo_id
            where d.tenant_id = @t and d.entidade_id = @e
              and (@veiculoId is null or d.veiculo_id = @veiculoId)
            order by d.data_vencimento
            limit @lim
            """, new { t, e, veiculoId = f.VeiculoId, lim = Math.Clamp(f.Tamanho, 1, 500) }, c);

    public Task<long> CriarDocumentoAsync(long t, long u, string corr, DocumentoInput i, CancellationToken c)
    {
        if (i.DataEmissao.HasValue && i.DataEmissao > i.DataVencimento)
            throw new ArgumentException("Data de vencimento não pode ser anterior à data de emissão.");

        const string q = """
            insert into sigov.frotas_documento(
                tenant_id, entidade_id, veiculo_id, tipo, numero, data_emissao, data_vencimento, observacao, created_by
            ) values (
                @t, @EntidadeId, @VeiculoId, @Tipo, @Numero, @DataEmissao, @DataVencimento, @Observacao, @u
            ) returning id
            """;

        return Insert(q, t, u, corr, i, "DOCUMENTO", c);
    }

    public async Task<IReadOnlyList<FrotaSelect>> ObterBensAtivosSelectAsync(long t, long e, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        const string q = """
            select id, (numero_patrimonio || ' — ' || descricao) as Texto, status as Detalhe
            from sigov.patrimonio_bem
            where tenant_id = @t and entidade_id = @e and status = 'ATIVO'
            order by numero_patrimonio
            limit 300
            """;
        return (await cn.QueryAsync<FrotaSelect>(Cmd(q, new { t, e }, c))).AsList();
    }

    public async Task<IReadOnlyList<FrotaSelect>> ObterFornecedoresAtivosSelectAsync(long t, long e, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        const string q = """
            select id, nome as Texto, tipo_pessoa as Subtexto
            from sigov.compras_fornecedor
            where tenant_id = @t and entidade_id = @e and status = 'ATIVO'
            order by nome
            limit 300
            """;
        return (await cn.QueryAsync<FrotaSelect>(Cmd(q, new { t, e }, c))).AsList();
    }

    public async Task<IReadOnlyList<FrotaSelect>> ObterContratosAtivosSelectAsync(long t, long e, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        const string q = """
            select id, (numero || ' — ' || objeto) as Texto
            from sigov.compras_contrato
            where tenant_id = @t and entidade_id = @e and status = 'VIGENTE'
            order by numero
            limit 300
            """;
        return (await cn.QueryAsync<FrotaSelect>(Cmd(q, new { t, e }, c))).AsList();
    }

    public async Task<IReadOnlyList<FrotaSelect>> ObterMateriaisConsumoSelectAsync(long t, long e, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        const string q = """
            select id, (codigo || ' — ' || descricao) as Texto, unidade_medida as Subtexto
            from sigov.almoxarifado_material
            where tenant_id = @t and entidade_id = @e and ativo = true and tipo_material = 'CONSUMO'
            order by descricao
            limit 500
            """;
        return (await cn.QueryAsync<FrotaSelect>(Cmd(q, new { t, e }, c))).AsList();
    }

    public async Task<IReadOnlyList<FrotaSelect>> ObterAlmoxarifadosSelectAsync(long t, long e, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        const string q = """
            select id, nome as Texto, codigo as Subtexto
            from sigov.almoxarifado_local
            where tenant_id = @t and entidade_id = @e and ativo = true
            order by nome
            limit 100
            """;
        return (await cn.QueryAsync<FrotaSelect>(Cmd(q, new { t, e }, c))).AsList();
    }

    public async Task<byte[]> ExportarCsvAsync(long t, long e, string tipo, long u, string corr, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        var sb = new StringBuilder();
        var tipoNorm = tipo?.Trim().ToLowerInvariant();

        switch (tipoNorm)
        {
            case "veiculos":
            {
                sb.AppendLine("Placa;Marca;Modelo;Tipo;Combustível;KM Atual;Status;Unidade Responsável;Tombamento");
                const string q = """
                    select
                        v.placa, v.marca, v.modelo, v.tipo, v.combustivel_principal,
                        v.quilometragem_atual, v.status, v.unidade_responsavel,
                        coalesce(b.numero_patrimonio, 'N/A') as tombamento
                    from sigov.frotas_veiculo v
                    left join sigov.patrimonio_bem b on b.id = v.bem_patrimonial_id and b.tenant_id = v.tenant_id
                    where v.tenant_id = @t and v.entidade_id = @e
                    order by v.placa
                    limit 5000
                    """;
                var rows = await cn.QueryAsync<dynamic>(Cmd(q, new { t, e }, c));
                foreach (var r in rows)
                {
                    sb.AppendLine($"{Sanitize(r.placa)};{Sanitize(r.marca)};{Sanitize(r.modelo)};{Sanitize(r.tipo)};{Sanitize(r.combustivel_principal)};{r.quilometragem_atual};{Sanitize(r.status)};{Sanitize(r.unidade_responsavel)};{Sanitize(r.tombamento)}");
                }
                break;
            }
            case "motoristas":
            {
                sb.AppendLine("Nome;CPF (LGPD);CNH;Categoria;Validade CNH;Status;Vínculo");
                const string q = """
                    select
                        nome,
                        case when length(cpf)=11 then left(cpf,3)||'.***.***-'||right(cpf,2) else '***' end as cpf_mascarado,
                        cnh, categoria_cnh, validade_cnh, status, vinculo_lotacao
                    from sigov.frotas_motorista
                    where tenant_id = @t and entidade_id = @e
                    order by nome
                    limit 5000
                    """;
                var rows = await cn.QueryAsync<dynamic>(Cmd(q, new { t, e }, c));
                foreach (var r in rows)
                {
                    sb.AppendLine($"{Sanitize(r.nome)};{Sanitize(r.cpf_mascarado)};{Sanitize(r.cnh)};{Sanitize(r.categoria_cnh)};{((DateTime)r.validade_cnh):dd/MM/yyyy};{Sanitize(r.status)};{Sanitize(r.vinculo_lotacao)}");
                }
                break;
            }
            case "utilizacoes":
            {
                sb.AppendLine("Placa;Motorista;Saída;Retorno;KM Saída;KM Retorno;Destino;Finalidade;Status");
                const string q = """
                    select
                        v.placa, m.nome as motorista, u.saida_em, u.retorno_em,
                        u.km_saida, u.km_retorno, u.destino, u.finalidade, u.status
                    from sigov.frotas_utilizacao u
                    join sigov.frotas_veiculo v on v.id = u.veiculo_id
                    join sigov.frotas_motorista m on m.id = u.motorista_id
                    where u.tenant_id = @t and u.entidade_id = @e
                    order by u.saida_em desc
                    limit 5000
                    """;
                var rows = await cn.QueryAsync<dynamic>(Cmd(q, new { t, e }, c));
                foreach (var r in rows)
                {
                    var saida = ((DateTimeOffset)r.saida_em).ToString("dd/MM/yyyy HH:mm");
                    var retorno = r.retorno_em is null ? "" : ((DateTimeOffset)r.retorno_em).ToString("dd/MM/yyyy HH:mm");
                    sb.AppendLine($"{Sanitize(r.placa)};{Sanitize(r.motorista)};{saida};{retorno};{r.km_saida};{r.km_retorno ?? ""};{Sanitize(r.destino)};{Sanitize(r.finalidade)};{Sanitize(r.status)}");
                }
                break;
            }
            case "abastecimentos":
            {
                sb.AppendLine("Placa;Data;KM Atual;Combustível;Litros;Valor Unitário;Valor Total;Nota Fiscal");
                const string q = """
                    select
                        v.placa, a.data_abastecimento, a.km_atual, a.tipo_combustivel,
                        a.litros, a.valor_unitario, a.valor_total, a.documento_fiscal
                    from sigov.frotas_abastecimento a
                    join sigov.frotas_veiculo v on v.id = a.veiculo_id
                    where a.tenant_id = @t and a.entidade_id = @e
                    order by a.data_abastecimento desc
                    limit 5000
                    """;
                var rows = await cn.QueryAsync<dynamic>(Cmd(q, new { t, e }, c));
                foreach (var r in rows)
                {
                    var data = ((DateTimeOffset)r.data_abastecimento).ToString("dd/MM/yyyy HH:mm");
                    sb.AppendLine($"{Sanitize(r.placa)};{data};{r.km_atual};{Sanitize(r.tipo_combustivel)};{r.litros};{r.valor_unitario};{r.valor_total};{Sanitize(r.documento_fiscal)}");
                }
                break;
            }
            case "ordens":
            {
                sb.AppendLine("Exercício;Número;Placa;Data Abertura;Previsão Conclusão;Descrição;Status");
                const string q = """
                    select
                        o.exercicio, o.numero, v.placa, o.data_abertura, o.previsao_conclusao,
                        o.descricao, o.status
                    from sigov.frotas_ordem_servico o
                    join sigov.frotas_veiculo v on v.id = o.veiculo_id
                    where o.tenant_id = @t and o.entidade_id = @e
                    order by o.data_abertura desc
                    limit 5000
                    """;
                var rows = await cn.QueryAsync<dynamic>(Cmd(q, new { t, e }, c));
                foreach (var r in rows)
                {
                    var abert = ((DateTimeOffset)r.data_abertura).ToString("dd/MM/yyyy HH:mm");
                    var prev = r.previsao_conclusao is null ? "" : ((DateTime)r.previsao_conclusao).ToString("dd/MM/yyyy");
                    sb.AppendLine($"{r.exercicio};{Sanitize(r.numero)};{Sanitize(r.placa)};{abert};{prev};{Sanitize(r.descricao)};{Sanitize(r.status)}");
                }
                break;
            }
            default:
                throw new ArgumentException($"Tipo de exportação '{tipo}' desconhecido.");
        }

        await Audit(cn, null, t, e, "FROTAS_EXPORTACAO", 0, "EXPORTAR", null, new { tipo = tipoNorm }, u, corr, c);
        return new UTF8Encoding(true).GetBytes(sb.ToString());
    }

    // ==================== MÉTODOS AUXILIARES ====================

    static async Task ValidarVinculoBemPatrimonialAsync(System.Data.Common.DbConnection cn, IDbTransaction tx, long t, long e, long? veiculoId, long bemId, CancellationToken c)
    {
        var bem = await One(cn, tx, "select status from sigov.patrimonio_bem where id=@bemId and tenant_id=@t and entidade_id=@e", new { bemId, t, e }, c);
        if (bem is null)
            throw new ArgumentException("Bem patrimonial não encontrado no mesmo tenant/entidade.");

        var statusBem = (string)bem.status;
        if (statusBem == "BAIXADO")
            throw new InvalidOperationException("Não é permitido vincular veículo a um bem patrimonial baixado.");

        var jaVinculado = await cn.ExecuteScalarAsync<bool>(Cmd("""
            select exists(
                select 1 from sigov.frotas_veiculo
                where tenant_id = @t and entidade_id = @e
                  and bem_patrimonial_id = @bemId
                  and status = 'ATIVO'
                  and (@veiculoId is null or id <> @veiculoId)
            )
            """, new { t, e, bemId, veiculoId }, tx, c));

        if (jaVinculado)
            throw new InvalidOperationException("Este bem patrimonial já está vinculado a outro veículo ativo no mesmo órgão/entidade.");
    }

    async Task<long> VehicleWrite<T>(long t, long u, string corr, long e, long vid, decimal km, string entity, T input, Func<System.Data.Common.DbConnection, IDbTransaction, Task<long>> write, CancellationToken c, bool valid = true) where T : notnull
    {
        if (!valid) throw new ArgumentException("Valores informados são inválidos.");
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(c);
        await using var tx = await cn.BeginTransactionAsync(c);

        var v = await One(cn, tx, "select * from sigov.frotas_veiculo where id=@vid and tenant_id=@t and entidade_id=@e for update", new { vid, t, e }, c)
            ?? throw new KeyNotFoundException("Veículo não encontrado.");

        if ((string)v.status is "INATIVO" or "BAIXADO")
            throw new InvalidOperationException("Veículo indisponível ou baixado.");

        if (km < (decimal)v.quilometragem_atual)
            throw new InvalidOperationException($"Quilometragem informada ({km}) não pode ser menor que a quilometragem atual do veículo ({(decimal)v.quilometragem_atual}).");

        var id = await write(cn, tx);
        await cn.ExecuteAsync(Cmd("update sigov.frotas_veiculo set quilometragem_atual=greatest(quilometragem_atual,@km), updated_at=now(), updated_by=@u where id=@vid and tenant_id=@t and entidade_id=@e", new { km, u, vid, t, e }, tx, c));
        await Audit(cn, tx, t, e, entity, id, "CRIAR", null, input, u, corr, c);
        await tx.CommitAsync(c);
        return id;
    }

    async Task<long> Insert<T>(string sql, long t, long u, string corr, T input, string entity, CancellationToken c) where T : notnull
    {
        try
        {
            await using var cn = factory.CreateConnection();
            await cn.OpenAsync(c);
            await using var tx = await cn.BeginTransactionAsync(c);

            var id = await cn.ExecuteScalarAsync<long>(Cmd(sql, Merge(input, new { t, u }), tx, c));
            var property = input.GetType().GetProperty("EntidadeId") ?? throw new InvalidOperationException($"{typeof(T).Name} não informa EntidadeId.");
            var value = property.GetValue(input) ?? throw new InvalidOperationException($"{typeof(T).Name}.EntidadeId não pode ser nulo.");
            var e = value is long entidadeId ? entidadeId : throw new InvalidOperationException($"{typeof(T).Name}.EntidadeId deve ser long.");

            await Audit(cn, tx, t, e, entity, id, "CRIAR", null, input, u, corr, c);
            await tx.CommitAsync(c);
            return id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha crítica ao persistir {Entidade}", entity);
            throw;
        }
    }

    Task<IReadOnlyList<T>> List<T>(string sql, long t, long e, FrotaFiltro f, CancellationToken c) =>
        List<T>(sql, new { t, e, status = f.Status, busca = f.Busca, lim = Math.Clamp(f.Tamanho, 1, 500) }, c);

    async Task<IReadOnlyList<T>> List<T>(string sql, object p, CancellationToken c)
    {
        await using var cn = factory.CreateConnection();
        return (await cn.QueryAsync<T>(Cmd(sql, p, c))).AsList();
    }

    static async Task<dynamic?> One(System.Data.Common.DbConnection cn, IDbTransaction tx, string sql, object p, CancellationToken c) =>
        await cn.QuerySingleOrDefaultAsync(Cmd(sql, p, tx, c));

    static Task<int> Audit(System.Data.Common.DbConnection cn, IDbTransaction? tx, long t, long e, string entity, long id, string op, object? before, object? after, long u, string corr, CancellationToken c) =>
        cn.ExecuteAsync(Cmd("""
            insert into sigov.frotas_auditoria(tenant_id, entidade_id, entidade, registro_id, operacao, antes, depois, usuario_id, correlation_id)
            values (@t, @e, @entity, @id, @op, cast(@before as jsonb), cast(@after as jsonb), @u, @corr)
            """, new
        {
            t, e, entity, id, op,
            before = before is null ? null : JsonSerializer.Serialize(before),
            after = after is null ? null : JsonSerializer.Serialize(after),
            u, corr
        }, tx, c));

    static CommandDefinition Cmd(string sql, object? p, CancellationToken c) => new(sql, p, cancellationToken: c);
    static CommandDefinition Cmd(string sql, object? p, IDbTransaction? tx, CancellationToken c) => new(sql, p, tx, cancellationToken: c);

    static object Merge(object a, object b)
    {
        var d = new Dictionary<string, object?>();
        foreach (var x in a.GetType().GetProperties()) d[x.Name] = x.GetValue(a);
        foreach (var x in b.GetType().GetProperties()) d[x.Name] = x.GetValue(b);
        return d;
    }

    static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());

    static void ValidateVehicle(VeiculoInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Placa) || string.IsNullOrWhiteSpace(i.Marca) || string.IsNullOrWhiteSpace(i.Modelo) || i.QuilometragemAtual < 0)
            throw new ArgumentException("Placa, marca, modelo e quilometragem válida (>= 0) são obrigatórios.");

        var st = i.Status?.Trim().ToUpperInvariant();
        if (st is not ("ATIVO" or "EM_MANUTENCAO" or "INATIVO" or "BAIXADO"))
            throw new ArgumentException($"Status '{i.Status}' inválido. Valores aceitos: ATIVO, EM_MANUTENCAO, INATIVO, BAIXADO.");
    }

    static string Sanitize(object? val)
    {
        if (val is null) return "";
        var str = val.ToString() ?? "";
        if (str.StartsWith('=') || str.StartsWith('+') || str.StartsWith('-') || str.StartsWith('@') || str.StartsWith('\t') || str.StartsWith('\r'))
            return "'" + str;
        return str.Replace(";", " ");
    }
}
