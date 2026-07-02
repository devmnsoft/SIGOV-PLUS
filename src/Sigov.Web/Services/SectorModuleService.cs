using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Models.Operational;

namespace Sigov.Web.Services;

public sealed class SectorModuleService
{
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly IAuditTrailService _auditTrail;
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<SectorModuleService> _logger;

    private static readonly IReadOnlyDictionary<string, string[]> ModuleTables = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Educação"] = new[] { "educacao_aluno", "educacao_escola", "educacao_turma", "educacao_matricula", "educacao_frequencia", "educacao_boletim", "educacao_avaliacao", "educacao_transporte_rota", "educacao_merenda_cardapio", "educacao_biblioteca_livro" },
        ["Saúde"] = new[] { "saude_paciente", "saude_unidade", "saude_atendimento", "saude_agenda", "saude_procedimento" },
        ["ACS"] = new[] { "saude_acs", "saude_visita_domiciliar", "saude_familia", "saude_domicilio" },
        ["Saneamento"] = new[] { "saneamento_consumidor", "saneamento_ligacao", "saneamento_hidrometro", "saneamento_leitura", "saneamento_fatura", "saneamento_ordem_servico", "saneamento_rede_gis" },
        ["Social"] = new[] { "social_familia", "social_pessoa", "social_atendimento", "social_beneficio", "social_visita" },
        ["Agro"] = new[] { "agro_produtor", "agro_propriedade", "agro_programa", "agro_servico" },
        ["Portal do Cidadão"] = new[] { "portal_servico", "portal_solicitacao", "ouvidoria_manifestacao" },
        ["Portal do Contribuinte"] = new[] { "contribuinte", "debito", "guia", "protocolo" },
        ["Ouvidoria"] = new[] { "ouvidoria_manifestacao", "portal_solicitacao", "protocolo", "atendimento" },
        ["Atendimento"] = new[] { "atendimento", "portal_solicitacao", "protocolo" },
        ["Mobile/Campo"] = new[] { "campo_roteiro", "campo_coleta", "campo_evidencia" },
        ["GIS"] = new[] { "gis_camada", "gis_geometria" },
        ["BI Setorial"] = new[] { "educacao_aluno", "saude_paciente", "saneamento_consumidor", "social_familia", "agro_produtor" }
    };

    public SectorModuleService(IDatabaseSchemaInspector schemaInspector, IAuditTrailService auditTrail, NpgsqlConnectionFactory connectionFactory, ILogger<SectorModuleService> logger)
    { _schemaInspector = schemaInspector; _auditTrail = auditTrail; _connectionFactory = connectionFactory; _logger = logger; }

    public async Task<SectorModuleViewModel> BuildAsync(string modulo, string titulo, string descricao, string[] kpis, string[] rotas, bool sensivel, string? filtro, CancellationToken cancellationToken)
    {
        try
        {
            var tables = ModuleTables.TryGetValue(modulo, out var configured) ? configured : Array.Empty<string>();
            var detected = new List<string>();
            foreach (var table in tables)
                if (await _schemaInspector.TableExistsAsync("sigov", table, cancellationToken).ConfigureAwait(false)) detected.Add($"sigov.{table}");

            var hasReal = detected.Count > 0;
            return new SectorModuleViewModel
            {
                Modulo = modulo,
                Titulo = titulo,
                Descricao = descricao,
                Status = hasReal ? (detected.Count == tables.Length ? "Funcional" : "Parcial") : "Em implantação",
                UsaDadosReais = hasReal,
                UsaFallback = !hasReal,
                ContemDadosSensiveis = sensivel,
                TabelasDetectadas = detected,
                Kpis = await BuildKpisAsync(kpis, tables, cancellationToken).ConfigureAwait(false),
                Registros = BuildFallbackRecords(modulo, filtro, hasReal),
                Rotas = rotas,
                Filtro = filtro,
                Pendencias = hasReal ? new[] { "Validar colunas obrigatórias antes de liberar gravações amplas.", "Conectar eventos ao Workflow/Tarefas conforme regras de negócio." } : new[] { "Criar schema físico não destrutivo para ativar dados reais.", "Manter fallback honesto sem simular gravação/sincronização." },
                AcaoPrincipalTexto = "Novo registro",
                AcaoPrincipalUrl = "#cadastro-setorial"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao montar módulo setorial {Modulo}", modulo);
            return new SectorModuleViewModel { Modulo = modulo, Titulo = titulo, Descricao = descricao, Status = "Indisponível", UsaFallback = true, ContemDadosSensiveis = sensivel, Pendencias = new[] { "Falha controlada: consulte logs; nenhum stacktrace foi exibido ao usuário." } };
        }
    }

    private async Task<IReadOnlyList<SectorKpiViewModel>> BuildKpisAsync(string[] titles, string[] tables, CancellationToken cancellationToken)
    {
        var list = new List<SectorKpiViewModel>();
        for (var i = 0; i < titles.Length; i++)
        {
            var table = i < tables.Length ? tables[i] : null;
            var value = "0";
            if (!string.IsNullOrWhiteSpace(table) && await _schemaInspector.TableExistsAsync("sigov", table, cancellationToken).ConfigureAwait(false))
            {
                try { using var c = _connectionFactory.CreateConnection(); value = (await c.ExecuteScalarAsync<long>(new CommandDefinition($"select count(1) from sigov.{table}", cancellationToken: cancellationToken))).ToString(); }
                catch (Exception ex) { _logger.LogWarning(ex, "KPI setorial em fallback para {Table}", table); }
            }
            list.Add(new SectorKpiViewModel { Titulo = titles[i], Valor = value, Descricao = table is null ? "Indicador preparado" : $"Fonte prevista: sigov.{table}", Status = value == "0" ? "Em implantação" : "Funcional" });
        }
        return list;
    }

    private static IReadOnlyList<SectorRecordViewModel> BuildFallbackRecords(string modulo, string? filtro, bool hasReal) => new[]
    {
        new SectorRecordViewModel { Id = "schema", Titulo = $"{modulo}: estrutura validada", Subtitulo = hasReal ? "Há tabelas físicas detectadas; consultas podem ser ativadas por entidade." : "Nenhuma tabela física detectada para listagem real neste ambiente.", DocumentoMascarado = "***.***.***-**", Status = hasReal ? "Parcial" : "Em implantação", Origem = hasReal ? "Schema real detectado" : "Fallback honesto" },
        new SectorRecordViewModel { Id = "lgpd", Titulo = "LGPD e auditoria", Subtitulo = "Documentos e dados sensíveis são mascarados; ações críticas devem registrar auditoria.", DocumentoMascarado = "••••••", Status = "Parcial", Origem = "Governança SIGOV PLUS" }
    };

    public async Task AuditAsync(string acao, string entidade, CancellationToken cancellationToken) => await _auditTrail.RegistrarAsync(null, null, acao, entidade, null, null, new { Entidade = entidade, Acao = acao }, null, null, Guid.NewGuid().ToString(), cancellationToken).ConfigureAwait(false);
}
