namespace Sigov.Domain.Saas;

public enum StatusTenant
{
    Ativo,
    Suspenso,
    Cancelado,
    Implantacao,
    Homologacao
}

public enum StatusAssinatura
{
    Trial,
    Ativa,
    Suspensa,
    Cancelada,
    Vencida
}

public enum TipoAmbiente
{
    Development,
    Homologacao,
    Production
}

public enum TipoModuloSaas
{
    Core,
    Seguranca,
    Auditoria,
    Lgpd,
    Suporte,
    Financeiro,
    Tributario,
    Processos,
    Compras,
    Rh,
    Educacao,
    Saude,
    Social,
    Saneamento,
    Integracao,
    Geo,
    Relatorios,
    Transparencia
}
