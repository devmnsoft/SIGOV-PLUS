using Sigov.Application.Abstractions;
using Sigov.Application.Lgpd;

namespace Sigov.Infrastructure.Lgpd;

public sealed class LgpdMaskingPolicy : ILgpdMaskingPolicy
{
    private readonly ILgpdClassificationService _classificationService;
    private readonly ILgpdMaskingService _maskingService;

    public LgpdMaskingPolicy(ILgpdClassificationService classificationService, ILgpdMaskingService maskingService)
    {
        _classificationService = classificationService;
        _maskingService = maskingService;
    }

    public string Mask(string? value, string fieldName)
    {
        if (_classificationService.IsSecret(fieldName))
        {
            return "***";
        }

        if (_classificationService.IsSensitiveData(fieldName))
        {
            return "***";
        }

        return _classificationService.IsPersonalData(fieldName) ? _maskingService.Mask(value, fieldName) : value ?? string.Empty;
    }
}
