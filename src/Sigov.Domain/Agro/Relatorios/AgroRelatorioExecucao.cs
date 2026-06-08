using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Relatorios;

public sealed class AgroRelatorioExecucao : Entity
{
    public AgroRelatorioExecucao(long tenantId, AgroRelatorioFormato? formato, long? entidadeId = null, long? exercicioId = null, long? modeloId = null, long? usuarioId = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        TenantId = tenantId; Formato = formato ?? throw new ArgumentNullException(nameof(formato), "Execução de relatório exige formato."); EntidadeId = entidadeId; ExercicioId = exercicioId; ModeloId = modeloId; UsuarioId = usuarioId; Status = AgroRelatorioStatus.SOLICITADO;
    }
    public long TenantId { get; } public long? EntidadeId { get; } public long? ExercicioId { get; } public long? ModeloId { get; } public long? UsuarioId { get; } public AgroRelatorioFormato Formato { get; } public AgroRelatorioStatus Status { get; }
}
