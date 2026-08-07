using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MdwConteudos.Api.Modules.ConteudosImpressos;

public sealed class ConteudosImpressosException : Exception
{
    public ConteudosImpressosException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}

public static partial class ConteudoTextCodec
{
    private static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);

    public static string Decode(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string text) return text;
        if (value is Stream stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            value = copy.ToArray();
        }
        if (value is not byte[] bytes) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (bytes.Length == 0) return string.Empty;
        foreach (var encoding in new[] { Utf8Strict, Encoding.GetEncoding(1252), Encoding.Latin1 })
        {
            try { return encoding.GetString(bytes); }
            catch (DecoderFallbackException) { }
        }
        return Encoding.UTF8.GetString(bytes);
    }

    public static byte[] EncodeUtf8(string? value) => Encoding.UTF8.GetBytes((value ?? string.Empty).Trim());

    public static string DecodeUploaded(byte[] value)
    {
        foreach (var encoding in new[] { Utf8Strict, Encoding.GetEncoding(1252), Encoding.Latin1 })
        {
            try { return encoding.GetString(value); }
            catch (DecoderFallbackException) { }
        }
        return Encoding.UTF8.GetString(value);
    }

    public static byte[] EncodeWindows1252(string value) => Encoding.GetEncoding(
        1252,
        EncoderFallback.ReplacementFallback,
        DecoderFallback.ReplacementFallback).GetBytes(value);

    public static bool IsRtf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return text.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase)
            || RtfMarkerRegex().IsMatch(value.Length > 800 ? value[..800] : value);
    }

    public static string ToDisplayHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (IsRtf(value)) return RtfToHtml(value);
        if (HtmlMarkerRegex().IsMatch(value)) return value;
        return $"<p>{WebUtility.HtmlEncode(value).Replace("\r\n", "<br>").Replace("\n", "<br>")}</p>";
    }

    private static string RtfToHtml(string rtf)
    {
        var text = HexCharRegex().Replace(rtf, match =>
        {
            var b = Convert.ToByte(match.Groups[1].Value, 16);
            return Encoding.GetEncoding(1252).GetString([b]);
        });
        text = UnicodeCharRegex().Replace(text, match =>
        {
            var code = short.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return char.ConvertFromUtf32(code < 0 ? code + 65536 : code);
        });
        text = text.Replace("\\par", "<br>", StringComparison.OrdinalIgnoreCase)
            .Replace("\\line", "<br>", StringComparison.OrdinalIgnoreCase);
        text = Regex.Replace(text, @"\\b0\b", "</strong>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\b\b", "<strong>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\i0\b", "</em>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\i\b", "<em>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\ulnone\b", "</u>", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\ul\b", "<u>", RegexOptions.IgnoreCase);
        text = RtfDestinationRegex().Replace(text, string.Empty);
        text = RtfCommandRegex().Replace(text, string.Empty);
        text = text.Replace("\\{", "{").Replace("\\}", "}").Replace("\\\\", "\\");
        text = text.Replace("{", string.Empty).Replace("}", string.Empty).Trim();
        return $"<div>{text}</div>";
    }

    public static string Clean(string? value) => ControlCharsRegex().Replace(value?.Trim() ?? string.Empty, string.Empty);

    [GeneratedRegex(@"\\'[0-9a-fA-F]{2}|\\par\b|\\viewkind", RegexOptions.IgnoreCase)]
    private static partial Regex RtfMarkerRegex();
    [GeneratedRegex(@"<[a-zA-Z!/]", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlMarkerRegex();
    [GeneratedRegex(@"\\'([0-9a-fA-F]{2})")]
    private static partial Regex HexCharRegex();
    [GeneratedRegex(@"\\u(-?\d+)\??")]
    private static partial Regex UnicodeCharRegex();
    [GeneratedRegex(@"\{\\(?:fonttbl|colortbl|stylesheet|info|pict)[^{}]*(?:\{[^{}]*\}[^{}]*)*\}", RegexOptions.IgnoreCase)]
    private static partial Regex RtfDestinationRegex();
    [GeneratedRegex(@"\\[a-zA-Z]+-?\d*\s?", RegexOptions.IgnoreCase)]
    private static partial Regex RtfCommandRegex();
    [GeneratedRegex("[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F\\x7F]")]
    private static partial Regex ControlCharsRegex();
}

public record GrupoFraseDto(int CodGrupo, string Nome, int Status);
public record GrupoFraseRequest(string Nome, int Status = 1);
public record FraseDto(int CodFrase, int CodGrupo, string Codigo, string Titulo, string Conteudo, string ConteudoHtml, string ConteudoRtf, int Status);
public record FraseRequest(int CodGrupo, string Codigo, string Titulo, string Conteudo, int Status = 1);

public record GrupoMensagemDto(int CodGrupoMensagem, string Nome, string? Descricao, int Ativo, DateTime? DthrUltModificacao);
public record GrupoMensagemRequest(string Nome, string? Descricao, int Ativo = 1);
public record ModeloMensagemDto(int CodModeloMensagem, int CodGrupoMensagem, string Titulo, string Conteudo, string TipoMensagem, int Ativo, DateTime? DthrUltModificacao);
public record ModeloMensagemRequest(int CodGrupoMensagem, string Titulo, string Conteudo, string TipoMensagem = "TEXTO", int Ativo = 1);
public record EmojiDto(string Emoji, string Nome, string Codigo);

public sealed class ImpressoForm
{
    public string Titulo { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;
    public bool UsoGeral { get; set; }
    public bool ImprimirCabecalho { get; set; }
    public IFormFile? ArquivoVbs { get; set; }
}

public sealed class ImpressoImportForm
{
    public string Titulo { get; set; } = string.Empty;
    public bool UsoGeral { get; set; }
    public bool ImprimirCabecalho { get; set; }
    public IFormFile? ArquivoMrd { get; set; }
    public IFormFile? ArquivoVbs { get; set; }
}

public record ImpressoListDto(int CodImpresso, string Titulo, int UsoGeral, int ImprimirCabecalho, DateTime? DthrUltModificacao, string? UsuarioNome, bool TemVbs);
public record ImpressoDto(int CodImpresso, string Titulo, string Conteudo, int UsoGeral, int ImprimirCabecalho, int? CodUsuario, DateTime? DthrUltModificacao, string? ScriptVbs, DateTime? DthrUltModificacaoVbs);
