namespace Sigov.Application.Health;

public interface IVersionInfoProvider
{
    string Application { get; }
    string Service { get; }
    string Version { get; }
}
