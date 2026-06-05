using Sigov.Domain.Common;

namespace Sigov.Domain.Processos;

public sealed class TipoProcesso : Entity
{
    public long TenantId { get; private set; }
    public long? EntidadeId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
}

public sealed class ProcessoDigital : AggregateRoot
{
    public long TenantId { get; private set; }
    public long? EntidadeId { get; private set; }
    public long? ExercicioId { get; private set; }
    public long TipoProcessoId { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public int Ano { get; private set; }
    public string Assunto { get; private set; } = string.Empty;
    public ProcessoStatus Status { get; private set; }
    public ProcessoPrioridade Prioridade { get; private set; }
    public bool Sigiloso { get; private set; }

    public ProcessoDigital() { }

    public ProcessoDigital(long tenantId, long tipoProcessoId, string numero, int ano, string assunto, ProcessoPrioridade prioridade, bool sigiloso)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant obrigatório.", nameof(tenantId));
        if (tipoProcessoId <= 0) throw new ArgumentException("Tipo de processo obrigatório.", nameof(tipoProcessoId));
        if (string.IsNullOrWhiteSpace(numero)) throw new ArgumentException("Número obrigatório.", nameof(numero));
        if (string.IsNullOrWhiteSpace(assunto)) throw new ArgumentException("Assunto obrigatório.", nameof(assunto));
        TenantId = tenantId;
        TipoProcessoId = tipoProcessoId;
        Numero = numero.Trim();
        Ano = ano;
        Assunto = assunto.Trim();
        Prioridade = prioridade;
        Sigiloso = sigiloso;
        Status = ProcessoStatus.ABERTO;
    }

    public bool PodeVisualizar(bool possuiPermissaoSigiloso) => !Sigiloso || possuiPermissaoSigiloso;

    public void Movimentar(string despacho)
    {
        if (Status == ProcessoStatus.ENCERRADO) throw new InvalidOperationException("Processo encerrado não pode ser movimentado.");
        if (Status == ProcessoStatus.CANCELADO) throw new InvalidOperationException("Processo cancelado não pode ser movimentado.");
        if (string.IsNullOrWhiteSpace(despacho)) throw new ArgumentException("Movimentação exige despacho.", nameof(despacho));
        Status = ProcessoStatus.EM_TRAMITACAO;
    }

    public void Encerrar() => Status = ProcessoStatus.ENCERRADO;
    public void Cancelar() => Status = ProcessoStatus.CANCELADO;
}

public sealed class ProcessoMovimentacao : Entity
{
    public ProcessoMovimentacao(string despacho)
    {
        if (string.IsNullOrWhiteSpace(despacho)) throw new ArgumentException("Movimentação exige despacho.", nameof(despacho));
        Despacho = despacho.Trim();
    }

    public string Despacho { get; private set; }
}

public sealed class ProcessoResponsavel : Entity { public long ProcessoDigitalId { get; private set; } public long UsuarioId { get; private set; } }

public sealed class ProcessoParecer : Entity
{
    public ProcessoParecer(string titulo, string texto)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("Parecer exige título.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(texto)) throw new ArgumentException("Parecer exige texto.", nameof(texto));
        Titulo = titulo.Trim();
        Texto = texto.Trim();
    }

    public string Titulo { get; private set; }
    public string Texto { get; private set; }
}

public sealed class ProcessoAnexo : Entity { public long ProcessoDigitalId { get; private set; } public string NomeArquivo { get; private set; } = string.Empty; }
public sealed class ModeloDocumento : Entity { public string Nome { get; private set; } = string.Empty; }
public sealed class DocumentoGerado : Entity { public string Titulo { get; private set; } = string.Empty; }
public sealed class AssinaturaDigital : Entity { public AssinaturaDigitalStatus Status { get; private set; } }
public sealed class FilaAssinatura : Entity { public long AssinaturaDigitalId { get; private set; } }

public sealed class ProtocoloAtendimento : AggregateRoot
{
    public ProtocoloAtendimento(bool convertido = false) => ProcessoDigitalId = convertido ? 1 : null;
    public long? ProcessoDigitalId { get; private set; }
    public ProtocoloStatus Status { get; private set; } = ProtocoloStatus.ABERTO;

    public void Converter(long processoDigitalId)
    {
        if (ProcessoDigitalId.HasValue) throw new InvalidOperationException("Protocolo pode ser convertido em processo digital uma única vez.");
        if (processoDigitalId <= 0) throw new ArgumentException("Processo digital obrigatório.", nameof(processoDigitalId));
        ProcessoDigitalId = processoDigitalId;
        Status = ProtocoloStatus.CONVERTIDO_PROCESSO;
    }
}

public sealed class OuvidoriaManifestacao : AggregateRoot
{
    public OuvidoriaManifestacao(bool anonima, long? pessoaId)
    {
        if (!anonima && !pessoaId.HasValue) throw new ArgumentException("Manifestação identificada exige pessoa.", nameof(pessoaId));
        Anonima = anonima;
        PessoaId = anonima ? null : pessoaId;
    }

    public bool Anonima { get; private set; }
    public bool Sigilosa { get; private set; } = true;
    public long? PessoaId { get; private set; }
    public bool DeveOcultarDadosPessoais(bool podeVerDadosPessoais) => Sigilosa && !podeVerDadosPessoais;
}

public sealed class DiarioOficialPublicacao : AggregateRoot
{
    public DiarioOficialStatus Status { get; private set; } = DiarioOficialStatus.RASCUNHO;
    public void Publicar() => Status = DiarioOficialStatus.PUBLICADO;
    public void ValidarEdicao(bool permissaoAdministrativa)
    {
        if (Status == DiarioOficialStatus.PUBLICADO && !permissaoAdministrativa) throw new InvalidOperationException("Diário publicado não pode ser editado sem permissão administrativa.");
    }
}

public sealed class AtoOficial : Entity
{
    public AtoOficial(long diarioOficialPublicacaoId)
    {
        if (diarioOficialPublicacaoId <= 0) throw new ArgumentException("Ato oficial deve pertencer a uma publicação.", nameof(diarioOficialPublicacaoId));
        DiarioOficialPublicacaoId = diarioOficialPublicacaoId;
    }

    public long DiarioOficialPublicacaoId { get; private set; }
}
