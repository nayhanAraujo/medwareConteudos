using ConversorHtml.Domain.Enums;

namespace ConversorHtml.Application;

public static class ConversionFormatParser
{
    public static ConversionOutputFormat Parse(string? value, ConversionOutputFormat fallback = ConversionOutputFormat.Html)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "html" => ConversionOutputFormat.Html,
            "modotexto" or "modo-texto" or "modo_texto" or "txt" or "text" => ConversionOutputFormat.ModoTexto,
            "jsonstudio" or "json-studio" or "json_studio" or "json" => ConversionOutputFormat.JsonStudio,
            _ => fallback
        };
    }

    public static string ToApiValue(ConversionOutputFormat format) => format switch
    {
        ConversionOutputFormat.ModoTexto => "modoTexto",
        ConversionOutputFormat.JsonStudio => "jsonStudio",
        _ => "html"
    };
}
