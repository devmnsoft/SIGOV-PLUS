namespace Sigov.Application.Saas.Parameters;

public sealed record TenantParameterDefinitionDto(long Id, string Codigo, string Nome, string? Descricao, string? Modulo, string TipoParametro, string Escopo, string? ValorPadraoJson, bool Obrigatorio, bool Sensivel, bool EditavelTenant, bool Ativo);
