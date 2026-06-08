using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Dicionario;

public sealed class AgroDicionarioDados : Entity
{
    public AgroDicionarioDados(string tabelaNome, string? campoNome, bool dadoPessoal = false, bool dadoSensivel = false, string? mascaraPadrao = null, long? tenantId = null)
    {
        TabelaNome = string.IsNullOrWhiteSpace(tabelaNome) ? throw new ArgumentException("Tabela é obrigatória.", nameof(tabelaNome)) : tabelaNome.Trim();
        CampoNome = campoNome?.Trim(); DadoPessoal = dadoPessoal; DadoSensivel = dadoSensivel; MascaraPadrao = mascaraPadrao; TenantId = tenantId;
        if ((DadoPessoal || DadoSensivel) && string.IsNullOrWhiteSpace(MascaraPadrao)) throw new ArgumentException("Campo pessoal no dicionário deve ter máscara.", nameof(mascaraPadrao));
    }
    public long? TenantId { get; } public string TabelaNome { get; } public string? CampoNome { get; } public bool DadoPessoal { get; } public bool DadoSensivel { get; } public string? MascaraPadrao { get; }
}
