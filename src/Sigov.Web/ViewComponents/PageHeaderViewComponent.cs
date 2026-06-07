using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Ui;

namespace Sigov.Web.ViewComponents;

public sealed class PageHeaderViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(PageHeaderViewModel model) => View(model);
}
