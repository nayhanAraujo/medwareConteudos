using System.Security.Cryptography;
using System.Text;

namespace MdwConteudos.Api.Infrastructure;

public static class ApiJwtKeyHelper
{
    public static byte[] GetKeyBytes(string secret)
    {
        var raw = Encoding.UTF8.GetBytes(secret ?? "");
        if (raw.Length >= 32) return raw;
        return SHA256.HashData(raw);
    }
}
