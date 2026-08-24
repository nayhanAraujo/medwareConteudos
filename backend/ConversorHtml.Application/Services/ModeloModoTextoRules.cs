using System.Text.RegularExpressions;

namespace ConversorHtml.Application.Services;

/// <summary>Regras puras compartilhadas pelo montador e pelo gerador de modo texto.</summary>
public static class ModeloModoTextoRules
{
    public static IReadOnlyList<string> ExtractDependencyTokens(string? formula, IReadOnlyList<string> knownTokens)
    {
        var formatted = ModoTextoCodigoFormatter.ToCodigoSuffix(formula, knownTokens);
        if (string.IsNullOrWhiteSpace(formatted)) return [];
        return Regex.Matches(formatted, @"<<VR_([A-Za-z0-9_]+)>>", RegexOptions.IgnoreCase)
            .Select(x => x.Groups[1].Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<int> ResolveDependencyOrder(
        IEnumerable<int> requested,
        IReadOnlyDictionary<int, IReadOnlyList<int>> graph,
        ISet<int>? alreadyPresent = null)
    {
        var result = new List<int>();
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>(alreadyPresent ?? new HashSet<int>());

        void Visit(int id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new InvalidOperationException("Foi detectado um ciclo entre as fórmulas das medidas selecionadas.");
            foreach (var dependency in graph.GetValueOrDefault(id, [])) Visit(dependency);
            visiting.Remove(id);
            visited.Add(id);
            result.Add(id);
        }

        foreach (var id in requested) Visit(id);
        return result;
    }

    public static string ComposeTwoColumns(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var lines = new List<string>(left);
        var firstRightName = right.FirstOrDefault(x => x.TrimStart().StartsWith('['));
        if (right.Count > 0)
        {
            lines.AddRange(right);
            for (var guard = 0; guard < Math.Max(20, left.Count + right.Count) && InferSecondColumnHeader(lines) != firstRightName; guard++)
                lines.Insert(left.Count, "");
        }
        return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
    }

    public static string? InferSecondColumnHeader(IReadOnlyList<string> lines)
    {
        var middle = Math.Max(0, (int)Math.Floor(lines.Count / 2d) - 1);
        for (var i = middle; i < lines.Count; i++)
            if (lines[i].TrimStart().StartsWith('[')) return lines[i];
        return null;
    }
}
