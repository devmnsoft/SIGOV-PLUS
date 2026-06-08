namespace Sigov.Application.Saas.B2B;

public sealed record B2BPlanoDto(long Id, string Codigo, string Nome, string Descricao, decimal ValorMensal, string PublicoAlvo, bool PermiteWhiteLabel, bool PermiteApi, long LimiteUsuarios, long LimiteMedicos, long LimiteHospitais, long LimitePlantoesMes, string SlaResumo);

public sealed record B2BPlanoComparativoDto(string Recurso, string Essencial, string Profissional, string Enterprise, string Revendedor, string Custom);

public sealed record WhiteLabelConfiguracaoDto(long TenantId, string NomePlataforma, string NomeComercial, string LogoPrincipalUrl, string LogoReduzidaUrl, string FaviconUrl, string BannerLoginUrl, string CorPrimaria, string CorSecundaria, string CorDestaque, string CorMenu, string CorFundo, string Tema, string Slogan, string TextoBoasVindas, string TextoRodape, string DominioCustomizado, string Subdominio, string EmailRemetente, bool Publicado);

public sealed record WhiteLabelAtualizarRequest(string NomePlataforma, string NomeComercial, string CorPrimaria, string CorSecundaria, string CorDestaque, string CorMenu, string CorFundo, string Tema, string Slogan, string TextoBoasVindas, string TextoRodape, string DominioCustomizado, string Subdominio, string EmailRemetente);

public sealed record SelfServiceCadastroRequest(string RazaoSocial, string NomeFantasia, string Cnpj, string ResponsavelNome, string ResponsavelEmail, string ResponsavelTelefone, string PlanoCodigo, bool AceiteTermos, bool AceiteLgpd);

public sealed record SelfServiceCadastroResult(bool Success, long? SolicitacaoId, string Message);

public sealed record DeveloperOverviewDto(string Autenticacao, IReadOnlyCollection<string> EscoposDisponiveis, IReadOnlyCollection<string> Endpoints, string RateLimitResumo, IReadOnlyCollection<string> WebhookEventos);

public sealed record ApiKeyCreateRequest(string Nome, IReadOnlyCollection<string> Escopos);

public sealed record ApiKeyCreateResult(long Id, string Nome, string Prefixo, string ChaveExibicaoUnica, IReadOnlyCollection<string> Escopos);

public sealed record AssinaturaUsoDto(long TenantId, string PlanoCodigo, long UsuariosAtivos, long MedicosAtivos, long HospitaisAtivos, long PlantoesMes, long RequisicoesApiMes, decimal ArmazenamentoGb);

public sealed record AssinaturaSolicitacaoRequest(string PlanoDestinoCodigo, string Motivo);

public sealed record ContratoSlaDto(long Id, long TenantId, string PlanoCodigo, string Status, DateTimeOffset InicioVigencia, DateTimeOffset? FimVigencia, decimal ValorMensal, decimal TaxaSetup, string UptimeContratado, string TempoRespostaSuporte, string TempoResolucaoCritico, string PropriedadeDados);

public sealed record SuporteChamadoRequest(string Titulo, string Descricao, string Prioridade, string Canal, bool Critico);

public sealed record SuporteChamadoDto(long Id, long TenantId, string Titulo, string Prioridade, string Status, DateTimeOffset CriadoEm, string SlaResumo);

public sealed record MonitoramentoB2BDto(long TenantsAtivos, long AlertasCriticos, long ErrosCriticos, long EndpointsLentos, long ChamadosCriticos, long IncidentesSla, DateTimeOffset ColetadoEm);

public sealed record GoToMarketMaterialDto(long Id, string Titulo, string Tipo, string Visibilidade, string ConteudoResumo);

public sealed record BetaFeedbackDto(long Id, long TenantId, string Titulo, string Severidade, string Status, int Satisfacao, DateTimeOffset CriadoEm);
