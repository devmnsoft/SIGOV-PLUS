using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Editais;
using Sigov.Web.Services;

namespace Sigov.Web.Services.Editais;

public sealed class EditalPocService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schema;
    private readonly IAuditTrailService _audit;
    private readonly ILogger<EditalPocService> _logger;
    public EditalPocService(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schema, IAuditTrailService audit, ILogger<EditalPocService> logger) { _connectionFactory = connectionFactory; _schema = schema; _audit = audit; _logger = logger; }
    public async Task<bool> SchemaEditalDisponivelAsync(CancellationToken ct) => await _schema.TableExistsAsync("sigov", "edital", ct).ConfigureAwait(false);
    public async Task<IReadOnlyList<EditalResumoViewModel>> ListarEditaisAsync(CancellationToken ct)
    {
        if (!await SchemaEditalDisponivelAsync(ct).ConfigureAwait(false)) return FallbackEditais();
        try { using var cn = _connectionFactory.CreateConnection(); var rows = await cn.QueryAsync<EditalResumoViewModel>(new CommandDefinition("select id, coalesce(nome,'Edital') nome, coalesce(orgao,'') orgao, coalesce(municipio,'') municipio, coalesce(uf,'') uf, coalesce(status,'Rascunho') status, coalesce(objeto,'') objeto, data_sessao DataSessao, valor_estimado ValorEstimado, true Persistido from sigov.edital order by id desc limit 100", cancellationToken: ct)); return rows.ToList(); }
        catch(Exception ex){ _logger.LogWarning(ex,"Fallback de editais"); return FallbackEditais(); }
    }
    public async Task<EditalDetalheViewModel> ObterDetalheAsync(long editalId, CancellationToken ct)
    {
        var editais = await ListarEditaisAsync(ct); var edital = editais.FirstOrDefault(x=>x.Id==editalId) ?? editais.First();
        var requisitos = await ListarRequisitosAsync(edital.Id, ct); var evidencias = await ListarEvidenciasAsync(edital.Id, null, ct); var resumo = CalcularResumo(requisitos);
        return new EditalDetalheViewModel(edital, requisitos, evidencias, CatalogoModulos(), resumo, await SchemaEditalDisponivelAsync(ct), await SchemaEditalDisponivelAsync(ct) ? "Dados reais quando tabelas existem; campos ausentes entram em fallback seguro." : "Tabela sigov.edital não encontrada. Nenhum edital foi simulado como salvo; tela opera como guia de implantação e pré-cadastro.");
    }
    public async Task<long?> CriarEditalAsync(EditalFormViewModel form, string trace, CancellationToken ct)
    { if (!await SchemaEditalDisponivelAsync(ct)) { await Auditar("EDITAL_FALLBACK_CRIAR", "edital", null, form, trace, ct); return null; }
      using var cn = _connectionFactory.CreateConnection(); var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.edital(nome,orgao,municipio,uf,modalidade,numero,ano,objeto,data_publicacao,data_sessao,status,observacoes,link_origem,valor_estimado,responsavel_interno,tenant_id) values(@Nome,@Orgao,@Municipio,@Uf,@Modalidade,@Numero,@Ano,@Objeto,@DataPublicacao,@DataSessao,@Status,@Observacoes,@LinkOrigem,@ValorEstimado,@ResponsavelInterno,@TenantId) returning id", form, cancellationToken: ct)); await Auditar("EDITAL_CRIADO", "edital", id.ToString(), form, trace, ct); return id; }
    public async Task<IReadOnlyList<RequisitoEditalViewModel>> ListarRequisitosAsync(long editalId, CancellationToken ct)
    { if (!await _schema.TableExistsAsync("sigov","edital_requisito",ct)) return FallbackRequisitos(editalId); try { using var cn=_connectionFactory.CreateConnection(); var rows=await cn.QueryAsync<RequisitoEditalViewModel>(new CommandDefinition("select id, edital_id EditalId, coalesce(codigo,'') Codigo, coalesce(item,'') Item, coalesce(descricao,'') Descricao, coalesce(modulo_relacionado,'') Modulo, coalesce(categoria,'Geral') Categoria, coalesce(criticidade,'Média') Criticidade, coalesce(obrigatorio,false) Obrigatorio, coalesce(eliminatorio,false) Eliminatorio, coalesce(percentual,0) Percentual, coalesce(status_aderencia,'Não avaliado') StatusAderencia, coalesce(observacao_tecnica,'') ObservacaoTecnica, 0 Evidencias, coalesce(responsavel,'') Responsavel from sigov.edital_requisito where edital_id=@editalId order by ordem,id", new{editalId}, cancellationToken:ct)); return rows.ToList(); } catch(Exception ex){_logger.LogWarning(ex,"Fallback requisitos"); return FallbackRequisitos(editalId);} }
    public async Task RegistrarAcaoAsync(string acao, string entidade, string? id, object dados, string trace, CancellationToken ct) => await Auditar(acao, entidade, id, dados, trace, ct);
    public Task<IReadOnlyList<EvidenciaEditalViewModel>> ListarEvidenciasAsync(long editalId, long? requisitoId, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenciaEditalViewModel>>(new[]{ new EvidenciaEditalViewModel(1, editalId, requisitoId ?? 1, "Rota", "Login e health check", "Evidência operacional por URL/rota, pendente de validação formal.", "/Auth/Login", "/Operacao/Health", "Segurança", "Pendente", "Pré-venda", DateTime.UtcNow)});
    public IReadOnlyList<ModuloAderenciaViewModel> CatalogoModulos() => new[]{"SaaS/Admin","Segurança","Pessoa/Endereço","RH","Protocolo","GED","Tributário","Contratos","Jurídico","Financeiro","Patrimônio","Obras","Portal","Transparência","Ouvidoria","Saúde","Educação","Saneamento","Social","Agro","BI","API","Integrações","Mobile/Campo"}.Select(x=>new ModuloAderenciaViewModel(x,"/"+x.Split('/')[0].Replace("ç","c"),"Operacional/parcial","Requer vínculo por evidência validada","docs/matriz-aderencia-poc-editais-sigov.md","Não declarar atendimento sem evidência","Evoluir persistência e automações por tabela real")).ToList();
    public MatrizResumoViewModel CalcularResumo(IReadOnlyList<RequisitoEditalViewModel> req){ var total=req.Count; var atende=req.Count(x=>x.StatusAderencia=="Atende" && x.Evidencias>0); var nao=req.Count(x=>x.StatusAderencia=="Não atende"); var parcial=req.Count(x=>x.StatusAderencia=="Parcial"); var imp=req.Count(x=>x.StatusAderencia=="Em implantação"); var crit=req.Count(x=>x.Criticidade.Contains("Cr",StringComparison.OrdinalIgnoreCase)||x.Eliminatorio); var critNao=req.Count(x=>(x.Criticidade.Contains("Cr",StringComparison.OrdinalIgnoreCase)||x.Eliminatorio)&&x.StatusAderencia!="Atende"); return new(total,atende,nao,parcial,imp,crit,critNao,total==0?0:Math.Round(100m*atende/total,2)); }
    private async Task Auditar(string acao,string entidade,string? id,object dados,string trace,CancellationToken ct)=> await _audit.RegistrarAsync(null,null,acao,entidade,id,null,dados,null,null,trace,ct);
    private static IReadOnlyList<EditalResumoViewModel> FallbackEditais()=>new[]{new EditalResumoViewModel(1,"Modelo de organização de edital (não persistido)","Órgão a definir","Município","UF","Rascunho","Fallback honesto para cadastro após criação das tabelas sigov.edital.",null,null,false)};
    private static IReadOnlyList<RequisitoEditalViewModel> FallbackRequisitos(long editalId)=>new[]{new RequisitoEditalViewModel(1,editalId,"TR-001","1.1","Sistema web integrado, SaaS e multi-tenant com auditoria e LGPD.","SaaS/Admin","SaaS","Crítica",true,true,0,"Não avaliado","Não marcar Atende sem evidência validada.",0,"Pré-venda"),new RequisitoEditalViewModel(2,editalId,"TR-002","2.1","Protocolo, GED, Tributário, Portal e Transparência integrados.","Protocolo","Geral","Alta",true,false,0,"Em implantação","Exige evidências por rota, relatório ou documento.",0,"Pré-venda")};
}
