namespace Sigov.Application.Auditoria;

public sealed class AuditoriaFiltroValidator
{
    public bool IsPeriodoValido(DateTime? inicio, DateTime? fim) => !inicio.HasValue || !fim.HasValue || inicio.Value <= fim.Value;
}
