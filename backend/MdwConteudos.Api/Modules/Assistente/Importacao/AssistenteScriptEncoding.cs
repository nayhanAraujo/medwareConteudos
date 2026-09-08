using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

public static class AssistenteScriptEncoding
{
    public static string ToBase64Structure(byte[] bytes) => Convert.ToBase64String(bytes);

    public static short ResolveTipoScript(string sistema, string? linguagem, byte[] content)
    {
        if (string.Equals(sistema, "Laudos UX", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (content.Length >= 2 && content[0] == (byte)'M' && content[1] == (byte)'Z')
            return 2;

        if (!string.IsNullOrWhiteSpace(linguagem) &&
            linguagem.Contains("C#", StringComparison.OrdinalIgnoreCase))
            return 2;

        return 1;
    }

    public static string PrepareFromReferencias(short tipoScript, byte[] content)
    {
        if (content.Length == 0)
            throw new AssistenteImportacaoException("O conteúdo do script de origem está vazio.");

        return tipoScript switch
        {
            3 => PrepareJsonContent(content, "O JSON do script de origem é inválido."),
            1 or 2 => PrepareDllContent(content),
            _ => throw new AssistenteImportacaoException("Tipo de script inválido.")
        };
    }

    public static string PrepareMrdFromReferencias(short tipoScript, string fileName, byte[] content)
    {
        if (tipoScript != 3)
            return AssistenteImportacaoValidator.PrepareMrd(fileName, content);

        if (content.Length == 0)
            throw new AssistenteImportacaoException("O conteúdo do MRD de origem está vazio.");

        if (content.LongLength > AssistenteImportacaoValidator.MaxFileSize)
            throw new AssistenteImportacaoException("O arquivo MRD deve ter no máximo 20 MB.");

        return PrepareJsonContent(content, "O JSON do MRD de origem é inválido.");
    }

    private static string PrepareDllContent(byte[] bytes)
    {
        if (bytes.Length < 64 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
            return ToBase64Structure(bytes);

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x3c, 4));
        if (peOffset < 0 || peOffset > bytes.Length - 4 ||
            bytes[peOffset] != (byte)'P' || bytes[peOffset + 1] != (byte)'E')
            return ToBase64Structure(bytes);

        return ToBase64Structure(bytes);
    }

    private static string PrepareJsonContent(byte[] bytes, string invalidMessage)
    {
        try
        {
            var json = Encoding.UTF8.GetString(bytes);
            using var _ = JsonDocument.Parse(json);
            return json;
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new AssistenteImportacaoException(invalidMessage);
        }
    }
}
