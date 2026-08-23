using System.Globalization;
using System.Text.RegularExpressions;

namespace ConversorHtml.Application.Services;

/// <summary>
/// Converte fórmulas do banco (FORMULA_VARIAVEL / EQUACOESLINGUAGEM) para o sufixo
/// <c> Código: ...</c> do modo texto, com placeholders <c>&lt;&lt;VR_...&gt;&gt;</c>.
/// </summary>
public static partial class ModoTextoCodigoFormatter
{
    private static readonly HashSet<string> ReservedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "else", "return", "true", "false", "null", "undefined", "new", "var", "let", "const",
        "function", "typeof", "void", "this", "Math", "Number", "String", "Boolean", "parseFloat",
        "parseInt", "toFixed", "pow", "round", "abs", "min", "max", "floor", "ceil", "sqrt"
    };

    public static string ToCodigoSuffix(string? expression, IEnumerable<string>? knownTokens = null)
    {
        var code = StripCodigoPrefix(expression);
        if (string.IsNullOrWhiteSpace(code)) return "";

        code = ReplaceDomLookups(code);
        code = WrapBareVrTokens(code);
        code = ReplaceKnownTokens(code, knownTokens);

        if (!code.Contains("<<VR_", StringComparison.Ordinal))
            return "";

        return "  Código: " + CollapseSpaces(code);
    }

    public static string FormatDecimal(decimal? value, bool useComma = false)
    {
        if (!value.HasValue) return "";
        var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        var culture = useComma ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.InvariantCulture;
        return rounded.ToString("0.##", culture);
    }

    public static string FormatDecimalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            || decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out n))
        {
            return FormatDecimal(n);
        }

        return value.Trim();
    }

    private static string StripCodigoPrefix(string? expression)
    {
        var code = (expression ?? "").Trim();
        if (code.StartsWith("Código:", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("Codigo:", StringComparison.OrdinalIgnoreCase))
        {
            var idx = code.IndexOf(':');
            code = idx >= 0 ? code[(idx + 1)..].Trim() : code;
        }

        return code;
    }

    private static string ReplaceDomLookups(string code) =>
        DomLookupRegex().Replace(code, "<<VR_$1>>");

    private static string WrapBareVrTokens(string code) =>
        BareVrRegex().Replace(code, "<<VR_$1>>");

    private static string ReplaceKnownTokens(string code, IEnumerable<string>? knownTokens)
    {
        if (knownTokens is null) return code;

        var tokens = knownTokens
            .Select(NormalizeToken)
            .Where(t => t.Length >= 2 && !ReservedTokens.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(t => t.Length)
            .ToList();

        foreach (var token in tokens)
        {
            var pattern = $@"(?<!<<)(?<![\w])(?:VR_)?{Regex.Escape(token)}(?![\w])(?!>>)";
            code = Regex.Replace(code, pattern, $"<<VR_{token}>>", RegexOptions.IgnoreCase);
        }

        return code;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var token = value.Trim().ToUpperInvariant();
        if (token.StartsWith("VR_", StringComparison.Ordinal)) token = token[3..];
        return new string(token.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
    }

    private static string CollapseSpaces(string value) =>
        Regex.Replace(value, @"[ \t]+", " ").Trim();

    [GeneratedRegex(@"(?:document\.)?getElementById\(\s*['""]VR_([A-Za-z0-9_]+)['""]\s*\)(?:\s*\.value)?", RegexOptions.IgnoreCase)]
    private static partial Regex DomLookupRegex();

    [GeneratedRegex(@"(?<!<)VR_([A-Za-z0-9_]+)(?!>)", RegexOptions.IgnoreCase)]
    private static partial Regex BareVrRegex();
}
