using System.Globalization;
using System.IO.Compression;

namespace MdwConteudos.Api.Modules.ApiPublica;

/// <summary>Wire formats of routes/api.py, isolated from the internal/modern API.</summary>
public static class ApiV1Contract
{
    // Python datetime.now()/Firebird TIMESTAMP are naive. Do not add a local offset or Z.
    public static string DateTimeIso(DateTime value)
    {
        var microseconds = value.Ticks % TimeSpan.TicksPerSecond / 10;
        var text = value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return microseconds == 0 ? text : $"{text}.{microseconds.ToString("D6", CultureInfo.InvariantCulture)}";
    }

    public static string? FormatDate(object? value) => value switch
    {
        null or DBNull => null,
        DateTime date => DateTimeIso(date),
        DateTimeOffset date => DateTimeIso(date.DateTime) + date.ToString("zzz", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    public static Dictionary<string, object?> Ok(object? data, int? total = null, DateTime? now = null)
    {
        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["data"] = data,
            ["timestamp"] = DateTimeIso(now ?? DateTime.Now)
        };
        if (total.HasValue) result["total"] = total.Value;
        return result;
    }

    public static object OkMessage(string message) => new
    {
        success = true, message, timestamp = DateTimeIso(DateTime.Now)
    };

    public static List<Dictionary<string, object?>> SistemaEspecialidades(IEnumerable<dynamic> rows) =>
        rows.Select(row => new Dictionary<string, object?>
        {
            ["especialidade"] = (string?)row.ESPECIALIDADE,
            ["total"] = Convert.ToInt32(row.TOTAL)
        }).ToList();

    public static string MrdSource(bool activeVersion) => activeVersion ? "SCRIPT_VERSAO_MRD" : "SCRIPTLAUDO_MRD";

    public static string ScriptBaseName(string? name, int code)
    {
        var safe = string.Concat((name ?? "").Select(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' ? c : '_')).Trim();
        return string.IsNullOrEmpty(safe) ? $"script_{code}" : safe;
    }

    public static ScriptDownloadFileResult CreateScriptZip(IEnumerable<(string Name, byte[] Data)> files,
        string zipBaseName, string? version, bool activeVersion)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in files)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(data);
            }
        }
        return new ScriptDownloadFileResult(buffer.ToArray(), $"{zipBaseName}.zip", version, activeVersion);
    }

    public static string RelatorioFileName(string? name, string? format, int code)
    {
        var filename = $"{(string.IsNullOrEmpty(name) ? $"relatorio_{code}" : name)}.{(string.IsNullOrEmpty(format) ? "bin" : format.ToLowerInvariant())}";
        return string.Concat(filename.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.')).Trim().Replace(' ', '_');
    }

    public static string? WorkflowKey(IReadOnlyDictionary<string, object?> item)
    {
        if (!item.TryGetValue("codscriptlaudo", out var cod) || cod is null) return null;
        if (!item.TryGetValue("data_verificacao", out var date) || date is null or "") return null;
        return $"{Convert.ToString(cod, CultureInfo.InvariantCulture)}|{FormatDate(date)}";
    }
}
