namespace SIGOV.Application.Abstractions;

public interface ILgpdMaskingService
{
    string Mask(string? value, string dataType);
}
