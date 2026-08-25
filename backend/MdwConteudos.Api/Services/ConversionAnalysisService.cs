using System.Globalization;
using System.Text;
using Dapper;
using ConversorHtml.Application.Dtos;
using ConversorHtml.Application.Services;
using MdwConteudos.Api.Controllers;
using MdwConteudos.Api.Infrastructure;

namespace MdwConteudos.Api.Services;

public interface IConversionAnalysisService
{
    Task<ConversionAnalysisResponseDto> MatchAsync(MeasureExtractionResultDto extraction, CancellationToken ct);
    Task<string> GenerateModoTextoAsync(IReadOnlyList<ReviewedMeasureDto> measures, int? codPadraoCliente, CancellationToken ct);
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
                .Where(x => x.Score >= 40)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Nome)
                .Take(25)
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

    public async Task<string> GenerateModoTextoAsync(IReadOnlyList<ReviewedMeasureDto> measures, int? codPadraoCliente, CancellationToken ct)
    {
        var kept = measures
            .Where(m => !string.Equals(m.Decision, "ignore", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (kept.Count == 0) throw new InvalidOperationException("Nenhuma medida selecionada para gerar o modo texto.");

        var effectivePadrao = codPadraoCliente ?? kept.Select(m => m.CodPadraoCliente).FirstOrDefault(p => p.HasValue);

        var ids = kept.Where(m => m.CodVariavel.HasValue).Select(m => m.CodVariavel!.Value).Distinct().ToArray();
        var variables = ids.Length == 0
            ? new Dictionary<int, VariableIndex>()
            : (await LoadVariablesAsync(ct, ids)).ToDictionary(x => x.CodVariavel);

        Dictionary<int, List<NormalityRow>> rangesByVariable;
        Dictionary<int, string> padraoCommentsByVariable = new();

        if (effectivePadrao.HasValue && effectivePadrao > 0)
        {
            rangesByVariable = ids.Length == 0
                ? new Dictionary<int, List<NormalityRow>>()
                : (await LoadPadraoNormalityRowsAsync(ct, effectivePadrao.Value, ids))
                    .GroupBy(x => x.CodVariavel).ToDictionary(g => g.Key, g => g.ToList());
            padraoCommentsByVariable = ids.Length == 0
                ? new Dictionary<int, string>()
                : await LoadPadraoCommentsAsync(ct, effectivePadrao.Value, ids);
        }
        else
        {
            rangesByVariable = ids.Length == 0
                ? new Dictionary<int, List<NormalityRow>>()
                : (await LoadNormalityRowsAsync(ct, ids)).GroupBy(x => x.CodVariavel).ToDictionary(g => g.Key, g => g.ToList());
        }

        var commentsByVariable = effectivePadrao.HasValue
            ? new Dictionary<(int, int), string>()
            : ids.Length == 0
                ? new Dictionary<(int, int), string>()
                : await LoadNormalityCommentsAsync(ct, ids);
        var formulasByVariable = ids.Length == 0
            ? new Dictionary<int, List<FormulaRow>>()
            : (await LoadFormulaRowsAsync(ct, ids)).GroupBy(x => x.CodVariavel).ToDictionary(g => g.Key, g => g.ToList());
        var knownTokens = formulasByVariable.Count == 0
            ? Array.Empty<string>()
            : await LoadFormulaTokensAsync(ct);

        var builder = new StringBuilder();
        foreach (var group in kept.GroupBy(m => NormalizeSection(m.Section)))
        {
            builder.AppendLine($"[{group.Key}]");
            foreach (var measure in group)
            {
                if (measure.CodVariavel.HasValue && variables.TryGetValue(measure.CodVariavel.Value, out var variable))
                {
                    rangesByVariable.TryGetValue(variable.CodVariavel, out var rows);
                    formulasByVariable.TryGetValue(variable.CodVariavel, out var formulas);
                    builder.AppendLine(BuildLine(
                        variable.Nome,
                        variable.Sigla,
                        variable.Unidade ?? measure.Unit,
                        rows,
                        effectivePadrao.HasValue ? null : measure.CodReferencia,
                        measure.NormalityMode,
                        effectivePadrao.HasValue
                            ? padraoCommentsByVariable.GetValueOrDefault(variable.CodVariavel)
                            : PickComment(commentsByVariable, variable.CodVariavel, measure.CodReferencia, rows),
                        PickFormulaExpression(formulas, measure.CodReferencia),
                        knownTokens));
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(measure.Label) ? "Campo" : measure.Label.Trim();
                var sigla = ToVariableToken(measure.Label);
                builder.AppendLine(BuildLine(label, sigla, measure.Unit, null, null, null, null, null, knownTokens));
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
                (SELECT LIST(A.ALTERNATIVA, '|') FROM VARIAVEISALTERNATIVAS A WHERE A.CODVARIAVEL = V.CODVARIAVEL) AS Alternativas
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

    private async Task<IReadOnlyList<NormalityRow>> LoadPadraoNormalityRowsAsync(CancellationToken ct, int codPadrao, IReadOnlyList<int> ids)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<NormalityRow>(@"
            SELECT
                F.CODVARIAVEL AS CodVariavel,
                CAST(NULL AS INTEGER) AS CodReferencia,
                F.SEXO AS Sexo,
                F.VALORMIN AS ValorMin,
                F.VALORMAX AS ValorMax,
                F.IDADE_MIN AS IdadeMin,
                F.IDADE_MAX AS IdadeMax,
                C.NOME AS Classificacao,
                P.NOME AS ReferenciaTitulo
            FROM PADRAONORMALIDADEFAIXA F
            LEFT JOIN CLASSIFICACOES C ON C.CODCLASSIFICACAO = F.CODCLASSIFICACAO
            JOIN CLIENTESPADRAONORMALIDADE P ON P.CODPADRAO = F.CODPADRAO
            WHERE F.CODPADRAO = @codPadrao AND F.CODVARIAVEL IN @ids
            ORDER BY F.CODVARIAVEL, F.SEXO, F.IDADE_MIN", new { codPadrao, ids });
        return rows.AsList();
    }

    private async Task<Dictionary<int, string>> LoadPadraoCommentsAsync(CancellationToken ct, int codPadrao, IReadOnlyList<int> ids)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PadraoCommentRow>(@"
            SELECT CODVARIAVEL AS CodVariavel, TEXTO AS Texto
            FROM PADRAONORMALIDADECOMENTARIO
            WHERE CODPADRAO = @codPadrao AND CODVARIAVEL IN @ids", new { codPadrao, ids });
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Texto))
            .ToDictionary(r => r.CodVariavel, r => r.Texto!.Trim());
    }

    private async Task<IReadOnlyList<NormalityRow>> LoadNormalityRowsAsync(CancellationToken ct, IReadOnlyList<int> ids)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<NormalityRow>(@"
            SELECT
                N.CODVARIAVEL AS CodVariavel,
                N.CODREFERENCIA AS CodReferencia,
                N.SEXO AS Sexo,
                N.VALORMIN AS ValorMin,
                N.VALORMAX AS ValorMax,
                N.IDADE_MIN AS IdadeMin,
                N.IDADE_MAX AS IdadeMax,
                C.NOME AS Classificacao,
                R.TITULO AS ReferenciaTitulo
            FROM NORMALIDADE N
            LEFT JOIN CLASSIFICACOES C ON C.CODCLASSIFICACAO = N.CODCLASSIFICACAO
            LEFT JOIN REFERENCIA R ON R.CODREFERENCIA = N.CODREFERENCIA
            WHERE N.CODVARIAVEL IN @ids
            ORDER BY N.CODVARIAVEL, N.CODREFERENCIA, N.SEXO, N.IDADE_MIN", new { ids });
        return rows.AsList();
    }

    private async Task<IReadOnlyList<FormulaRow>> LoadFormulaRowsAsync(CancellationToken ct, IReadOnlyList<int> ids)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<FormulaRow>(@"
            SELECT
                COALESCE(FV.CODVARIAVEL, F.CODVARIAVEL) AS CodVariavel,
                F.FORMULA AS Formula,
                EL.EQUACAO AS Equacao,
                TL.NOME AS Linguagem,
                EL.CODREFERENCIA AS CodReferencia
            FROM FORMULAS F
            LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
                AND FV.CODVARIAVEL IN @ids
            LEFT JOIN EQUACOESLINGUAGEM EL ON EL.CODFORMULA = F.CODFORMULA
            LEFT JOIN TIPOLINGUAGEM TL ON TL.CODLINGUAGEM = EL.CODLINGUAGEM
            WHERE F.CODVARIAVEL IN @ids OR FV.CODVARIAVEL IN @ids", new { ids });
        return rows.AsList();
    }

    private async Task<IReadOnlyList<string>> LoadFormulaTokensAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<VariableTokenRow>(@"
            SELECT SIGLA AS Sigla, VARIAVEL AS Variavel FROM VARIAVEIS
            WHERE SIGLA IS NOT NULL OR VARIAVEL IS NOT NULL");
        return rows
            .SelectMany(r => new[] { r.Sigla, r.Variavel })
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? PickFormulaExpression(IReadOnlyList<FormulaRow>? rows, int? codReferencia)
    {
        if (rows is null || rows.Count == 0) return null;

        IEnumerable<FormulaRow> pool = rows;
        if (codReferencia.HasValue)
        {
            var matched = rows.Where(r => r.CodReferencia == codReferencia.Value).ToList();
            if (matched.Count > 0) pool = matched;
        }

        return pool
            .Select(r => new
            {
                Expression = !string.IsNullOrWhiteSpace(r.Formula) ? r.Formula : r.Equacao,
                HasFormula = string.IsNullOrWhiteSpace(r.Formula) ? 0 : 1,
                JsScore = IsJavaScript(r.Linguagem) ? 2 : 0
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Expression))
            .OrderByDescending(x => x.HasFormula)
            .ThenByDescending(x => x.JsScore)
            .Select(x => x.Expression)
            .FirstOrDefault();
    }

    private static bool IsJavaScript(string? linguagem)
    {
        var value = (linguagem ?? "").Trim();
        return value.Contains("javascript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "js", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLine(
        string label,
        string sigla,
        string? unit,
        IReadOnlyList<NormalityRow>? rows,
        int? codReferencia,
        string? normalityMode,
        string? storedComment,
        string? formula,
        IReadOnlyList<string>? knownTokens)
    {
        var selected = SelectRows(rows, codReferencia);
        var mode = ResolveMode(selected, normalityMode, storedComment);
        var simple = mode == "simple" || mode == "classificacao" ? BuildSimpleRanges(selected) : "";
        var comment = mode switch
        {
            "texto" => BuildTextoComment(storedComment),
            "classificacao" => BuildClassificationComment(selected),
            _ => ""
        };
        var codigo = ModoTextoCodigoFormatter.ToCodigoSuffix(formula, knownTokens);
        return $"{label.Trim()} ({ToVariableToken(sigla)}): 0.0  {unit?.Trim() ?? "sem unidade"}{simple}{comment}{codigo}";
    }

    private static IReadOnlyList<NormalityRow> SelectRows(IReadOnlyList<NormalityRow>? rows, int? codReferencia)
    {
        if (rows is null || rows.Count == 0) return [];
        if (codReferencia.HasValue)
        {
            var filtered = rows.Where(r => r.CodReferencia == codReferencia.Value).ToList();
            if (filtered.Count > 0) return filtered;
        }

        var firstRef = rows.Select(r => r.CodReferencia).FirstOrDefault(x => x.HasValue);
        if (firstRef.HasValue)
        {
            var filtered = rows.Where(r => r.CodReferencia == firstRef.Value).ToList();
            if (filtered.Count > 0) return filtered;
        }

        return rows;
    }

    private static string ResolveMode(IReadOnlyList<NormalityRow> rows, string? requested, string? storedComment)
    {
        if (string.Equals(requested, "simple", StringComparison.OrdinalIgnoreCase)) return "simple";
        if (string.Equals(requested, "texto", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(storedComment) ? "simple" : "texto";
        if (string.Equals(requested, "classificacao", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requested, "comment", StringComparison.OrdinalIgnoreCase))
            return "classificacao";

        var perSex = rows.GroupBy(r => NormalizeSex(r.Sexo)).Select(g => g.Count());
        return rows.Count > 2 || perSex.Any(c => c > 1) || rows.Any(r => r.IdadeMin.HasValue || r.IdadeMax.HasValue)
            ? "classificacao"
            : "simple";
    }

    private static string? PickComment(
        IReadOnlyDictionary<(int CodVariavel, int CodReferencia), string> comments,
        int codVariavel,
        int? requestedRef,
        IReadOnlyList<NormalityRow>? rows)
    {
        if (requestedRef.HasValue && comments.TryGetValue((codVariavel, requestedRef.Value), out var exact))
            return exact;

        var firstRef = rows?.Select(r => r.CodReferencia).FirstOrDefault(x => x.HasValue);
        if (firstRef.HasValue && comments.TryGetValue((codVariavel, firstRef.Value), out var fallback))
            return fallback;
        return null;
    }

    private static string BuildSimpleRanges(IReadOnlyList<NormalityRow> rows)
    {
        if (rows.Count == 0) return "";
        var parts = new List<string>();
        foreach (var sexo in new[] { "M", "F", "A" })
        {
            var ofSex = rows.Where(r => NormalizeSex(r.Sexo) == sexo).ToList();
            if (ofSex.Count == 0) continue;
            var min = ofSex.Select(r => r.ValorMin).Where(v => v.HasValue).DefaultIfEmpty().Min();
            var max = ofSex.Select(r => r.ValorMax).Where(v => v.HasValue).DefaultIfEmpty().Max();
            if (!min.HasValue && !max.HasValue) continue;
            parts.Add($"({sexo}: {FormatDecimal(min)} a {FormatDecimal(max)})");
        }

        return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
    }

    private static string BuildClassificationComment(IReadOnlyList<NormalityRow> rows)
    {
        if (rows.Count == 0) return "";
        var bands = rows.Select(r =>
        {
            var sexo = NormalizeSex(r.Sexo);
            var cor = NormalidadeZonas.MapColor(r.Classificacao);
            var zona = string.IsNullOrWhiteSpace(r.Classificacao) ? "" : $" {r.Classificacao.Trim()}";
            return $"({sexo}:{zona} {{{FormatDecimalComma(r.ValorMin)}, {FormatDecimalComma(r.ValorMax)},  {cor} }})";
        });
        return "  Comentário: " + string.Join(",", bands);
    }

    private static string BuildTextoComment(string? storedComment)
        => string.IsNullOrWhiteSpace(storedComment) ? "" : $"  Comentário: {storedComment.Trim()}";

    private static string NormalizeSex(string? sexo)
    {
        var s = (sexo ?? "").Trim().ToUpperInvariant();
        if (s.StartsWith('F')) return "F";
        if (s.StartsWith('M')) return "M";
        if (s.StartsWith('A') || s.StartsWith('U')) return "A";
        return s.Length == 0 ? "-" : s[..1];
    }

    private async Task<Dictionary<(int, int), string>> LoadNormalityCommentsAsync(CancellationToken ct, IReadOnlyList<int> ids)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<CommentRow>(@"
            SELECT CODVARIAVEL AS CodVariavel, CODREFERENCIA AS CodReferencia, TEXTO AS Texto
            FROM NORMALIDADECOMENTARIO
            WHERE CODVARIAVEL IN @ids", new { ids });
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Texto))
            .ToDictionary(r => (r.CodVariavel, r.CodReferencia), r => r.Texto!.Trim());
    }

    internal static string FormatDecimal(decimal? value) =>
        ModoTextoCodigoFormatter.FormatDecimal(value);

    private static string FormatDecimalComma(decimal? value) =>
        ModoTextoCodigoFormatter.FormatDecimal(value, useComma: true);

    private sealed class FormulaRow
    {
        public int CodVariavel { get; set; }
        public string? Formula { get; set; }
        public string? Equacao { get; set; }
        public string? Linguagem { get; set; }
        public int? CodReferencia { get; set; }
    }

    private sealed class VariableTokenRow
    {
        public string? Sigla { get; set; }
        public string? Variavel { get; set; }
    }

    private sealed class NormalityRow
    {
        public int CodVariavel { get; set; }
        public int? CodReferencia { get; set; }
        public string? Sexo { get; set; }
        public decimal? ValorMin { get; set; }
        public decimal? ValorMax { get; set; }
        public decimal? IdadeMin { get; set; }
        public decimal? IdadeMax { get; set; }
        public string? Classificacao { get; set; }
        public string? ReferenciaTitulo { get; set; }
    }

    private sealed class CommentRow
    {
        public int CodVariavel { get; set; }
        public int CodReferencia { get; set; }
        public string? Texto { get; set; }
    }

    private sealed class PadraoCommentRow
    {
        public int CodVariavel { get; set; }
        public string? Texto { get; set; }
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
