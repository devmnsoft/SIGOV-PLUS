using Sigov.Domain.Common;

namespace Sigov.Domain.Core;

public enum TipoPessoa
{
    Fisica,
    Juridica
}

public sealed class Pessoa : AggregateRoot
{
    public Pessoa(TipoPessoa tipo, string nome, string? documento)
    {
        Tipo = tipo;
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
        Documento = NormalizarDocumento(documento);

        if (Tipo == TipoPessoa.Fisica && Documento is not null && Documento.Length != 11)
        {
            throw new ArgumentException("CPF deve conter 11 dígitos.", nameof(documento));
        }

        if (Tipo == TipoPessoa.Juridica && Documento is not null && Documento.Length != 14)
        {
            throw new ArgumentException("CNPJ deve conter 14 dígitos.", nameof(documento));
        }
    }

    public TipoPessoa Tipo { get; }
    public string Nome { get; private set; }
    public string? Documento { get; private set; }

    public void Renomear(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome é obrigatório.", nameof(nome)) : nome.Trim();
    }

    public static string? NormalizarDocumento(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return null;
        }

        var digits = new string(documento.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }
}
