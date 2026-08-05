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
