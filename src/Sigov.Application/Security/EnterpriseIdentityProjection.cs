using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sigov.Application.Security;

/// <summary>
/// Projeção estável do usuário do núcleo para o UUID de identidade ("sub") usado
/// no contexto empresarial (compras). Deve permanecer equivalente à expressão
/// PostgreSQL <c>md5('sigov:usuario:' || usuario.id::text)::uuid</c>.
/// </summary>
public static class EnterpriseIdentityProjection
{
    public static Guid ForUserId(long coreUserId)
    {
        var hex = Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes($"sigov:usuario:{coreUserId.ToString(CultureInfo.InvariantCulture)}"))).ToLowerInvariant();
        return Guid.Parse($"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}");
    }
}
