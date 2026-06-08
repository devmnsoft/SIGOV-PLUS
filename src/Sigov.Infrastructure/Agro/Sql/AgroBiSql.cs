namespace Sigov.Infrastructure.Agro.Sql;

public static class AgroBiSql
{
    public const string Dashboard = @"select tenant_id as TenantId, entidade_id as EntidadeId,
       total_produtores as TotalProdutores, produtores_ativos as ProdutoresAtivos,
       total_propriedades as TotalPropriedades, area_total_mapeada as AreaTotalMapeada,
       area_produtiva as AreaProdutiva, culturas_cadastradas as CulturasCadastradas,
       producao_estimada as ProducaoEstimada, producao_realizada as ProducaoRealizada,
       total_rebanhos as TotalRebanhos, total_animais as TotalAnimais,
       visitas_tecnicas_mes as VisitasTecnicasMes, beneficios_entregues_mes as BeneficiosEntreguesMes,
       servicos_maquina_executados_mes as ServicosMaquinaExecutadosMes, horas_maquinas_mes as HorasMaquinasMes,
       area_atendida_maquinas_mes as AreaAtendidaMaquinasMes, estradas_vicinais_km as EstradasVicinaisKm,
       pontos_criticos_abertos as PontosCriticosAbertos, feiras_ativas as FeirasAtivas,
       agroindustrias_ativas as AgroindustriasAtivas, compras_af_mes as ComprasAfMes
  from sigov.vw_agro_bi_resumo
 where tenant_id = @TenantId and ((@EntidadeId is null and entidade_id is null) or entidade_id = @EntidadeId)
 limit 1;
";
}
