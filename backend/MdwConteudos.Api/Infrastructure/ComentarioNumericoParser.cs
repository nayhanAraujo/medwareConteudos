using System.Globalization;
using System.Text.RegularExpressions;

namespace MdwConteudos.Api.Infrastructure;

public sealed record ComentarioParseResult(bool Ok, string? TextoConvertido, string? Motivo, decimal? MaiorNumero);

/// <summary>
/// Parser fail-closed de textos de comentário de normalidade.
/// Só converte se o texto inteiro casar com um padrão numérico conhecido.
/// </summary>
public static partial class ComentarioNumericoParser
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [GeneratedRegex(@"^\s*(?<a>-?\d+(?:[.,]\d+)?)\s*[-–—]\s*(?<b>-?\d+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^\s*(?<a>-?\d+(?:[.,]\d+)?)\s*(?<op>\+/-|±)\s*(?<b>-?\d+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PlusMinusRegex();

    [GeneratedRegex(@"^\s*(?<op><=|>=|≤|≥|<|>)\s*(?<a>-?\d+(?:[.,]\d+)?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CompareRegex();

    public static ComentarioParseResult TryConvert(string? texto, decimal fator, int casasDecimais)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new ComentarioParseResult(false, null, "vazio", null);

        // Normaliza símbolos Unicode para ASCII antes do match (espelha armazenamento)
        var raw = texto.Trim();
        var normalized = Iso88591SafeText.ForStorage(raw);

        var mRange = RangeRegex().Match(normalized);
        if (mRange.Success)
        {
            if (!TryParseNum(mRange.Groups["a"].Value, out var a) || !TryParseNum(mRange.Groups["b"].Value, out var b))
                return new ComentarioParseResult(false, null, "numero_invalido", null);
            var a2 = Scale(a, fator, casasDecimais);
            var b2 = Scale(b, fator, casasDecimais);
            var outText = $"{UnidadeConversaoCatalog.FormatNumber(a2)}-{UnidadeConversaoCatalog.FormatNumber(b2)}";
            return new ComentarioParseResult(true, outText, null, MaxAbs(a, b));
        }

        var mPm = PlusMinusRegex().Match(normalized);
        if (mPm.Success)
        {
            if (!TryParseNum(mPm.Groups["a"].Value, out var a) || !TryParseNum(mPm.Groups["b"].Value, out var b))
                return new ComentarioParseResult(false, null, "numero_invalido", null);
            var op = mPm.Groups["op"].Value.Contains('±', StringComparison.Ordinal) ? "+/-" : mPm.Groups["op"].Value;
            // Preferir +/- no storage (ISO8859_1)
            var a2 = Scale(a, fator, casasDecimais);
            var b2 = Scale(b, fator, casasDecimais);
            var outText = $"{UnidadeConversaoCatalog.FormatNumber(a2)} {op} {UnidadeConversaoCatalog.FormatNumber(b2)}";
            return new ComentarioParseResult(true, outText, null, MaxAbs(a, b));
        }

        var mCmp = CompareRegex().Match(normalized);
        if (mCmp.Success)
        {
            if (!TryParseNum(mCmp.Groups["a"].Value, out var a))
                return new ComentarioParseResult(false, null, "numero_invalido", null);
            var op = mCmp.Groups["op"].Value switch
            {
                "≤" => "<=",
                "≥" => ">=",
                var x => x
            };
            var a2 = Scale(a, fator, casasDecimais);
            var outText = $"{op} {UnidadeConversaoCatalog.FormatNumber(a2)}";
            return new ComentarioParseResult(true, outText, null, Math.Abs(a));
        }

        return new ComentarioParseResult(false, null, "padrao_nao_reconhecido", ExtractMaxNumber(normalized));
    }

    /// <summary>Classifica comentário para heurística m/s↔cm/s usando o maior número do texto.</summary>
    public static string ClassificarComentario(string? de, string? para, string? texto)
    {
        var from = UnidadeConversaoCatalog.NormalizeDescricao(de);
        var to = UnidadeConversaoCatalog.NormalizeDescricao(para);
        var parsed = TryConvert(texto, 1m, 6); // fator 1 só para extrair número / validar padrão
        var maior = parsed.MaiorNumero ?? ExtractMaxNumber(Iso88591SafeText.ForStorage(texto ?? ""));

        if (maior is null)
            return "ambigua";

        if (from == "m/s" && to == "cm/s")
        {
            if (maior <= 5m) return "compativel_origem";
            if (maior >= 10m) return "ja_parece_destino";
            return "ambigua";
        }

        if (from == "cm/s" && to == "m/s")
        {
            if (maior >= 10m) return "compativel_origem";
            if (maior <= 5m) return "ja_parece_destino";
            return "ambigua";
        }

        return parsed.Ok ? "compativel_origem" : "ambigua";
    }

    private static decimal Scale(decimal v, decimal fator, int casas) =>
        UnidadeConversaoCatalog.ApplyFactor(v, fator, casas) ?? v;

    private static bool TryParseNum(string s, out decimal value)
    {
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Float, Inv, out value);
    }

    private static decimal MaxAbs(decimal a, decimal b) => Math.Max(Math.Abs(a), Math.Abs(b));

    private static decimal? ExtractMaxNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        decimal? max = null;
        foreach (Match m in Regex.Matches(text, @"-?\d+(?:[.,]\d+)?"))
        {
            if (!TryParseNum(m.Value, out var n)) continue;
            var abs = Math.Abs(n);
            max = max is null ? abs : Math.Max(max.Value, abs);
        }
        return max;
    }
}
