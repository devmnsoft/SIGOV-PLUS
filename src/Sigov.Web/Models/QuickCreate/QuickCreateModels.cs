namespace Sigov.Web.Models.QuickCreate;

public sealed record QuickCreateOption(string Key, string Permission, string Destination, bool AdminOnly = false);

