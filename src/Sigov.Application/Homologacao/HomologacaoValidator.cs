namespace Sigov.Application.Homologacao;

public sealed class HomologacaoValidator : IHomologacaoValidator
{
    public void EnsureCanRun(string environmentName)
    {
        if (string.Equals(environmentName, "Production", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Homologação não pode ser preparada em Production.");
        }
    }
}
