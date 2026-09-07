using System.Globalization;
using System.Text;

namespace MdwConteudos.Api.Infrastructure;

/// <summary>
/// Converte símbolos Unicode comuns (fora de ISO8859_1) para ASCII seguro na gravação
/// e opcionalmente reapresenta os símbolos na leitura para a UI.
/// Evita que Firebird ISO8859_1 substitua caracteres como ≥ por '?'.
/// </summary>
public static class Iso88591SafeText
{
    /// <summary>Normaliza para armazenamento em colunas ISO8859_1 (ex.: NORMALIDADECOMENTARIO.TEXTO).</summary>
    public static string ForStorage(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        return text
            .Replace("≥", ">=", StringComparison.Ordinal)
            .Replace("≦", "<=", StringComparison.Ordinal) // U+2266
            .Replace("≤", "<=", StringComparison.Ordinal)
            .Replace("≠", "!=", StringComparison.Ordinal)
            .Replace("≈", "~=", StringComparison.Ordinal)
            .Replace("±", "+/-", StringComparison.Ordinal)
            .Replace("×", "x", StringComparison.Ordinal)
            .Replace("÷", "/", StringComparison.Ordinal)
            .Replace("−", "-", StringComparison.Ordinal) // minus U+2212
            .Replace("–", "-", StringComparison.Ordinal) // en-dash
            .Replace("—", "-", StringComparison.Ordinal) // em-dash
            .Replace("…", "...", StringComparison.Ordinal)
            .Replace("∞", "inf", StringComparison.Ordinal);
    }

    /// <summary>Reapresenta operadores ASCII como símbolos na UI (o banco continua com ASCII).</summary>
    public static string ForDisplay(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        return text
            .Replace(">=", "≥", StringComparison.Ordinal)
            .Replace("<=", "≤", StringComparison.Ordinal)
            .Replace("!=", "≠", StringComparison.Ordinal)
            .Replace("+/-", "±", StringComparison.Ordinal);
    }

    /// <summary>
    /// Corrige UTF-8 gravado/lido como ISO8859_1 (ex.: "DiÃ¢metro" → "Diâmetro").
    /// </summary>
    public static string RepairMojibake(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (!text.Contains('Ã') && !text.Contains('Â')) return text;

        try
        {
            var decoded = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(text));
            if (!decoded.Contains('\uFFFD') && decoded != text)
                return decoded;
        }
        catch
        {
            // mantém o original
        }

        return text;
    }

    /// <summary>Chave para unificar nomes iguais com encoding ou acento diferente.</summary>
    public static string NomeChave(string? text)
    {
        var repaired = RepairMojibake(text);
        if (string.IsNullOrWhiteSpace(repaired)) return "";

        var formD = repaired.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return string.Join(
            " ",
            sb.ToString().Normalize(NormalizationForm.FormC)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
    }
}
