using System.Globalization;
using System.Text;
using Dapper;
using ConversorHtml.Application.Dtos;
using MdwConteudos.Api.Controllers;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Services;

public interface IConversionAnalysisService
{
    Task<ConversionAnalysisResponseDto> MatchAsync(MeasureExtractionResultDto extraction, CancellationToken ct);
    Task<string> GenerateModoTextoAsync(IReadOnlyList<ReviewedMeasureDto> measures, CancellationToken ct);
}

public sealed class ConversionAnalysisService : IConversionAnalysisService
{
    private const int AutoSelectScore = 88;
    private readonly IFirebirdConnectionFactory _db;

    public ConversionAnalysisService(IFirebirdConnectionFactory db) => _db = db;

    public async Task<ConversionAnalysisResponseDto> MatchAsync(MeasureExtractionResultDto extraction, CancellationToken ct)
    {
        var variables = await LoadVariablesAsync(ct);
        var measures = extraction.Measures.Select(m =>
        {
            var candidates = variables
                .Select(v => Score(m, v))
                .Where(x => x.Score >= 45)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Nome)
                .Take(5)
                .ToList();

            var selected = candidates.FirstOrDefault(x => x.Score >= AutoSelectScore);
            return new AnalyzedMeasureDto
            {
                Id = string.IsNullOrWhiteSpace(m.Id) ? Guid.NewGuid().ToString("N") : m.Id,
                Label = m.Label,
                VariableName = m.VariableName,
                Section = string.IsNullOrWhiteSpace(m.Section) ? "GERAL" : m.Section,
                Unit = m.Unit,
                OriginalText = m.OriginalText,
                Candidates = candidates,
                SelectedCandidate = selected,
                Status = selected is not null ? "matched" : candidates.Count > 0 ? "lowConfidence" : "unresolved"
            };
        }).ToList();

        return new ConversionAnalysisResponseDto
        {
            SourceFileName = extraction.SourceFileName,
            AnalyzedAt = extraction.AnalyzedAt,
            Provider = extraction.Provider,
            Measures = measures
        };
    }

    public async Task<string> GenerateModoTextoAsync(IReadOnlyList<ReviewedMeasureDto> measures, CancellationToken ct)
    {
        var kept = measures
            .Where(m => !string.Equals(m.Decision, "ignore", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (kept.Count == 0) throw new InvalidOperationException("Nenhuma medida selecionada para gerar o modo texto.");

        var ids = kept.Where(m => m.CodVariavel.HasValue).Select(m => m.CodVariavel!.Value).Distinct().ToArray();
        var variables = ids.Length == 0
            ? new Dictionary<int, VariableIndex>()
            : (await LoadVariablesAsync(ct, ids)).ToDictionary(x => x.CodVariavel);

        var builder = new StringBuilder();
        foreach (var group in kept.GroupBy(m => NormalizeSection(m.Section)))
        {
            builder.AppendLine($"[{group.Key}]");
            foreach (var measure in group)
            {
                if (measure.CodVariavel.HasValue && variables.TryGetValue(measure.CodVariavel.Value, out var variable))
                {
                    builder.AppendLine(BuildLine(variable.Nome, variable.Sigla, variable.Unidade ?? measure.Unit, variable.Normalidade));
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(measure.Label) ? "Campo" : measure.Label.Trim();
                var sigla = ToVariableToken(measure.Label);
                builder.AppendLine(BuildLine(label, sigla, measure.Unit, null));
            }
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private async Task<IReadOnlyList<VariableIndex>> LoadVariablesAsync(CancellationToken ct, IReadOnlyList<int>? ids = null)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var where = ids is { Count: > 0 } ? "WHERE V.CODVARIAVEL IN @ids" : "";
        var rows = await conn.QueryAsync<VariableIndex>($@"
            SELECT
                V.CODVARIAVEL AS CodVariavel,
                COALESCE(V.NOME, '') AS Nome,
                COALESCE(V.SIGLA, '') AS Sigla,
                V.VARIAVEL AS Variavel,
                V.ABREVIACAO AS Abreviacao,
                V.DESCRICAO AS Descricao,
                U.DESCRICAO AS Unidade,
                (SELECT LIST(A.ALTERNATIVA, '|') FROM VARIAVEIS_ALTERNATIVAS A WHERE A.CODVARIAVEL = V.CODVARIAVEL) AS Alternativas,
                (SELECT LIST(COALESCE(N.SEXO, '-') || ': ' || COALESCE(CAST(N.VALORMIN AS VARCHAR(30)), '') || ' a ' || COALESCE(CAST(N.VALORMAX AS VARCHAR(30)), ''), '; ')
                   FROM NORMALIDADE N
                  WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS Normalidade
            FROM VARIAVEIS V
            LEFT JOIN UNIDADEMEDIDA U ON U.CODUNIDADEMEDIDA = V.CODUNIDADEMEDIDA
            {where}
            ORDER BY V.NOME", new { ids });
        return rows.AsList();
    }

    private static VariableMatchCandidateDto Score(ExtractedMeasureDto measure, VariableIndex variable)
    {
        var label = Normalize($"{measure.Label} {measure.VariableName}");
        var best = 0;
        var reason = "similaridade";

        foreach (var field in variable.SearchFields())
        {
            var value = Normalize(field.Value);
            if (string.IsNullOrWhiteSpace(value)) continue;

            var score = Similarity(label, value);
            if (label.Contains(value) || value.Contains(label)) score = Math.Max(score, 82);
            if (string.Equals(Normalize(measure.VariableName), value, StringComparison.Ordinal)) score = 100;
            if (string.Equals(Normalize(measure.Label), value, StringComparison.Ordinal)) score = Math.Max(score, 96);
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

    private static string BuildLine(string label, string sigla, string? unit, string? normality)
    {
        var ranges = string.IsNullOrWhiteSpace(normality)
            ? ""
            : " " + string.Join(" ", normality.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => $"({x})"));
        return $"{label.Trim()} ({ToVariableToken(sigla)}): 0.0  {unit?.Trim() ?? "sem unidade"}{ranges}";
    }

    private static string NormalizeSection(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "GERAL" : value.Trim().ToUpperInvariant();

    private static string ToVariableToken(string? value)
    {
        var normalized = Normalize(value).ToUpperInvariant();
        normalized = normalized.StartsWith("VR_", StringComparison.OrdinalIgnoreCase) ? normalized[3..] : normalized;
        normalized = new string(normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "CAMPO" : normalized;
    }

    private static string Normalize(string? value)
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

    private sealed class VariableIndex
    {
        public int CodVariavel { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Sigla { get; set; } = string.Empty;
        public string? Variavel { get; set; }
        public string? Abreviacao { get; set; }
        public string? Descricao { get; set; }
        public string? Unidade { get; set; }
        public string? Alternativas { get; set; }
        public string? Normalidade { get; set; }

        public IEnumerable<(string Name, string? Value)> SearchFields()
        {
            yield return ("nome", Nome);
            yield return ("sigla", Sigla);
            yield return ("variavel", Variavel);
            yield return ("abreviacao", Abreviacao);
            yield return ("descricao", Descricao);
            foreach (var alt in (Alternativas ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return ("alternativa", alt);
        }
    }
}
