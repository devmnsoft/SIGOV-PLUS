using Dapper;
using Sigov.Application.Agro.Dashboard;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro;

public sealed class AgroDashboardRepository : IAgroDashboardRepository
{
    private readonly DapperContext _context;

    public AgroDashboardRepository(DapperContext context) => _context = context;

    public async Task<AgroDashboardResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken)
    {
        const string sql = """
            select tenant_id as TenantId,
                   entidade_id as EntidadeId,
                   total_camadas as TotalCamadas,
                   total_feicoes as TotalFeicoes,
                   total_eventos as TotalEventos,
                   total_produtores as TotalProdutores,
                   total_propriedades as TotalPropriedades,
                   total_visitas as TotalVisitas,
                   total_servicos_maquina as TotalServicosMaquina,
                   total_pontos_criticos as TotalPontosCriticos,
                   produtores_ativos as ProdutoresAtivos,
                   area_total_mapeada as AreaTotalMapeada,
                   area_produtiva as AreaProdutiva,
                   total_talhoes as TotalTalhoes,
                   culturas_cadastradas as CulturasCadastradas,
                   safras_ativas as SafrasAtivas,
                   producao_estimada as ProducaoEstimada,
                   producao_realizada as ProducaoRealizada,
                   total_programas as TotalProgramas,
                   total_beneficios as TotalBeneficios,
                   beneficios_concedidos_mes as BeneficiosConcedidosMes,
                   total_maquinas as TotalMaquinas,
                   servicos_maquina_pendentes as ServicosMaquinaPendentes,
                   servicos_maquina_executados_mes as ServicosMaquinaExecutadosMes,
                   concessoes_solicitadas as ConcessoesSolicitadas,
                   concessoes_autorizadas as ConcessoesAutorizadas,
                   concessoes_entregues_mes as ConcessoesEntreguesMes,
                   insumos_distribuidos_mes as InsumosDistribuidosMes,
                   servicos_maquina_agendados as ServicosMaquinaAgendados,
                   horas_trabalhadas_mes as HorasTrabalhadasMes,
                   area_atendida_mes as AreaAtendidaMes,
                   alertas_conflito_agenda as AlertasConflitoAgenda,
                   total_estradas_vicinais as TotalEstradasVicinais,
                   total_extensao_km as TotalExtensaoKm,
                   pontos_criticos_abertos as PontosCriticosAbertos,
                   pontos_criticos_criticos as PontosCriticosCriticos,
                   ocorrencias_abertas as OcorrenciasAbertas,
                   manutencoes_programadas as ManutencoesProgramadas,
                   feiras_ativas as FeirasAtivas,
                   feirantes_autorizados as FeirantesAutorizados,
                   bancas_ocupadas as BancasOcupadas,
                   agroindustrias_ativas as AgroindustriasAtivas,
                   inspecoes_pendentes as InspecoesPendentes,
                   compras_agricultura_familiar_mes as ComprasAgriculturaFamiliarMes,
                   valor_comprado_mes as ValorCompradoMes,
                   alertas_autorizacao_vencida as AlertasAutorizacaoVencida,
                   alertas_inspecao_pendente as AlertasInspecaoPendente,
                   alertas_estrada_interditada as AlertasEstradaInterditada
              from sigov.vw_agro_dashboard
             where tenant_id = @TenantId
               and ((@EntidadeId is null and entidade_id is null) or entidade_id = @EntidadeId)
             limit 1;
            """;
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AgroDashboardResponse>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId }, cancellationToken: cancellationToken)).ConfigureAwait(false)
            ?? new AgroDashboardResponse(tenantId, entidadeId, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
