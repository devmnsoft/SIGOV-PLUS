using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using Sigov.Application.Security;

namespace Sigov.Infrastructure.Security;

public sealed class PasswordRecoveryEmailOptions
{
    public const string SectionName = "PasswordRecovery:Email";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "SIGOV+";
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class SmtpPasswordRecoveryEmailSender(IOptions<PasswordRecoveryEmailOptions> options) : IPasswordRecoveryEmailSender
{
    public async Task SendAsync(string recipientName, string recipientEmail, string resetUrl, TimeSpan validity, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("O canal SMTP de recuperação de senha não está configurado.");

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8),
            Subject = "SIGOV+ — Redefinição de senha",
            Body = BuildBody(recipientName, resetUrl, validity),
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(recipientEmail, recipientName, Encoding.UTF8));
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(settings.Username),
            Credentials = string.IsNullOrWhiteSpace(settings.Username) ? null : new NetworkCredential(settings.Username, settings.Password)
        };
        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildBody(string name, string resetUrl, TimeSpan validity)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeUrl = WebUtility.HtmlEncode(resetUrl);
        return $"<!doctype html><html lang=\"pt-BR\"><body style=\"font-family:Arial,sans-serif;color:#172033\"><h1 style=\"font-size:22px\">Redefinição de senha</h1><p>Olá, {safeName}.</p><p>Recebemos uma solicitação para redefinir sua senha no SIGOV+.</p><p><a href=\"{safeUrl}\" style=\"display:inline-block;padding:12px 20px;background:#155eef;color:#fff;text-decoration:none;border-radius:6px\">Redefinir senha</a></p><p>Este link é pessoal, pode ser usado uma única vez e expira em {(int)validity.TotalMinutes} minutos.</p><p>Se você não fez esta solicitação, ignore esta mensagem. Não compartilhe este link.</p></body></html>";
    }
}
