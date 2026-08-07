using System.Text;

namespace MdwConteudos.Api.Modules.Assistente.Core;

public static class AssistantContentCodec
{
    private static readonly Encoding Iso88591 = Encoding.Latin1;

    public static string EncodeBytesToBase64(ReadOnlySpan<byte> content) => Convert.ToBase64String(content);

    public static byte[] DecodeBase64ToBytes(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];
        try { return Convert.FromBase64String(content.Trim()); }
        catch (FormatException ex) { throw new InvalidDataException("Conteúdo Base64 inválido.", ex); }
    }

    public static string EncodeTextToBase64(string? content) =>
        EncodeBytesToBase64(Iso88591.GetBytes(content ?? string.Empty));

    public static string DecodeBase64ToText(string? content) =>
        Iso88591.GetString(DecodeBase64ToBytes(content));

    public static byte[] EncodeIso88591(string? content) => Iso88591.GetBytes(content ?? string.Empty);

    public static string DecodeIso88591(ReadOnlySpan<byte> content) => Iso88591.GetString(content);
}
