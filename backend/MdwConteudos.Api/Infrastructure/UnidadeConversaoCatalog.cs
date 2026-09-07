using System.Globalization;

namespace MdwConteudos.Api.Infrastructure;

/// <summary>Catálogo v1 de fatores de conversão entre unidades equivalentes.</summary>
public static class UnidadeConversaoCatalog
{
    private static readonly Dictionary<(string From, string To), decimal> Factors = new()
    {
        [("mm", "cm")] = 0.1m,
        [("cm", "mm")] = 10m,
        [("m/s", "cm/s")] = 100m,
        [("cm/s", "m/s")] = 0.01m
    };

    public static string NormalizeDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao)) return "";
        return descricao.Trim().ToLowerInvariant()
            .Replace('µ', 'u')
            .Replace("μ", "u", StringComparison.Ordinal);
    }

    public static bool TryGetFactor(string? de, string? para, out decimal fator)
    {
        var from = NormalizeDescricao(de);
        var to = NormalizeDescricao(para);
        if (Factors.TryGetValue((from, to), out fator))
            return true;
        fator = 0;
        return false;
    }

    public static IReadOnlyList<(string De, string Para, decimal Fator)> ListPares() =>
        Factors.Select(kv => (kv.Key.From, kv.Key.To, kv.Value)).OrderBy(x => x.From).ThenBy(x => x.To).ToList();

    /// <summary>
    /// Heurística anti-dupla-conversão: para m/s→cm/s, valores já em dezenas
    /// (ex. 54–111) provavelmente já estão no destino.
    /// </summary>
    public static string ClassificarFaixa(string? de, string? para, decimal? valorMin, decimal? valorMax)
    {
        var from = NormalizeDescricao(de);
        var to = NormalizeDescricao(para);
        if (!TryGetFactor(from, to, out var fator))
            return "ambigua";

        // Sentinelas
        if (IsSentinel(valorMin) && IsSentinel(valorMax))
            return "ambigua";

        if (from == "m/s" && to == "cm/s")
        {
            var vmax = AbsMax(valorMin, valorMax);
            if (vmax <= 5m) return "compativel_origem";
            if (vmax >= 10m) return "ja_parece_destino";
            return "ambigua";
        }

        if (from == "cm/s" && to == "m/s")
        {
            var vmax = AbsMax(valorMin, valorMax);
            if (vmax >= 10m) return "compativel_origem";
            if (vmax > 0 && vmax <= 5m) return "ja_parece_destino";
            return "ambigua";
        }

        if (from == "mm" && to == "cm")
        {
            var vmax = AbsMax(valorMin, valorMax);
            // mm tipicamente dezenas/centenas; cm tipicamente unidades/dezenas baixas
            if (vmax >= 20m) return "compativel_origem";
            if (vmax > 0 && vmax < 5m) return "ja_parece_destino";
            return "ambigua";
        }

        if (from == "cm" && to == "mm")
        {
            var vmax = AbsMax(valorMin, valorMax);
            if (vmax > 0 && vmax < 20m) return "compativel_origem";
            if (vmax >= 50m) return "ja_parece_destino";
            return "ambigua";
        }

        _ = fator;
        return "compativel_origem";
    }

    public static bool IsSentinel(decimal? v) =>
        v is null || v == 999m || v == -999m || v == 999.0m || v == -999.0m;

    public static decimal? ApplyFactor(decimal? value, decimal fator, int casasDecimais)
    {
        if (value is null) return null;
        if (IsSentinel(value)) return value;
        var scaled = value.Value * fator;
        if (casasDecimais < 0) casasDecimais = 0;
        return Math.Round(scaled, casasDecimais, MidpointRounding.AwayFromZero);
    }

    private static decimal AbsMax(decimal? a, decimal? b)
    {
        decimal ma = 0;
        if (a is not null && !IsSentinel(a)) ma = Math.Abs(a.Value);
        if (b is not null && !IsSentinel(b)) ma = Math.Max(ma, Math.Abs(b.Value));
        return ma;
    }

    public static string FormatNumber(decimal value)
    {
        // Remove casas inúteis: 80.0 → 80, 0.50 → 0.5
        var s = value.ToString("0.######", CultureInfo.InvariantCulture);
        return s;
    }
}
