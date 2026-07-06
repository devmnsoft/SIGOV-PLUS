using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Services;

namespace Sigov.Web.Services.Operational;

public sealed class SiaficService : OperationalModuleDataServiceBase
{
    public SiaficService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<SiaficService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Siafic";
    protected override IReadOnlyList<string> CandidateTables => new[] { "plano_contas", "dotacao_orcamentaria", "empenho", "liquidacao", "pagamento", "receita_arrecadada", "fonte_recurso", "natureza_receita", "natureza_despesa" };
}
public sealed class PlanejamentoService : OperationalModuleDataServiceBase
{
    public PlanejamentoService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<PlanejamentoService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Planejamento";
    protected override IReadOnlyList<string> CandidateTables => new[] { "ppa", "ldo", "loa", "programa_governo", "acao_governo", "alteracao_orcamentaria" };
}
public sealed class TesourariaService : OperationalModuleDataServiceBase
{
    public TesourariaService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<TesourariaService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Tesouraria";
    protected override IReadOnlyList<string> CandidateTables => new[] { "conta_bancaria", "movimento_bancario", "conciliacao_bancaria", "pagamento", "receita_arrecadada" };
}
public sealed class ComprasService : OperationalModuleDataServiceBase
{
    public ComprasService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<ComprasService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Compras";
    protected override IReadOnlyList<string> CandidateTables => new[] { "compra_solicitacao", "compra_item", "fornecedor", "produto_servico" };
}
public sealed class LicitacoesService : OperationalModuleDataServiceBase
{
    public LicitacoesService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<LicitacoesService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Licitacoes";
    protected override IReadOnlyList<string> CandidateTables => new[] { "licitacao", "licitacao_item", "fornecedor", "compra_solicitacao" };
}
public sealed class AlmoxarifadoService : OperationalModuleDataServiceBase
{
    public AlmoxarifadoService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<AlmoxarifadoService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Almoxarifado";
    protected override IReadOnlyList<string> CandidateTables => new[] { "almoxarifado_produto", "almoxarifado_movimento", "almoxarifado_estoque", "fornecedor" };
}
public sealed class PatrimonioService : OperationalModuleDataServiceBase
{
    public PatrimonioService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<PatrimonioService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Patrimonio";
    protected override IReadOnlyList<string> CandidateTables => new[] { "patrimonio_bem", "patrimonio_grupo", "patrimonio_localizacao", "patrimonio_responsavel", "patrimonio_movimento", "patrimonio_inventario", "patrimonio_inventario_item", "patrimonio_baixa", "patrimonio_depreciacao", "rh_servidor", "rh_lotacao" };
}
public sealed class FrotasService : OperationalModuleDataServiceBase
{
    public FrotasService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<FrotasService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Frotas";
    protected override IReadOnlyList<string> CandidateTables => new[] { "frota_veiculo", "frota_abastecimento", "frota_manutencao", "frota_multa" };
}
public sealed class ObrasService : OperationalModuleDataServiceBase
{
    public ObrasService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<ObrasService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Obras";
    protected override IReadOnlyList<string> CandidateTables => new[] { "obra", "obra_contrato", "obra_medicao", "obra_diario", "obra_foto", "obra_ocorrencia", "obra_fiscalizacao", "obra_garantia", "contrato", "contrato_fiscal" };
}
public sealed class TransparenciaService : OperationalModuleDataServiceBase
{
    public TransparenciaService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<TransparenciaService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Transparencia";
    protected override IReadOnlyList<string> CandidateTables => new[] { "receita_arrecadada", "empenho", "pagamento", "contrato", "licitacao", "servidor", "obra" };
}
public sealed class InventarioService : OperationalModuleDataServiceBase
{
    public InventarioService(NpgsqlConnectionFactory c, IDatabaseSchemaInspector s, IAuditTrailService a, OperationalDemoService f, ILogger<InventarioService> l) : base(c, s, a, f, l) { }
    protected override string ModuleKey => "Inventario";
    protected override IReadOnlyList<string> CandidateTables => new[] { "patrimonio_inventario", "patrimonio_inventario_item", "patrimonio_bem", "patrimonio_localizacao", "patrimonio_responsavel" };
}
