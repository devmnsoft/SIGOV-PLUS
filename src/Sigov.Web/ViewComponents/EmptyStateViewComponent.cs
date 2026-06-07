using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Ui;

namespace Sigov.Web.ViewComponents;

public sealed class EmptyStateViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(EmptyStateViewModel model) => View(model);
}
