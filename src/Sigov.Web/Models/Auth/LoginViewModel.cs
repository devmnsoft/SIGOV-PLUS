using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Auth;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o login ou e-mail.")]
    [Display(Name = "Login ou e-mail")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = string.Empty;

    public bool LembrarLogin { get; set; }

    public string? MensagemErro { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Informe o login ou e-mail para recuperação.")]
    [Display(Name = "Login ou e-mail")]
    public string LoginOuEmail { get; set; } = string.Empty;

    public bool Solicitado { get; set; }
    public string? Mensagem { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Senha atual")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Nova senha")]
    public string NovaSenha { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NovaSenha), ErrorMessage = "A confirmação não confere."), Display(Name = "Confirme a nova senha")]
    public string Confirmacao { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Nova senha")]
    public string NovaSenha { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NovaSenha), ErrorMessage = "A confirmação não confere."), Display(Name = "Confirme a nova senha")]
    public string Confirmacao { get; set; } = string.Empty;
}
