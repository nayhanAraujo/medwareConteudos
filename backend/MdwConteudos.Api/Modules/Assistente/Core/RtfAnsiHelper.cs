using System.Text;
using System.Text.RegularExpressions;

namespace MdwConteudos.Api.Modules.Assistente.Core;

public static partial class RtfAnsiHelper
{
    private static readonly Encoding Windows1252 = Encoding.GetEncoding(1252);

    private const string RtfHeader =
        @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fswiss Arial;}}\viewkind4\uc1\pard\f0\fs20 ";

    public static bool IsRtf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return text.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToPlainText(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf)) return string.Empty;
        if (!IsRtf(rtf)) return rtf.Trim();

        var text = HexCharRegex().Replace(rtf, match =>
        {
            var b = Convert.ToByte(match.Groups[1].Value, 16);
            return Windows1252.GetString([b]);
        });
        text = UnicodeCharRegex().Replace(text, match =>
        {
            var code = short.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return char.ConvertFromUtf32(code < 0 ? code + 65536 : code);
        });
        text = text.Replace("\\par", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\\line", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\\tab", "\t", StringComparison.OrdinalIgnoreCase);
        text = RtfDestinationRegex().Replace(text, string.Empty);
        text = RtfCommandRegex().Replace(text, string.Empty);
        text = text.Replace("\\{", "{").Replace("\\}", "}").Replace("\\\\", "\\");
        text = text.Replace("{", string.Empty).Replace("}", string.Empty);
        return text.Trim();
    }

    public static string FromPlainText(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return RtfHeader + "\\par}";

        var sb = new StringBuilder(RtfHeader);
        foreach (var ch in plain)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '{':
                    sb.Append("\\{");
                    break;
                case '}':
                    sb.Append("\\}");
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append("\\par ");
                    break;
                default:
                    if (ch is >= (char)0x20 and <= (char)0x7E)
                        sb.Append(ch);
                    else
                    {
                        var bytes = Windows1252.GetBytes([ch]);
                        sb.Append("\\'").Append(bytes[0].ToString("x2"));
                    }
                    break;
            }
        }
        sb.Append("\\par}");
        return sb.ToString();
    }

    public static string NormalizeForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return IsRtf(value) ? value : FromPlainText(value);
    }

    public static string NormalizeForEditing(string? value) =>
        IsRtf(value) ? ToPlainText(value) : (value ?? string.Empty);

    [GeneratedRegex(@"\\'([0-9a-fA-F]{2})")]
    private static partial Regex HexCharRegex();

    [GeneratedRegex(@"\\u(-?\d+)\??")]
    private static partial Regex UnicodeCharRegex();

    [GeneratedRegex(@"\{\\(?:fonttbl|colortbl|stylesheet|info|pict|\*\\)[^{}]*(?:\{[^{}]*\}[^{}]*)*\}", RegexOptions.IgnoreCase)]
    private static partial Regex RtfDestinationRegex();

    [GeneratedRegex(@"\\[a-zA-Z]+-?\d*\s?")]
    private static partial Regex RtfCommandRegex();
}
