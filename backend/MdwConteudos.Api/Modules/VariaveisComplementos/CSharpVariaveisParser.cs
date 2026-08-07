using System.Globalization;
using System.Text.RegularExpressions;

namespace MdwConteudos.Api.Modules.Web;

internal static partial class CSharpVariaveisParser
{
    public static ImportacaoCsPreview Parse(string source)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in VariableRegex().Matches(source))
        {
            var code = match.Value;
            if (!code.Contains("CHART", StringComparison.OrdinalIgnoreCase)) codes.Add(code);
        }

        var variables = codes.OrderBy(x => x).Select(code =>
        {
            var shortName = code[3..];
            return new ImportacaoCsVariavel(code, shortName.Replace('_', ' ').ToLowerInvariant(), shortName, shortName, "unknown", false);
        }).ToList();

        var formulas = new List<ImportacaoCsFormula>();
        foreach (Match method in CalculateMethodRegex().Matches(source))
        {
            var variable = $"VR_{method.Groups["name"].Value.ToUpperInvariant()}";
            var expression = method.Groups["expression"].Value.Trim();
            if (codes.Contains(variable) && expression.Length > 0)
                formulas.Add(new ImportacaoCsFormula(variable, expression, 1));
        }

        var normalidades = new List<ImportacaoCsNormalidade>();
        foreach (Match chart in ChartBlockRegex().Matches(source))
        {
            var variable = chart.Groups["variable"].Value;
            if (!codes.Contains(variable)) continue;
            var body = chart.Groups["body"].Value;
            var min = ReadDecimal(body, "Normal") ?? ReadDecimal(body, "Min");
            var max = ReadDecimal(body, "Max");
            if (min.HasValue && max.HasValue)
                normalidades.Add(new ImportacaoCsNormalidade(variable, "M", min.Value, max.Value, 0, 150, "Unknown"));
        }

        return new ImportacaoCsPreview(variables, formulas, normalidades);
    }

    private static decimal? ReadDecimal(string body, string property)
    {
        var match = Regex.Match(body, $@"\b{property}\s*=\s*(-?\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    [GeneratedRegex(@"\bVR_[A-Za-z0-9_]+", RegexOptions.Compiled)]
    private static partial Regex VariableRegex();

    [GeneratedRegex(@"\b(?:[A-Za-z0-9_<>,?\[\]\s]+)\s+Calcular(?<name>[A-Za-z0-9_]+)\s*\([^)]*\)\s*\{(?:(?!\}).)*?return\s+(?<expression>[^;]+);", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex CalculateMethodRegex();

    [GeneratedRegex("""(?:case\s+)?["'](?<variable>VR_[A-Za-z0-9_]+)_CHART["']\s*:\s*(?<body>(?:(?!case\s+|default\s*:).)*)""", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ChartBlockRegex();
}
