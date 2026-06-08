using Sigov.Domain.Agro.Relatorios;

namespace Sigov.Application.Agro.Relatorios;

public sealed class AgroRelatorioValidator
{
    public AgroRelatorioModelo ValidateModelo(AgroRelatorioModeloCreateRequest request, long tenantId, long? entidadeId)
    {
        var tipo = Enum.TryParse<AgroRelatorioTipo>(request.TipoRelatorio, true, out var parsedTipo) ? parsedTipo : AgroRelatorioTipo.PRODUTORES;
        var formato = Enum.TryParse<AgroRelatorioFormato>(request.FormatoPadrao, true, out var parsedFormato) ? parsedFormato : AgroRelatorioFormato.HTML;
        return new AgroRelatorioModelo(tenantId, entidadeId, request.Codigo, request.Nome, tipo, formato, request.PublicoNoTenant, request.ContemDadosPessoais, request.ContemDadosSensiveis);
    }

    public AgroRelatorioExecucao ValidateExecucao(ExecutarAgroRelatorioRequest request, long tenantId, long? entidadeId, long? exercicioId, long modeloId, long usuarioId)
    {
        var formato = Enum.TryParse<AgroRelatorioFormato>(request.Formato, true, out var parsed) ? parsed : throw new ArgumentException("Formato de relatório inválido.", nameof(request));
        return new AgroRelatorioExecucao(tenantId, formato, entidadeId, exercicioId, modeloId, usuarioId);
    }
}
