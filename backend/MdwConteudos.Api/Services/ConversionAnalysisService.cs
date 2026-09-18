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
    Task<string> GenerateJsonStudioAsync(IReadOnlyList<ReviewedMeasureDto> measures, int? codPadraoCliente, CancellationToken ct);
    Task RegisterAlternativaAsync(int codVariavel, string alternativa, CancellationToken ct);
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
                .Select(v => VariableMatchScorer.Score(m, v))
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

    public async Task<string> GenerateJsonStudioAsync(IReadOnlyList<ReviewedMeasureDto> measures, int? codPadraoCliente, CancellationToken ct)
    {
        var text = await GenerateModoTextoAsync(measures, codPadraoCliente, ct);
        return ModoTextoToJsonStudioConverter.Convert(text);
    }

    public async Task RegisterAlternativaAsync(int codVariavel, string alternativa, CancellationToken ct)
    {
        var alt = (alternativa ?? "").Trim();
        if (codVariavel <= 0) throw new InvalidOperationException("Variável inválida.");
        if (string.IsNullOrWhiteSpace(alt)) throw new InvalidOperationException("Informe o texto da alternativa.");

        await using var conn = await _db.OpenConnectionAsync(ct);
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VARIAVEIS WHERE CODVARIAVEL = @codVariavel",
            new { codVariavel });
        if (exists == 0) throw new InvalidOperationException("Variável não encontrada.");

        var already = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM VARIAVEISALTERNATIVAS
            WHERE CODVARIAVEL = @codVariavel AND UPPER(ALTERNATIVA) = UPPER(@alt)",
            new { codVariavel, alt });
        if (already > 0) return;

        var codUsuario = await conn.ExecuteScalarAsync<int?>(
            "SELECT FIRST 1 CODUSUARIO FROM USUARIO ORDER BY CODUSUARIO") ?? 1;

        await conn.ExecuteAsync(@"
            INSERT INTO VARIAVEISALTERNATIVAS (CODVARIAVEL, ALTERNATIVA, CODUSUARIO, DTHRULTMODIFICACAO)
            VALUES (@codVariavel, @alt, @codUsuario, @now)",
            new { codVariavel, alt, codUsuario, now = DateTime.Now });
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
            ? new Dictionary<int, VariableMatchIndex>()
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

    private async Task<IReadOnlyList<VariableMatchIndex>> LoadVariablesAsync(CancellationToken ct, IReadOnlyList<int>? ids = null)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        var where = ids is { Count: > 0 } ? "WHERE V.CODVARIAVEL IN @ids" : "";
        var rows = await conn.QueryAsync<VariableMatchIndex>($@"
            SELECT
                V.CODVARIAVEL AS CodVariavel,
                COALESCE(V.NOME, '') AS Nome,
                COALESCE(V.SIGLA, '') AS Sigla,
                V.VARIAVEL AS Variavel,
                V.ABREVIACAO AS Abreviacao,
                V.DESCRICAO AS Descricao,
                U.DESCRICAO AS Unidade,
                (SELECT LIST(A.ALTERNATIVA, '|') FROM VARIAVEISALTERNATIVAS A WHERE A.CODVARIAVEL = V.CODVARIAVEL) AS Alternativas,
                (SELECT LIST(N.NOME, '|') FROM VARIAVEISNOMESCLINICOS N WHERE N.CODVARIAVEL = V.CODVARIAVEL) AS NomesClinicos
            FROM VARIAVEIS V
            LEFT JOIN UNIDADEMEDIDA U ON U.CODUNIDADEMEDIDA = V.CODUNIDADEMEDIDA
            {where}
            ORDER BY V.NOME", new { ids });
        return rows.AsList();
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
            SELECT CODVARIAVEL AS CodVariavel, SEXO AS Sexo, IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax, TEXTO AS Texto
            FROM PADRAONORMALIDADECOMENTARIO
            WHERE CODPADRAO = @codPadrao AND CODVARIAVEL IN @ids
            ORDER BY CODVARIAVEL, SEXO, IDADE_MIN", new { codPadrao, ids });

        var result = new Dictionary<int, string>();
        foreach (var group in rows.Where(r => !string.IsNullOrWhiteSpace(r.Texto)).GroupBy(r => r.CodVariavel))
        {
            var aggregated = AggregateCommentTexts(group.Select(r => new CommentPart(r.Sexo, r.IdadeMin, r.IdadeMax, r.Texto)));
            if (!string.IsNullOrWhiteSpace(aggregated))
                result[group.Key] = aggregated;
        }
        return result;
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
        // Só a variável DONA do cálculo (FORMULAS.CODVARIAVEL) recebe Código:/funcao.
        // FORMULA_VARIAVEL lista dependências (ex.: PESO/ALTURA no cálculo de SUPCOR) —
        // não deve atribuir a fórmula a essas entradas.
        // Se CODVARIAVEL da fórmula for nulo, cai no vínculo legado via FORMULA_VARIAVEL.
        var rows = await conn.QueryAsync<FormulaRow>(@"
            SELECT
                COALESCE(F.CODVARIAVEL, FV.CODVARIAVEL) AS CodVariavel,
                F.FORMULA AS Formula,
                EL.EQUACAO AS Equacao,
                TL.NOME AS Linguagem,
                EL.CODREFERENCIA AS CodReferencia
            FROM FORMULAS F
            LEFT JOIN FORMULA_VARIAVEL FV ON FV.CODFORMULA = F.CODFORMULA
                AND F.CODVARIAVEL IS NULL
                AND FV.CODVARIAVEL IN @ids
            LEFT JOIN EQUACOESLINGUAGEM EL ON EL.CODFORMULA = F.CODFORMULA
            LEFT JOIN TIPOLINGUAGEM TL ON TL.CODLINGUAGEM = EL.CODLINGUAGEM
            WHERE F.CODVARIAVEL IN @ids
               OR (F.CODVARIAVEL IS NULL AND FV.CODVARIAVEL IN @ids)", new { ids });
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

        // Prefer FORMULAS.FORMULA; equação algébrica só entra se a linguagem for JavaScript.
        return pool
            .Select(r => new
            {
                Expression = !string.IsNullOrWhiteSpace(r.Formula)
                    ? r.Formula
                    : IsJavaScript(r.Linguagem) ? r.Equacao : null,
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
        // Sempre emite (M:/F:) quando há faixas — inclusive no modo texto —
        // para alimentar referenciaNormalidade no JSON Studio.
        var simple = selected.Count > 0 ? BuildSimpleRanges(selected) : "";
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

            // Prefere a faixa normal/verde; senão agrega min/max.
            var preferred = ofSex.FirstOrDefault(r =>
            {
                var cor = NormalidadeZonas.MapColor(r.Classificacao);
                var nome = (r.Classificacao ?? "").Trim();
                return string.Equals(cor, "verde", StringComparison.OrdinalIgnoreCase)
                    || nome.Contains("Normal", StringComparison.OrdinalIgnoreCase);
            });

            decimal? min;
            decimal? max;
            if (preferred is not null)
            {
                min = preferred.ValorMin;
                max = preferred.ValorMax;
            }
            else
            {
                min = ofSex.Select(r => r.ValorMin).Where(v => v.HasValue).DefaultIfEmpty().Min();
                max = ofSex.Select(r => r.ValorMax).Where(v => v.HasValue).DefaultIfEmpty().Max();
            }

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
            SELECT CODVARIAVEL AS CodVariavel, CODREFERENCIA AS CodReferencia,
                   SEXO AS Sexo, IDADE_MIN AS IdadeMin, IDADE_MAX AS IdadeMax, TEXTO AS Texto
            FROM NORMALIDADECOMENTARIO
            WHERE CODVARIAVEL IN @ids
            ORDER BY CODVARIAVEL, CODREFERENCIA, SEXO, IDADE_MIN", new { ids });

        // Vários comentários por (variável, referência) são válidos (sexo/idade/texto distintos).
        // Agrega sem ToDictionary rígido para não quebrar a geração do JSON/TXT.
        var result = new Dictionary<(int, int), string>();
        foreach (var group in rows.Where(r => !string.IsNullOrWhiteSpace(r.Texto)).GroupBy(r => (r.CodVariavel, r.CodReferencia)))
        {
            var aggregated = AggregateCommentTexts(group.Select(r => new CommentPart(r.Sexo, r.IdadeMin, r.IdadeMax, r.Texto)));
            if (!string.IsNullOrWhiteSpace(aggregated))
                result[group.Key] = aggregated;
        }
        return result;
    }

    /// <summary>
    /// Junta textos de NORMALIDADECOMENTARIO / PADRAO preservando diferenças por sexo e idade.
    /// Ex.: "F: &lt;= 36; M: &lt;= 40" ou faixas etárias concatenadas.
    /// </summary>
    private static string AggregateCommentTexts(IEnumerable<CommentPart> parts)
    {
        var bySex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.Texto)) continue;
            var sex = NormalizeSex(part.Sexo);
            var text = Iso88591SafeText.ForDisplay(part.Texto.Trim());
            var age = FormatAgeLabel(part.IdadeMin, part.IdadeMax);
            var entry = string.IsNullOrEmpty(age) ? text : $"{age}: {text}";

            if (!bySex.TryGetValue(sex, out var list))
            {
                list = [];
                bySex[sex] = list;
            }

            if (!list.Contains(entry, StringComparer.OrdinalIgnoreCase))
                list.Add(entry);
        }

        if (bySex.Count == 0) return "";

        if (bySex.TryGetValue("A", out var amboss) && amboss.Count > 0)
            return string.Join("; ", amboss);

        var combined = new List<string>();
        if (bySex.TryGetValue("F", out var f) && f.Count > 0)
            combined.Add(f.Count == 1 ? $"F: {f[0]}" : $"F: {string.Join(" | ", f)}");
        if (bySex.TryGetValue("M", out var m) && m.Count > 0)
            combined.Add(m.Count == 1 ? $"M: {m[0]}" : $"M: {string.Join(" | ", m)}");

        foreach (var kv in bySex.Where(kv => kv.Key is not ("A" or "F" or "M" or "-")))
            combined.Add($"{kv.Key}: {string.Join(" | ", kv.Value)}");

        return string.Join("; ", combined);
    }

    private static string FormatAgeLabel(decimal? idadeMin, decimal? idadeMax)
    {
        static bool Meaningful(decimal? v) => v.HasValue && v.Value >= 0;
        var hasMin = Meaningful(idadeMin);
        var hasMax = Meaningful(idadeMax);
        if (!hasMin && !hasMax) return "";
        if (hasMin && hasMax) return $"{FormatDecimal(idadeMin)}-{FormatDecimal(idadeMax)}a";
        if (hasMin) return $">={FormatDecimal(idadeMin)}a";
        return $"<={FormatDecimal(idadeMax)}a";
    }

    private readonly record struct CommentPart(string? Sexo, decimal? IdadeMin, decimal? IdadeMax, string? Texto);

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
        public string? Sexo { get; set; }
        public decimal? IdadeMin { get; set; }
        public decimal? IdadeMax { get; set; }
        public string? Texto { get; set; }
    }

    private sealed class PadraoCommentRow
    {
        public int CodVariavel { get; set; }
        public string? Sexo { get; set; }
        public decimal? IdadeMin { get; set; }
        public decimal? IdadeMax { get; set; }
        public string? Texto { get; set; }
    }

    private static string NormalizeSection(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "GERAL" : value.Trim().ToUpperInvariant();

    private static string ToVariableToken(string? value)
    {
        var normalized = VariableMatchScorer.Normalize(value).ToUpperInvariant();
        normalized = normalized.StartsWith("VR_", StringComparison.OrdinalIgnoreCase) ? normalized[3..] : normalized;
        normalized = new string(normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "CAMPO" : normalized;
    }

}
