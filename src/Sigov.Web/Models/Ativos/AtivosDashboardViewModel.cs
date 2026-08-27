using Sigov.Application.Almoxarifado;
using Sigov.Application.Frotas;
using Sigov.Application.Patrimonio;

namespace Sigov.Web.Models.Ativos;

public sealed record AtivosDashboardViewModel(
    PatrimonioDashboard Patrimonio,
    AlmoxarifadoDashboard Almoxarifado,
    FrotasDashboardDto Frotas);
