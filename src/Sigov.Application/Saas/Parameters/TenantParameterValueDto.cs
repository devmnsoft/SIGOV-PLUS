namespace Sigov.Application.Saas.Parameters;

public sealed record TenantParameterValueDto(long Id, long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string? ModuloCodigo, string Escopo, string ValorJson, string? ValorMascarado, DateOnly? VigenteInicio, DateOnly? VigenteFim, bool Ativo);
