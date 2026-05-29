using System.Globalization;
using System.Text;

namespace MdwConteudos.Api.Infrastructure;

public static class HttpHeaderSanitizer
{
    /// <summary>
    /// Converte texto para ASCII seguro para headers HTTP (Kestrel rejeita caracteres &gt; 127).
    /// Ex.: "padrão" → "padrao".
    /// </summary>
    public static string ToAscii(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? "";

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c is >= (char)32 and <= (char)127 ? c : '_');
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
