namespace Sigov.Application.Saas.Modules;

public sealed record ModuleFeatureItem(string Codigo, string Nome, string Descricao, bool HabilitadaPorPadrao = true);
