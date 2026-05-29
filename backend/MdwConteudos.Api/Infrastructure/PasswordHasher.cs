using System.Security.Cryptography;
using System.Text;

namespace MdwConteudos.Api.Infrastructure;

public static class PasswordHasher
{
    public static string Sha256Hex(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
}
