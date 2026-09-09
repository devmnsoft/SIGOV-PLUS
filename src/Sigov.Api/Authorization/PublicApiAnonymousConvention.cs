using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Sigov.Api.Authorization;

/// <summary>Declares the api/publico route namespace as the only convention-based anonymous API surface.</summary>
public sealed class PublicApiAnonymousConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var action in controller.Actions)
            {
                foreach (var selector in action.Selectors)
                {
                    if (!IsPublic(controller, selector)) continue;
                    selector.EndpointMetadata.Add(new AllowAnonymousAttribute());
                }
            }
        }
    }

    private static bool IsPublic(ControllerModel controller, SelectorModel actionSelector)
    {
        foreach (var controllerSelector in controller.Selectors)
        {
            var route = AttributeRouteModel.CombineAttributeRouteModel(
                controllerSelector.AttributeRouteModel,
                actionSelector.AttributeRouteModel)?.Template;
            if (route?.TrimStart('/').StartsWith("api/publico/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return actionSelector.AttributeRouteModel?.Template?.TrimStart('/')
            .StartsWith("api/publico/", StringComparison.OrdinalIgnoreCase) == true;
    }
}
