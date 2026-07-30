using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
namespace Sigov.Infrastructure.Commercial;
public interface ICommercialPersonalDataProtector { string? Protect(string? value); string? Unprotect(string? value); string? Hash(string? value); string? MaskDocument(string? value); string? MaskEmail(string? value); string? MaskPhone(string? value); }
public sealed class CommercialPersonalDataProtector(IConfiguration configuration) : ICommercialPersonalDataProtector
{
    private byte[] Key => SHA256.HashData(Encoding.UTF8.GetBytes(configuration["Sigov:Security:CommercialDataProtectionKey"] ?? configuration["Sigov:Security:BootstrapToken"] ?? throw new InvalidOperationException("Chave de proteção comercial não configurada.")));
    public string? Protect(string? value){if(string.IsNullOrWhiteSpace(value))return null;var nonce=RandomNumberGenerator.GetBytes(12);var tag=new byte[16];var plain=Encoding.UTF8.GetBytes(value.Trim());var cipher=new byte[plain.Length];using var aes=new AesGcm(Key,16);aes.Encrypt(nonce,plain,cipher,tag);return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());}
    public string? Unprotect(string? value){if(string.IsNullOrWhiteSpace(value))return null;var b=Convert.FromBase64String(value);var p=new byte[b.Length-28];using var aes=new AesGcm(Key,16);aes.Decrypt(b[..12],b[12..28],b[28..],p);return Encoding.UTF8.GetString(p);}
    public string? Hash(string? value)=>string.IsNullOrWhiteSpace(value)?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(N(value)))).ToLowerInvariant();
    public string? MaskDocument(string? value){var n=N(value);return n.Length<4?null:$"***{n[^4..]}";} public string? MaskEmail(string? value){if(string.IsNullOrWhiteSpace(value)||!value.Contains('@'))return null;var p=value.Split('@',2);return $"{p[0][0]}***@{p[1]}";} public string? MaskPhone(string? value){var n=N(value);return n.Length<4?null:$"(**) *****-{n[^4..]}";} private static string N(string? v)=>new((v??"").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
