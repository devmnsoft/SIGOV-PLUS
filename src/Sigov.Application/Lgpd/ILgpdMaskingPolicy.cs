namespace Sigov.Application.Lgpd;

public interface ILgpdMaskingPolicy
{
    string Mask(string? value, string fieldName);
}
