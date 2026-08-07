using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace MdwConteudos.Api.Modules.Assistente.Importacao;

public static class AssistenteImportacaoValidator
{
    public const long MaxFileSize = 20L * 1024 * 1024;
    public const string MrdHeader = "Medware Designer Report 1.0";

    public static string ValidateTitle(string? value, string field, int maxLength)
    {
        var title = value?.Trim() ?? string.Empty;
        if (title.Length == 0) throw new AssistenteImportacaoException($"{field} é obrigatório.");
        if (title.Length > maxLength) throw new AssistenteImportacaoException($"{field} deve ter no máximo {maxLength} caracteres.");
        return title;
    }

    public static string PrepareScript(short type, string fileName, byte[] bytes)
    {
        ValidateBytes(bytes, "O arquivo do script");
        return type switch
        {
            1 => Convert.ToBase64String(bytes),
            2 => PrepareDll(fileName, bytes),
            3 => PrepareJson(fileName, bytes),
            _ => throw new AssistenteImportacaoException("Tipo de script inválido. Use 1, 2 ou 3.")
        };
    }

    public static string PrepareMrd(string fileName, byte[] bytes)
    {
        ValidateBytes(bytes, "O arquivo MRD");
        if (!string.Equals(Path.GetExtension(fileName), ".mrd", StringComparison.OrdinalIgnoreCase))
            throw new AssistenteImportacaoException("O modelo deve possuir extensão .mrd.");

        var headerLength = Math.Min(bytes.Length, 256);
        var header = Encoding.ASCII.GetString(bytes, 0, headerLength);
        if (!header.StartsWith(MrdHeader, StringComparison.Ordinal))
            throw new AssistenteImportacaoException($"Arquivo MRD inválido: cabeçalho '{MrdHeader}' não encontrado.");
        return Convert.ToBase64String(bytes);
    }

    public static byte[] DecodeStored(short type, string content) =>
        type is 1 or 2 ? Convert.FromBase64String(content) : Encoding.UTF8.GetBytes(content);

    private static string PrepareDll(string fileName, byte[] bytes)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".dll", StringComparison.OrdinalIgnoreCase))
            throw new AssistenteImportacaoException("Scripts do tipo 2 devem possuir extensão .dll.");
        if (bytes.Length < 64 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
            throw new AssistenteImportacaoException("DLL inválida: assinatura MZ ausente.");

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x3c, 4));
        if (peOffset < 0 || peOffset > bytes.Length - 4 ||
            bytes[peOffset] != (byte)'P' || bytes[peOffset + 1] != (byte)'E' ||
            bytes[peOffset + 2] != 0 || bytes[peOffset + 3] != 0)
            throw new AssistenteImportacaoException("DLL inválida: assinatura PE ausente.");
        return Convert.ToBase64String(bytes);
    }

    private static string PrepareJson(string fileName, byte[] bytes)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
            throw new AssistenteImportacaoException("Scripts do tipo 3 devem possuir extensão .json.");
        string json;
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            json = utf8.GetString(bytes);
            using var _ = JsonDocument.Parse(json);
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new AssistenteImportacaoException("Arquivo JSON inválido.");
        }
        return json;
    }

    private static void ValidateBytes(byte[] bytes, string field)
    {
        if (bytes.Length == 0) throw new AssistenteImportacaoException($"{field} não pode estar vazio.");
        if (bytes.LongLength > MaxFileSize) throw new AssistenteImportacaoException($"{field} excede o limite de 20 MB.");
    }
}
