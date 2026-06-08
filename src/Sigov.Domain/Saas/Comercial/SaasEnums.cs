namespace Sigov.Domain.Saas.Comercial;

public enum SaasPlanoTipo { Publico, Interno, Enterprise, Personalizado }
public enum SaasPeriodicidade { Mensal, Anual, Personalizada }
public enum SaasAssinaturaStatus { EmImplantacao, Ativa, Suspensa, Cancelada, Expirada, Teste, Demo }
public enum SaasSolicitacaoStatus { Recebida, EmAnalise, Aprovada, ConvertidaTenant, Recusada, Cancelada }
public enum SaasAddonTipo { ModuloExtra, UsuariosExtras, ArmazenamentoExtra, WhiteLabel, DominioCustomizado, SuportePremium, IntegracaoExtra, Treinamento, Outros }
