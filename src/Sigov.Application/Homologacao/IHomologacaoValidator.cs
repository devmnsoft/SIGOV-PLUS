namespace Sigov.Application.Homologacao;

public interface IHomologacaoValidator
{
    void EnsureCanRun(string environmentName);
}
