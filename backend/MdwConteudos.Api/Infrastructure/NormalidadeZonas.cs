namespace MdwConteudos.Api.Infrastructure;

/// <summary>
/// Mapeia CLASSIFICACOES.NOME para zona de API e cor do modo texto.
/// Reconhece o grupo PT (Normal/Leve/Moderado/Grave) e o legado EN.
/// </summary>
public static class NormalidadeZonas
{
    public static string MapZona(string? classificacao)
    {
        if (string.IsNullOrWhiteSpace(classificacao)) return "default";
        var u = classificacao.Trim().ToUpperInvariant();

        if (u is "NORMAL" or "NORMAIS") return "normal";
        if (u is "LEVE" or "LEVES") return "leve";
        if (u is "MODERADO" or "MODERADA" or "MODERADOS") return "moderado";
        if (u is "GRAVE" or "GRAVES") return "grave";

        if (u.Contains("BAIXO") || u.Contains("LOW")) return "low";
        if (u.Contains("MODERATED")) return "moderated";
        if (u.Contains("ELEVADO") || u.Contains("ELEVATED")) return "elevated";
        if (u.Contains("ALTO") || u.Contains("HIGH")) return "high";
        if (u.Contains("MODERADO")) return "moderado";
        if (u.Contains("GRAVE")) return "grave";
        if (u.Contains("LEVE")) return "leve";
        if (u.Contains("NORMAL")) return "normal";
        return "default";
    }

    public static string MapColor(string? classificacao)
    {
        var u = (classificacao ?? "").Trim().ToUpperInvariant();
        if (u.Contains("VERMELH") || u.Contains("GRAVE") || u.Contains("ALTO") || u.Contains("HIGH")) return "vermelho";
        if (u.Contains("LARANJ") || u.Contains("MODERADO") || u.Contains("MODERATED")) return "laranja";
        if (u.Contains("AMAREL") || u.Contains("LIMIT") || u.Contains("LEVE")) return "amarelo";
        if (u.Contains("AZUL") || u.Contains("BAIXO") || u.Contains("LOW")) return "azul";
        if (u.Contains("VERDE") || u.Contains("NORMAL")) return "verde";
        return "verde";
    }
}
