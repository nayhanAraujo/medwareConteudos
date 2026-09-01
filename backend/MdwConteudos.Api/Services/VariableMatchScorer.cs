using System.Globalization;
using System.Text;
using ConversorHtml.Application.Dtos;
using MdwConteudos.Api.Controllers;

namespace MdwConteudos.Api.Services;

public sealed class VariableMatchIndex
{
    public int CodVariavel { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Sigla { get; init; } = string.Empty;
    public string? Variavel { get; init; }
    public string? Abreviacao { get; init; }
    public string? Descricao { get; init; }
    public string? Unidade { get; init; }
    public string? Alternativas { get; init; }
    public string? NomesClinicos { get; init; }

    public IEnumerable<(string Name, string? Value)> SearchFields()
    {
        yield return ("nome", Nome);
        yield return ("sigla", Sigla);
        yield return ("variavel", Variavel);
        yield return ("abreviacao", Abreviacao);
        yield return ("descricao", Descricao);
        foreach (var alt in SplitPipe(Alternativas))
            yield return ("alternativa", alt);
        foreach (var nomeClinico in SplitPipe(NomesClinicos))
            yield return ("nomeClinico", nomeClinico);
    }

    private static IEnumerable<string> SplitPipe(string? value) =>
        (value ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public static class VariableMatchScorer
{
    public static VariableMatchCandidateDto Score(ExtractedMeasureDto measure, VariableMatchIndex variable)
    {
        var query = BuildQuery(measure);
        var best = 0;
        var reason = "similaridade";

        foreach (var field in variable.SearchFields())
        {
            var value = Normalize(field.Value);
            if (string.IsNullOrWhiteSpace(value)) continue;

            var score = Similarity(query, value);
            if (query.Contains(value) || value.Contains(query)) score = Math.Max(score, SubstringBoost(field.Name));
            if (string.Equals(Normalize(measure.VariableName), value, StringComparison.Ordinal)) score = 100;
            if (string.Equals(Normalize(measure.Label), value, StringComparison.Ordinal)) score = Math.Max(score, 96);
            if (field.Name == "nomeClinico")
            {
                var label = Normalize(measure.Label);
                if (!string.IsNullOrWhiteSpace(label) && string.Equals(label, value, StringComparison.Ordinal))
                    score = Math.Max(score, 98);
                if (!string.IsNullOrWhiteSpace(Normalize(measure.OriginalText))
                    && string.Equals(Normalize(measure.OriginalText), value, StringComparison.Ordinal))
                    score = Math.Max(score, 97);
            }

            if (score > best)
            {
                best = score;
                reason = field.Name;
            }
        }

        if (!string.IsNullOrWhiteSpace(measure.Unit) && !string.IsNullOrWhiteSpace(variable.Unidade)
            && Normalize(measure.Unit) == Normalize(variable.Unidade))
        {
            best = Math.Min(100, best + 5);
        }

        return new VariableMatchCandidateDto
        {
            CodVariavel = variable.CodVariavel,
            Nome = variable.Nome,
            Sigla = variable.Sigla,
            Variavel = variable.Variavel,
            Unidade = variable.Unidade,
            Score = best,
            Motivo = reason
        };
    }

    internal static string BuildQuery(ExtractedMeasureDto measure) =>
        Normalize($"{measure.Label} {measure.VariableName} {measure.OriginalText}");

    private static int SubstringBoost(string fieldName) =>
        fieldName switch
        {
            "nomeClinico" => 88,
            "alternativa" => 84,
            _ => 82
        };

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var formD = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
        }
        return string.Join(' ', sb.ToString()
            .Replace("VR ", "", StringComparison.OrdinalIgnoreCase)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static int Similarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        if (a == b) return 100;
        var distance = Levenshtein(a, b);
        var max = Math.Max(a.Length, b.Length);
        return Math.Max(0, (int)Math.Round((1.0 - (double)distance / max) * 100));
    }

    private static int Levenshtein(string a, string b)
    {
        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) costs[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var temp = costs[j];
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = temp;
            }
        }
        return costs[b.Length];
    }
}
